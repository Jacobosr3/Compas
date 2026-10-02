using Compas.Api.Data;
using Compas.Api.Dtos;
using Compas.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Compas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CalendarEventsController : ControllerBase
{
    private readonly CompasDbContext _db;

    public CalendarEventsController(CompasDbContext db) => _db = db;

    /// <summary>Lista eventos. Usa ?date=yyyy-MM-dd para filtrar por un día concreto.</summary>
    [HttpGet]
    public async Task<ActionResult<List<CalendarEventDto>>> GetAll([FromQuery] DateOnly? date, CancellationToken ct)
    {
        var query = _db.CalendarEvents.AsQueryable();

        if (date is not null)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue);
            var end = start.AddDays(1);
            query = query.Where(e => e.StartTime < end && e.EndTime > start);
        }

        var events = await query.OrderBy(e => e.StartTime).ToListAsync(ct);
        return events.Select(ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CalendarEventDto>> GetById(int id, CancellationToken ct)
    {
        var evt = await _db.CalendarEvents.FindAsync(new object?[] { id }, ct);
        return evt is null ? NotFound() : ToDto(evt);
    }

    [HttpPost]
    public async Task<ActionResult<CalendarEventDto>> Create(CreateCalendarEventDto dto, CancellationToken ct)
    {
        if (dto.EndTime <= dto.StartTime)
            return BadRequest("La hora de fin debe ser posterior a la hora de inicio.");

        var evt = new CalendarEvent
        {
            Title = dto.Title,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            IsAllDay = dto.IsAllDay,
            Notes = dto.Notes,
            Source = EventSource.Manual
        };

        _db.CalendarEvents.Add(evt);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = evt.Id }, ToDto(evt));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var evt = await _db.CalendarEvents.FindAsync(new object?[] { id }, ct);
        if (evt is null) return NotFound();

        _db.CalendarEvents.Remove(evt);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static CalendarEventDto ToDto(CalendarEvent e) =>
        new(e.Id, e.Title, e.StartTime, e.EndTime, e.IsAllDay, e.Notes);
}
