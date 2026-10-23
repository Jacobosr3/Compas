import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Topbar } from "../components/Topbar";
import { ConnectionBanner } from "../components/ConnectionBanner";
import { TaskModal, TaskRow } from "../components/TaskParts";
import { ProjectBoard } from "../components/ProjectBoard";
import { useApiData } from "../hooks/useApiData";
import { projectsApi, workItemsApi } from "../api";
import type { WorkItemDto } from "../api/types";
import { projectStatusLabel } from "../utils/format";

type Filter = "all" | "open" | "done";
type View = "status" | "week" | "list";

const VIEWS: { value: View; label: string }[] = [
  { value: "list", label: "Lista" },
  { value: "status", label: "Estados" },
  { value: "week", label: "Semana" },
];

const FILTERS: { value: Filter; label: string }[] = [
  { value: "all", label: "Todas" },
  { value: "open", label: "Abiertas" },
  { value: "done", label: "Completadas" },
];

export function ProjectDetailPage() {
  const projectId = Number(useParams().id);

  const project = useApiData(() => projectsApi.get(projectId), [projectId]);
  const tasks = useApiData(() => workItemsApi.list(undefined, projectId), [projectId]);
  const allProjects = useApiData(() => projectsApi.list(), []);

  const [filter, setFilter] = useState<Filter>("all");
  const [view, setView] = useState<View>("list");
  const [modalTask, setModalTask] = useState<WorkItemDto | "new" | null>(null);
  const [expanded, setExpanded] = useState<Set<number>>(new Set());

  function toggleExpanded(id: number) {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  // Tras cualquier cambio se recargan tareas y proyecto (el progreso depende de las tareas).
  function reloadAll() {
    tasks.reload();
    project.reload();
  }

  const status = project.status === "error" || tasks.status === "error"
    ? "error"
    : project.status === "loading" || tasks.status === "loading"
      ? "loading"
      : "ok";

  const p = project.data;
  const visible = (tasks.data ?? []).filter((t) =>
    filter === "all" ? true : filter === "done" ? t.status === "Completed" : t.status !== "Completed" && t.status !== "Cancelled"
  );

  return (
    <main className="main">
      <Topbar
        title={p?.name ?? "Proyecto"}
        subtitle="Tareas asociadas a este proyecto"
        status={status}
        onRefresh={reloadAll}
      />

      {status === "error" && <ConnectionBanner message={project.error || tasks.error || ""} />}

      <Link className="back-link" to="/proyectos">
        ← Todos los proyectos
      </Link>

      {p && (
        <div className="project-head">
          <div className="record-meta">
            <span className="project-dot" style={{ background: p.colorHex }} />
            <span className="tag">{projectStatusLabel[p.status] ?? p.status}</span>
            <span>
              {p.completedWorkItems}/{p.totalWorkItems} tareas completadas
            </span>
          </div>
          {p.description && <p className="project-desc">{p.description}</p>}
          <div className="bar" style={{ marginTop: 8, maxWidth: 260 }}>
            <i style={{ width: `${Math.round(p.progressPercent)}%` }} />
          </div>
        </div>
      )}

      <div className="section-head">
        <h2>Tareas</h2>

        <div className="actions-inline">
          <div className="segmented" role="tablist" aria-label="Vista de tareas">
            {VIEWS.map((v) => (
              <button
                key={v.value}
                role="tab"
                aria-selected={v.value === view}
                className={v.value === view ? "btn-secondary" : "btn-ghost"}
                onClick={() => setView(v.value)}
              >
                {v.label}
              </button>
            ))}
          </div>
          {view === "list" &&
            FILTERS.map((f) => (
              <button
                key={f.value}
                className={f.value === filter ? "btn-secondary" : "btn-ghost"}
                onClick={() => setFilter(f.value)}
              >
                {f.label}
              </button>
            ))}
          <button className="btn-primary" onClick={() => setModalTask("new")}>
            + Nueva tarea
          </button>
        </div>
      </div>

      {tasks.status === "ok" && (tasks.data ?? []).length === 0 && (
        <div className="empty-state">
          <p>Este proyecto aún no tiene tareas.</p>
        </div>
      )}

      {view !== "list" && (tasks.data ?? []).length > 0 && (
        <ProjectBoard
          tasks={tasks.data ?? []}
          mode={view}
          onEdit={(t) => setModalTask(t)}
          onChanged={reloadAll}
        />
      )}

      {view === "list" && (
        <>
          {tasks.status === "ok" && (tasks.data ?? []).length > 0 && visible.length === 0 && (
            <div className="empty-state">
              <p>No hay tareas en este filtro.</p>
            </div>
          )}

          <div className="record-list">
            {visible.map((t) => (
              <TaskRow
                key={t.id}
                task={t}
                expanded={expanded.has(t.id)}
                onToggleExpanded={() => toggleExpanded(t.id)}
                onEdit={() => setModalTask(t)}
                onChanged={reloadAll}
              />
            ))}
          </div>
        </>
      )}

      {modalTask && (
        <TaskModal
          task={modalTask}
          projects={allProjects.data ?? []}
          defaultProjectId={projectId}
          onClose={() => setModalTask(null)}
          onSaved={reloadAll}
        />
      )}
    </main>
  );
}
