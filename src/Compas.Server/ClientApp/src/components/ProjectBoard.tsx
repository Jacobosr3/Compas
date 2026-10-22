import { useState, type DragEvent } from "react";
import { workItemsApi } from "../api";
import { SubtasksModal } from "./TaskParts";
import type { Priority, WorkItemDto, WorkItemStatus } from "../api/types";
import { fmtDate, priorityLabel, statusLabel } from "../utils/format";

export type BoardMode = "status" | "week";

const STATUS_COLUMNS: WorkItemStatus[] = ["Pending", "Scheduled", "InProgress", "Completed"];
const ALL_STATUSES: WorkItemStatus[] = [...STATUS_COLUMNS, "Cancelled"];

/** Más urgente primero. */
const PRIORITY_RANK: Record<Priority, number> = { Urgent: 0, High: 1, Medium: 2, Low: 3 };

function byPriority(a: WorkItemDto, b: WorkItemDto): number {
  const byRank = PRIORITY_RANK[a.priority] - PRIORITY_RANK[b.priority];
  if (byRank !== 0) return byRank;
  // A igual prioridad, antes la que vence antes (sin fecha al final).
  const da = a.dueDate ? new Date(a.dueDate).getTime() : Infinity;
  const db = b.dueDate ? new Date(b.dueDate).getTime() : Infinity;
  if (da !== db) return da < db ? -1 : 1;
  return a.id - b.id;
}

