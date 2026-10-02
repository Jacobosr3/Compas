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
}
