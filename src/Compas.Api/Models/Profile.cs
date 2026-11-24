namespace Compas.Api.Models;

/// <summary>Fase actual: volumen (superávit), mantenimiento o definición (déficit).</summary>
public enum NutritionPhase
{
    Bulk,
    Maintenance,
    Cut
}

/// <summary>
/// Datos personales y objetivo de composición corporal del usuario (una fila por usuario). Los lee la automatización de
/// Claude para decidir la progresión de pesos y, más adelante, el contador de calorías.
/// </summary>
public class UserProfile : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }

    public double? HeightCm { get; set; }
    public DateTime? BirthDate { get; set; }
    /// <summary>"male" o "female" (para estimar el gasto energético); opcional.</summary>
    public string? Sex { get; set; }
    /// <summary>sedentary, light, moderate, active o very_active (para estimar el gasto energético); opcional.</summary>
    public string? ActivityLevel { get; set; }

    public NutritionPhase Phase { get; set; } = NutritionPhase.Maintenance;
    public DateTime? PhaseStartDate { get; set; }
    public double? TargetWeightKg { get; set; }
    public DateTime? TargetDate { get; set; }
    public string? Notes { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Registro de peso y medidas de un día (una fila por usuario y fecha).</summary>
public class BodyMeasurement : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime Date { get; set; }

    public double? WeightKg { get; set; }
    public double? BodyFatPct { get; set; }
    public double? WaistCm { get; set; }
    public double? ChestCm { get; set; }
    public double? HipCm { get; set; }
    public double? ArmCm { get; set; }
    public double? ThighCm { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Objetivo de fuerza: llegar a un peso × repeticiones en un ejercicio.</summary>
public class LiftGoal : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>Nombre del ejercicio tal como aparece en Hevy; con el usuario forma la clave de upsert (sin distinguir mayúsculas).</summary>
    public string ExerciseName { get; set; } = string.Empty;
    /// <summary>Id de la plantilla del ejercicio en Hevy, si se conoce.</summary>
    public string? HevyExerciseTemplateId { get; set; }

    public double TargetWeightKg { get; set; }
    public int TargetReps { get; set; } = 1;
    public DateTime? TargetDate { get; set; }
    public string? Notes { get; set; }
    public bool Achieved { get; set; }
}

/// <summary>
/// Hueco semanal en el que el usuario puede entrenar. Quien programa los entrenos (la automatización de Claude) los coloca
/// dentro de estos huecos, comprobando antes que no choquen con otros eventos del calendario.
/// </summary>
public class TrainingSlot : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>Día de la semana, 1 = lunes … 7 = domingo.</summary>
    public int DayOfWeek { get; set; }
    /// <summary>Hora de inicio, "HH:mm".</summary>
    public string StartTime { get; set; } = "18:00";
    /// <summary>Tiempo disponible, en minutos.</summary>
    public int DurationMinutes { get; set; } = 75;
    /// <summary>Tipo de sesión que se prefiere ese día (p. ej. "Push"); vacío = a elección de quien programa.</summary>
    public string? Label { get; set; }
}

/// <summary>Regla de progresión de un ejercicio: rango de repeticiones y cuánto subir al completarlo.</summary>
public class ExerciseRule : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>Nombre del ejercicio como en Hevy; con el usuario forma la clave de upsert (sin distinguir mayúsculas).</summary>
    public string ExerciseName { get; set; } = string.Empty;
    /// <summary>Repeticiones mínimas del rango objetivo: por debajo se mantiene o se baja el peso.</summary>
    public int RepMin { get; set; }
    /// <summary>Repeticiones máximas del rango objetivo: al llegar a este número en todas las series se sube el peso.</summary>
    public int RepMax { get; set; }
    /// <summary>Salto de peso al progresar, en kg (el escalón real del material: mancuernas, polea, barra…).</summary>
    public double IncrementKg { get; set; }
    public string? Notes { get; set; }
}
