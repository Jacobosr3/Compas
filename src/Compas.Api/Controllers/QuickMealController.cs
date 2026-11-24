using Compas.Api.Dtos;
using Compas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Compas.Api.Controllers;

/// <summary>
/// Captura rápida de comidas desde un atajo del móvil (Atajos de iOS, Tasker...): recibe la foto tal cual en el cuerpo de
/// la petición y crea la comida pendiente de analizar con la fecha y hora actuales. No usa la cookie de sesión: se
/// autentica con un token de captura (cmq_...) en "Authorization: Bearer", que solo vale para esto, no para el MCP.
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("quick")]
[Route("api/quick")]
public class QuickMealController : ControllerBase
{
    private readonly ApiTokenService _tokens;
    private readonly MealService _meals;
    private readonly CurrentUser _currentUser;

    public QuickMealController(ApiTokenService tokens, MealService meals, CurrentUser currentUser)
    {
        _tokens = tokens;
        _meals = meals;
        _currentUser = currentUser;
    }

    /// <param name="note">Nota opcional (máx. 500 caracteres).</param>
    /// <param name="date">yyyy-MM-dd; por defecto, hoy.</param>
    /// <param name="time">HH:mm; por defecto, la hora actual.</param>
    [HttpPost("meal")]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromQuery] string? note, [FromQuery] string? date, [FromQuery] string? time, CancellationToken ct)
    {
        var header = Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : null;
        var userId = await _tokens.ResolveUserAsync(token, ct, ApiTokenService.QuickPrefix);
        if (userId is null)
        {
            Response.Headers.WWWAuthenticate = "Bearer";
            return Unauthorized();
        }
        _currentUser.Set(userId.Value);

        // El tipo MIME puede venir con parámetros ("image/jpeg; charset=...").
        var contentType = (Request.ContentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();

        using var ms = new MemoryStream();
        try { await Request.Body.CopyToAsync(ms, ct); }
        catch (Microsoft.AspNetCore.Http.BadHttpRequestException)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, "La foto es demasiado grande: reduce su tamaño en el atajo (máx. 3 MB).");
        }
        if (ms.Length == 0 && string.IsNullOrWhiteSpace(note)) return BadRequest("Envía la foto en el cuerpo de la petición.");

        var now = DateTime.Now; // la zona horaria del servidor es Europe/Madrid (TZ del Dockerfile)
        var input = new MealCreateInput(
            string.IsNullOrWhiteSpace(date) ? now.ToString("yyyy-MM-dd") : date,
            string.IsNullOrWhiteSpace(time) ? now.ToString("HH:mm") : time,
            null,
            note,
            ms.Length == 0 ? null : Convert.ToBase64String(ms.ToArray()),
            ms.Length == 0 ? null : contentType);
        try
        {
            var meal = await _meals.CreateAsync(input, ct);
            return Ok(new { meal.Id, meal.Date, meal.Time, meal.Status });
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }
}
