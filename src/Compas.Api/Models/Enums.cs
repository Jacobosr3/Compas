namespace Compas.Api.Models;

public enum Priority
{
    Low,
    Medium,
    High,
    Urgent
}

public enum ProjectStatus
{
    Active,
    OnHold,
    Completed,
    Archived
}

public enum WorkItemStatus
{
    Pending,
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}

public enum EventSource
{
    Manual,
    Import
}

/// <summary>
/// Tipo de bloque dentro de un plan diario generado por el motor de planificación.
/// </summary>
public enum PlanEntryKind
{
    /// <summary>Tarea proveniente de un WorkItem, asignada por el motor.</summary>
    Task,
    /// <summary>Evento fijo importado del calendario (no se puede mover).</summary>
    CalendarEvent,
    /// <summary>Descanso insertado automáticamente tras un bloque largo.</summary>
    Break,
    /// <summary>Hueco libre que el motor no pudo o no necesitó rellenar.</summary>
    Free
}
