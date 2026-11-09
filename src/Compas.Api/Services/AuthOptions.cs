namespace Compas.Api.Services;

/// <summary>Configuración del inicio de sesión (sección "Auth" de appsettings o user-secrets).</summary>
public class AuthOptions
{
    /// <summary>Cualquiera puede crear una cuenta (con correo y contraseña o con Google); cada cuenta tiene sus propios datos.</summary>
    public bool AllowRegistration { get; set; } = true;

    /// <summary>
    /// Si se indica, solo estos correos pueden crear cuenta (útil para una instalación privada). Quien ya tiene cuenta
    /// puede seguir entrando aunque no esté en la lista.
    /// </summary>
    public List<string> AllowedEmails { get; set; } = new();

    /// <summary>Días que dura la sesión de un dispositivo recordado (se renueva con el uso).</summary>
    public int RememberDays { get; set; } = 90;

    /// <summary>Horas que dura la sesión si no se marca «Recordar este dispositivo».</summary>
    public int SessionHours { get; set; } = 12;
}
