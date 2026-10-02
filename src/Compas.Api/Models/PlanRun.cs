namespace Compas.Api.Models;

/// <summary>
/// Una "ejecución" del motor de planificación para un día concreto. Cada vez que se
/// regenera el plan (porque cambió la agenda, se añadió una tarea urgente, etc.)
/// se crea un nuevo PlanRun, lo que permite comparar contra el anterior y detectar
/// qué bloques se movieron.
/// </summary>
public class PlanRun
{
    public int Id { get; set; }
    public DateOnly PlanDate { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PlanEntry> Entries { get; set; } = new List<PlanEntry>();
}

/// <summary>
/// Un bloque individual dentro de un plan diario: puede ser una tarea asignada,
/// un evento fijo de calendario, un descanso o un hueco libre.
/// </summary>
public class PlanEntry
{
    public int Id { get; set; }

    public int PlanRunId { get; set; }
    public PlanRun? PlanRun { get; set; }

    public int? WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }

    public int? CalendarEventId { get; set; }
    public CalendarEvent? CalendarEvent { get; set; }

    public PlanEntryKind Kind { get; set; }

    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    /// <summary>Explicación en lenguaje natural de por qué el motor colocó esto aquí.</summary>
    public string? Reason { get; set; }

    /// <summary>True si esta tarea ocupaba un hueco distinto en el PlanRun anterior del mismo día.</summary>
    public bool WasRescheduled { get; set; }
}
