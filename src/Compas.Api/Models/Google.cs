namespace Compas.Api.Models;

/// <summary>
/// Cuenta de Google conectada. Compás es de un solo usuario, así que solo hay una fila.
/// El token de actualización se guarda cifrado (ASP.NET Data Protection), nunca en claro.
/// </summary>
public class GoogleAccount
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;

    /// <summary>Token de actualización cifrado.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncError { get; set; }
}

/// <summary>Un calendario de la cuenta de Google y si el usuario quiere incluirlo en Compás.</summary>
public class GoogleCalendarSelection
{
    public int Id { get; set; }
    public string CalendarId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
    public bool IsPrimary { get; set; }
    public bool Selected { get; set; }
}
