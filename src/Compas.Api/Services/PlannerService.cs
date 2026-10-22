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
            return await GenerateAsync(today, DateTime.Now, ct: ct);
        }

        return ToDto(existing);
    }

    /// <summary>Máximo de días que puede abarcar un plan con desbordamiento (hoy + siguientes).</summary>
    private const int MaxSpillDays = 14;

    /// <summary>
    /// Genera el plan de un día desde cero, a partir de la hora de inicio indicada
    /// (normalmente el principio de la jornada, o "ahora" si es para el resto del día de hoy).
    /// Con <paramref name="spillToNextDays"/>, lo que no cabe antes de medianoche pasa al día
    /// siguiente (desde las 08:00) y así sucesivamente, en lugar de quedarse en la bandeja.
    /// </summary>
    public async Task<DailyPlanDto> GenerateAsync(
        DateOnly date,
        DateTime effectiveStart,
        int? budgetMinutes = null,
        IReadOnlyList<int>? preferredOrder = null,
        bool spillToNextDays = false,
        bool exactStart = false,
        CancellationToken ct = default)
    {
        var previousRun = await _db.PlanRuns
            .Where(r => r.PlanDate == date)
            .OrderByDescending(r => r.GeneratedAt)
            .Include(r => r.Entries)
            .FirstOrDefaultAsync(ct);

        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var maxDays = spillToNextDays ? MaxSpillDays : 1;
        var horizonEnd = dayStart.AddDays(maxDays);

        var fixedEvents = await _db.CalendarEvents
            .Where(e => e.StartTime < horizonEnd && e.EndTime > dayStart && !e.IsAllDay)
            .OrderBy(e => e.StartTime)
            .ToListAsync(ct);

        var pendingTasks = await _db.WorkItems
            .Include(w => w.Project)
            .Where(w => w.ParentId == null &&
                        (w.Status == WorkItemStatus.Pending || w.Status == WorkItemStatus.Scheduled))
            .ToListAsync(ct);

        // El motor planifica un día cada vez; lo que no cabe pasa al siguiente (desde el inicio de su jornada).
        var engineBlocks = new List<EngineBlock>();
        var remaining = pendingTasks.ToList();
        var budgetLeft = budgetMinutes;
        var planDay = date;
        var planStart = effectiveStart;

        for (var i = 0; i < maxDays; i++)
        {
            var dayBlocks = _engine.BuildPlan(planDay, fixedEvents, remaining, planStart, budgetLeft, preferredOrder,
                exactStart: exactStart && i == 0); // los días siguientes empiezan al inicio de la jornada
            engineBlocks.AddRange(dayBlocks);

            var placed = dayBlocks
                .Where(b => b.Kind == PlanEntryKind.Task && b.WorkItem is not null)
                .Select(b => b.WorkItem!)
                .ToHashSet();

            // Hoy puede no caber nada porque queda poco día; pero si un día entero no coloca
            // ninguna tarea (son más largas que la jornada), tampoco cabrán en los siguientes.
            if (placed.Count == 0 && i > 0) break;

            remaining.RemoveAll(placed.Contains);
            if (budgetLeft is not null)
            {
                budgetLeft -= placed.Sum(t => t.EstimatedMinutes);
                if (budgetLeft <= 0) break;
            }
            if (!remaining.Any(t => t.EstimatedMinutes > 0)) break;

            planDay = planDay.AddDays(1);
            planStart = planDay.ToDateTime(TimeOnly.MinValue);
        }

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

        // Con un límite de tiempo (o un hueco que se cierra) hay tareas que se quedan fuera:
        // las que estaban programadas vuelven a la bandeja en vez de conservar un horario viejo.
        var placedIds = run.Entries.Where(e => e.WorkItemId is not null).Select(e => e.WorkItemId!.Value).ToHashSet();
        foreach (var task in pendingTasks.Where(t => !placedIds.Contains(t.Id) && t.Status == WorkItemStatus.Scheduled))
        {
            task.Status = WorkItemStatus.Pending;
            task.ScheduledStart = null;
            task.ScheduledEnd = null;
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
    /// Vuelve a planificar a partir de "ahora". Sin tope de medianoche: lo que no cabe hoy pasa
    /// a los días siguientes. Esto es lo que se llama cuando cambia algo (nueva tarea urgente,
    /// reunión añadida, etc.) y el plan tiene que adaptarse.
    /// </summary>
    public Task<DailyPlanDto> ReplanFromNowAsync(int? budgetMinutes = null, CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        return GenerateAsync(today, now, budgetMinutes, null, spillToNextDays: true, exactStart: true, ct: ct);
    }

    /// <summary>
    /// Aplica el orden que el usuario ha elegido a mano: vuelve a colocar las tareas del plan
    /// de hoy, empezando donde empieza el plan que se está viendo y respetando los eventos
    /// fijos, con esas tareas primero y en ese orden. (Planificar desde "ahora" movería
    /// todo el plan al instante actual y descartaría lo que ya queda atrás.)
    /// </summary>
    public async Task<DailyPlanDto> ReorderTodayAsync(IReadOnlyList<int> workItemIds, int? budgetMinutes = null, CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);

        var firstStart = await _db.PlanRuns
            .Where(r => r.PlanDate == today)
            .OrderByDescending(r => r.GeneratedAt)
            .Select(r => r.Entries.Select(e => (DateTime?)e.Start).Min())
            .FirstOrDefaultAsync(ct);

        // Si el plan que se está viendo ya ocupa días siguientes, el reordenado los conserva.
        var lastStart = await _db.PlanRuns
            .Where(r => r.PlanDate == today)
            .OrderByDescending(r => r.GeneratedAt)
            .Select(r => r.Entries.Select(e => (DateTime?)e.Start).Max())
            .FirstOrDefaultAsync(ct);
        var spill = lastStart is not null && DateOnly.FromDateTime(lastStart.Value) > today;

        return await GenerateAsync(today, firstStart ?? now, budgetMinutes, workItemIds, spillToNextDays: spill, exactStart: true, ct: ct);
    }

    /// <summary>
    /// Devuelve a la bandeja todas las tareas programadas y deja el plan de hoy vacío.
    /// Se guarda un plan nuevo sin entradas para que GetToday no lo regenere solo.
    /// </summary>
    public async Task<DailyPlanDto> ClearScheduledAsync(CancellationToken ct = default)
    {
        var scheduled = await _db.WorkItems
            .Where(w => w.Status == WorkItemStatus.Scheduled || w.Status == WorkItemStatus.InProgress)
            .ToListAsync(ct);

        foreach (var task in scheduled)
        {
            task.Status = WorkItemStatus.Pending;
            task.ScheduledStart = null;
            task.ScheduledEnd = null;
        }

        var run = new PlanRun
        {
            PlanDate = DateOnly.FromDateTime(DateTime.Now),
            GeneratedAt = DateTime.UtcNow
        };
        _db.PlanRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        return ToDto(run);
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
