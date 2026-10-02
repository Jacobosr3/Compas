using Compas.Api.Data;
using Compas.Api.Dtos;
using Compas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Compas.Api.Services;

public class PlannerService
{
    private readonly CompasDbContext _db;
    private readonly IPlanningEngine _engine;

    public PlannerService(CompasDbContext db, IPlanningEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    /// <summary>Devuelve el plan más reciente para hoy, generándolo si todavía no existe.</summary>
    public async Task<DailyPlanDto> GetTodayAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var existing = await _db.PlanRuns
            .Where(r => r.PlanDate == today)
            .OrderByDescending(r => r.GeneratedAt)
            .Include(r => r.Entries).ThenInclude(e => e.WorkItem).ThenInclude(w => w!.Project)
            .Include(r => r.Entries).ThenInclude(e => e.CalendarEvent)
            .FirstOrDefaultAsync(ct);

        if (existing is null)
        {
            return await GenerateAsync(today, DateTime.Now, ct);
        }

        return ToDto(existing);
    }

    /// <summary>
    /// Genera el plan de un día desde cero, a partir de la hora de inicio indicada
    /// (normalmente el principio de la jornada, o "ahora" si es para el resto del día de hoy).
    /// </summary>
    public async Task<DailyPlanDto> GenerateAsync(DateOnly date, DateTime effectiveStart, CancellationToken ct = default)
    {
        var previousRun = await _db.PlanRuns
            .Where(r => r.PlanDate == date)
            .OrderByDescending(r => r.GeneratedAt)
            .Include(r => r.Entries)
            .FirstOrDefaultAsync(ct);

        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);

        var fixedEvents = await _db.CalendarEvents
            .Where(e => e.StartTime < dayEnd && e.EndTime > dayStart && !e.IsAllDay)
            .OrderBy(e => e.StartTime)
            .ToListAsync(ct);

        var pendingTasks = await _db.WorkItems
            .Include(w => w.Project)
            .Where(w => w.Status == WorkItemStatus.Pending || w.Status == WorkItemStatus.Scheduled)
            .ToListAsync(ct);

        var engineBlocks = _engine.BuildPlan(date, fixedEvents, pendingTasks, effectiveStart);

        var run = new PlanRun
        {
            PlanDate = date,
            GeneratedAt = DateTime.UtcNow
        };

        foreach (var block in engineBlocks)
        {
            var wasRescheduled = block.Kind == PlanEntryKind.Task && previousRun is not null &&
                previousRun.Entries.Any(e =>
                    e.WorkItemId == block.WorkItem!.Id &&
                    e.Start != block.Start);

            run.Entries.Add(new PlanEntry
            {
                Kind = block.Kind,
                Start = block.Start,
                End = block.End,
                WorkItemId = block.WorkItem?.Id,
                CalendarEventId = block.CalendarEvent?.Id,
                Reason = block.Reason,
                WasRescheduled = wasRescheduled
            });

            // Refleja la asignación en la propia tarea para que la bandeja y el
            // resto de la API sepan que ya tiene hueco en el día.
            if (block.Kind == PlanEntryKind.Task && block.WorkItem is not null)
            {
                block.WorkItem.ScheduledStart = block.Start;
                block.WorkItem.ScheduledEnd = block.End;
                if (block.WorkItem.Status == WorkItemStatus.Pending)
                    block.WorkItem.Status = WorkItemStatus.Scheduled;
            }
        }

        _db.PlanRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        // Recarga con las navegaciones necesarias para mapear a DTO.
        await _db.Entry(run).Collection(r => r.Entries).LoadAsync(ct);
        foreach (var entry in run.Entries)
        {
            if (entry.WorkItemId is not null)
                await _db.Entry(entry).Reference(e => e.WorkItem).LoadAsync(ct);
            if (entry.CalendarEventId is not null)
                await _db.Entry(entry).Reference(e => e.CalendarEvent).LoadAsync(ct);
            if (entry.WorkItem?.Project is not null)
                await _db.Entry(entry.WorkItem).Reference(w => w.Project).LoadAsync(ct);
        }

        return ToDto(run);
    }

    /// <summary>
    /// Vuelve a planificar únicamente lo que queda del día a partir de "ahora",
    /// conservando lo que ya ha pasado. Esto es lo que se llama cuando cambia algo
    /// (nueva tarea urgente, reunión añadida, etc.) y el plan tiene que adaptarse.
    /// </summary>
    public Task<DailyPlanDto> ReplanFromNowAsync(CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        return GenerateAsync(today, now, ct);
    }

    private static DailyPlanDto ToDto(PlanRun run)
    {
        var now = DateTime.Now;
        var entries = run.Entries
            .OrderBy(e => e.Start)
            .Select(e => new PlanEntryDto(
                e.Id,
                e.Kind.ToString(),
                Title: e.Kind switch
                {
                    PlanEntryKind.Task => e.WorkItem?.Title ?? "Tarea",
                    PlanEntryKind.CalendarEvent => e.CalendarEvent?.Title ?? "Evento",
                    PlanEntryKind.Break => "Descanso",
                    _ => "Hueco libre"
                },
                e.Start,
                e.End,
                ProjectName: e.WorkItem?.Project?.Name,
                Priority: e.WorkItem?.Priority.ToString(),
                Reason: e.Reason,
                WasRescheduled: e.WasRescheduled,
                IsCurrent: e.Start <= now && now < e.End,
                WorkItemId: e.WorkItemId,
                CalendarEventId: e.CalendarEventId))
            .ToList();

        var nowEntry = entries.FirstOrDefault(e => e.IsCurrent);

        return new DailyPlanDto(run.PlanDate, run.GeneratedAt, entries, nowEntry);
    }
}
