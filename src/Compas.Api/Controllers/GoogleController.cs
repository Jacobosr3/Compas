using Compas.Api.Dtos;
using Compas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Compas.Api.Controllers;

/// <summary>Conexión con Google Calendar: cuenta, calendarios incluidos y sincronización.</summary>
[ApiController]
[Route("api/[controller]")]
public class GoogleController : ControllerBase
{
    private readonly GoogleCalendarService _google;
    private readonly AuthService _auth;
    private readonly CurrentUser _currentUser;
    private readonly ILogger<GoogleController> _logger;

    public GoogleController(GoogleCalendarService google, AuthService auth, CurrentUser currentUser, ILogger<GoogleController> logger)
    {
        _google = google;
        _auth = auth;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>Estado de la conexión y calendarios de la cuenta (con los que están incluidos).</summary>
    [HttpGet("status")]
    public async Task<GoogleStatusDto> Status(CancellationToken ct) => await _google.GetStatusAsync(ct);

    /// <summary>
    /// Devuelve la dirección de Google a la que hay que enviar al usuario para que elija la cuenta
    /// y autorice el acceso de solo lectura a sus calendarios.
    /// </summary>
    [HttpPost("connect")]
    public ActionResult<GoogleConnectResponse> Connect(GoogleConnectRequest request)
    {
        try
        {
            return new GoogleConnectResponse(_google.BuildAuthUrl(request.ReturnUrl));
        }
        catch (GoogleException ex)
        {
            return Conflict(ex.Message); // 409: falta configurar las credenciales
        }
    }

    /// <summary>
    /// Destino de la redirección de Google tras elegir cuenta; vuelve al cliente web. Sirve para dos cosas:
    /// conectar el calendario (con sesión iniciada) o iniciar sesión en Compás con esa cuenta.
    /// No exige sesión porque en el segundo caso aún no la hay; el "state" (de un solo uso) ata la vuelta a quien salió.
    /// </summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken ct)
    {
        var oauth = _google.TakeState(state);
        if (oauth is null)
            return Redirect(QueryHelpers.AddQueryString(_google.DefaultReturnUrl(),
                new Dictionary<string, string?> { ["google"] = "error", ["message"] = "La solicitud de conexión no es válida o ha caducado." }));

        var back = oauth.ReturnUrl;
        var signIn = oauth.Purpose == GoogleOAuthPurpose.SignIn;
        // En el inicio de sesión los avisos viajan como "auth"; en la conexión del calendario, como "google".
        string Fail(string message) => QueryHelpers.AddQueryString(back,
            new Dictionary<string, string?> { [signIn ? "auth" : "google"] = "error", ["message"] = message });

        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
            return Redirect(Fail(error == "access_denied" ? "No has dado permiso a Compás." : "Google no completó la conexión."));

        // Conectar el calendario solo tiene sentido con una sesión iniciada.
        if (!signIn && User.Identity?.IsAuthenticated != true)
            return Redirect(Fail("Inicia sesión en Compás antes de conectar Google Calendar."));

        try
        {
            if (!signIn)
            {
                await _google.CompleteConnectionAsync(code, ct);
                return Redirect(QueryHelpers.AddQueryString(back, "google", "connected"));
            }

            // Primero se comprueba quién es y si tiene acceso; solo entonces se toca la conexión con el calendario.
            var google = await _google.ExchangeCodeAsync(code, ct);
            var user = await _auth.SignInWithGoogleAsync(google, ct);
            await _auth.SignInAsync(HttpContext, user, oauth.Remember);
            _currentUser.Set(user.Id); // esta petición aún no lleva la cookie nueva: lo que sigue (calendario) es de este usuario

            // Con la misma cuenta, el calendario queda conectado de una vez. Si ya hay otra distinta conectada no se sustituye.
            try
            {
                if (!await _google.IsConnectedToOtherAccountAsync(google.Email, ct))
                    await _google.SaveConnectionAsync(google, ct);
            }
            catch (GoogleException ex)
            {
                _logger.LogWarning(ex, "Sesión iniciada, pero no se pudo conectar el calendario de Google.");
            }
            return Redirect(QueryHelpers.AddQueryString(back, "auth", "ok"));
        }
        catch (AuthException ex)
        {
            return Redirect(Fail(ex.Message));
        }
        catch (GoogleException ex)
        {
            return Redirect(Fail(ex.Message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Es una navegación del navegador: nunca debe acabar en una página de error en bruto.
            _logger.LogError(ex, "Error al completar la conexión con Google.");
            return Redirect(Fail(signIn ? "No se pudo iniciar sesión con Google." : "No se pudo completar la conexión con Google."));
        }
    }

    /// <summary>Vuelve a leer la lista de calendarios de la cuenta.</summary>
    [HttpPost("calendars/refresh")]
    public async Task<ActionResult<GoogleStatusDto>> RefreshCalendars(CancellationToken ct)
    {
        try
        {
            await _google.RefreshCalendarsAsync(ct);
            return await _google.GetStatusAsync(ct);
        }
        catch (GoogleException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>Elige qué calendarios se incluyen en Compás y sincroniza sus eventos.</summary>
    [HttpPut("calendars")]
    public async Task<ActionResult<GoogleStatusDto>> SaveSelection(GoogleSelectionDto dto, CancellationToken ct)
    {
        try
        {
            await _google.SaveSelectionAsync(dto.CalendarIds ?? new List<string>(), ct);
            return await _google.GetStatusAsync(ct);
        }
        catch (GoogleException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>Elige el calendario de Google donde se crean los eventos nuevos de Compás.</summary>
    [HttpPut("target")]
    public async Task<ActionResult<GoogleStatusDto>> SetTarget(GoogleTargetDto dto, CancellationToken ct)
    {
        try
        {
            await _google.SetDefaultCalendarAsync(dto.CalendarId, ct);
            return await _google.GetStatusAsync(ct);
        }
        catch (GoogleException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Refresco automático: sincroniza solo si la última sincronización tiene más de 30 segundos y nunca falla
    /// (el error, si lo hay, queda en el estado). El cliente lo llama periódicamente para ver los cambios hechos en Google.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<GoogleStatusDto> Refresh(CancellationToken ct)
    {
        await _google.TrySyncIfStaleAsync(TimeSpan.FromSeconds(30), ct);
        return await _google.GetStatusAsync(ct);
    }

    /// <summary>Importa ahora los eventos de los calendarios incluidos.</summary>
    [HttpPost("sync")]
    public async Task<ActionResult<GoogleStatusDto>> Sync(CancellationToken ct)
    {
        try
        {
            await _google.SyncAsync(ct);
            return await _google.GetStatusAsync(ct);
        }
        catch (GoogleException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>Desconecta la cuenta: revoca el permiso y borra calendarios y eventos importados.</summary>
    [HttpDelete]
    public async Task<ActionResult<GoogleStatusDto>> Disconnect(CancellationToken ct)
    {
        await _google.DisconnectAsync(ct);
        return await _google.GetStatusAsync(ct);
    }
}
