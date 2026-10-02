using Compas.Api.Data;
using Compas.Api.Dtos;
using Compas.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Compas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly CompasDbContext _db;

    public ProjectsController(CompasDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<ProjectDto>>> GetAll(CancellationToken ct)
    {
        var projects = await _db.Projects.Include(p => p.WorkItems).ToListAsync(ct);
        return projects.Select(ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectDto>> GetById(int id, CancellationToken ct)
    {
        var project = await _db.Projects.Include(p => p.WorkItems)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return project is null ? NotFound() : ToDto(project);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectDto dto, CancellationToken ct)
    {
        var project = new Project
        {
            Name = dto.Name,
            Description = dto.Description,
            ColorHex = string.IsNullOrWhiteSpace(dto.ColorHex) ? "#7C9EFF" : dto.ColorHex,
            TargetDate = dto.TargetDate,
            Status = ProjectStatus.Active
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = project.Id }, ToDto(project));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateProjectDto dto, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object?[] { id }, ct);
        if (project is null) return NotFound();

        if (!Enum.TryParse<ProjectStatus>(dto.Status, ignoreCase: true, out var status))
            return BadRequest($"Estado de proyecto no válido: {dto.Status}");

        project.Name = dto.Name;
        project.Description = dto.Description;
        project.ColorHex = dto.ColorHex;
        project.TargetDate = dto.TargetDate;
        project.Status = status;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object?[] { id }, ct);
        if (project is null) return NotFound();

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static ProjectDto ToDto(Project p)
    {
        var total = p.WorkItems.Count;
        var completed = p.WorkItems.Count(w => w.Status == WorkItemStatus.Completed);
        var progress = total == 0 ? 0 : Math.Round(completed * 100.0 / total, 0);

        return new ProjectDto(p.Id, p.Name, p.Description, p.ColorHex, p.Status.ToString(),
            p.TargetDate, progress, total, completed);
    }
}
