namespace Compas.Api.Dtos;

public record UserProfileDto(
    double? HeightCm, string? BirthDate, string? Sex, string? ActivityLevel,
    string Phase, string? PhaseStartDate, double? TargetWeightKg, string? TargetDate, string? Notes);

public record BodyMeasurementDto(
    int Id, string Date, double? WeightKg, double? BodyFatPct, double? WaistCm, double? ChestCm,
    double? HipCm, double? ArmCm, double? ThighCm, string? Notes);

public record LiftGoalDto(
    int Id, string ExerciseName, string? HevyExerciseTemplateId, double TargetWeightKg, int TargetReps,
    string? TargetDate, string? Notes, bool Achieved);

public record TrainingSlotDto(int Id, int DayOfWeek, string StartTime, int DurationMinutes, string? Label);

public record ExerciseRuleDto(int Id, string ExerciseName, int RepMin, int RepMax, double IncrementKg, string? Notes);

/// <summary>Todo lo que necesita quien programa los pesos: perfil, últimas medidas, objetivos, huecos de entreno y reglas por ejercicio.</summary>
public record ProfileOverviewDto(
    UserProfileDto Profile, List<BodyMeasurementDto> Measurements, List<LiftGoalDto> LiftGoals,
    List<TrainingSlotDto> TrainingSlots, List<ExerciseRuleDto> ExerciseRules);

/// <summary>Hueco semanal de entreno: día 1 = lunes … 7 = domingo, hora "HH:mm".</summary>
public record TrainingSlotInput(int DayOfWeek, string StartTime, int? DurationMinutes, string? Label);

/// <summary>Regla de progresión; si ya existe una para ese ejercicio, la actualiza.</summary>
public record ExerciseRuleInput(string ExerciseName, int RepMin, int RepMax, double IncrementKg, string? Notes);

/// <summary>
/// Actualización parcial del perfil: solo se cambian los campos que vienen con valor.
/// Fechas en yyyy-MM-dd; Phase: Bulk, Maintenance o Cut.
/// </summary>
public record UpdateProfileInput(
    double? HeightCm, string? BirthDate, string? Sex, string? ActivityLevel,
    string? Phase, string? PhaseStartDate, double? TargetWeightKg, string? TargetDate, string? Notes);

/// <summary>Medidas de un día; si ya hay un registro de esa fecha, sustituye los campos que vienen con valor.</summary>
public record BodyMeasurementInput(
    string Date, double? WeightKg, double? BodyFatPct, double? WaistCm, double? ChestCm,
    double? HipCm, double? ArmCm, double? ThighCm, string? Notes);

/// <summary>Objetivo de fuerza; si ya existe uno para ese ejercicio, lo actualiza.</summary>
public record LiftGoalInput(
    string ExerciseName, double TargetWeightKg, int? TargetReps, string? HevyExerciseTemplateId,
    string? TargetDate, string? Notes, bool? Achieved);
