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
    /// <param name="minutes">Tiempo máximo a dedicar a tareas (opcional; sin él se rellena todo el día).</param>
    [HttpPost("generate")]
    public async Task<ActionResult<DailyPlanDto>> Generate([FromQuery] DateOnly? date, [FromQuery] int? minutes, CancellationToken ct)
    {
        if (minutes is <= 0) return BadRequest("El tiempo a dedicar debe ser mayor que 0.");

        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Now);
        var dayStart = targetDate.ToDateTime(TimeOnly.MinValue);
        return await _planner.GenerateAsync(targetDate, dayStart, minutes, null, ct: ct);
    }

    /// <summary>
    /// Vuelve a planificar el resto del día de hoy a partir de este instante.
    /// Llamar aquí cuando cambie algo: una tarea urgente nueva, una reunión añadida, etc.
    /// </summary>
    [HttpPost("replan")]
    public async Task<ActionResult<DailyPlanDto>> Replan([FromQuery] int? minutes, CancellationToken ct)
    {
        if (minutes is <= 0) return BadRequest("El tiempo a dedicar debe ser mayor que 0.");
        return await _planner.ReplanFromNowAsync(minutes, ct);
    }

    /// <summary>Vacía el plan de hoy y devuelve a la bandeja todas las tareas programadas.</summary>
    [HttpPost("clear")]
    public async Task<ActionResult<DailyPlanDto>> Clear(CancellationToken ct)
        => await _planner.ClearScheduledAsync(ct);

    /// <summary>Aplica el orden manual de tareas elegido por el usuario y recoloca el resto del día.</summary>
    [HttpPost("reorder")]
    public async Task<ActionResult<DailyPlanDto>> Reorder([FromBody] ReorderPlanDto dto, [FromQuery] int? minutes, CancellationToken ct)
    {
        if (minutes is <= 0) return BadRequest("El tiempo a dedicar debe ser mayor que 0.");
        return await _planner.ReorderTodayAsync(dto.WorkItemIds, minutes, ct);
    }
}
