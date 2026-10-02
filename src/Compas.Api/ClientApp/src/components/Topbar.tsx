import { useState } from "react";
import type { AsyncStatus } from "../hooks/useApiData";
import { getApiBase, setApiBase } from "../api/client";

interface TopbarProps {
  title: string;
  subtitle?: string;
  status: AsyncStatus;
  onRefresh: () => void;
}

const statusLabel: Record<AsyncStatus, string> = {
  loading: "Conectando…",
  ok: "Conectado",
  error: "Sin conexión con la API",
};

export function Topbar({ title, subtitle, status, onRefresh }: TopbarProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(getApiBase());

  function save() {
    setApiBase(draft);
    setEditing(false);
    onRefresh();
  }

  return (
    <div className="topbar">
      <div>
        <h1>{title}</h1>
        {subtitle && <div className="date">{subtitle}</div>}
      </div>
      <div className="topbar-right">
        <button className="icon-btn" title="Actualizar" onClick={onRefresh}>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
            <path d="M21 12a9 9 0 11-3-6.7M21 4v5h-5" />
          </svg>
        </button>
        <button className="icon-btn" title="Configurar conexión con la API" onClick={() => setEditing(true)}>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
            <circle cx="12" cy="12" r="3" />
            <path d="M19.4 15a1.7 1.7 0 00.34 1.87l.06.06a2 2 0 11-2.83 2.83l-.06-.06a1.7 1.7 0 00-1.87-.34 1.7 1.7 0 00-1 1.55V21a2 2 0 11-4 0v-.09a1.7 1.7 0 00-1-1.55 1.7 1.7 0 00-1.87.34l-.06.06a2 2 0 11-2.83-2.83l.06-.06a1.7 1.7 0 00.34-1.87 1.7 1.7 0 00-1.55-1H3a2 2 0 110-4h.09a1.7 1.7 0 001.55-1 1.7 1.7 0 00-.34-1.87l-.06-.06a2 2 0 112.83-2.83l.06.06a1.7 1.7 0 001.87.34H9a1.7 1.7 0 001-1.55V3a2 2 0 114 0v.09a1.7 1.7 0 001 1.55 1.7 1.7 0 001.87-.34l.06-.06a2 2 0 112.83 2.83l-.06.06a1.7 1.7 0 00-.34 1.87V9a1.7 1.7 0 001.55 1H21a2 2 0 110 4h-.09a1.7 1.7 0 00-1.55 1z" />
          </svg>
        </button>
        <div className={"status-pill " + status}>
          <span className="dot" /> <span>{statusLabel[status]}</span>
        </div>
      </div>

      {editing && (
        <div className="modal-overlay" onClick={() => setEditing(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Conexión con la API</h2>
            <div className="field">
              <label>URL base del backend</label>
              <input value={draft} onChange={(e) => setDraft(e.target.value)} placeholder="http://localhost:5080" />
            </div>
            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setEditing(false)}>
                Cancelar
              </button>
              <button className="btn-primary" onClick={save}>
                Guardar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
