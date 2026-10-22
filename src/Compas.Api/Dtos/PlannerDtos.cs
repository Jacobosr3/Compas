namespace Compas.Api.Dtos;

public record PlanEntryDto(
    int Id,
    string Kind,
    string Title,
    DateTime Start,
    DateTime End,
    string? ProjectName,
    string? Priority,
    string? Reason,
    bool WasRescheduled,
    bool IsCurrent,
    int? WorkItemId,
    int? CalendarEventId);

/// <summary>Orden manual de las tareas del plan: ids de WorkItem, de la primera a la última.</summary>
public record ReorderPlanDto(List<int> WorkItemIds);

public record DailyPlanDto(
    DateOnly Date,
    DateTime GeneratedAt,
    List<PlanEntryDto> Entries,
    PlanEntryDto? NowEntry);
