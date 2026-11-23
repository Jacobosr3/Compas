using System.Globalization;
using System.Text.Json;
using Compas.Api.Data;
using Compas.Api.Dtos;
using Compas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Compas.Api.Services;

/// <summary>Sesiones de entrenamiento: lógica compartida por la API REST y el conector MCP.</summary>
public class WorkoutService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly CompasDbContext _db;

    public WorkoutService(CompasDbContext db) => _db = db;

    public static bool TryParseDate(string? text, out DateTime date) =>
        DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static WorkoutSessionDto ToDto(WorkoutSession w) => new(
        w.Id, w.Title, w.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), w.Notes, w.Completed, w.Source, w.ExternalId,
        JsonSerializer.Deserialize<List<WorkoutExercise>>(w.ExercisesJson, Json) ?? []);

    public async Task<List<WorkoutSessionDto>> ListAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var q = _db.WorkoutSessions.AsNoTracking().AsQueryable();
        if (from is { } f) q = q.Where(w => w.Date >= f.Date);
        if (to is { } t) q = q.Where(w => w.Date <= t.Date);
        return (await q.OrderBy(w => w.Date).ThenBy(w => w.Id).ToListAsync(ct)).Select(ToDto).ToList();
    }

    /// <summary>Crea la sesión o, si ya existe una con el mismo Source+ExternalId (o, sin ellos, mismo día y título), la sustituye.</summary>
    public async Task<(WorkoutSessionDto Session, bool Created)> UpsertAsync(WorkoutSessionInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Title)) throw new ArgumentException("El título es obligatorio.");
        if (!TryParseDate(input.Date, out var date)) throw new ArgumentException("La fecha debe tener el formato yyyy-MM-dd.");

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
        w.Notes = input.Notes;
        w.Source = source;
        w.ExternalId = externalId;
        w.ExercisesJson = JsonSerializer.Serialize(input.Exercises ?? [], Json);
        w.UpdatedAt = DateTime.UtcNow;
        if (created) _db.WorkoutSessions.Add(w);
        await _db.SaveChangesAsync(ct);
        return (ToDto(w), created);
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

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var w = await _db.WorkoutSessions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (w is null) return false;
        _db.WorkoutSessions.Remove(w);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
