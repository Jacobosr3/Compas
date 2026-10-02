import { useState } from "react";
import { Topbar } from "../components/Topbar";
import { ConnectionBanner } from "../components/ConnectionBanner";
import { useApiData } from "../hooks/useApiData";
import { projectsApi, workItemsApi } from "../api";
import type { Priority, WorkItemDto, WorkItemStatus } from "../api/types";
import { fmtDate, priorityLabel, statusLabel } from "../utils/format";

const STATUS_FILTERS: { value: WorkItemStatus | "all"; label: string }[] = [
  { value: "all", label: "Todas" },
  { value: "Pending", label: "Pendientes" },
  { value: "Scheduled", label: "Programadas" },
  { value: "InProgress", label: "En curso" },
  { value: "Completed", label: "Completadas" },
];

const PRIORITY_OPTIONS: Priority[] = ["Low", "Medium", "High", "Urgent"];

export function BandejaPage() {
  const [filter, setFilter] = useState<WorkItemStatus | "all">("all");
  const tasks = useApiData(() => (filter === "all" ? workItemsApi.list() : workItemsApi.list(filter)), [filter]);
  const projects = useApiData(() => projectsApi.list(), []);
  const [modalTask, setModalTask] = useState<WorkItemDto | "new" | null>(null);
  const [quickTitle, setQuickTitle] = useState("");

  const status = tasks.status === "error" ? "error" : tasks.status === "loading" ? "loading" : "ok";

  async function quickAdd() {
    const title = quickTitle.trim();
    if (!title) return;
    await workItemsApi.create({ title, priority: "Medium", estimatedMinutes: 30 });
    setQuickTitle("");
    tasks.reload();
  }

  async function toggleComplete(task: WorkItemDto) {
    await workItemsApi.update(task.id, {
      title: task.title,
      description: task.description,
      projectId: task.projectId,
      priority: task.priority,
      estimatedMinutes: task.estimatedMinutes,
      dueDate: task.dueDate,
      status: task.status === "Completed" ? "Pending" : "Completed",
    });
    tasks.reload();
  }

  return (
    <main className="main">
      <Topbar title="Bandeja" subtitle="Todas tus tareas, programadas o no" status={status} onRefresh={tasks.reload} />
      {status === "error" && <ConnectionBanner message={tasks.error || ""} />}

      <div className="section-head">
        <h2>Tareas</h2>
        <div className="actions-inline">
          {STATUS_FILTERS.map((f) => (
            <button
              key={f.value}
              className={f.value === filter ? "btn-secondary" : "btn-ghost"}
              onClick={() => setFilter(f.value)}
            >
              {f.label}
            </button>
          ))}
        </div>
      </div>

      <div className="quick-add" style={{ marginBottom: 18, maxWidth: 420 }}>
        <input
          placeholder="Nueva tarea rápida…"
          value={quickTitle}
          onChange={(e) => setQuickTitle(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && quickAdd()}
        />
        <button onClick={quickAdd}>Añadir</button>
      </div>

      {status === "loading" && (
        <div className="empty-state">
          <p>Cargando tareas…</p>
        </div>
      )}
      {status === "ok" && (!tasks.data || tasks.data.length === 0) && (
        <div className="empty-state">
          <p>No hay tareas en este filtro.</p>
        </div>
      )}

      <div className="record-list">
        {tasks.data?.map((t) => (
          <div className="record-row" key={t.id}>
            <div className="record-main">
              <div className="record-title" style={{ opacity: t.status === "Completed" ? 0.55 : 1 }}>
                {t.status === "Completed" ? "✓ " : ""}
                {t.title}
              </div>
              <div className="record-meta">
                <span className={"tag" + (t.priority === "High" || t.priority === "Urgent" ? " priority-" + t.priority.toLowerCase() : "")}>
                  {priorityLabel[t.priority]}
                </span>
                <span className="tag">{statusLabel[t.status]}</span>
                <span>{t.estimatedMinutes}m estimados</span>
                {t.projectName && <span>📁 {t.projectName}</span>}
                {t.dueDate && <span>Vence: {fmtDate(t.dueDate)}</span>}
              </div>
            </div>
            <div className="record-actions">
              <button className="btn-secondary" onClick={() => toggleComplete(t)}>
                {t.status === "Completed" ? "Reabrir" : "Completar"}
              </button>
              <button className="btn-secondary" onClick={() => setModalTask(t)}>
                Editar
              </button>
              <button
                className="btn-danger"
                onClick={async () => {
                  if (confirm(`¿Eliminar "${t.title}"?`)) {
                    await workItemsApi.remove(t.id);
                    tasks.reload();
                  }
                }}
              >
                Eliminar
              </button>
            </div>
          </div>
        ))}
      </div>

      {modalTask && (
        <TaskModal
          task={modalTask}
          projects={projects.data ?? []}
          onClose={() => setModalTask(null)}
          onSaved={tasks.reload}
        />
      )}
    </main>
  );
}

function TaskModal({
  task,
  projects,
  onClose,
  onSaved,
}: {
  task: WorkItemDto | "new";
  projects: { id: number; name: string }[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const isNew = task === "new";
  const [title, setTitle] = useState(isNew ? "" : task.title);
  const [description, setDescription] = useState(isNew ? "" : task.description ?? "");
  const [projectId, setProjectId] = useState<string>(isNew ? "" : task.projectId?.toString() ?? "");
  const [priority, setPriority] = useState<Priority>(isNew ? "Medium" : task.priority);
  const [estimatedMinutes, setEstimatedMinutes] = useState(isNew ? 30 : task.estimatedMinutes);
  const [dueDate, setDueDate] = useState(isNew ? "" : task.dueDate ? task.dueDate.slice(0, 10) : "");
  const [saving, setSaving] = useState(false);

  async function save() {
    if (!title.trim()) return;
    setSaving(true);
    try {
      const parsedProjectId = projectId ? Number(projectId) : null;
      const parsedDueDate = dueDate ? new Date(dueDate).toISOString() : null;

      if (isNew) {
        await workItemsApi.create({
          title,
          description: description || null,
          projectId: parsedProjectId,
          priority,
          estimatedMinutes,
          dueDate: parsedDueDate,
        });
      } else {
        await workItemsApi.update(task.id, {
          title,
          description: description || null,
          projectId: parsedProjectId,
          priority,
          estimatedMinutes,
          dueDate: parsedDueDate,
          status: task.status,
        });
      }
      onSaved();
      onClose();
    } catch (err) {
      alert("No se pudo guardar: " + (err instanceof Error ? err.message : String(err)));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <h2>{isNew ? "Nueva tarea" : "Editar tarea"}</h2>

        <div className="field">
          <label>Título</label>
          <input value={title} onChange={(e) => setTitle(e.target.value)} autoFocus />
        </div>

        <div className="field">
          <label>Descripción (opcional)</label>
          <textarea value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>

        <div className="field-row">
          <div className="field">
            <label>Proyecto</label>
            <select value={projectId} onChange={(e) => setProjectId(e.target.value)}>
              <option value="">Sin proyecto</option>
              {projects.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label>Prioridad</label>
            <select value={priority} onChange={(e) => setPriority(e.target.value as Priority)}>
              {PRIORITY_OPTIONS.map((p) => (
                <option key={p} value={p}>
                  {priorityLabel[p]}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="field-row">
          <div className="field">
            <label>Duración estimada (min)</label>
            <input
              type="number"
              min={5}
              step={5}
              value={estimatedMinutes}
              onChange={(e) => setEstimatedMinutes(Number(e.target.value))}
            />
          </div>
          <div className="field">
            <label>Fecha de vencimiento</label>
            <input type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
          </div>
        </div>

        <div className="modal-actions">
          <button className="btn-secondary" onClick={onClose}>
            Cancelar
          </button>
          <button className="btn-primary" disabled={saving || !title.trim()} onClick={save}>
            {saving ? "Guardando…" : "Guardar"}
          </button>
        </div>
      </div>
    </div>
  );
}
