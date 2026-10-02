using Compas.Api.Models;
using Microsoft.Extensions.Options;

namespace Compas.Api.Services;

/// <summary>
/// Motor de planificación basado en una puntuación transparente (urgencia + prioridad +
/// encaje del hueco), no en un modelo de IA. Sirve como implementación de referencia y
/// como línea base para comparar contra un futuro motor basado en LLM.
/// </summary>
public class HeuristicPlanningEngine : IPlanningEngine
{
    private readonly PlanningOptions _options;

    public HeuristicPlanningEngine(IOptions<PlanningOptions> options)
    {
        _options = options.Value;
    }

    public List<EngineBlock> BuildPlan(
        DateOnly date,
        IReadOnlyList<CalendarEvent> fixedEvents,
        IReadOnlyList<WorkItem> pendingItems,
        DateTime effectiveStart)
    {
        var dayStart = date.ToDateTime(TimeOnly.FromTimeSpan(_options.WorkDayStart));
        var dayEnd = date.ToDateTime(TimeOnly.FromTimeSpan(_options.WorkDayEnd));

        var cursor = effectiveStart > dayStart ? effectiveStart : dayStart;
        var blocks = new List<EngineBlock>();
        var remainingTasks = pendingItems
            .Where(t => t.EstimatedMinutes > 0)
            .OrderBy(t => t.Id) // orden estable; la puntuación decide la prioridad real
            .ToList();

        var orderedEvents = fixedEvents
            .Where(e => e.EndTime > cursor && e.StartTime < dayEnd)
            .OrderBy(e => e.StartTime)
            .ToList();

        foreach (var evt in orderedEvents)
        {
            var gapStart = cursor;
            var gapEnd = evt.StartTime < dayEnd ? evt.StartTime : dayEnd;

            if (gapEnd > gapStart)
            {
                FillGap(gapStart, gapEnd, remainingTasks, blocks);
            }

            var eventStart = evt.StartTime > cursor ? evt.StartTime : cursor;
            if (evt.EndTime > eventStart)
            {
                blocks.Add(new EngineBlock(
                    PlanEntryKind.CalendarEvent,
                    eventStart,
                    evt.EndTime,
                    null,
                    evt,
                    "Evento fijo de tu agenda."));
            }

            cursor = evt.EndTime > cursor ? evt.EndTime : cursor;
        }

        if (cursor < dayEnd)
        {
            FillGap(cursor, dayEnd, remainingTasks, blocks);
        }

        return blocks.OrderBy(b => b.Start).ToList();
    }

    /// <summary>
    /// Rellena un hueco libre concreto encadenando la mejor tarea disponible, insertando
    /// descansos tras bloques largos, hasta que no quepa ninguna tarea más.
    /// </summary>
    private void FillGap(DateTime gapStart, DateTime gapEnd, List<WorkItem> remainingTasks, List<EngineBlock> blocks)
    {
        var cursor = gapStart;
        var minutesSinceBreak = 0;

        while (cursor < gapEnd)
        {
            var availableMinutes = (int)(gapEnd - cursor).TotalMinutes;
            if (availableMinutes < 5) break;

            var candidate = PickBestTask(remainingTasks, availableMinutes, cursor);
            if (candidate is null)
            {
                // No hay tarea que quepa: el resto del hueco queda libre.
                blocks.Add(new EngineBlock(PlanEntryKind.Free, cursor, gapEnd, null, null,
                    "Hueco libre: ninguna tarea pendiente encaja aquí."));
                return;
            }

            var (task, reason) = candidate.Value;
            var end = cursor.AddMinutes(task.EstimatedMinutes);

            blocks.Add(new EngineBlock(PlanEntryKind.Task, cursor, end, task, null, reason));
            remainingTasks.Remove(task);

            minutesSinceBreak += task.EstimatedMinutes;
            cursor = end;

            if (minutesSinceBreak >= _options.BreakAfterMinutes && cursor < gapEnd)
            {
                var breakEnd = cursor.AddMinutes(_options.BreakDurationMinutes);
                if (breakEnd > gapEnd) breakEnd = gapEnd;

                blocks.Add(new EngineBlock(PlanEntryKind.Break, cursor, breakEnd, null, null,
                    "Descanso recomendado tras un bloque largo de concentración."));
                cursor = breakEnd;
                minutesSinceBreak = 0;
            }

            if ((gapEnd - cursor).TotalMinutes < _options.MinGapMinutes)
            {
                if (cursor < gapEnd)
                {
                    blocks.Add(new EngineBlock(PlanEntryKind.Free, cursor, gapEnd, null, null,
                        "Hueco demasiado pequeño para otra tarea."));
                }
                return;
            }
        }
    }

    /// <summary>
    /// Puntúa las tareas candidatas y devuelve la de mayor puntuación que quepa en el
    /// tiempo disponible, junto con la explicación en lenguaje natural de por qué se eligió.
    /// </summary>
    private (WorkItem Task, string Reason)? PickBestTask(List<WorkItem> candidates, int availableMinutes, DateTime now)
    {
        WorkItem? best = null;
        double bestScore = double.MinValue;
        string bestReason = string.Empty;

        foreach (var task in candidates)
        {
            if (task.EstimatedMinutes > availableMinutes) continue;

            var urgencyScore = ScoreUrgency(task.DueDate, now, out var dueLabel);
            var priorityScore = ScorePriority(task.Priority);
            var fitScore = 100.0 * task.EstimatedMinutes / availableMinutes;

            var total = urgencyScore * 0.40 + priorityScore * 0.35 + fitScore * 0.25;

            if (total > bestScore)
            {
                bestScore = total;
                best = task;
                bestReason = BuildReason(task, availableMinutes, dueLabel);
            }
        }

        return best is null ? null : (best, bestReason);
    }

    private static double ScoreUrgency(DateTime? dueDate, DateTime now, out string? dueLabel)
    {
        dueLabel = null;
        if (dueDate is null) return 20;

        var hoursLeft = (dueDate.Value - now).TotalHours;

        if (hoursLeft <= 0) { dueLabel = "está vencida"; return 100; }
        if (hoursLeft <= 24) { dueLabel = "vence hoy"; return 90; }
        if (hoursLeft <= 48) { dueLabel = "vence mañana"; return 60; }
        if (hoursLeft <= 24 * 7) { dueLabel = "vence esta semana"; return 35; }

        dueLabel = "no es urgente todavía";
        return 10;
    }

    private static double ScorePriority(Priority priority) => priority switch
    {
        Priority.Urgent => 100,
        Priority.High => 70,
        Priority.Medium => 40,
        Priority.Low => 15,
        _ => 40
    };

    private static string BuildReason(WorkItem task, int availableMinutes, string? dueLabel)
    {
        var parts = new List<string>
        {
            $"encaja en el hueco de {availableMinutes} min disponible"
        };

        if (dueLabel is not null)
            parts.Add(dueLabel);

        if (task.Priority is Priority.High or Priority.Urgent)
            parts.Add($"prioridad {(task.Priority == Priority.Urgent ? "urgente" : "alta")}");

        return char.ToUpperInvariant(parts[0][0]) + parts[0][1..] + (parts.Count > 1
            ? " · " + string.Join(" · ", parts.Skip(1))
            : "") + ".";
    }
}
