import { useEffect, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { Topbar } from "../components/Topbar";
import { ConfirmModal } from "../components/ConfirmModal";
import { ConnectionBanner } from "../components/ConnectionBanner";
import { GoogleCalendarCard } from "../components/GoogleCalendarCard";
import { WeekCalendar } from "../components/WeekCalendar";
import { EventModal } from "../components/EventModal";
import { useApiData } from "../hooks/useApiData";
import { calendarEventsApi, googleApi } from "../api";
import { errorText } from "../utils/errors";
import type { CalendarEventDto } from "../api/types";
import { fmtTime, mondayOf, toDateInputValue, toDateTimeLocalValue } from "../utils/format";

type View = "day" | "week";
const VIEW_KEY = "compas.agendaView";

/** Vista inicial: ?vista=semana, si no la última elegida y, si no, el día. */
function readInitialView(): View {
  const query = new URLSearchParams(window.location.search);
  const param = query.get("vista");
  if (param === "semana" || query.has("semana")) return "week";
  if (param === "dia") return "day";
  try {
    return localStorage.getItem(VIEW_KEY) === "week" ? "week" : "day";
  } catch {
    return "day";
  }
}

export function AgendaPage() {
  const [selectedDate, setSelectedDate] = useState(() => toDateInputValue(new Date()));
  const day = useApiData(() => calendarEventsApi.list(selectedDate), [selectedDate]);

  const [view, setView] = useState<View>(readInitialView);
  // ?semana=yyyy-MM-dd abre esa semana (p. ej. desde el mini calendario de la pantalla Hoy).
  const [weekStart, setWeekStart] = useState(() => {
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(new URLSearchParams(window.location.search).get("semana") ?? "");
    return mondayOf(m ? new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3])) : new Date());
  });
  const weekEnd = new Date(weekStart.getFullYear(), weekStart.getMonth(), weekStart.getDate() + 6);
  const week = useApiData(
    () => calendarEventsApi.range(toDateInputValue(weekStart), toDateInputValue(weekEnd)),
    [weekStart.getTime()],
  );

  function changeView(next: View) {
    if (next !== "week" && expanded) toggleExpanded(false);
    setView(next);
    try {
      localStorage.setItem(VIEW_KEY, next);
    } catch {
      /* sin almacenamiento: solo se pierde la preferencia */
    }
  }

  function shiftWeek(weeks: number) {
    setWeekStart((cur) => new Date(cur.getFullYear(), cur.getMonth(), cur.getDate() + weeks * 7));
  }

  const current = view === "day" ? day : week;
  const { data, status, error } = current;
  // Tras un cambio se recargan las dos vistas.
  const reload = () => {
    day.reload();
    week.reload();
  };
  // Estado de Google: calendarios en los que se puede crear un evento nuevo y colores de la leyenda.
  const google = useApiData(() => googleApi.status(), []);

  // Refresco automático: los cambios hechos en Google aparecen solos, sin pulsar "Sincronizar".
  const connected = google.data?.connected ?? false;
  const lastSync = useRef<string | null>(null);
  const refreshAll = useRef<() => void>(() => {});
  useEffect(() => {
    refreshAll.current = () => {
      day.reload();
      week.reload();
      google.reload();
    };
    // Última sincronización conocida (de la carga inicial): a partir de ahí se detectan los cambios.
    if (lastSync.current === null && google.data) lastSync.current = google.data.lastSyncAt ?? "";
  });
  useEffect(() => {
    if (!connected) return;
    let stopped = false;
    const check = async () => {
      try {
        const status = await googleApi.refresh();
        if (stopped) return;
        // Solo se recargan los eventos si la sincronización trajo algo nuevo.
        const current = status.lastSyncAt ?? "";
        if (lastSync.current !== null && current !== lastSync.current) refreshAll.current();
        lastSync.current = current;
      } catch {
        /* sin conexión: se reintenta en el siguiente ciclo */
      }
    };
    check();
    const timer = window.setInterval(check, 60_000);
    const onVisible = () => {
      if (document.visibilityState === "visible") check();
    };
    document.addEventListener("visibilitychange", onVisible);
    window.addEventListener("focus", check);
    return () => {
      stopped = true;
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", onVisible);
      window.removeEventListener("focus", check);
    };
  }, [connected]);

  // Posición provisional de los eventos arrastrados mientras Google confirma el cambio.
  const [moved, setMoved] = useState<Record<number, { startTime: string; endTime: string }>>({});
  const weekEvents = (week.data ?? []).map((e) => (moved[e.id] ? { ...e, ...moved[e.id] } : e));

  async function moveEvent(evt: CalendarEventDto, start: Date, end: Date) {
    const startTime = toDateTimeLocalValue(start) + ":00";
    const endTime = toDateTimeLocalValue(end) + ":00";
    setMoved((m) => ({ ...m, [evt.id]: { startTime, endTime } }));
    try {
      await calendarEventsApi.update(evt.id, {
        title: evt.title,
        startTime,
        endTime,
        isAllDay: evt.isAllDay,
        notes: evt.notes,
      });
    } catch (err) {
      alert("No se pudo mover el evento: " + errorText(err));
    } finally {
      // Se recarga con lo que realmente quedó guardado (si falló, el evento vuelve a su sitio).
      day.reload();
      week.reload();
      setMoved((m) => {
        const rest = { ...m };
        delete rest[evt.id];
        return rest;
      });
    }
  }

  // Vista semanal a pantalla completa: cubre toda la app y, si el navegador lo permite, usa la pantalla completa real.
  // ?completa=1 abre la semana ya a pantalla completa.
  const [expanded, setExpanded] = useState(() => new URLSearchParams(window.location.search).get("completa") === "1");

  // Si la semana se abrió desde otra pantalla (p. ej. el mini calendario de Hoy), al salir se vuelve a ella.
  // Si se entró desde la propia Agenda con su botón, se queda en la Agenda.
  const navigate = useNavigate();
  const returnTo = useRef<string | null>((useLocation().state as { returnTo?: string } | null)?.returnTo ?? null);

  function toggleExpanded(next: boolean) {
    setExpanded(next);
    if (next) {
      document.documentElement.requestFullscreen?.().catch(() => {
        /* el navegador no lo permite: queda la vista ampliada dentro de la ventana */
      });
      return;
    }
    if (document.fullscreenElement) document.exitFullscreen().catch(() => {});
    if (returnTo.current) {
      const destination = returnTo.current;
      returnTo.current = null; // solo se vuelve una vez
      navigate(destination);
    }
  }

  useEffect(() => {
    if (!expanded) return;
    // Salir de la pantalla completa del navegador (p. ej. con Esc) cierra también la vista ampliada.
    const onFullscreenChange = () => {
      if (!document.fullscreenElement) toggleExpanded(false);
    };
    // Esc también sale cuando el navegador no entró en pantalla completa (salvo que haya un diálogo abierto).
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape" && !document.querySelector(".modal-overlay")) toggleExpanded(false);
    };
    document.addEventListener("fullscreenchange", onFullscreenChange);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("fullscreenchange", onFullscreenChange);
      document.removeEventListener("keydown", onKey);
    };
    // toggleExpanded solo usa estado estable (set de estado, navigate y una ref): no hace falta resuscribirse.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [expanded]);

  // Los parámetros de apertura solo valen para la primera carga: se quitan para que un refresco no vuelva a ampliar.
  useEffect(() => {
    const query = new URLSearchParams(window.location.search);
    if (query.has("semana") || query.has("completa")) window.history.replaceState(window.history.state, "", window.location.pathname + "?vista=semana");
  }, []);

  // Al salir de la página se abandona la pantalla completa del navegador.
  useEffect(
    () => () => {
      if (document.fullscreenElement) document.exitFullscreen().catch(() => {});
    },
    [],
  );

  const [showForm, setShowForm] = useState(false);
  // Horas elegidas arrastrando en el calendario semanal para crear un evento.
  const [createRange, setCreateRange] = useState<{ start: Date; end: Date } | null>(null);
  const [eventToDelete, setEventToDelete] = useState<CalendarEventDto | null>(null);
  const [eventToEdit, setEventToEdit] = useState<CalendarEventDto | null>(null);

  return (
    <main className="main">
      <Topbar title="Agenda" subtitle="Eventos fijos que el planificador siempre respeta" status={status} onRefresh={reload} />
      {status === "error" && <ConnectionBanner message={error || ""} />}

      <GoogleCalendarCard onSynced={reload} />

      <div className="section-head">
        <h2>{view === "day" ? "Eventos del día" : "Semana"}</h2>
        <div className="actions-inline" style={{ alignItems: "center" }}>
          <div className="segmented" role="tablist" aria-label="Vista de la agenda">
            {([["day", "Día"], ["week", "Semana"]] as const).map(([value, label]) => (
              <button
                key={value}
                role="tab"
                aria-selected={view === value}
                className={view === value ? "btn-secondary" : "btn-ghost"}
                onClick={() => changeView(value)}
              >
                {label}
              </button>
            ))}
          </div>
          {view === "day" && (
            <input
              type="date"
              value={selectedDate}
              onChange={(e) => setSelectedDate(e.target.value)}
              className="date-input"
            />
          )}
          <button className="btn-primary" onClick={() => setShowForm(true)}>
            + Nuevo evento
          </button>
        </div>
      </div>

      {view === "day" && (
        <>
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
                    <span className="tag">{evt.colorHex && <i className="weekcal-key" style={{ background: evt.colorHex }} />}
                    {evt.googleLinked ? "Google Calendar" : "🔒 Fijo"}
                  </span>
                    <span className="mono">
                      {evt.isAllDay ? "Todo el día" : `${fmtTime(evt.startTime)} — ${fmtTime(evt.endTime)}`}
                    </span>
                    {evt.notes && <span>{evt.notes}</span>}
                  </div>
                </div>
                <div className="record-actions">
                  <button className="btn-secondary" onClick={() => setEventToEdit(evt)}>
                    Editar
                  </button>
                  <button className="btn-danger" onClick={() => setEventToDelete(evt)}>
                    Eliminar
                  </button>
                </div>
              </div>
            ))}
        </div>
        </>
      )}

      {view === "week" && (
        <div className={"week-panel" + (expanded ? " expanded" : "")}>
          <div className="week-nav">
            <button className="btn-secondary" onClick={() => shiftWeek(-1)} aria-label="Semana anterior">
              ←
            </button>
            <span className="week-range">
              {weekStart.toLocaleDateString("es-ES", { day: "numeric", month: "short" })} –{" "}
              {weekEnd.toLocaleDateString("es-ES", { day: "numeric", month: "short" })}
            </span>
            <button className="btn-secondary" onClick={() => shiftWeek(1)} aria-label="Semana siguiente">
              →
            </button>
            {weekStart.getTime() !== mondayOf(new Date()).getTime() && (
              <button className="btn-ghost" onClick={() => setWeekStart(mondayOf(new Date()))}>
                Esta semana
              </button>
            )}
            {expanded && (
              <button className="btn-primary" onClick={() => setShowForm(true)}>
                + Nuevo evento
              </button>
            )}
            <button
              className="btn-secondary week-fullscreen"
              onClick={() => toggleExpanded(!expanded)}
              aria-pressed={expanded}
              aria-label={expanded ? "Salir de pantalla completa" : "Pantalla completa"}
              title={expanded ? "Salir de la pantalla completa (Esc)" : "Ver la semana a pantalla completa"}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
                {expanded ? <path d="M9 4v5H4M15 4v5h5M9 20v-5H4M15 20v-5h5" /> : <path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5" />}
              </svg>
              <span className="week-fullscreen-label">{expanded ? "Salir de pantalla completa" : "Pantalla completa"}</span>
            </button>
            <span className="weekcal-legend">
              <span>
                <i className="weekcal-key manual" /> Solo en Compás
              </span>
              {(google.data?.calendars ?? [])
                .filter((c) => c.selected)
                .map((c) => (
                  <span key={c.id}>
                    <i className="weekcal-key" style={{ background: c.colorHex ?? "var(--panel)", border: c.colorHex ? "none" : "1px solid var(--color-ink)" }} />{" "}
                    {c.summary}
                  </span>
                ))}
            </span>
          </div>

          {week.status === "loading" && !week.data ? (
            <div className="empty-state">
              <p>Cargando la semana…</p>
            </div>
          ) : (
            <WeekCalendar weekStart={weekStart} events={weekEvents} onEdit={setEventToEdit}
              onMove={moveEvent}
              onCreate={(start, end) => setCreateRange({ start, end })}
            />
          )}
        </div>
      )}

      {eventToDelete && (
        <ConfirmModal
          title="Eliminar evento"
          message={
            `¿Seguro que quieres eliminar "${eventToDelete.title}"? No se puede deshacer.` +
            (eventToDelete.googleLinked ? " También se borrará de Google Calendar." : "")
          }
          onConfirm={async () => {
            await calendarEventsApi.remove(eventToDelete.id);
            reload();
          }}
          onCancel={() => setEventToDelete(null)}
        />
      )}

      {(showForm || eventToEdit || createRange) && (
        <EventModal
          initialStart={createRange?.start}
          initialEnd={createRange?.end}
          defaultDate={selectedDate}
          event={eventToEdit ?? undefined}
          google={google.data}
          onDelete={(evt) => {
            // Se cierra el formulario y se pide confirmación antes de borrar.
            setEventToEdit(null);
            setShowForm(false);
            setEventToDelete(evt);
          }}
          onClose={() => {
            setShowForm(false);
            setEventToEdit(null);
            setCreateRange(null);
          }}
          onSaved={() => {
            reload();
            google.reload(); // el calendario elegido queda como predeterminado: se vuelve a leer
          }}
        />
      )}
    </main>
  );
}
