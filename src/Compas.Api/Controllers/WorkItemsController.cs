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

    /// <summary>Lista todas las tareas. Usa ?status=Pending y/o ?projectId=3 para filtrar.</summary>
    [HttpGet]
    public async Task<ActionResult<List<WorkItemDto>>> GetAll(
        [FromQuery] string? status, [FromQuery] int? projectId, CancellationToken ct)
    {
        var query = _db.WorkItems.Include(w => w.Project).Include(w => w.Subtasks)
            .Where(w => w.ParentId == null);

        if (projectId is not null)
            query = query.Where(w => w.ProjectId == projectId);

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
            .Include(w => w.Subtasks)
            .Where(w => w.ParentId == null && w.Status == WorkItemStatus.Pending && w.ScheduledStart == null)
            .OrderByDescending(w => w.Priority)
            .ThenBy(w => w.DueDate)
            .ToListAsync(ct);

        return items.Select(ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkItemDto>> GetById(int id, CancellationToken ct)
    {
        var item = await _db.WorkItems.Include(w => w.Project).Include(w => w.Subtasks)
            .FirstOrDefaultAsync(w => w.Id == id && w.ParentId == null, ct);
        return item is null ? NotFound() : ToDto(item);
    }

    [HttpPost]
    public async Task<ActionResult<WorkItemDto>> Create(CreateWorkItemDto dto, CancellationToken ct)
    {
        if (!Enum.TryParse<Priority>(dto.Priority, ignoreCase: true, out var priority))
            return BadRequest($"Prioridad no válida: {dto.Priority}");
        if (ValidateEstimate(dto.EstimatedHours, dto.EstimatedMinutes) is { } estimateError)
            return BadRequest(estimateError);

        var totalMinutes = dto.EstimatedHours * 60 + dto.EstimatedMinutes;

        var item = new WorkItem
        {
            Title = dto.Title,
            Description = dto.Description,
            ProjectId = dto.ProjectId,
            Priority = priority,
            EstimatedMinutes = totalMinutes <= 0 ? 30 : totalMinutes,
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
        var item = await _db.WorkItems.FirstOrDefaultAsync(w => w.Id == id && w.ParentId == null, ct);
        if (item is null) return NotFound();

        if (!Enum.TryParse<Priority>(dto.Priority, ignoreCase: true, out var priority))
            return BadRequest($"Prioridad no válida: {dto.Priority}");
        if (!Enum.TryParse<WorkItemStatus>(dto.Status, ignoreCase: true, out var status))
            return BadRequest($"Estado no válido: {dto.Status}");
        if (ValidateEstimate(dto.EstimatedHours, dto.EstimatedMinutes) is { } estimateError)
            return BadRequest(estimateError);

        item.Title = dto.Title;
        item.Description = dto.Description;
        item.ProjectId = dto.ProjectId;
        item.Priority = priority;
        item.EstimatedMinutes = dto.EstimatedHours * 60 + dto.EstimatedMinutes;
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
        var item = await _db.WorkItems.FirstOrDefaultAsync(w => w.Id == id && w.ParentId == null, ct);
        if (item is null) return NotFound();

        // Las subtareas se borran en cascada con su tarea padre.
        _db.WorkItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Añade una subtarea (paso) a una tarea.</summary>
    [HttpPost("{id:int}/subtasks")]
    public async Task<ActionResult<SubtaskDto>> CreateSubtask(int id, CreateSubtaskDto dto, CancellationToken ct)
    {
        var title = dto.Title?.Trim();
        if (string.IsNullOrEmpty(title)) return BadRequest("El título de la subtarea es obligatorio.");

        var parentExists = await _db.WorkItems.AnyAsync(w => w.Id == id && w.ParentId == null, ct);
        if (!parentExists) return NotFound();

        var sub = new WorkItem { Title = title, ParentId = id, EstimatedMinutes = 0, Status = WorkItemStatus.Pending };
        _db.WorkItems.Add(sub);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id }, ToSubtaskDto(sub));
    }

    [HttpPut("{id:int}/subtasks/{subtaskId:int}")]
    public async Task<IActionResult> UpdateSubtask(int id, int subtaskId, UpdateSubtaskDto dto, CancellationToken ct)
    {
        var title = dto.Title?.Trim();
        if (string.IsNullOrEmpty(title)) return BadRequest("El título de la subtarea es obligatorio.");

        var sub = await _db.WorkItems.FirstOrDefaultAsync(w => w.Id == subtaskId && w.ParentId == id, ct);
        if (sub is null) return NotFound();

        sub.Title = title;
        var wasCompleted = sub.Status == WorkItemStatus.Completed;
        sub.Status = dto.IsCompleted ? WorkItemStatus.Completed : WorkItemStatus.Pending;
        if (dto.IsCompleted && !wasCompleted) sub.CompletedAt = DateTime.UtcNow;
        if (!dto.IsCompleted) sub.CompletedAt = null;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}/subtasks/{subtaskId:int}")]
    public async Task<IActionResult> DeleteSubtask(int id, int subtaskId, CancellationToken ct)
    {
        var sub = await _db.WorkItems.FirstOrDefaultAsync(w => w.Id == subtaskId && w.ParentId == id, ct);
        if (sub is null) return NotFound();

        _db.WorkItems.Remove(sub);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // En BD se guarda el total en minutos; la API lo expone como horas + minutos (0-59).
    private static string? ValidateEstimate(int hours, int minutes)
    {
        if (hours < 0) return "Las horas estimadas no pueden ser negativas.";
        if (minutes < 0 || minutes > 59) return "Los minutos estimados deben estar entre 0 y 59.";
        return null;
    }

    private static WorkItemDto ToDto(WorkItem w) => new(
        w.Id, w.Title, w.Description, w.ProjectId, w.Project?.Name,
        w.Priority.ToString(), w.EstimatedMinutes / 60, w.EstimatedMinutes % 60, w.DueDate, w.Status.ToString(),
        w.ScheduledStart, w.ScheduledEnd,
        w.Subtasks.OrderBy(s => s.Id).Select(ToSubtaskDto).ToList());

    private static SubtaskDto ToSubtaskDto(WorkItem s) =>
        new(s.Id, s.Title, s.Status == WorkItemStatus.Completed);
}
