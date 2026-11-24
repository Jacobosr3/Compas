namespace Compas.Api.Models;

/// <summary>
/// Estado de una comida: Pending = foto subida, esperando que Claude la analice; Proposed = Claude ha estimado alimentos y calorías,
/// falta que el usuario lo revise; Confirmed = revisada por el usuario (solo estas cuentan en los totales).
/// </summary>
public enum MealStatus
{
    Pending,
    Proposed,
    Confirmed
}

/// <summary>Una comida registrada con foto (opcional) y, tras el análisis, la lista de alimentos con sus calorías y macros.</summary>
public class MealLog : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>Día de la comida (solo la fecha).</summary>
    public DateTime Date { get; set; }
    /// <summary>Hora aproximada, "HH:mm"; opcional.</summary>
    public string? Time { get; set; }
    /// <summary>breakfast, lunch, snack, dinner u other; opcional (Claude puede proponerlo).</summary>
    public string? MealType { get; set; }
    /// <summary>Texto del usuario para ayudar al análisis (p. ej. "con 2 cucharadas de aceite").</summary>
    public string? Note { get; set; }

    /// <summary>Foto ya reducida en el navegador (máx. 3 MB), guardada en la base de datos.</summary>
    public byte[]? Photo { get; set; }
    public string? PhotoContentType { get; set; }

    public MealStatus Status { get; set; } = MealStatus.Pending;
    /// <summary>Lista de <see cref="MealItem"/> serializada en JSON.</summary>
    public string ItemsJson { get; set; } = "[]";
    public double TotalCalories { get; set; }
    public double? ProteinG { get; set; }
    public double? CarbsG { get; set; }
    public double? FatG { get; set; }
    /// <summary>Observaciones de Claude sobre la estimación (incertidumbre, supuestos).</summary>
    public string? AnalysisNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AnalyzedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}

/// <summary>Un alimento de una comida con su cantidad estimada y sus calorías.</summary>
public record MealItem(string Name, string? Quantity, double Calories, double? ProteinG, double? CarbsG, double? FatG);
