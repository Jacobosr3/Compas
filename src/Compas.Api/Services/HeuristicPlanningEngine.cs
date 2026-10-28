using Compas.Api.Models;
using Microsoft.Extensions.Options;

namespace Compas.Api.Services;

/// <summary>
/// Motor de planificación basado en un orden transparente y fijo, no en un modelo de IA:
/// primero la fecha de vencimiento (la más cercana antes; sin fecha, al final), después la
/// prioridad y, a igualdad, la tarea de menor duración. Sirve como implementación de
/// referencia y como línea base para comparar contra un futuro motor basado en LLM.
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
        DateTime effectiveStart,
        int? budgetMinutes = null,
        IReadOnlyList<int>? preferredOrder = null,
        bool exactStart = false)
    {
        var budgetLeft = budgetMinutes ?? int.MaxValue;
        var dayStart = date.ToDateTime(TimeOnly.FromTimeSpan(_options.WorkDayStart));
        // Sin hora de fin de jornada: se puede planificar hasta medianoche.
        var dayEnd = date.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var cursor = exactStart || effectiveStart > dayStart ? effectiveStart : dayStart;
        var blocks = new List<EngineBlock>();
        var remainingTasks = pendingItems
            .Where(t => t.EstimatedMinutes > 0)
            .OrderBy(t => t.Id) // orden estable; PickBestTask decide la prioridad real
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
                FillGap(gapStart, gapEnd, remainingTasks, blocks, ref budgetLeft, preferredOrder, gapEndsAtEvent: true);
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
            FillGap(cursor, dayEnd, remainingTasks, blocks, ref budgetLeft, preferredOrder, gapEndsAtEvent: false);
        }

        // Sin tope de jornada, el hueco final hasta medianoche no aporta nada: no se muestra
        // (salvo que el tiempo indicado por el usuario se haya agotado, que sí explica por qué sobra hueco).
        if (blocks.Count > 0 && blocks[^1] is { Kind: PlanEntryKind.Free } trailing && trailing.End == dayEnd && budgetLeft > 0)
            blocks.RemoveAt(blocks.Count - 1);

        return blocks.OrderBy(b => b.Start).ToList();
    }

    /// <summary>
    /// Rellena un hueco libre concreto encadenando la mejor tarea disponible, insertando
    /// descansos tras bloques largos, hasta que no quepa ninguna tarea más.
    /// </summary>
    private void FillGap(
        DateTime gapStart,
        DateTime gapEnd,
        List<WorkItem> remainingTasks,
        List<EngineBlock> blocks,
        ref int budgetLeft,
        IReadOnlyList<int>? preferredOrder,
        bool gapEndsAtEvent)
    {
        var cursor = gapStart;
        var minutesSinceBreak = 0;

        while (cursor < gapEnd)
        {
            var availableMinutes = (int)(gapEnd - cursor).TotalMinutes;
            if (availableMinutes < 1) break;

            var candidate = PickBestTask(remainingTasks, availableMinutes, budgetLeft, cursor, preferredOrder, gapEndsAtEvent);
            if (candidate is null)
            {
                // No hay tarea que quepa: el resto del hueco queda libre.
                var limitedByBudget = budgetLeft < availableMinutes;
                blocks.Add(new EngineBlock(PlanEntryKind.Free, cursor, gapEnd, null, null,
                    limitedByBudget
                        ? "Hueco libre: dentro del tiempo que has indicado no cabe ninguna tarea más."
                        : "Hueco libre: ninguna tarea pendiente encaja aquí."));
                return;
            }

            var (task, reason) = candidate.Value;
            var end = cursor.AddMinutes(task.EstimatedMinutes);

            blocks.Add(new EngineBlock(PlanEntryKind.Task, cursor, end, task, null, reason));
            remainingTasks.Remove(task);

            minutesSinceBreak += task.EstimatedMinutes;
            if (budgetLeft != int.MaxValue) budgetLeft -= task.EstimatedMinutes;
            cursor = end;

            if (budgetLeft > 0 && minutesSinceBreak >= _options.BreakAfterMinutes && cursor < gapEnd)
            {
                var breakEnd = cursor.AddMinutes(_options.BreakDurationMinutes);
                if (breakEnd > gapEnd) breakEnd = gapEnd;

                blocks.Add(new EngineBlock(PlanEntryKind.Break, cursor, breakEnd, null, null,
                    "Descanso recomendado tras un bloque largo de concentración."));
                cursor = breakEnd;
                minutesSinceBreak = 0;
            }
        }
    }

    /// <summary>
    /// Elige la tarea que quepa en el tiempo disponible según este orden: 1) fecha de vencimiento
    /// (por día; las vencidas y las más próximas primero, las que no tienen fecha al final),
    /// 2) prioridad (urgente antes que baja) y 3) duración estimada (la más corta primero).
    /// Devuelve también la explicación en lenguaje natural de por qué se eligió.
    /// </summary>
    private (WorkItem Task, string Reason)? PickBestTask(
        List<WorkItem> candidates,
        int availableMinutes,
        int budgetLeft,
        DateTime now,
        IReadOnlyList<int>? preferredOrder,
        bool gapEndsAtEvent)
    {
        // Una tarea tiene que caber a la vez en el hueco y en el tiempo que queda por dedicar.
        var limit = Math.Min(availableMinutes, budgetLeft);

        if (preferredOrder is not null)
        {
            foreach (var id in preferredOrder)
            {
                var chosen = candidates.FirstOrDefault(t => t.Id == id);
                if (chosen is not null && chosen.EstimatedMinutes <= limit)
                    return (chosen, "Colocada en el orden que has elegido.");
            }
        }

        var best = candidates
            .Where(t => t.EstimatedMinutes <= limit)
            .OrderBy(t => t.DueDate?.Date ?? DateTime.MaxValue)
            .ThenByDescending(t => PriorityRank(t.Priority))
            .ThenBy(t => t.EstimatedMinutes)
            .ThenBy(t => t.Id)
            .FirstOrDefault();

        return best is null ? null : (best, BuildReason(best, availableMinutes, DueLabel(best.DueDate, now), gapEndsAtEvent));
    }

    private static string? DueLabel(DateTime? dueDate, DateTime now)
    {
        if (dueDate is null) return null;

        var days = (dueDate.Value.Date - now.Date).Days;
        if (days < 0) return "está vencida";
        if (days == 0) return "vence hoy";
        if (days == 1) return "vence mañana";
        if (days <= 7) return "vence esta semana";
        return "no es urgente todavía";
    }

    private static int PriorityRank(Priority priority) => priority switch
    {
        Priority.Urgent => 4,
        Priority.High => 3,
        Priority.Medium => 2,
        Priority.Low => 1,
        _ => 2
    };

    /// <summary>
    /// Explica por qué se eligió la tarea: fecha de vencimiento y prioridad. El tamaño del hueco
    /// solo se menciona cuando lo limita un evento de la agenda; el resto del día no tiene tope
    /// y decir "encaja en el hueco de 1425 min" no informa de nada.
    /// </summary>
    private static string BuildReason(WorkItem task, int availableMinutes, string? dueLabel, bool gapEndsAtEvent)
    {
        var parts = new List<string>();

        if (dueLabel is not null && dueLabel != "no es urgente todavía")
            parts.Add(dueLabel);
        else if (task.DueDate is null)
            parts.Add("sin fecha de vencimiento");

        parts.Add(task.Priority switch
        {
            Priority.Urgent => "prioridad urgente",
            Priority.High => "prioridad alta",
            Priority.Low => "prioridad baja",
            _ => "prioridad media"
        });

        parts.Add($"{task.EstimatedMinutes} min");

        if (gapEndsAtEvent)
            parts.Add($"cabe antes de tu próximo evento ({availableMinutes} min libres)");

        var text = string.Join(" · ", parts);
        return char.ToUpperInvariant(text[0]) + text[1..] + ".";
    }
}
