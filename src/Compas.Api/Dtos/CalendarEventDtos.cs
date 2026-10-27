namespace Compas.Api.Dtos;

public record CalendarEventDto(
    int Id,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    bool IsAllDay,
    string? Notes,
    string Source,
    bool GoogleLinked,
    string? SyncWarning = null,
    /// <summary>Color del calendario de Google al que pertenece el evento (#rrggbb); null si no está vinculado.</summary>
    string? ColorHex = null,
    /// <summary>Id del calendario de Google donde está el evento; null si solo existe en Compás.</summary>
    string? CalendarId = null);

public record CreateCalendarEventDto(
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    bool IsAllDay,
    string? Notes,
    /// <summary>Calendario de Google donde crear el evento: null = el de por defecto; "" = solo en Compás.</summary>
    string? CalendarId = null);

public record UpdateCalendarEventDto(
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    bool IsAllDay,
    string? Notes,
    /// <summary>Mover el evento a este calendario de Google (null = no cambiarlo). Si el evento solo está en Compás, se crea allí.</summary>
    string? CalendarId = null);
