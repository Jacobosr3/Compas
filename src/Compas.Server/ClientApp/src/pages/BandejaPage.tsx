import { useState } from "react";
import { Topbar } from "../components/Topbar";
import { ConnectionBanner } from "../components/ConnectionBanner";
import { useApiData } from "../hooks/useApiData";
import { projectsApi, workItemsApi } from "../api";
import type { WorkItemDto, WorkItemStatus } from "../api/types";
import { TaskModal, TaskRow } from "../components/TaskParts";

const STATUS_FILTERS: { value: WorkItemStatus | "all"; label: string }[] = [
  { value: "all", label: "Todas" },
  { value: "Pending", label: "Pendientes" },
  { value: "Scheduled", label: "Programadas" },
  { value: "InProgress", label: "En curso" },
  { value: "Completed", label: "Completadas" },
];

export function BandejaPage() {
  const [filter, setFilter] = useState<WorkItemStatus | "all">("all");

  const tasks = useApiData(
    () => (filter === "all" ? workItemsApi.list() : workItemsApi.list(filter)),
    [filter]
  );

  const projects = useApiData(() => projectsApi.list(), []);

  const [modalTask, setModalTask] = useState<WorkItemDto | "new" | null>(null);
  const [quickTitle, setQuickTitle] = useState("");
  const [expanded, setExpanded] = useState<Set<number>>(new Set());

  function toggleExpanded(id: number) {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  const status =
    tasks.status === "error"
      ? "error"
      : tasks.status === "loading"
        ? "loading"
        : "ok";

  async function quickAdd() {
    const title = quickTitle.trim();

    if (!title) return;

    await workItemsApi.create({
      title,
      priority: "Medium",
      estimatedHours: 0,
      estimatedMinutes: 30,
    });

    setQuickTitle("");
    tasks.reload();
  }

  return (
    <main className="main">
      <Topbar
        title="Bandeja"
        subtitle="Todas tus tareas, programadas o no"
        status={status}
        onRefresh={tasks.reload}
      />

      {status === "error" && (
        <ConnectionBanner message={tasks.error || ""} />
      )}

      <div className="section-head">
        <h2>Tareas</h2>

        <div className="actions-inline">
          {STATUS_FILTERS.map((f) => (
            <button
              key={f.value}
              className={
                f.value === filter
                  ? "btn-secondary"
                  : "btn-ghost"
              }
              onClick={() => setFilter(f.value)}
            >
              {f.label}
            </button>
          ))}
        </div>
      </div>

      <div
        className="quick-add"
        style={{ marginBottom: 18, maxWidth: 420 }}
      >
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

      {status === "ok" &&
        (!tasks.data || tasks.data.length === 0) && (
          <div className="empty-state">
            <p>No hay tareas en este filtro.</p>
          </div>
        )}

      <div className="record-list">
        {tasks.data?.map((t) => (
          <TaskRow
            key={t.id}
            task={t}
            expanded={expanded.has(t.id)}
            onToggleExpanded={() => toggleExpanded(t.id)}
            onEdit={() => setModalTask(t)}
            onChanged={tasks.reload}
          />
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
