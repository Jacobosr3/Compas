namespace Compas.Api.Dtos;

public record CalendarEventDto(
    int Id,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    bool IsAllDay,
    string? Notes);

public record CreateCalendarEventDto(
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    bool IsAllDay,
    string? Notes);
