using Compas.Api.Models;

namespace Compas.Api.Dtos;

public record MealDto(
    int Id, string Date, string? Time, string? MealType, string? Note, bool HasPhoto, string Status,
    List<MealItem> Items, double TotalCalories, double? ProteinG, double? CarbsG, double? FatG,
    string? AnalysisNotes, DateTime CreatedAt, DateTime? AnalyzedAt, DateTime? ConfirmedAt);

/// <summary>
/// Comida nueva. La foto va en base64 (ya reducida en el navegador; máx. 3 MB) con su tipo MIME (image/jpeg, image/png o image/webp).
/// Hace falta al menos foto o nota.
/// </summary>
public record MealCreateInput(string Date, string? Time, string? MealType, string? Note, string? PhotoBase64, string? PhotoContentType);

/// <summary>Alimento propuesto por Claude o corregido por el usuario.</summary>
public record MealItemInput(string Name, string? Quantity, double Calories, double? ProteinG, double? CarbsG, double? FatG);

/// <summary>Propuesta de Claude para una comida pendiente. Nunca la confirma.</summary>
public record MealAnalysisInput(int Id, List<MealItemInput> Items, string? MealType, string? Notes);

/// <summary>Confirmación del usuario; si vienen alimentos, sustituyen a los propuestos.</summary>
public record MealConfirmInput(List<MealItemInput>? Items, string? MealType, string? Date, string? Time, string? Note);

public record NutritionDayDto(string Date, int Meals, double Calories, double ProteinG, double CarbsG, double FatG);

/// <summary>Gasto y objetivo calórico estimados con Mifflin-St Jeor; Note explica lo que falta si no se pudo calcular.</summary>
public record NutritionTargetsDto(
    double? WeightKg, double? Bmr, double? MaintenanceCalories, double? TargetCalories, string Phase, string? Note);

public record NutritionSummaryDto(
    string From, string To, List<NutritionDayDto> Days, double AverageCalories, int PendingMeals, int ProposedMeals,
    NutritionTargetsDto Targets);
