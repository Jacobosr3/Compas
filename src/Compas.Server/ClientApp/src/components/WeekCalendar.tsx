import { useEffect, useMemo, useRef, useState, type CSSProperties, type PointerEvent as ReactPointerEvent } from "react";
import type { CalendarEventDto } from "../api/types";
import { fmtTime } from "../utils/format";
import { readableTextOn } from "../utils/color";

const HOUR_PX = 48;
const MIN_BLOCK_PX = 22;
const FIRST_VISIBLE_HOUR = 7; // al abrir, el calendario se desplaza hasta esta hora
const SNAP_MINUTES = 15; // al arrastrar, la hora se ajusta a cuartos de hora
const DRAG_THRESHOLD_PX = 5; // por debajo de esto es un clic, no un arrastre
const EDGE_SCROLL_PX = 36;
const LONG_PRESS_MS = 350; // en móvil el arrastre de un evento empieza con pulsación larga, para poder desplazarse con el dedo
/** Mismo criterio que el CSS de móvil: pantalla estrecha o móvil en horizontal. */
const PHONE_QUERY = "(max-width: 720px), (pointer: coarse) and (max-height: 500px)";

const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate());
const addDays = (d: Date, n: number) => {
  const r = new Date(d);
  r.setDate(r.getDate() + n);
  return r;
};
const addMinutes = (d: Date, n: number) => new Date(d.getTime() + n * 60000);
const minutesOf = (d: Date) => d.getHours() * 60 + d.getMinutes();

interface Segment {
  event: CalendarEventDto;
  top: number; // minutos desde las 00:00
  bottom: number;
  col: number;
  cols: number;
}

/** Trozos de evento que caen en un día (los que cruzan la medianoche se recortan), con su columna si se solapan. */
function layoutDay(events: CalendarEventDto[], day: Date): Segment[] {
  const dayStart = day.getTime();
  const dayEnd = addDays(day, 1).getTime();

  const segs = events
    .filter((e) => !e.isAllDay)
    .map((event) => {
      const s = new Date(event.startTime).getTime();
      const en = new Date(event.endTime).getTime();
      if (en <= dayStart || s >= dayEnd) return null;
      const top = (Math.max(s, dayStart) - dayStart) / 60000;
      const bottom = (Math.min(en, dayEnd) - dayStart) / 60000;
      return { event, top, bottom, col: 0, cols: 1 };
    })
    .filter((x): x is Segment => x !== null)
    .sort((a, b) => a.top - b.top || a.bottom - b.bottom);

  // Reparto en columnas dentro de cada grupo de eventos que se solapan.
  let group: Segment[] = [];
  let groupEnd = -1;
  const flush = () => {
    const colEnds: number[] = [];
    for (const seg of group) {
      let c = colEnds.findIndex((end) => end <= seg.top);
      if (c === -1) c = colEnds.length;
      colEnds[c] = seg.bottom;
      seg.col = c;
    }
    group.forEach((seg) => (seg.cols = colEnds.length));
    group = [];
  };
  for (const seg of segs) {
    if (group.length > 0 && seg.top >= groupEnd) flush();
    group.push(seg);
    groupEnd = Math.max(groupEnd, seg.bottom);
  }
  flush();
  return segs;
}

function coversDay(e: CalendarEventDto, day: Date): boolean {
  const s = new Date(e.startTime).getTime();
  const en = new Date(e.endTime).getTime();
  return s < addDays(day, 1).getTime() && en > day.getTime();
}

/** Clase y estilo de un evento: con el color de su calendario de Google si lo tiene; si no, el estilo de Compás. */
function appearance(e: CalendarEventDto): { kind: string; style?: CSSProperties } {
  if (e.colorHex) return { kind: "gcal", style: { background: e.colorHex, color: readableTextOn(e.colorHex), borderColor: e.colorHex } };
  return { kind: e.source === "Import" ? "google" : "manual" };
}

