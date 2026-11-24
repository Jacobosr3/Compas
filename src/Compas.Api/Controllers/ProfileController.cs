using Compas.Api.Dtos;
using Compas.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Compas.Api.Controllers;

/// <summary>Perfil, medidas corporales y objetivos de fuerza del usuario de la sesión.</summary>
[ApiController]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly ProfileService _profile;

    public ProfileController(ProfileService profile) => _profile = profile;

    [HttpGet]
    public async Task<ProfileOverviewDto> Get([FromQuery] int measurements = 60, CancellationToken ct = default) =>
        await _profile.GetAsync(measurements, ct);

    [HttpPut]
    public async Task<ActionResult<UserProfileDto>> Update(UpdateProfileInput input, CancellationToken ct)
    {
        try { return await _profile.UpdateProfileAsync(input, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("measurements")]
    public async Task<ActionResult<BodyMeasurementDto>> LogMeasurement(BodyMeasurementInput input, CancellationToken ct)
    {
        try { return await _profile.LogMeasurementAsync(input, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpDelete("measurements/{id:int}")]
    public async Task<IActionResult> DeleteMeasurement(int id, CancellationToken ct) =>
        await _profile.DeleteMeasurementAsync(id, ct) ? NoContent() : NotFound();

    [HttpPut("lift-goals")]
    public async Task<ActionResult<LiftGoalDto>> UpsertLiftGoal(LiftGoalInput input, CancellationToken ct)
    {
        try { return await _profile.UpsertLiftGoalAsync(input, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Sustituye todos los huecos semanales de entreno.</summary>
    [HttpPut("training-slots")]
    public async Task<ActionResult<List<TrainingSlotDto>>> SetTrainingSlots(List<TrainingSlotInput> slots, CancellationToken ct)
    {
        try { return await _profile.SetTrainingSlotsAsync(slots, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("exercise-rules")]
    public async Task<ActionResult<List<ExerciseRuleDto>>> UpsertExerciseRules(List<ExerciseRuleInput> rules, CancellationToken ct)
    {
        try { return await _profile.UpsertExerciseRulesAsync(rules, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpDelete("exercise-rules/{id:int}")]
    public async Task<IActionResult> DeleteExerciseRule(int id, CancellationToken ct) =>
        await _profile.DeleteExerciseRuleAsync(id, ct) ? NoContent() : NotFound();

    [HttpDelete("lift-goals/{id:int}")]
    public async Task<IActionResult> DeleteLiftGoal(int id, CancellationToken ct) =>
        await _profile.DeleteLiftGoalAsync(id, ct) ? NoContent() : NotFound();
}
