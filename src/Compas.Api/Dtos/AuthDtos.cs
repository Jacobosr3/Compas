namespace Compas.Api.Dtos;

/// <summary>Quién es el usuario de la sesión y qué opciones de acceso ofrece la app.</summary>
public record AuthStatusDto(
    bool Authenticated,
    string? Email,
    string? DisplayName,
    bool RegistrationOpen,
    bool GoogleConfigured);

public record RegisterDto(string Email, string Password, string? DisplayName, bool Remember = true);

public record GoogleSignInRequest(string? ReturnUrl, bool Remember = true);

public record LoginDto(string Email, string Password, bool Remember = true);
