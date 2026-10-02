namespace Compas.Api.Dtos;

public record WorkItemDto(
    int Id,
    string Title,
    string? Description,
    int? ProjectId,
    string? ProjectName,
    string Priority,
    int EstimatedMinutes,
    DateTime? DueDate,
    string Status,
    DateTime? ScheduledStart,
    DateTime? ScheduledEnd);

public record CreateWorkItemDto(
    string Title,
    string? Description,
    int? ProjectId,
    string Priority,
    int EstimatedMinutes,
    DateTime? DueDate);

public record UpdateWorkItemDto(
    string Title,
    string? Description,
    int? ProjectId,
    string Priority,
    int EstimatedMinutes,
    DateTime? DueDate,
    string Status);
