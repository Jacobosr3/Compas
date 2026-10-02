namespace Compas.Api.Dtos;

public record ProjectDto(
    int Id,
    string Name,
    string? Description,
    string ColorHex,
    string Status,
    DateTime? TargetDate,
    double ProgressPercent,
    int TotalWorkItems,
    int CompletedWorkItems);

public record CreateProjectDto(
    string Name,
    string? Description,
    string? ColorHex,
    DateTime? TargetDate);

public record UpdateProjectDto(
    string Name,
    string? Description,
    string ColorHex,
    string Status,
    DateTime? TargetDate);
