import { useState, type ReactNode } from "react";
import type { AsyncStatus } from "../hooks/useApiData";
import { InfoModal } from "./InfoModal";

interface TopbarProps {
  title: string;
  subtitle?: string;
  status: AsyncStatus;
  onRefresh: () => void;
  /** Si se indica, aparece un botón de información junto a "Actualizar" que abre este contenido. */
  info?: ReactNode;
}

const statusLabel: Record<AsyncStatus, string> = {
  loading: "Conectando…",
  ok: "Conectado",
  error: "Sin conexión con la API",
};

export function Topbar({ title, subtitle, status, onRefresh, info }: TopbarProps) {
  const [showInfo, setShowInfo] = useState(false);

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
        {info && (
          <button className="icon-btn" title="Cómo funciona" aria-label="Cómo funciona" onClick={() => setShowInfo(true)}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
              <circle cx="12" cy="12" r="9" />
              <path d="M12 11v5M12 8h.01" />
            </svg>
          </button>
        )}
        <div className={"status-pill " + status}>
          <span className="dot" /> <span>{statusLabel[status]}</span>
        </div>
      </div>

      {showInfo && (
        <InfoModal title="Cómo funciona" onClose={() => setShowInfo(false)}>
          {info}
        </InfoModal>
      )}
    </div>
  );
}
