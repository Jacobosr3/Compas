import { useState } from "react";
import { calendarEventsApi } from "../api";
import type { CalendarEventDto, GoogleStatusDto } from "../api/types";
import { errorText } from "../utils/errors";
import { fmtDuration, toDateInputValue } from "../utils/format";

const pad = (n: number) => String(n).padStart(2, "0");
const timeOf = (d: Date) => `${pad(d.getHours())}:${pad(d.getMinutes())}`;
const toDate = (date: string, time: string) => new Date(`${date}T${time || "00:00"}:00`);
const addDays = (d: Date, n: number) => new Date(d.getFullYear(), d.getMonth(), d.getDate() + n, d.getHours(), d.getMinutes());
const localIso = (d: Date) => `${toDateInputValue(d)}T${timeOf(d)}:00`; // hora local sin zona (no UTC)

/** Valor del selector de calendario para "no enviarlo a Google". */
const ONLY_COMPAS = "";
const LAST_CALENDAR_KEY = "compas.lastEventCalendar";

function readLastCalendar(): string | null {
  try {
    return localStorage.getItem(LAST_CALENDAR_KEY);
  } catch {
    return null; // sin almacenamiento: se usa el predeterminado del servidor
  }
}

/**
 * Formulario de evento al estilo de Google Calendar: fecha y hora de inicio y fin por separado, "todo el día"
 * y, al cambiar el inicio, el fin se mueve para conservar la duración. Al crear se elige el calendario de Google.
 */
