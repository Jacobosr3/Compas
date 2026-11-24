using System.Globalization;
using System.Text;
using System.Text.Json;
using Compas.Api.Data;
using Compas.Api.Dtos;
using Compas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Compas.Api.Services;

/// <summary>
/// Sesiones de entrenamiento: lógica compartida por la API REST y el conector MCP. Una sesión con hora de inicio se coloca
/// además como evento del calendario (y, si hay una cuenta de Google conectada con permiso de escritura, en Google Calendar);
/// el evento se crea, mueve y borra junto con la sesión.
/// </summary>
public class WorkoutService
{
    private const int DefaultDurationMinutes = 75;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly CompasDbContext _db;
    private readonly GoogleCalendarService _google;

    public WorkoutService(CompasDbContext db, GoogleCalendarService google)
    {
        _db = db;
        _google = google;
    }

    public static bool TryParseDate(string? text, out DateTime date) =>
        DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static WorkoutSessionDto ToDto(WorkoutSession w) => new(
        w.Id, w.Title, w.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), w.StartTime, w.DurationMinutes, w.Notes, w.Completed,
        w.Source, w.ExternalId, JsonSerializer.Deserialize<List<WorkoutExercise>>(w.ExercisesJson, Json) ?? []);

    public async Task<List<WorkoutSessionDto>> ListAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var q = _db.WorkoutSessions.AsNoTracking().AsQueryable();
        if (from is { } f) q = q.Where(w => w.Date >= f.Date);
        if (to is { } t) q = q.Where(w => w.Date <= t.Date);
        return (await q.OrderBy(w => w.Date).ThenBy(w => w.StartTime).ThenBy(w => w.Id).ToListAsync(ct)).Select(ToDto).ToList();
    }

    /// <summary>
    /// Bloques ocupados del calendario entre dos días (ambos incluidos), para elegir huecos libres. Antes se refresca Google
    /// si la última sincronización tiene más de 10 minutos.
    /// </summary>
    public async Task<List<BusyBlockDto>> ListBusyAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        await _google.TrySyncIfStaleAsync(TimeSpan.FromMinutes(10), ct);
        var start = from.Date;
        var end = to.Date.AddDays(1);
        var events = await _db.CalendarEvents.AsNoTracking()
            .Where(e => e.StartTime < end && e.EndTime > start).OrderBy(e => e.StartTime).ToListAsync(ct);
        const string fmt = "yyyy-MM-dd'T'HH:mm";
        return events.Select(e => new BusyBlockDto(
            e.Title, e.StartTime.ToString(fmt, CultureInfo.InvariantCulture), e.EndTime.ToString(fmt, CultureInfo.InvariantCulture), e.IsAllDay)).ToList();
    }

    /// <summary>Crea la sesión o, si ya existe una con el mismo Source+ExternalId (o, sin ellos, mismo día y título), la sustituye.</summary>
    public async Task<WorkoutUpsertResult> UpsertAsync(WorkoutSessionInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Title)) throw new ArgumentException("El título es obligatorio.");
        if (!TryParseDate(input.Date, out var date)) throw new ArgumentException("La fecha debe tener el formato yyyy-MM-dd.");

        string? startText = null;
        if (!string.IsNullOrWhiteSpace(input.StartTime))
        {
            if (!ProfileService.TryParseTime(input.StartTime.Trim(), out var t)) throw new ArgumentException("'startTime' debe tener el formato HH:mm.");
            startText = t.ToString("HH:mm", CultureInfo.InvariantCulture);
        }
        if (input.DurationMinutes is < 15 or > 300) throw new ArgumentException("'durationMinutes' debe estar entre 15 y 300.");

        var title = input.Title.Trim();
        var source = string.IsNullOrWhiteSpace(input.Source) ? null : input.Source.Trim();
        var externalId = string.IsNullOrWhiteSpace(input.ExternalId) ? null : input.ExternalId.Trim();

        var existing = externalId is not null
            ? await _db.WorkoutSessions.FirstOrDefaultAsync(w => w.Source == source && w.ExternalId == externalId, ct)
            : await _db.WorkoutSessions.FirstOrDefaultAsync(w => w.Date == date && w.Title == title, ct);

        var created = existing is null;
        var w = existing ?? new WorkoutSession();
        w.Title = title;
        w.Date = date;
        w.StartTime = startText;
        w.DurationMinutes = startText is null ? null : input.DurationMinutes ?? DefaultDurationMinutes;
        w.Notes = input.Notes;
        w.Source = source;
        w.ExternalId = externalId;
        w.ExercisesJson = JsonSerializer.Serialize(input.Exercises ?? [], Json);
        w.UpdatedAt = DateTime.UtcNow;
        if (created) _db.WorkoutSessions.Add(w);
        await _db.SaveChangesAsync(ct);

        var warning = await SyncCalendarEventAsync(w, ct);
        return new WorkoutUpsertResult(created, ToDto(w), warning);
    }

    public async Task<bool> SetCompletedAsync(int id, bool completed, CancellationToken ct)
    {
        var w = await _db.WorkoutSessions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (w is null) return false;
        w.Completed = completed;
        w.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Borra la sesión y su evento de calendario; lanza <see cref="GoogleException"/> (sin borrar nada) si Google rechaza borrarlo.</summary>
    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var w = await _db.WorkoutSessions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (w is null) return false;
        await RemoveCalendarEventAsync(w, ct);
        _db.WorkoutSessions.Remove(w);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---------------- Calendario ----------------

    private async Task RemoveCalendarEventAsync(WorkoutSession w, CancellationToken ct)
    {
        if (w.CalendarEventId is not { } eventId) return;
        var evt = await _db.CalendarEvents.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (evt is not null)
        {
            await _google.PushDeleteAsync(evt, ct);
            _db.CalendarEvents.Remove(evt);
        }
        w.CalendarEventId = null;
    }

    /// <summary>
    /// Deja el evento de calendario de la sesión como debe estar: creado o actualizado si tiene hora, borrado si no. Si Google
    /// falla, la sesión se conserva y se devuelve un aviso (el evento puede quedar solo en Compás o sin actualizar).
    /// </summary>
    private async Task<string?> SyncCalendarEventAsync(WorkoutSession w, CancellationToken ct)
    {
        try
        {
            if (w.StartTime is null)
            {
                if (w.CalendarEventId is null) return null;
                await RemoveCalendarEventAsync(w, ct);
                await _db.SaveChangesAsync(ct);
                return null;
            }

            ProfileService.TryParseTime(w.StartTime, out var time);
            var start = DateTime.SpecifyKind(w.Date.Date + time.ToTimeSpan(), DateTimeKind.Unspecified);
            var end = start.AddMinutes(w.DurationMinutes ?? DefaultDurationMinutes);
            var title = "Entreno: " + w.Title;
            var notes = Describe(w);

            var evt = w.CalendarEventId is { } id ? await _db.CalendarEvents.FirstOrDefaultAsync(e => e.Id == id, ct) : null;
            if (evt is null)
            {
                evt = new CalendarEvent { Title = title, StartTime = start, EndTime = end, Notes = notes, Source = EventSource.Manual };
                _db.CalendarEvents.Add(evt);
                await _db.SaveChangesAsync(ct);
                w.CalendarEventId = evt.Id;
                await _db.SaveChangesAsync(ct);
                return await _google.PushNewEventAsync(evt, null, ct);
            }

            // Primero a Google (si el evento está vinculado) y solo después en local, para que no se desincronicen.
            await _google.PushUpdateAsync(evt, title, start, end, false, notes, ct);
            evt.Title = title;
            evt.StartTime = start;
            evt.EndTime = end;
            evt.IsAllDay = false;
            evt.Notes = notes;
            await _db.SaveChangesAsync(ct);
            return null;
        }
        catch (GoogleException ex)
        {
            return "La sesión se ha guardado, pero el evento de Google Calendar no se pudo crear o actualizar: " + ex.Message;
        }
    }

    /// <summary>Descripción del evento: ejercicios con sus series, para verlos desde el calendario del móvil.</summary>
    private static string Describe(WorkoutSession w)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(w.Notes)) sb.AppendLine(w.Notes).AppendLine();
        foreach (var ex in JsonSerializer.Deserialize<List<WorkoutExercise>>(w.ExercisesJson, Json) ?? [])
        {
            var sets = ex.Sets.Select(s =>
            {
                var load = s.WeightKg is { } kg ? $"{kg.ToString("0.##", CultureInfo.InvariantCulture)} kg" : "pc";
                var tag = s.Type switch { "warmup" => " (cal.)", "dropset" => " (drop)", "failure" => " (fallo)", _ => "" };
                return $"{load}{(s.Reps is { } r ? $"×{r}" : "")}{tag}";
            });
            sb.AppendLine($"{ex.Name}: {string.Join(", ", sets)}");
        }
        return sb.ToString().TrimEnd();
    }
}
