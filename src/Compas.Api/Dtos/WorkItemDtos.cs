namespace Compas.Api.Dtos;

public record WorkItemDto(
    int Id,
    string Title,
    string? Description,
    int? ProjectId,
    string? ProjectName,
    string Priority,
    int EstimatedHours,
    int EstimatedMinutes,
    DateTime? DueDate,
    string Status,
    DateTime? ScheduledStart,
    DateTime? ScheduledEnd,
    List<SubtaskDto> Subtasks);

public record SubtaskDto(int Id, string Title, bool IsCompleted);

public record CreateSubtaskDto(string Title);

public record UpdateSubtaskDto(string Title, bool IsCompleted);

public record CreateWorkItemDto(
    string Title,
    string? Description,
    int? ProjectId,
    string Priority,
    int EstimatedHours,
    int EstimatedMinutes,
    DateTime? DueDate);

public record UpdateWorkItemDto(
    string Title,
    string? Description,
    int? ProjectId,
    string Priority,
    int EstimatedHours,
    int EstimatedMinutes,
    DateTime? DueDate,
    string Status);
