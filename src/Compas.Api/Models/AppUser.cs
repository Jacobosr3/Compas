namespace Compas.Api.Models;

/// <summary>
/// Usuario de Compás. Hoy la app es de un solo propietario (ver AuthService.CanRegisterAsync), pero el usuario
/// ya es una entidad propia para poder asociarle datos más adelante sin cambiar el inicio de sesión.
/// </summary>
public class AppUser
{
    public int Id { get; set; }

    /// <summary>Correo en minúsculas: es el identificador de inicio de sesión.</summary>
    public string Email { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    /// <summary>Hash PBKDF2 de la contraseña (nunca la contraseña); null si el usuario solo entra con Google.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Identificador estable ("sub") de su cuenta de Google, una vez vinculada.</summary>
    public string? GoogleSubject { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}
