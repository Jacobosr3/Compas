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

public record DailyPlanDto(
    DateOnly Date,
    DateTime GeneratedAt,
    List<PlanEntryDto> Entries,
    PlanEntryDto? NowEntry);
