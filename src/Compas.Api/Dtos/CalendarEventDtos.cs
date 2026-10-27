namespace Compas.Api.Dtos;

public record CalendarEventDto(
    int Id,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    bool IsAllDay,
    string? Notes,
    string Source);

public record CreateCalendarEventDto(
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    bool IsAllDay,
    string? Notes);
