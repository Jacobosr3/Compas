import { useState } from "react";
import { Topbar } from "../components/Topbar";
import { ConnectionBanner } from "../components/ConnectionBanner";
import { useApiData } from "../hooks/useApiData";
import { projectsApi } from "../api";
import type { ProjectDto, ProjectStatus } from "../api/types";
import { fmtDate, projectStatusLabel } from "../utils/format";

const STATUS_OPTIONS: ProjectStatus[] = ["Active", "OnHold", "Completed", "Archived"];

interface FormState {
  name: string;
  description: string;
  colorHex: string;
  status: ProjectStatus;
  targetDate: string;
}

const emptyForm: FormState = {
  name: "",
  description: "",
  colorHex: "#7C9EFF",
  status: "Active",
  targetDate: "",
};

export function ProjectsPage() {
  const { data, status, error, reload } = useApiData(() => projectsApi.list(), []);
  const [modalProject, setModalProject] = useState<ProjectDto | "new" | null>(null);

  return (
    <main className="main">
      <Topbar title="Proyectos" subtitle="Todos tus proyectos y su progreso real" status={status} onRefresh={reload} />
      {status === "error" && <ConnectionBanner message={error || ""} />}

      <div className="section-head">
        <h2>Todos los proyectos</h2>
        <div className="actions-inline">
          <button className="btn-primary" onClick={() => setModalProject("new")}>
            + Nuevo proyecto
          </button>
        </div>
      </div>

      {status === "loading" && (
        <div className="empty-state">
          <p>Cargando proyectos…</p>
        </div>
      )}

      {status === "ok" && (!data || data.length === 0) && (
        <div className="empty-state">
          <p>Todavía no tienes proyectos. Crea el primero.</p>
        </div>
      )}

      <div className="record-list">
        {data?.map((p) => (
          <div className="record-row" key={p.id}>
            <div className="record-main">
              <div className="record-title" style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <span style={{ width: 8, height: 8, borderRadius: 4, background: p.colorHex, display: "inline-block" }} />
                {p.name}
              </div>
              <div className="record-meta">
                <span className="tag">{projectStatusLabel[p.status] ?? p.status}</span>
                <span>
                  {p.completedWorkItems}/{p.totalWorkItems} tareas completadas
                </span>
                {p.targetDate && <span>Próximo hito: {fmtDate(p.targetDate)}</span>}
              </div>
              <div className="bar" style={{ marginTop: 8, maxWidth: 220 }}>
                <i style={{ width: `${Math.round(p.progressPercent)}%` }} />
              </div>
            </div>
            <div className="record-actions">
              <button className="btn-secondary" onClick={() => setModalProject(p)}>
                Editar
              </button>
              <button
                className="btn-danger"
                onClick={async () => {
                  if (confirm(`¿Eliminar "${p.name}"? Esto no borra sus tareas, pero quedarán sin proyecto.`)) {
                    await projectsApi.remove(p.id);
                    reload();
                  }
                }}
              >
                Eliminar
              </button>
            </div>
          </div>
        ))}
      </div>

      {modalProject && <ProjectModal project={modalProject} onClose={() => setModalProject(null)} onSaved={reload} />}
    </main>
  );
}

function ProjectModal({
  project,
  onClose,
  onSaved,
}: {
  project: ProjectDto | "new";
  onClose: () => void;
  onSaved: () => void;
}) {
  const isNew = project === "new";
  const [form, setForm] = useState<FormState>(
    isNew
      ? emptyForm
      : {
          name: project.name,
          description: project.description ?? "",
          colorHex: project.colorHex,
          status: project.status,
          targetDate: project.targetDate ? project.targetDate.slice(0, 10) : "",
        },
  );
  const [saving, setSaving] = useState(false);

  async function save() {
    if (!form.name.trim()) return;
    setSaving(true);
    try {
      const targetDate = form.targetDate ? new Date(form.targetDate).toISOString() : null;
      if (isNew) {
        await projectsApi.create({
          name: form.name,
          description: form.description || null,
          colorHex: form.colorHex,
          targetDate,
        });
      } else {
        await projectsApi.update(project.id, {
          name: form.name,
          description: form.description || null,
          colorHex: form.colorHex,
          status: form.status,
          targetDate,
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
        <h2>{isNew ? "Nuevo proyecto" : "Editar proyecto"}</h2>

        <div className="field">
          <label>Nombre</label>
          <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} autoFocus />
        </div>

        <div className="field">
          <label>Descripción</label>
          <textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
        </div>

        <div className="field-row">
          <div className="field">
            <label>Color</label>
            <input type="color" value={form.colorHex} onChange={(e) => setForm({ ...form, colorHex: e.target.value })} />
          </div>
          <div className="field">
            <label>Próximo hito</label>
            <input type="date" value={form.targetDate} onChange={(e) => setForm({ ...form, targetDate: e.target.value })} />
          </div>
        </div>

        {!isNew && (
          <div className="field">
            <label>Estado</label>
            <select value={form.status} onChange={(e) => setForm({ ...form, status: e.target.value as ProjectStatus })}>
              {STATUS_OPTIONS.map((s) => (
                <option key={s} value={s}>
                  {projectStatusLabel[s]}
                </option>
              ))}
            </select>
          </div>
        )}

        <div className="modal-actions">
          <button className="btn-secondary" onClick={onClose}>
            Cancelar
          </button>
          <button className="btn-primary" disabled={saving || !form.name.trim()} onClick={save}>
            {saving ? "Guardando…" : "Guardar"}
          </button>
        </div>
      </div>
    </div>
  );
}
