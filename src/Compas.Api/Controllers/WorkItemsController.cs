using Compas.Api.Data;
using Compas.Api.Dtos;
using Compas.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Compas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkItemsController : ControllerBase
{
    private readonly CompasDbContext _db;

    public WorkItemsController(CompasDbContext db) => _db = db;

    /// <summary>Lista todas las tareas. Usa ?status=Pending para filtrar por estado.</summary>
    [HttpGet]
    public async Task<ActionResult<List<WorkItemDto>>> GetAll([FromQuery] string? status, CancellationToken ct)
    {
        var query = _db.WorkItems.Include(w => w.Project).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<WorkItemStatus>(status, ignoreCase: true, out var parsed))
                return BadRequest($"Estado no válido: {status}");
            query = query.Where(w => w.Status == parsed);
        }

        var items = await query.OrderByDescending(w => w.CreatedAt).ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    /// <summary>Tareas pendientes que todavía no tienen hueco asignado en ningún plan: la "bandeja".</summary>
    [HttpGet("inbox")]
    public async Task<ActionResult<List<WorkItemDto>>> GetInbox(CancellationToken ct)
    {
        var items = await _db.WorkItems
            .Include(w => w.Project)
            .Where(w => w.Status == WorkItemStatus.Pending && w.ScheduledStart == null)
            .OrderByDescending(w => w.Priority)
            .ThenBy(w => w.DueDate)
            .ToListAsync(ct);

        return items.Select(ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkItemDto>> GetById(int id, CancellationToken ct)
    {
        var item = await _db.WorkItems.Include(w => w.Project).FirstOrDefaultAsync(w => w.Id == id, ct);
        return item is null ? NotFound() : ToDto(item);
    }

    [HttpPost]
    public async Task<ActionResult<WorkItemDto>> Create(CreateWorkItemDto dto, CancellationToken ct)
    {
        if (!Enum.TryParse<Priority>(dto.Priority, ignoreCase: true, out var priority))
            return BadRequest($"Prioridad no válida: {dto.Priority}");

        var item = new WorkItem
        {
            Title = dto.Title,
            Description = dto.Description,
            ProjectId = dto.ProjectId,
            Priority = priority,
            EstimatedMinutes = dto.EstimatedMinutes <= 0 ? 30 : dto.EstimatedMinutes,
            DueDate = dto.DueDate,
            Status = WorkItemStatus.Pending
        };

        _db.WorkItems.Add(item);
        await _db.SaveChangesAsync(ct);
        await _db.Entry(item).Reference(w => w.Project).LoadAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, ToDto(item));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateWorkItemDto dto, CancellationToken ct)
    {
        var item = await _db.WorkItems.FindAsync(new object?[] { id }, ct);
        if (item is null) return NotFound();

        if (!Enum.TryParse<Priority>(dto.Priority, ignoreCase: true, out var priority))
            return BadRequest($"Prioridad no válida: {dto.Priority}");
        if (!Enum.TryParse<WorkItemStatus>(dto.Status, ignoreCase: true, out var status))
            return BadRequest($"Estado no válido: {dto.Status}");

        item.Title = dto.Title;
        item.Description = dto.Description;
        item.ProjectId = dto.ProjectId;
        item.Priority = priority;
        item.EstimatedMinutes = dto.EstimatedMinutes;
        item.DueDate = dto.DueDate;

        var wasCompleted = item.Status == WorkItemStatus.Completed;
        item.Status = status;
        if (status == WorkItemStatus.Completed && !wasCompleted)
            item.CompletedAt = DateTime.UtcNow;

        // Si vuelve a quedar pendiente, libera su hueco para que el planificador la reasigne.
        if (status == WorkItemStatus.Pending)
        {
            item.ScheduledStart = null;
            item.ScheduledEnd = null;
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.WorkItems.FindAsync(new object?[] { id }, ct);
        if (item is null) return NotFound();

        _db.WorkItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static WorkItemDto ToDto(WorkItem w) => new(
        w.Id, w.Title, w.Description, w.ProjectId, w.Project?.Name,
        w.Priority.ToString(), w.EstimatedMinutes, w.DueDate, w.Status.ToString(),
        w.ScheduledStart, w.ScheduledEnd);
}
