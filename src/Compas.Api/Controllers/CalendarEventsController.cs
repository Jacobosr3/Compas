using Compas.Api.Data;
using Compas.Api.Dtos;
using Compas.Api.Models;
using Compas.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Compas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CalendarEventsController : ControllerBase
{
    private readonly CompasDbContext _db;
    private readonly GoogleCalendarService _google;

    public CalendarEventsController(CompasDbContext db, GoogleCalendarService google)
    {
        _db = db;
        _google = google;
    }

    /// <summary>
    /// Lista eventos. Usa ?date=yyyy-MM-dd para un día concreto, o ?from=...&to=... (ambos incluidos)
    /// para un rango de días, por ejemplo una semana.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CalendarEventDto>>> GetAll(
        [FromQuery] DateOnly? date, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var query = _db.CalendarEvents.AsQueryable();

        if (date is not null)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue);
            var end = start.AddDays(1);
            query = query.Where(e => e.StartTime < end && e.EndTime > start);
        }
        else if (from is not null && to is not null)
        {
            if (to < from) return BadRequest("La fecha final no puede ser anterior a la inicial.");
            var start = from.Value.ToDateTime(TimeOnly.MinValue);
            var end = to.Value.ToDateTime(TimeOnly.MinValue).AddDays(1);
            query = query.Where(e => e.StartTime < end && e.EndTime > start);
        }

        var events = await query.OrderBy(e => e.StartTime).ToListAsync(ct);
        var colors = await CalendarColorsAsync(ct);
        return events.Select(e => ToDto(e, colors)).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CalendarEventDto>> GetById(int id, CancellationToken ct)
    {
        var evt = await _db.CalendarEvents.FindAsync(new object?[] { id }, ct);
        return evt is null ? NotFound() : ToDto(evt, await CalendarColorsAsync(ct));
    }

    /// <summary>Crea un evento. Si hay una cuenta de Google conectada con permiso de escritura, también se crea allí.</summary>
    [HttpPost]
    public async Task<ActionResult<CalendarEventDto>> Create(CreateCalendarEventDto dto, CancellationToken ct)
    {
        var start = ToLocal(dto.StartTime);
        var end = ToLocal(dto.EndTime);
        if (end <= start)
            return BadRequest("La hora de fin debe ser posterior a la hora de inicio.");

        var evt = new CalendarEvent
        {
            Title = dto.Title,
            StartTime = start,
            EndTime = end,
            IsAllDay = dto.IsAllDay,
            Notes = dto.Notes,
            Source = EventSource.Manual
        };

        _db.CalendarEvents.Add(evt);
        await _db.SaveChangesAsync(ct);

        // El evento ya existe en Compás; si Google falla se avisa pero no se pierde.
        string? warning = null;
        try
        {
            warning = await _google.PushNewEventAsync(evt, dto.CalendarId, ct);
        }
        catch (GoogleException ex)
        {
            warning = "El evento se ha guardado en Compás, pero no se pudo crear en Google Calendar: " + ex.Message;
        }

        return CreatedAtAction(nameof(GetById), new { id = evt.Id }, ToDto(evt, await CalendarColorsAsync(ct), warning));
    }

    /// <summary>
    /// Edita un evento. Si está vinculado a Google (creado allí o enviado desde Compás) el cambio se
    /// envía primero a Google; si Google lo rechaza no se modifica nada, para que no se desincronicen.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<CalendarEventDto>> Update(int id, UpdateCalendarEventDto dto, CancellationToken ct)
    {
        var evt = await _db.CalendarEvents.FindAsync(new object?[] { id }, ct);
        if (evt is null) return NotFound();

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest("El título del evento es obligatorio.");

        var start = ToLocal(dto.StartTime);
        var end = ToLocal(dto.EndTime);
        if (end <= start)
            return BadRequest("La hora de fin debe ser posterior a la hora de inicio.");

        var title = dto.Title.Trim();
        var wasLinked = evt.ExternalId is not null;
        try
        {
            // Cambio de calendario: primero se mueve en Google y luego se envían el resto de cambios al nuevo calendario.
            if (wasLinked && !string.IsNullOrEmpty(dto.CalendarId) && dto.CalendarId != evt.ExternalCalendarId)
                await _google.PushMoveAsync(evt, dto.CalendarId, ct);

            await _google.PushUpdateAsync(evt, title, start, end, dto.IsAllDay, dto.Notes, ct);
        }
        catch (GoogleException ex)
        {
            return Conflict(ex.Message);
        }

        evt.Title = title;
        evt.StartTime = start;
        evt.EndTime = end;
        evt.IsAllDay = dto.IsAllDay;
        // En los eventos importados la nota es la ubicación de Google, que Compás no escribe de vuelta.
        if (evt.Source == EventSource.Manual) evt.Notes = dto.Notes;

        await _db.SaveChangesAsync(ct);

        // Un evento que solo estaba en Compás y se asigna a un calendario de Google se crea allí.
        string? warning = null;
        if (!wasLinked && !string.IsNullOrEmpty(dto.CalendarId))
        {
            try
            {
                warning = await _google.PushNewEventAsync(evt, dto.CalendarId, ct);
            }
            catch (GoogleException ex)
            {
                warning = "Los cambios se han guardado en Compás, pero no se pudo crear el evento en Google Calendar: " + ex.Message;
            }
        }

        return ToDto(evt, await CalendarColorsAsync(ct), warning);
    }

    /// <summary>Borra un evento; si está vinculado a Google, también se borra allí.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var evt = await _db.CalendarEvents.FindAsync(new object?[] { id }, ct);
        if (evt is null) return NotFound();

        try
        {
            await _google.PushDeleteAsync(evt, ct);
        }
        catch (GoogleException ex)
        {
            return Conflict(ex.Message);
        }

        _db.CalendarEvents.Remove(evt);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// El planificador trabaja con la hora local del equipo. Si el cliente manda una hora en UTC
    /// ("...Z") se pasa a local; si no, ya se interpreta como local.
    /// </summary>
    private static DateTime ToLocal(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;

    /// <summary>Color de cada calendario de Google (por id), para pintar sus eventos.</summary>
    private async Task<Dictionary<string, string?>> CalendarColorsAsync(CancellationToken ct) =>
        await _db.GoogleCalendarSelections.AsNoTracking().ToDictionaryAsync(c => c.CalendarId, c => c.ColorHex, ct);

    private static CalendarEventDto ToDto(CalendarEvent e, Dictionary<string, string?> colors, string? warning = null) =>
        new(e.Id, e.Title, e.StartTime, e.EndTime, e.IsAllDay, e.Notes, e.Source.ToString(), e.ExternalId != null, warning,
            e.ExternalCalendarId is not null && colors.TryGetValue(e.ExternalCalendarId, out var color) ? color : null,
            e.ExternalCalendarId);
}
