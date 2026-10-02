namespace Compas.Api.Models;

/// <summary>
/// Una tarea o unidad de trabajo. Se llama "WorkItem" (y no "Task") para no chocar
/// con System.Threading.Tasks.Task en el resto del código.
/// </summary>
public class WorkItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    public Priority Priority { get; set; } = Priority.Medium;

    /// <summary>Duración estimada en minutos. Usada por el motor de planificación para encajar huecos.</summary>
    public int EstimatedMinutes { get; set; } = 30;

    public DateTime? DueDate { get; set; }

    public WorkItemStatus Status { get; set; } = WorkItemStatus.Pending;

    /// <summary>
    /// Última vez que el motor de planificación asignó esta tarea a un hueco concreto.
    /// Null mientras la tarea está en la "bandeja" sin asignar.
    /// </summary>
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
