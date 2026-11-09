using Compas.Api.Dtos;
using Compas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace Compas.Api.Controllers;

/// <summary>Inicio de sesión: correo y contraseña, o con la cuenta de Google (que además conecta el calendario).</summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly GoogleCalendarService _google;

    public AuthController(AuthService auth, GoogleCalendarService google)
    {
        _auth = auth;
        _google = google;
    }

    /// <summary>Estado de la sesión y qué ofrece la pantalla de acceso (crear cuenta, Google).</summary>
    [HttpGet("status")]
    public async Task<AuthStatusDto> Status(CancellationToken ct)
    {
        var authenticated = User.Identity?.IsAuthenticated == true;
        return new AuthStatusDto(
            authenticated,
            authenticated ? User.FindFirstValue(ClaimTypes.Email) : null,
            authenticated ? User.FindFirstValue(ClaimTypes.Name) : null,
            await _auth.IsRegistrationOpenAsync(ct),
            _google.IsConfigured);
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthStatusDto>> Register(RegisterDto dto, CancellationToken ct)
    {
        try
        {
            var user = await _auth.RegisterAsync(dto.Email, dto.Password, dto.DisplayName, ct);
            await _auth.SignInAsync(HttpContext, user, dto.Remember);
            return new AuthStatusDto(true, user.Email, user.DisplayName ?? user.Email, await _auth.IsRegistrationOpenAsync(ct), _google.IsConfigured);
        }
        catch (AuthException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthStatusDto>> Login(LoginDto dto, CancellationToken ct)
    {
        try
        {
            var user = await _auth.LoginAsync(dto.Email, dto.Password, ct);
            await _auth.SignInAsync(HttpContext, user, dto.Remember);
            return new AuthStatusDto(true, user.Email, user.DisplayName ?? user.Email, await _auth.IsRegistrationOpenAsync(ct), _google.IsConfigured);
        }
        catch (AuthException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await AuthService.SignOutAsync(HttpContext);
        return NoContent();
    }

    /// <summary>
    /// Empieza el inicio de sesión con Google: devuelve la dirección de Google a la que debe navegar el cliente
    /// (igual que "connect"; una redirección HTTP no atraviesa bien los proxies de desarrollo). Google vuelve a
    /// /api/google/callback. Pide también el permiso del calendario para dejarlo conectado de una vez.
    /// </summary>
    [HttpPost("google")]
    [EnableRateLimiting("oauth")]
    public ActionResult<GoogleConnectResponse> GoogleLogin(GoogleSignInRequest request)
    {
        try
        {
            return new GoogleConnectResponse(_google.BuildAuthUrl(request.ReturnUrl, GoogleOAuthPurpose.SignIn, request.Remember));
        }
        catch (GoogleException ex)
        {
            return Conflict(ex.Message); // 409: falta configurar las credenciales
        }
    }
}
