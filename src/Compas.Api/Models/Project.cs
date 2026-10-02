namespace Compas.Api.Models;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ColorHex { get; set; } = "#7C9EFF";
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    /// <summary>Próximo hito / fecha objetivo del proyecto, mostrada en el panel lateral.</summary>
    public DateTime? TargetDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
}
