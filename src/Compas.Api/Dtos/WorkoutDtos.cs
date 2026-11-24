using Compas.Api.Models;

namespace Compas.Api.Dtos;

public record WorkoutSessionDto(int Id, string Title, string Date, string? StartTime, int? DurationMinutes, string? Notes, bool Completed, string? Source, string? ExternalId, List<WorkoutExercise> Exercises);

/// <summary>StartTime ("HH:mm") y DurationMinutes (por defecto 75) colocan la sesión en el calendario; sin StartTime queda solo el día.</summary>
public record WorkoutSessionInput(string Title, string Date, string? StartTime, int? DurationMinutes, string? Notes, string? Source, string? ExternalId, List<WorkoutExercise>? Exercises);

/// <summary>Resultado de guardar una sesión; CalendarWarning explica por qué no se pudo crear o actualizar su evento en Google Calendar.</summary>
public record WorkoutUpsertResult(bool Created, WorkoutSessionDto Session, string? CalendarWarning);

/// <summary>Bloque ocupado del calendario (para elegir huecos libres).</summary>
public record BusyBlockDto(string Title, string Start, string End, bool AllDay);

public record WorkoutCompletedDto(bool Completed);

public record ApiTokenDto(int Id, string Name, string Prefix, DateTime CreatedAt, DateTime? LastUsedAt);

/// <summary>Kind: "quick" crea un token de captura rápida (solo sube comidas); cualquier otro valor, el del conector MCP.</summary>
public record CreateApiTokenRequest(string? Name, string? Kind = null);

/// <summary>Respuesta de crear un token: el valor en claro solo aparece aquí.</summary>
public record CreatedApiTokenDto(ApiTokenDto Token, string Value, string McpUrl, string? QuickMealUrl = null);
