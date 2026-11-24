using Compas.Api.Dtos;
using Compas.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Compas.Api.Controllers;

/// <summary>Comidas con foto del usuario de la sesión: subida, revisión y confirmación.</summary>
[ApiController]
[Route("api/meals")]
public class MealsController : ControllerBase
{
    private readonly MealService _meals;

    public MealsController(MealService meals) => _meals = meals;

    [HttpGet]
    public async Task<ActionResult<List<MealDto>>> List([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        DateTime? f = null, t = null;
        if (!string.IsNullOrEmpty(from)) { if (!WorkoutService.TryParseDate(from, out var d)) return BadRequest("'from' debe ser yyyy-MM-dd."); f = d; }
        if (!string.IsNullOrEmpty(to)) { if (!WorkoutService.TryParseDate(to, out var d)) return BadRequest("'to' debe ser yyyy-MM-dd."); t = d; }
        return await _meals.ListAsync(f, t, ct);
    }

    // La foto llega en base64 dentro del JSON (el navegador ya la reduce): 3 MB de imagen son unos 4 MB de texto.
    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<MealDto>> Create(MealCreateInput input, CancellationToken ct)
    {
        try { return await _meals.CreateAsync(input, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{id:int}/photo")]
    public async Task<IActionResult> Photo(int id, CancellationToken ct)
    {
        var photo = await _meals.GetPhotoAsync(id, ct);
        if (photo is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(photo.Value.Data, photo.Value.ContentType);
    }

    [HttpPut("{id:int}/confirm")]
    public async Task<ActionResult<MealDto>> Confirm(int id, MealConfirmInput input, CancellationToken ct)
    {
        try { return await _meals.ConfirmAsync(id, input, ct) is { } dto ? dto : NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{id:int}/reanalyze")]
    public async Task<ActionResult<MealDto>> Reanalyze(int id, CancellationToken ct)
    {
        try { return await _meals.ReanalyzeAsync(id, ct) is { } dto ? dto : NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await _meals.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("summary")]
    public async Task<ActionResult<NutritionSummaryDto>> Summary([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        var t = DateTime.Today;
        var f = t.AddDays(-6);
        if (!string.IsNullOrEmpty(from) && !WorkoutService.TryParseDate(from, out f)) return BadRequest("'from' debe ser yyyy-MM-dd.");
        if (!string.IsNullOrEmpty(to) && !WorkoutService.TryParseDate(to, out t)) return BadRequest("'to' debe ser yyyy-MM-dd.");
        if (t < f || (t - f).TotalDays > 366) return BadRequest("Rango no válido (máximo 366 días).");
        return await _meals.SummaryAsync(f, t, ct);
    }
}
