namespace Compas.Api.Services;

/// <summary>
/// Configuración de la integración con Google Calendar (sección "Google" de appsettings).
/// ClientId y ClientSecret son credenciales: van en user-secrets, no en el repositorio.
/// </summary>
public class GoogleOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>URI de redirección registrada en Google Cloud; debe apuntar a /api/google/callback de la API.</summary>
    public string RedirectUri { get; set; } = "http://localhost:5080/api/google/callback";

    /// <summary>Dirección del cliente web a la que se vuelve si no se conoce otra (p. ej. error de estado).</summary>
    public string DefaultClientUrl { get; set; } = "http://localhost:5174";

    /// <summary>Días hacia delante cuyos eventos se importan.</summary>
    public int SyncDays { get; set; } = 90;

    /// <summary>Días hacia atrás (para ver semanas pasadas en la agenda).</summary>
    public int SyncPastDays { get; set; } = 14;

    // Endpoints de Google; se pueden cambiar solo para pruebas con un servidor simulado.
    public string AuthUrl { get; set; } = "https://accounts.google.com/o/oauth2/v2/auth";
    public string TokenUrl { get; set; } = "https://oauth2.googleapis.com/token";
    public string RevokeUrl { get; set; } = "https://oauth2.googleapis.com/revoke";
    public string UserInfoUrl { get; set; } = "https://openidconnect.googleapis.com/v1/userinfo";
    public string CalendarApiBase { get; set; } = "https://www.googleapis.com/calendar/v3";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
