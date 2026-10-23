using Compas.Api.Data;
using Compas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Compas.Api.Services;

/// <summary>
/// Mantiene el estado de los proyectos según sus tareas:
/// sin tareas → Archivado; todas completadas → Completado; si vuelve a haber alguna sin completar,
/// un proyecto Completado o Archivado pasa a Activo. "Activo" y "En pausa" los elige el usuario.
/// Las tareas canceladas y las subtareas no cuentan.
/// </summary>
public class ProjectStatusService
{
    private readonly CompasDbContext _db;

    public ProjectStatusService(CompasDbContext db) => _db = db;

    /// <summary>Recalcula el estado de los proyectos indicados (los ids nulos se ignoran).</summary>
    public Task SyncAsync(IEnumerable<int?> projectIds, CancellationToken ct = default)
    {
        var ids = projectIds.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        return ids.Count == 0 ? Task.CompletedTask : SyncCoreAsync(ids, ct);
    }

    /// <summary>Recalcula el estado de todos los proyectos (se usa al arrancar la API).</summary>
    public Task SyncAllAsync(CancellationToken ct = default) => SyncCoreAsync(null, ct);

    private async Task SyncCoreAsync(List<int>? ids, CancellationToken ct)
    {
        var stats = await _db.WorkItems
            .Where(w => w.ParentId == null && w.ProjectId != null && w.Status != WorkItemStatus.Cancelled
                        && (ids == null || ids.Contains(w.ProjectId.Value)))
            .GroupBy(w => w.ProjectId!.Value)
            .Select(g => new { ProjectId = g.Key, Total = g.Count(), Done = g.Count(w => w.Status == WorkItemStatus.Completed) })
            .ToDictionaryAsync(x => x.ProjectId, ct);

        var projects = await _db.Projects.Where(p => ids == null || ids.Contains(p.Id)).ToListAsync(ct);

        foreach (var project in projects)
        {
            var (total, done) = stats.TryGetValue(project.Id, out var s) ? (s.Total, s.Done) : (0, 0);

            var target = total == 0 ? ProjectStatus.Archived
                : done == total ? ProjectStatus.Completed
                : project.Status is ProjectStatus.Completed or ProjectStatus.Archived ? ProjectStatus.Active
                : project.Status;

            project.Status = target;
        }

        await _db.SaveChangesAsync(ct);
    }
}
