import { useState } from "react";
import { Topbar } from "../components/Topbar";
import { ConnectionBanner } from "../components/ConnectionBanner";
import { useApiData } from "../hooks/useApiData";
import { calendarEventsApi } from "../api";
import { fmtTime, toDateInputValue, toDateTimeLocalValue } from "../utils/format";

export function AgendaPage() {
  const [selectedDate, setSelectedDate] = useState(() => toDateInputValue(new Date()));
  const { data, status, error, reload } = useApiData(() => calendarEventsApi.list(selectedDate), [selectedDate]);
  const [showForm, setShowForm] = useState(false);

  return (
    <main className="main">
      <Topbar title="Agenda" subtitle="Eventos fijos que el planificador siempre respeta" status={status} onRefresh={reload} />
      {status === "error" && <ConnectionBanner message={error || ""} />}

      <div className="section-head">
        <h2>Eventos del día</h2>
        <div className="actions-inline" style={{ alignItems: "center" }}>
          <input
            type="date"
            value={selectedDate}
            onChange={(e) => setSelectedDate(e.target.value)}
            style={{
              background: "var(--panel-raised)",
              border: "1px solid var(--line)",
              borderRadius: 8,
              padding: "7px 10px",
              color: "var(--text)",
              fontSize: 12.5,
            }}
          />
          <button className="btn-primary" onClick={() => setShowForm(true)}>
            + Nuevo evento
          </button>
        </div>
      </div>

      {status === "loading" && (
        <div className="empty-state">
          <p>Cargando eventos…</p>
        </div>
      )}

      {status === "ok" && (!data || data.length === 0) && (
        <div className="empty-state">
          <p>No hay eventos fijos ese día. El planificador tendrá toda la jornada libre para tus tareas.</p>
        </div>
      )}

      <div className="record-list">
        {data
          ?.slice()
          .sort((a, b) => a.startTime.localeCompare(b.startTime))
          .map((evt) => (
            <div className="record-row" key={evt.id}>
              <div className="record-main">
                <div className="record-title">{evt.title}</div>
                <div className="record-meta">
                  <span className="tag">🔒 Fijo</span>
                  <span className="mono">
                    {evt.isAllDay ? "Todo el día" : `${fmtTime(evt.startTime)} — ${fmtTime(evt.endTime)}`}
                  </span>
                  {evt.notes && <span>{evt.notes}</span>}
                </div>
              </div>
              <div className="record-actions">
                <button
                  className="btn-danger"
                  onClick={async () => {
                    if (confirm(`¿Eliminar "${evt.title}"?`)) {
                      await calendarEventsApi.remove(evt.id);
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

      {showForm && (
        <EventModal
          defaultDate={selectedDate}
          onClose={() => setShowForm(false)}
          onSaved={reload}
        />
      )}
    </main>
  );
}

function EventModal({
  defaultDate,
  onClose,
  onSaved,
}: {
  defaultDate: string;
  onClose: () => void;
  onSaved: () => void;
}) {
  const baseStart = new Date(defaultDate + "T09:00:00");
  const baseEnd = new Date(defaultDate + "T10:00:00");

  const [title, setTitle] = useState("");
  const [start, setStart] = useState(toDateTimeLocalValue(baseStart));
  const [end, setEnd] = useState(toDateTimeLocalValue(baseEnd));
  const [notes, setNotes] = useState("");
  const [saving, setSaving] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);

  async function save() {
    setValidationError(null);
    if (!title.trim()) return;

    const startDate = new Date(start);
    const endDate = new Date(end);
    if (endDate <= startDate) {
      setValidationError("La hora de fin debe ser posterior a la de inicio.");
      return;
    }

    setSaving(true);
    try {
      await calendarEventsApi.create({
        title,
        startTime: startDate.toISOString(),
        endTime: endDate.toISOString(),
        isAllDay: false,
        notes: notes || null,
      });
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
        <h2>Nuevo evento fijo</h2>

        <div className="field">
          <label>Título</label>
          <input value={title} onChange={(e) => setTitle(e.target.value)} autoFocus placeholder="Reunión de equipo" />
        </div>

        <div className="field-row">
          <div className="field">
            <label>Inicio</label>
            <input type="datetime-local" value={start} onChange={(e) => setStart(e.target.value)} />
          </div>
          <div className="field">
            <label>Fin</label>
            <input type="datetime-local" value={end} onChange={(e) => setEnd(e.target.value)} />
          </div>
        </div>

        <div className="field">
          <label>Notas (opcional)</label>
          <textarea value={notes} onChange={(e) => setNotes(e.target.value)} />
        </div>

        {validationError && <div style={{ color: "#FFC5A8", fontSize: 12.5, marginBottom: 10 }}>{validationError}</div>}

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
