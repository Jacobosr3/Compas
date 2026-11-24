namespace Compas.Api.Models;

/// <summary>
/// Token personal para que una automatización externa (p. ej. Claude) use el conector MCP de Compás en nombre de un usuario.
/// Solo se guarda el hash SHA-256: el token en claro se muestra una vez, al crearlo.
/// </summary>
public class ApiToken : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    /// <summary>Primeros caracteres del token, solo para reconocerlo en la lista.</summary>
    public string Prefix { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>Sesión de entrenamiento programada para un día, con sus ejercicios y pesos objetivo.</summary>
public class WorkoutSession : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>Día del entrenamiento (sin hora).</summary>
    public DateTime Date { get; set; }
    public string? Notes { get; set; }
    public bool Completed { get; set; }

    /// <summary>Hora de inicio ("HH:mm"); con ella la sesión se coloca como evento en el calendario (y en Google Calendar). Null = solo el día.</summary>
    public string? StartTime { get; set; }
    public int? DurationMinutes { get; set; }
    /// <summary>Evento de calendario vinculado a esta sesión (se crea, mueve y borra con ella); null si no tiene hora.</summary>
    public int? CalendarEventId { get; set; }

    /// <summary>Origen del dato, p. ej. "hevy"; permite que la automatización reescriba sus propias sesiones sin duplicar.</summary>
    public string? Source { get; set; }
    /// <summary>Identificador estable en el origen (p. ej. id de rutina de Hevy + fecha). Con Source forma la clave de upsert.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Ejercicios y series en JSON (lista de <see cref="WorkoutExercise"/>): solo se leen y escriben enteros.</summary>
    public string ExercisesJson { get; set; } = "[]";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public record WorkoutSet(string Type, double? WeightKg, int? Reps);

public record WorkoutExercise(string Name, string? Notes, List<WorkoutSet> Sets);