function dayKey(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** Día (YYYY-MM-DD) en el que "cae" una tarea en la vista semanal: su fecha de vencimiento. */
function taskDay(t: WorkItemDto): string | null {
  return t.dueDate ? t.dueDate.slice(0, 10) : null;
}

function mondayOf(date: Date): Date {
  const d = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  d.setDate(d.getDate() - ((d.getDay() + 6) % 7));
  return d;
}

export function ProjectBoard({
  tasks,
  mode,
  showProject = false,
  onEdit,
  onChanged,
}: {
  tasks: WorkItemDto[];
  mode: BoardMode;
  /** Muestra el nombre del proyecto en cada tarjeta (útil cuando el tablero mezcla proyectos). */
  showProject?: boolean;
  onEdit: (task: WorkItemDto) => void;
  onChanged: () => void;
}) {
  const [weekOffset, setWeekOffset] = useState(0);
  const [dragOver, setDragOver] = useState<string | null>(null);

  async function changeStatus(t: WorkItemDto, status: WorkItemStatus) {
    if (t.status === status) return;
    try {
      await workItemsApi.update(t.id, {
        title: t.title,
        description: t.description,
        projectId: t.projectId,
        priority: t.priority,
        estimatedHours: t.estimatedHours,
        estimatedMinutes: t.estimatedMinutes,
        dueDate: t.dueDate,
        status,
      });
      onChanged();
    } catch (err) {
      alert("No se pudo guardar: " + (err instanceof Error ? err.message : String(err)));
    }
  }

  /** Mueve la tarea a un día (YYYY-MM-DD) cambiando su vencimiento; null la deja sin fecha. */
  async function changeDay(t: WorkItemDto, key: string | null) {
    if (taskDay(t) === key) return;
    try {
      await workItemsApi.update(t.id, {
        title: t.title,
        description: t.description,
        projectId: t.projectId,
        priority: t.priority,
        estimatedHours: t.estimatedHours,
        estimatedMinutes: t.estimatedMinutes,
        dueDate: key ? new Date(key).toISOString() : null,
        status: t.status,
      });
      onChanged();
    } catch (err) {
      alert("No se pudo guardar: " + (err instanceof Error ? err.message : String(err)));
    }
  }

  function dropHandlers(zone: string, key: string | null) {
    return {
      onDragOver: (e: DragEvent) => {
        e.preventDefault();
        setDragOver(zone);
      },
      onDragLeave: () => setDragOver((cur) => (cur === zone ? null : cur)),
      onDrop: (e: DragEvent) => {
        e.preventDefault();
        setDragOver(null);
        const task = tasks.find((t) => t.id === Number(e.dataTransfer.getData("text/plain")));
        if (task) changeDay(task, key);
      },
    };
  }

  const sorted = [...tasks].sort(byPriority);

  if (mode === "status") {
    const columns = [...STATUS_COLUMNS];
    // La columna de canceladas solo aparece si hay alguna.
    if (sorted.some((t) => t.status === "Cancelled")) columns.push("Cancelled");

    return (
      <div className="board">
        {columns.map((status) => {
          const items = sorted.filter((t) => t.status === status);
          return (
            <section
              key={status}
              className={"board-col" + (dragOver === status ? " drag-over" : "")}
              onDragOver={(e) => {
                e.preventDefault();
                setDragOver(status);
              }}
              onDragLeave={() => setDragOver((cur) => (cur === status ? null : cur))}
              onDrop={(e) => {
                e.preventDefault();
                setDragOver(null);
                const id = Number(e.dataTransfer.getData("text/plain"));
                const task = tasks.find((t) => t.id === id);
                if (task) changeStatus(task, status);
              }}
            >
              <header className="board-col-head">
                <h3>{statusLabel[status]}</h3>
                <span className="tag">{items.length}</span>
              </header>
              <div className="board-cards">
                {items.length === 0 && <div className="empty-mini">Sin tareas</div>}
                {items.map((t) => (
                  <BoardCard key={t.id} showProject={showProject} task={t} draggable onEdit={onEdit} onStatus={changeStatus} onChanged={onChanged} />
                ))}
              </div>
            </section>
          );
        })}
      </div>
    );
  }

  // ----- Vista por días de la semana -----
  const today = new Date();
  const monday = mondayOf(today);
  monday.setDate(monday.getDate() + weekOffset * 7);
  const days = Array.from({ length: 7 }, (_, i) => {
    const d = new Date(monday);
    d.setDate(monday.getDate() + i);
    return d;
  });
  const weekKeys = new Set(days.map(dayKey));
  const todayKey = dayKey(today);

  const undated = sorted.filter((t) => taskDay(t) === null);
  const otherWeeks = sorted.filter((t) => {
    const k = taskDay(t);
    return k !== null && !weekKeys.has(k);
  }).length;

  const rangeLabel = `${days[0].toLocaleDateString("es-ES", { day: "numeric", month: "short" })} – ${days[6].toLocaleDateString("es-ES", { day: "numeric", month: "short" })}`;

  return (
    <>
      <div className="week-nav">
        <button className="btn-secondary" onClick={() => setWeekOffset((w) => w - 1)} aria-label="Semana anterior">
          ←
        </button>
        <span className="week-range">{rangeLabel}</span>
        <button className="btn-secondary" onClick={() => setWeekOffset((w) => w + 1)} aria-label="Semana siguiente">
          →
        </button>
        {weekOffset !== 0 && (
          <button className="btn-ghost" onClick={() => setWeekOffset(0)}>
            Esta semana
          </button>
        )}
      </div>

      <div className="board board-week">
        {days.map((d) => {
          const key = dayKey(d);
          const items = sorted.filter((t) => taskDay(t) === key);
          const label = d.toLocaleDateString("es-ES", { weekday: "short", day: "numeric" });
          return (
            <section
              key={key}
              className={"board-col" + (key === todayKey ? " today" : "") + (dragOver === key ? " drag-over" : "")}
              {...dropHandlers(key, key)}
            >
              <header className="board-col-head">
                <h3 style={{ textTransform: "capitalize" }}>{label}</h3>
                <span className={"tag" + (key === todayKey ? " priority-high" : "")}>{items.length}</span>
              </header>
              <div className="board-cards">
                {items.length === 0 && <div className="empty-mini">Sin tareas</div>}
                {items.map((t) => (
                  <BoardCard key={t.id} showProject={showProject} task={t} draggable onEdit={onEdit} onStatus={changeStatus} onChanged={onChanged} />
                ))}
              </div>
            </section>
          );
        })}

        <section
          className={"board-col board-col-wide" + (dragOver === "none" ? " drag-over" : "")}
          {...dropHandlers("none", null)}
        >
          <header className="board-col-head">
            <h3>Sin día</h3>
            <span className="tag">{undated.length}</span>
          </header>
          <div className="board-cards">
            {undated.length === 0 && <div className="empty-mini">Sin tareas</div>}
            {undated.map((t) => (
              <BoardCard key={t.id} showProject={showProject} task={t} draggable onEdit={onEdit} onStatus={changeStatus} onChanged={onChanged} />
            ))}
          </div>
        </section>
      </div>

      <p className="board-note">
        Cada tarea aparece en el día de su fecha de vencimiento. Arrástrala a otro día para cambiarla, o a "Sin día" para quitársela.
        {otherWeeks > 0 && ` ${otherWeeks} ${otherWeeks === 1 ? "tarea cae" : "tareas caen"} en otras semanas.`}
      </p>
    </>
  );
}

function BoardCard({
  task: t,
  draggable = false,
  showProject = false,
  onEdit,
  onStatus,
  onChanged,
}: {
  task: WorkItemDto;
  draggable?: boolean;
  showProject?: boolean;
  onEdit: (task: WorkItemDto) => void;
  onStatus: (task: WorkItemDto, status: WorkItemStatus) => void;
  onChanged: () => void;
}) {
  const [open, setOpen] = useState(false);
  const done = t.subtasks.filter((s) => s.isCompleted).length;

  return (
    <article
      className="board-card"
      draggable={draggable}
      onDragStart={(e) => e.dataTransfer.setData("text/plain", String(t.id))}
      onClick={() => onEdit(t)}
    >
      <div className={"board-card-title" + (t.status === "Completed" ? " done" : "")}>{t.title}</div>
      {showProject && <div className="board-card-project">{t.projectName ?? "Sin proyecto"}</div>}

      <div className="record-meta">
        <span className={"tag" + (t.priority === "High" || t.priority === "Urgent" ? " priority-" + t.priority.toLowerCase() : "")}>
          {priorityLabel[t.priority]}
        </span>
        <span>
          {t.estimatedHours > 0 ? `${t.estimatedHours}h ` : ""}
          {t.estimatedMinutes > 0 || t.estimatedHours === 0 ? `${t.estimatedMinutes}m` : ""}
        </span>
        {t.dueDate && <span>Vence {fmtDate(t.dueDate)}</span>}
        {t.subtasks.length > 0 && (
          <button
            className="tag tag-button"
            aria-haspopup="dialog"
            aria-label={`Ver pasos de "${t.title}"`}
            onClick={(e) => {
              e.stopPropagation();
              setOpen((o) => !o);
            }}
          >
            ☑ {done}/{t.subtasks.length}
          </button>
        )}
      </div>

      {open && <SubtasksModal task={t} onClose={() => setOpen(false)} onChanged={onChanged} />}

      <select
        className="board-status"
        value={t.status}
        aria-label={`Estado de "${t.title}"`}
        onClick={(e) => e.stopPropagation()}
        onChange={(e) => onStatus(t, e.target.value as WorkItemStatus)}
      >
        {ALL_STATUSES.map((s) => (
          <option key={s} value={s}>
            {statusLabel[s]}
          </option>
        ))}
      </select>
    </article>
  );
}
