using Compas.Api.Models;

namespace Compas.Api.Services;

/// <summary>
/// Resultado de planificación para un único bloque, antes de persistirse como PlanEntry.
/// </summary>
public record EngineBlock(
    PlanEntryKind Kind,
    DateTime Start,
    DateTime End,
    WorkItem? WorkItem,
    CalendarEvent? CalendarEvent,
    string? Reason);

/// <summary>
/// Contrato del motor que decide qué hacer en cada momento del día.
///
/// Esta interfaz es el punto donde vive "la IA" del producto. La implementación
/// por defecto (<see cref="HeuristicPlanningEngine"/>) usa una fórmula de puntuación
/// simple y transparente. Puede sustituirse por una implementación que llame a un
/// modelo de lenguaje (o a un servicio de ML propio) sin cambiar nada en los
/// controladores ni en <see cref="PlannerService"/>: solo hay que registrar otra
/// clase que implemente IPlanningEngine en Program.cs.
/// </summary>
public interface IPlanningEngine
{
    /// <summary>
    /// Construye la lista ordenada de bloques del día, combinando eventos fijos de
    /// calendario con las tareas pendientes que mejor encajan en cada hueco libre.
    /// </summary>
    /// <param name="date">Día que se está planificando.</param>
    /// <param name="fixedEvents">Eventos de calendario del día, ya ordenados por hora de inicio.</param>
    /// <param name="pendingItems">Tareas candidatas a ser programadas (sin horario fijo).</param>
    /// <param name="effectiveStart">
    /// Momento a partir del cual se puede planificar (para "hoy" suele ser el instante
    /// actual, para generar un día completo desde cero es el inicio de la jornada).
    /// </param>
    List<EngineBlock> BuildPlan(
        DateOnly date,
        IReadOnlyList<CalendarEvent> fixedEvents,
        IReadOnlyList<WorkItem> pendingItems,
        DateTime effectiveStart);
}
