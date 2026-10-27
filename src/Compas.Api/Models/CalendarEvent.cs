namespace Compas.Api.Models;

/// <summary>
/// Un evento de agenda "fijo" (reunión, cita, etc.). El motor de planificación
/// nunca mueve estos bloques: los respeta como límites duros del día.
/// </summary>
public class CalendarEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsAllDay { get; set; }
    public EventSource Source { get; set; } = EventSource.Manual;
    public string? Notes { get; set; }

    /// <summary>Id del evento en el servicio de origen (Google); null en eventos manuales.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Id del calendario de origen (Google); null en eventos manuales.</summary>
    public string? ExternalCalendarId { get; set; }
}
