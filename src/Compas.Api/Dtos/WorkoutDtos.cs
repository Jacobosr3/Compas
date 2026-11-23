using Compas.Api.Models;

namespace Compas.Api.Dtos;

public record WorkoutSessionDto(int Id, string Title, string Date, string? Notes, bool Completed, string? Source, string? ExternalId, List<WorkoutExercise> Exercises);

public record WorkoutSessionInput(string Title, string Date, string? Notes, string? Source, string? ExternalId, List<WorkoutExercise>? Exercises);

public record WorkoutCompletedDto(bool Completed);

public record ApiTokenDto(int Id, string Name, string Prefix, DateTime CreatedAt, DateTime? LastUsedAt);

public record CreateApiTokenRequest(string? Name);

/// <summary>Respuesta de crear un token: el valor en claro solo aparece aquí.</summary>
public record CreatedApiTokenDto(ApiTokenDto Token, string Value, string McpUrl);
