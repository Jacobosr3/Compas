using Compas.Api.Dtos;
using Compas.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Compas.Api.Controllers;

/// <summary>Conexión con Google Calendar: cuenta, calendarios incluidos y sincronización.</summary>
[ApiController]
[Route("api/[controller]")]
public class GoogleController : ControllerBase
{
    private readonly GoogleCalendarService _google;
    private readonly ILogger<GoogleController> _logger;

    public GoogleController(GoogleCalendarService google, ILogger<GoogleController> logger)
    {
        _google = google;
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

    /// <summary>Destino de la redirección de Google tras elegir cuenta; vuelve al cliente web.</summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken ct)
    {
        var back = _google.TakeReturnUrl(state);
        if (back is null)
            return Redirect(QueryHelpers.AddQueryString(_google.DefaultReturnUrl,
                new Dictionary<string, string?> { ["google"] = "error", ["message"] = "La solicitud de conexión no es válida o ha caducado." }));

        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
            return Redirect(QueryHelpers.AddQueryString(back,
                new Dictionary<string, string?> { ["google"] = "error", ["message"] = error == "access_denied" ? "No has dado permiso a Compás." : "Google no completó la conexión." }));

        try
        {
            await _google.CompleteConnectionAsync(code, ct);
            return Redirect(QueryHelpers.AddQueryString(back, "google", "connected"));
        }
        catch (GoogleException ex)
        {
            return Redirect(QueryHelpers.AddQueryString(back,
                new Dictionary<string, string?> { ["google"] = "error", ["message"] = ex.Message }));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Es una navegación del navegador: nunca debe acabar en una página de error en bruto.
            _logger.LogError(ex, "Error al completar la conexión con Google.");
            return Redirect(QueryHelpers.AddQueryString(back,
                new Dictionary<string, string?> { ["google"] = "error", ["message"] = "No se pudo completar la conexión con Google." }));
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