export function EventModal({
  defaultDate,
  event,
  google,
  initialStart,
  initialEnd,
  onClose,
  onSaved,
  onDelete,
}: {
  defaultDate: string;
  /** Horas con las que se abre un evento nuevo (p. ej. las elegidas arrastrando en el calendario). */
  initialStart?: Date;
  initialEnd?: Date;
  /** Si se indica, el formulario edita ese evento en lugar de crear uno nuevo. */
  event?: CalendarEventDto;
  /** Estado de Google: de ahí salen los calendarios donde se puede crear el evento. */
  google: GoogleStatusDto | null;
  onClose: () => void;
  onSaved: () => void;
  /** Si se indica y se está editando, aparece el botón Eliminar. */
  onDelete?: (event: CalendarEventDto) => void;
}) {
  const startValue = event ? new Date(event.startTime) : initialStart ?? new Date(defaultDate + "T09:00:00");
  const endValue = event ? new Date(event.endTime) : initialEnd ?? new Date(defaultDate + "T10:00:00");
  // En los eventos de todo el día el fin guardado es exclusivo; en pantalla se muestra el último día incluido.
  const shownEnd = event?.isAllDay ? addDays(endValue, -1) : endValue;

  const [title, setTitle] = useState(event?.title ?? "");
  const [notes, setNotes] = useState(event?.notes ?? "");
  const [allDay, setAllDay] = useState(event?.isAllDay ?? false);
  const [sDate, setSDate] = useState(toDateInputValue(startValue));
  const [sTime, setSTime] = useState(event?.isAllDay ? "09:00" : timeOf(startValue));
  const [eDate, setEDate] = useState(toDateInputValue(shownEnd < startValue ? startValue : shownEnd));
  const [eTime, setETime] = useState(event?.isAllDay ? "10:00" : timeOf(endValue));
  const [saving, setSaving] = useState(false);
  const [pickEndDate, setPickEndDate] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);

  // Calendarios de Google donde se puede crear el evento (solo con permiso de escritura).
  const targets = google?.canWrite ? google.calendars.filter((c) => c.selected && c.canEdit) : [];
  // Se recuerda la última elección: primero la guardada en este navegador (si sigue disponible) y luego la del servidor.
  // Al editar, el selector parte del calendario en el que ya está el evento.
  const [calendarId, setCalendarId] = useState(() => {
    if (event) return event.calendarId ?? ONLY_COMPAS;
    const last = readLastCalendar();
    if (last === ONLY_COMPAS) return ONLY_COMPAS;
    return (
      targets.find((c) => c.id === last)?.id ??
      targets.find((c) => c.id === google?.defaultCalendarId)?.id ??
      targets[0]?.id ??
      ONLY_COMPAS
    );
  });

  // Como en Google, la fecha de fin solo se muestra si el evento termina otro día (o si se pide).
  const showEndDate = allDay || pickEndDate || eDate !== sDate;

  // Al editar se puede cambiar de calendario si el actual es editable (o si el evento solo está en Compás).
  const currentInTargets = !event?.calendarId || targets.some((c) => c.id === event.calendarId);
  const canPickCalendar = targets.length > 0 && (!event || currentInTargets);

  const startAt = toDate(sDate, sTime);
  const endAt = toDate(eDate, eTime);
  const durationMin = Math.round((endAt.getTime() - startAt.getTime()) / 60000);

  /** Cambia el inicio y mueve el fin para que la duración se mantenga. */
  function changeStart(date: string, time: string) {
    const keep = durationMin > 0 ? durationMin : 60;
    if (allDay) {
      const dayShift = Math.round((toDate(date, "00:00").getTime() - toDate(sDate, "00:00").getTime()) / 86400000);
      setEDate(toDateInputValue(addDays(toDate(eDate, "00:00"), dayShift)));
    } else {
      const newEnd = new Date(toDate(date, time).getTime() + keep * 60000);
      setEDate(toDateInputValue(newEnd));
      setETime(timeOf(newEnd));
    }
    setSDate(date);
    setSTime(time);
  }

  /** Si la hora de fin queda antes que la de inicio el mismo día, el evento termina al día siguiente (como en Google). */
  function changeEndTime(time: string) {
    setETime(time);
    if (eDate === sDate && toDate(eDate, time) <= startAt) setEDate(toDateInputValue(addDays(startAt, 1)));
  }

  async function save() {
    setValidationError(null);
    if (!title.trim()) return;

    let start: Date;
    let end: Date;
    if (allDay) {
      start = toDate(sDate, "00:00");
      end = addDays(toDate(eDate, "00:00"), 1); // fin exclusivo
    } else {
      start = startAt;
      end = endAt;
    }
    if (end <= start) {
      setValidationError(allDay ? "El último día no puede ser anterior al primero." : "La hora de fin debe ser posterior a la de inicio.");
      return;
    }

    setSaving(true);
    try {
      const dto = { title, startTime: localIso(start), endTime: localIso(end), isAllDay: allDay, notes: notes || null };
      if (event) {
        // Solo se manda el calendario si se ha cambiado.
        const moveTo = canPickCalendar && calendarId !== (event.calendarId ?? ONLY_COMPAS) ? calendarId : null;
        const updated = await calendarEventsApi.update(event.id, { ...dto, calendarId: moveTo });
        if (updated.syncWarning) alert(updated.syncWarning);
      } else {
        const created = await calendarEventsApi.create({ ...dto, calendarId: targets.length > 0 ? calendarId : null });
        if (targets.length > 0) {
          try {
            localStorage.setItem(LAST_CALENDAR_KEY, calendarId);
          } catch {
            /* sin almacenamiento: solo se pierde la preferencia */
          }
        }
        if (created.syncWarning) alert(created.syncWarning);
      }
      onSaved();
      onClose();
    } catch (err) {
      alert("No se pudo guardar: " + errorText(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <h2>{event ? "Editar evento" : "Nuevo evento"}</h2>
        {event?.googleLinked && <p className="confirm-message">Este evento está en Google Calendar: los cambios también se guardarán allí.</p>}

        <div className="field">
          <label>Título</label>
          <input value={title} onChange={(e) => setTitle(e.target.value)} autoFocus placeholder="Reunión de equipo" />
        </div>

        <div className="field">
          <label>Fecha y hora</label>
          <div className="when-row">
            <input type="date" value={sDate} aria-label="Fecha de inicio" onChange={(e) => e.target.value && changeStart(e.target.value, sTime)} />
            {!allDay && (
              <input type="time" step={900} value={sTime} aria-label="Hora de inicio" onChange={(e) => e.target.value && changeStart(sDate, e.target.value)} />
            )}
            <span className="when-dash">–</span>
            {!allDay && <input type="time" step={900} value={eTime} aria-label="Hora de fin" onChange={(e) => e.target.value && changeEndTime(e.target.value)} />}
            {showEndDate && (
              <input type="date" value={eDate} min={sDate} aria-label="Fecha de fin" onChange={(e) => e.target.value && setEDate(e.target.value)} />
            )}
          </div>
          <div className="when-meta">
            <label className="when-allday">
              <input type="checkbox" checked={allDay} onChange={(e) => setAllDay(e.target.checked)} />
              Todo el día
            </label>
            {!allDay && !showEndDate && (
              <button type="button" className="link-btn" onClick={() => setPickEndDate(true)}>
                Termina otro día
              </button>
            )}
            {!allDay && durationMin > 0 && <span className="when-duration">Duración: {fmtDuration(durationMin)}</span>}
          </div>
        </div>

        {canPickCalendar && (
          <div className="field">
            <label>{event ? "Calendario" : "Los eventos nuevos de Compás se crean en"}</label>
            <select value={calendarId} onChange={(e) => setCalendarId(e.target.value)}>
              {targets.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.summary}
                </option>
              ))}
              {/* Un evento que ya está en Google no puede volver a ser solo de Compás desde aquí (habría que borrarlo en Google). */}
              {!event?.calendarId && <option value={ONLY_COMPAS}>Solo en Compás (sin enviarlo a Google)</option>}
            </select>
          </div>
        )}

        <div className="field">
          <label>Notas (opcional)</label>
          <textarea value={notes} onChange={(e) => setNotes(e.target.value)} />
        </div>

        {validationError && <div style={{ color: "var(--danger)", fontSize: 14, marginBottom: 12 }}>{validationError}</div>}

        <div className="modal-actions">
          {event && onDelete && (
            <button className="btn-danger modal-delete" onClick={() => onDelete(event)} disabled={saving}>
              Eliminar
            </button>
          )}
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