/** Selección de horas en curso (arrastrando sobre un día vacío) para crear un evento. */
interface Selection {
  dayIndex: number;
  startMin: number;
  endMin: number;
}

const floorSnap = (m: number) => Math.floor(m / SNAP_MINUTES) * SNAP_MINUTES;
const ceilSnap = (m: number) => Math.ceil(m / SNAP_MINUTES) * SNAP_MINUTES;
const clockOf = (min: number) => `${String(Math.floor(min / 60) % 24).padStart(2, "0")}:${String(min % 60).padStart(2, "0")}`;

/** Arrastre en curso: qué evento y cuánto se ha movido (en días y minutos, ya ajustado). */
interface DragState {
  eventId: number;
  dayDelta: number;
  minuteDelta: number;
  offsetX: number; // desplazamiento visual en píxeles (columna destino)
  offsetY: number;
}

/**
 * Semana en formato calendario: una columna por día, eje de horas y un bloque por evento.
 * Los eventos de Compás van en oscuro y los de Google con borde; los de todo el día, en una fila
 * superior. Cualquier evento con hora se puede arrastrar para cambiarle el día y la hora.
 */
export function WeekCalendar({
  weekStart,
  events,
  onEdit,
  onMove,
  onCreate,
}: {
  weekStart: Date;
  events: CalendarEventDto[];
  onEdit: (event: CalendarEventDto) => void;
  /** Se llama al soltar un evento arrastrado, con su nuevo inicio y fin (misma duración). */
  onMove: (event: CalendarEventDto, start: Date, end: Date) => void;
  /** Se llama al terminar de arrastrar sobre un día vacío: horas elegidas para un evento nuevo. */
  onCreate: (start: Date, end: Date) => void;
}) {
  const days = useMemo(() => Array.from({ length: 7 }, (_, i) => addDays(weekStart, i)), [weekStart]);
  const today = startOfDay(new Date());
  const now = new Date();
  const scrollRef = useRef<HTMLDivElement>(null);
  const topRef = useRef<HTMLDivElement>(null);
  const colRefs = useRef<(HTMLDivElement | null)[]>([]);
  const justDragged = useRef(false);
  const [drag, setDrag] = useState<DragState | null>(null);
  const [selection, setSelection] = useState<Selection | null>(null);

  // Abre el calendario cerca del inicio de la jornada en vez de a las 00:00.
  useEffect(() => {
    const scroller = scrollRef.current;
    if (!scroller) return;
    scroller.scrollTop = FIRST_VISIBLE_HOUR * HOUR_PX - 8;
    // En móvil a pantalla completa la semana se parte en lunes–jueves y viernes–domingo: se abre en la mitad de hoy.
    // En el resto de casos no hay scroll horizontal y esto no hace nada.
    const half = days.findIndex((d) => d.getTime() === startOfDay(new Date()).getTime()) >= 4 ? 4 : 0;
    const firstCol = scroller.closest(".expanded") && window.matchMedia(PHONE_QUERY).matches ? colRefs.current[half] : null;
    scroller.scrollLeft = firstCol ? firstCol.offsetLeft - (colRefs.current[0]?.offsetLeft ?? 0) : 0;
  }, [weekStart, days]);

  const allDay = events.filter((e) => e.isAllDay);

  function startDrag(down: ReactPointerEvent<HTMLDivElement>, event: CalendarEventDto, dayIndex: number) {
    if (down.button !== 0) return;
    const originX = down.clientX;
    const originY = down.clientY;
    const originScroll = scrollRef.current?.scrollTop ?? 0;
    let started = false;
    let last: DragState | null = null;

    // Con el dedo en móvil, el evento solo se arrastra tras una pulsación larga; un gesto normal desplaza el calendario.
    const needsHold = down.pointerType === "touch" && window.matchMedia(PHONE_QUERY).matches;
    let armed = !needsHold;
    let holdTimer: number | undefined;
    if (needsHold) {
      holdTimer = window.setTimeout(() => {
        armed = true;
        navigator.vibrate?.(15);
        setDrag({ eventId: event.id, dayDelta: 0, minuteDelta: 0, offsetX: 0, offsetY: 0 }); // evento "levantado"
      }, LONG_PRESS_MS);
    }
    // Una vez armado, se impide que el navegador convierta el gesto en scroll.
    const onTouchMove = (ev: TouchEvent) => {
      if (armed) ev.preventDefault();
    };

    const onMovePtr = (ev: PointerEvent) => {
      const scroller = scrollRef.current;
      const dxRaw = ev.clientX - originX;
      const dyRaw = ev.clientY - originY;
      if (!armed) {
        if (Math.hypot(dxRaw, dyRaw) > DRAG_THRESHOLD_PX * 2) finish(); // es un desplazamiento, no un arrastre
        return;
      }
      if (!started && Math.hypot(dxRaw, dyRaw) < DRAG_THRESHOLD_PX) return;
      started = true;

      // Desplazamiento automático al acercarse a un borde del calendario.
      if (scroller) {
        const box = scroller.getBoundingClientRect();
        if (ev.clientY > box.bottom - EDGE_SCROLL_PX) scroller.scrollTop += 14;
        else if (ev.clientY < box.top + EDGE_SCROLL_PX) scroller.scrollTop -= 14;
        if (ev.clientX > box.right - EDGE_SCROLL_PX) scroller.scrollLeft += 14;
        else if (ev.clientX < box.left + EDGE_SCROLL_PX) scroller.scrollLeft -= 14;
      }

      // Columna (día) bajo el puntero y minutos movidos, contando lo que se ha desplazado el scroll.
      const rects = colRefs.current.map((c) => c?.getBoundingClientRect()); // en vivo: el scroll horizontal las mueve
      const originLeft = rects[dayIndex]?.left ?? 0;
      let target = dayIndex;
      rects.forEach((r, i) => {
        if (r && ev.clientX >= r.left && ev.clientX < r.right) target = i;
      });
      const scrolled = (scroller?.scrollTop ?? 0) - originScroll;
      const minuteDelta = Math.round((((dyRaw + scrolled) / HOUR_PX) * 60) / SNAP_MINUTES) * SNAP_MINUTES;

      last = {
        eventId: event.id,
        dayDelta: target - dayIndex,
        minuteDelta,
        offsetX: (rects[target]?.left ?? originLeft) - originLeft,
        offsetY: (minuteDelta / 60) * HOUR_PX - scrolled,
      };
      setDrag(last);
    };

    const finish = () => {
      window.clearTimeout(holdTimer);
      window.removeEventListener("pointermove", onMovePtr);
      window.removeEventListener("pointerup", finish);
      window.removeEventListener("pointercancel", finish);
      window.removeEventListener("touchmove", onTouchMove);
      setDrag(null);
      if (!started || !last) {
        // Una pulsación larga soltada sin mover no debe abrir la edición.
        if (needsHold && armed) {
          justDragged.current = true;
          setTimeout(() => (justDragged.current = false), 0);
        }
        return;
      }

      // Evita que el clic que sigue al soltar abra la edición.
      justDragged.current = true;
      setTimeout(() => (justDragged.current = false), 0);

      const total = last.dayDelta * 24 * 60 + last.minuteDelta;
      if (total !== 0) onMove(event, addMinutes(new Date(event.startTime), total), addMinutes(new Date(event.endTime), total));
    };

    window.addEventListener("pointermove", onMovePtr);
    window.addEventListener("pointerup", finish);
    window.addEventListener("pointercancel", finish);
    if (needsHold) window.addEventListener("touchmove", onTouchMove, { passive: false });
  }

  /** Arrastrar sobre un hueco vacío de un día selecciona un rango de horas y, al soltar, pide crear el evento. */
  function startSelect(down: ReactPointerEvent<HTMLDivElement>, dayIndex: number) {
    if (down.button !== 0 || down.pointerType === "touch") return; // con el dedo se desplaza la página
    if ((down.target as HTMLElement).closest(".weekcal-event")) return; // eso es mover un evento, no crear
    const col = colRefs.current[dayIndex];
    if (!col) return;

    const minuteAt = (clientY: number) => {
      const rect = col.getBoundingClientRect(); // incluye el desplazamiento del calendario
      return Math.min(24 * 60, Math.max(0, ((clientY - rect.top) / HOUR_PX) * 60));
    };
    const anchor = floorSnap(minuteAt(down.clientY));
    const originX = down.clientX;
    const originY = down.clientY;
    let started = false;
    let last: Selection | null = null;

    const onMovePtr = (ev: PointerEvent) => {
      if (!started && Math.hypot(ev.clientX - originX, ev.clientY - originY) < DRAG_THRESHOLD_PX) return;
      started = true;

      const scroller = scrollRef.current;
      if (scroller) {
        const box = scroller.getBoundingClientRect();
        if (ev.clientY > box.bottom - EDGE_SCROLL_PX) scroller.scrollTop += 14;
        else if (ev.clientY < box.top + EDGE_SCROLL_PX) scroller.scrollTop -= 14;
      }

      const current = minuteAt(ev.clientY);
      // Hacia abajo: de la casilla inicial al cuarto de hora bajo el puntero; hacia arriba, al revés.
      const startMin = current >= anchor ? anchor : floorSnap(current);
      const endMin = current >= anchor ? Math.max(ceilSnap(current), anchor + SNAP_MINUTES) : anchor + SNAP_MINUTES;
      last = { dayIndex, startMin: Math.max(0, startMin), endMin: Math.min(24 * 60, endMin) };
      setSelection(last);
    };

    const finish = () => {
      window.removeEventListener("pointermove", onMovePtr);
      window.removeEventListener("pointerup", finish);
      window.removeEventListener("pointercancel", finish);
      setSelection(null);
      if (!started || !last) return; // un clic suelto no crea nada
      const day = days[dayIndex];
      onCreate(addMinutes(day, last.startMin), addMinutes(day, last.endMin));
    };

    window.addEventListener("pointermove", onMovePtr);
    window.addEventListener("pointerup", finish);
    window.addEventListener("pointercancel", finish);
  }

  return (
    <div className={"weekcal" + (drag || selection ? " dragging" : "")}>
      {/* En móvil las columnas no caben y el calendario se desplaza en horizontal: la cabecera sigue ese scroll. */}
      <div className="weekcal-top" ref={topRef}>
      <div className="weekcal-row weekcal-head">
        <div className="weekcal-gutter" />
        {days.map((d) => (
          <div key={d.getTime()} className={"weekcal-dayhead" + (d.getTime() === today.getTime() ? " today" : "")}>
            <span>{d.toLocaleDateString("es-ES", { weekday: "short" })}</span>
            <b>{d.getDate()}</b>
          </div>
        ))}
      </div>

      {allDay.length > 0 && (
        <div className="weekcal-row weekcal-allday">
          <div className="weekcal-gutter">todo el día</div>
          {days.map((d) => (
            <div key={d.getTime()} className="weekcal-allday-cell">
              {allDay
                .filter((e) => coversDay(e, d))
                .map((e) => (
                  <span
                    key={e.id}
                    className={"weekcal-chip " + appearance(e).kind}
                    style={appearance(e).style}
                    title={e.title}
                    role="button"
                    tabIndex={0}
                    onClick={() => onEdit(e)}
                    onKeyDown={(k) => {
                      if (k.key === "Enter" || k.key === " ") {
                        k.preventDefault();
                        onEdit(e);
                      }
                    }}
                  >
                    {e.title}
                  </span>
                ))}
            </div>
          ))}
        </div>
      )}
      </div>

      <div
        className="weekcal-scroll"
        ref={scrollRef}
        onScroll={(e) => {
          if (topRef.current) topRef.current.scrollLeft = e.currentTarget.scrollLeft;
        }}
      >
        <div className="weekcal-row weekcal-body" style={{ height: 24 * HOUR_PX }}>
          <div className="weekcal-gutter weekcal-hours">
            {Array.from({ length: 24 }, (_, h) => (
              <span key={h} style={{ top: h * HOUR_PX }}>
                {h === 0 ? "" : `${String(h).padStart(2, "0")}:00`}
              </span>
            ))}
          </div>

          {days.map((d, dayIndex) => {
            const isToday = d.getTime() === today.getTime();
            return (
              <div
                key={d.getTime()}
                ref={(el) => {
                  colRefs.current[dayIndex] = el;
                }}
                className={"weekcal-col" + (isToday ? " today" : "")}
                onPointerDown={(e) => startSelect(e, dayIndex)}
              >
                {Array.from({ length: 24 }, (_, h) => (
                  <i key={h} className="weekcal-line" style={{ top: h * HOUR_PX }} />
                ))}

                {layoutDay(events, d).map((seg) => {
                  const height = Math.max(((seg.bottom - seg.top) / 60) * HOUR_PX, MIN_BLOCK_PX);
                  const google = seg.event.source === "Import";
                  const look = appearance(seg.event);
                  const width = 100 / seg.cols;
                  const dragging = drag?.eventId === seg.event.id;
                  // Durante el arrastre la hora que se muestra es la nueva.
                  const shift = dragging ? drag.dayDelta * 24 * 60 + drag.minuteDelta : 0;
                  const startLabel = fmtTime(addMinutes(new Date(seg.event.startTime), shift).toISOString());
                  const endLabel = fmtTime(addMinutes(new Date(seg.event.endTime), shift).toISOString());
                  return (
                    <div
                      key={`${seg.event.id}-${d.getTime()}`}
                      className={
                        "weekcal-event " + look.kind + (height < 40 ? " compact" : "") + (dragging ? " moving" : "")
                      }
                      style={{
                        ...look.style,
                        top: (seg.top / 60) * HOUR_PX,
                        height,
                        left: `calc(${seg.col * width}% + 2px)`,
                        width: `calc(${width}% - 4px)`,
                        transform: dragging ? `translate(${drag.offsetX}px, ${drag.offsetY}px)` : undefined,
                      }}
                      role="button"
                      tabIndex={0}
                      title={`${seg.event.title} · ${fmtTime(seg.event.startTime)} — ${fmtTime(seg.event.endTime)}${google ? " · Google Calendar" : ""} · arrastra para mover`}
                      onPointerDown={(e) => startDrag(e, seg.event, dayIndex)}
                      onClick={() => {
                        if (!justDragged.current) onEdit(seg.event);
                      }}
                      onKeyDown={(e) => {
                        if (e.key === "Enter" || e.key === " ") {
                          e.preventDefault();
                          onEdit(seg.event);
                        }
                      }}
                    >
                      <span className="weekcal-event-title">{seg.event.title}</span>
                      {(height >= 40 || dragging) && (
                        <span className="weekcal-event-time">
                          {startLabel} — {endLabel}
                        </span>
                      )}
                    </div>
                  );
                })}

                {selection?.dayIndex === dayIndex && (
                  <div
                    className="weekcal-selection"
                    style={{
                      top: (selection.startMin / 60) * HOUR_PX,
                      height: ((selection.endMin - selection.startMin) / 60) * HOUR_PX,
                    }}
                  >
                    <span>
                      {clockOf(selection.startMin)} — {clockOf(selection.endMin === 24 * 60 ? 0 : selection.endMin)}
                    </span>
                  </div>
                )}

                {isToday && <div className="weekcal-now" style={{ top: (minutesOf(now) / 60) * HOUR_PX }} />}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
