import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Topbar } from "../components/Topbar";
import { ConnectionBanner } from "../components/ConnectionBanner";
import { TaskModal, TaskRow } from "../components/TaskParts";
import { useApiData } from "../hooks/useApiData";
import { projectsApi, workItemsApi } from "../api";
import type { WorkItemDto } from "../api/types";
import { fmtDate, projectStatusLabel } from "../utils/format";

type Filter = "all" | "open" | "done";

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
            {p.targetDate && <span>Próximo hito: {fmtDate(p.targetDate)}</span>}
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
          {FILTERS.map((f) => (
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

      {tasks.status === "ok" && visible.length === 0 && (
        <div className="empty-state">
          <p>{filter === "all" ? "Este proyecto aún no tiene tareas." : "No hay tareas en este filtro."}</p>
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
