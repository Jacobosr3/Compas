namespace Compas.Api.Services;

/// <summary>Configuración del inicio de sesión (sección "Auth" de appsettings o user-secrets).</summary>
public class AuthOptions
{
    /// <summary>
    /// Correo del propietario. Si se indica, solo esa cuenta puede registrarse o entrar (con contraseña o con Google).
    /// Si está vacío, el primer usuario que se registre pasa a ser el propietario y el registro se cierra.
    /// </summary>
    public string? OwnerEmail { get; set; }

    /// <summary>Días que dura la sesión de un dispositivo recordado (se renueva con el uso).</summary>
    public int RememberDays { get; set; } = 90;

    /// <summary>Horas que dura la sesión si no se marca «Recordar este dispositivo».</summary>
    public int SessionHours { get; set; } = 12;
}
