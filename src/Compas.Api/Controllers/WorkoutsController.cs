using Compas.Api.Dtos;
using Compas.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Compas.Api.Controllers;

/// <summary>Entrenamientos programados (los que escribe el conector MCP) y tokens del conector, con la sesión de la app.</summary>
[ApiController]
[Route("api")]
public class WorkoutsController : ControllerBase
{
    private readonly WorkoutService _workouts;
    private readonly ApiTokenService _tokens;

    public WorkoutsController(WorkoutService workouts, ApiTokenService tokens)
    {
        _workouts = workouts;
        _tokens = tokens;
    }

    [HttpGet("workouts")]
    public async Task<ActionResult<List<WorkoutSessionDto>>> List([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        DateTime? f = null, t = null;
        if (!string.IsNullOrEmpty(from)) { if (!WorkoutService.TryParseDate(from, out var d)) return BadRequest("'from' debe ser yyyy-MM-dd."); f = d; }
        if (!string.IsNullOrEmpty(to)) { if (!WorkoutService.TryParseDate(to, out var d)) return BadRequest("'to' debe ser yyyy-MM-dd."); t = d; }
        return await _workouts.ListAsync(f, t, ct);
    }

    [HttpPut("workouts/{id:int}/completed")]
    public async Task<IActionResult> SetCompleted(int id, WorkoutCompletedDto dto, CancellationToken ct) =>
        await _workouts.SetCompletedAsync(id, dto.Completed, ct) ? NoContent() : NotFound();

    [HttpDelete("workouts/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try { return await _workouts.DeleteAsync(id, ct) ? NoContent() : NotFound(); }
        catch (GoogleException ex) { return Conflict(ex.Message); } // Google no dejó borrar el evento: no se borra nada
    }

    // ---- Tokens del conector ----

    [HttpGet("tokens")]
    public async Task<List<ApiTokenDto>> Tokens(CancellationToken ct) => await _tokens.ListAsync(ct);

    [HttpPost("tokens")]
    public async Task<CreatedApiTokenDto> CreateToken(CreateApiTokenRequest request, CancellationToken ct)
    {
        var (dto, value) = await _tokens.CreateAsync(request.Name, ct);
        return new CreatedApiTokenDto(dto, value, $"{Request.Scheme}://{Request.Host}/mcp/{value}");
    }

    [HttpDelete("tokens/{id:int}")]
    public async Task<IActionResult> RevokeToken(int id, CancellationToken ct) =>
        await _tokens.RevokeAsync(id, ct) ? NoContent() : NotFound();
}
