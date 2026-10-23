namespace Compas.Api.Dtos;

public record ProjectDto(
    int Id,
    string Name,
    string? Description,
    string ColorHex,
    string Status,
    double ProgressPercent,
    int TotalWorkItems,
    int CompletedWorkItems);

public record CreateProjectDto(
    string Name,
    string? Description,
    string? ColorHex);

public record UpdateProjectDto(
    string Name,
    string? Description,
    string ColorHex,
    string Status);
