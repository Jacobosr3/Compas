using Compas.Api.Dtos;
using Compas.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Compas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlannerController : ControllerBase
{
    private readonly PlannerService _planner;

    public PlannerController(PlannerService planner) => _planner = planner;

    /// <summary>Plan de hoy (lo genera si aún no existe). Esto alimenta el panel "Ahora mismo" y la línea de tiempo.</summary>
    [HttpGet("today")]
    public async Task<ActionResult<DailyPlanDto>> GetToday(CancellationToken ct)
        => await _planner.GetTodayAsync(ct);

    /// <summary>Genera (o regenera) el plan completo de una fecha desde el inicio de la jornada.</summary>
    [HttpPost("generate")]
    public async Task<ActionResult<DailyPlanDto>> Generate([FromQuery] DateOnly? date, CancellationToken ct)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Now);
        var dayStart = targetDate.ToDateTime(TimeOnly.MinValue);
        return await _planner.GenerateAsync(targetDate, dayStart, ct);
    }

    /// <summary>
    /// Vuelve a planificar el resto del día de hoy a partir de este instante.
    /// Llamar aquí cuando cambie algo: una tarea urgente nueva, una reunión añadida, etc.
    /// </summary>
    [HttpPost("replan")]
    public async Task<ActionResult<DailyPlanDto>> Replan(CancellationToken ct)
        => await _planner.ReplanFromNowAsync(ct);
}
