import { useEffect, useState } from "react";
import { createPortal } from "react-dom";

/**
 * Modal de confirmación para acciones destructivas. Se renderiza en un portal sobre
 * <body> para que no le afecten los transform/overflow de la fila desde la que se abre.
 */
export function ConfirmModal({
  title,
  message,
  confirmLabel = "Eliminar",
  onConfirm,
  onCancel,
}: {
  title: string;
  message: string;
  confirmLabel?: string;
  onConfirm: () => Promise<void>;
  onCancel: () => void;
}) {
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape" && !busy) onCancel();
    }
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [busy, onCancel]);

  async function confirm() {
    setBusy(true);
    try {
      await onConfirm();
      onCancel();
    } catch (err) {
      alert("No se pudo completar: " + (err instanceof Error ? err.message : String(err)));
      setBusy(false);
    }
  }

  return createPortal(
    <div
      className="modal-overlay"
      onClick={(e) => {
        // El portal sigue burbujeando por el árbol de React: no debe activar la fila de origen.
        e.stopPropagation();
        if (!busy) onCancel();
      }}
    >
      <div
        className="modal modal-confirm"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="confirm-title"
        aria-describedby="confirm-message"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 id="confirm-title">{title}</h2>
        <p id="confirm-message" className="confirm-message">
          {message}
        </p>

        <div className="modal-actions">
          <button className="btn-secondary" onClick={onCancel} disabled={busy} autoFocus>
            Cancelar
          </button>
          <button className="btn-danger" onClick={confirm} disabled={busy}>
            {busy ? "Un momento…" : confirmLabel}
          </button>
        </div>
      </div>
    </div>,
    document.body,
  );
}
