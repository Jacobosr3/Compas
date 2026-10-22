import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Topbar } from "../components/Topbar";
import { ConnectionBanner } from "../components/ConnectionBanner";
import { ConfirmModal } from "../components/ConfirmModal";
import { useApiData } from "../hooks/useApiData";
import { plannerApi, projectsApi, workItemsApi } from "../api";
import type { PlanEntryDto, WorkItemDto } from "../api/types";
import { SubtasksModal, TaskModal } from "../components/TaskParts";
import { fmtDateLong, fmtDuration, totalMinutes, fmtTime } from "../utils/format";

const TIME_KEY = "compas.planTime";

type TimeInput = { hours: string; minutes: string };

function readSavedTime(): TimeInput {
  try {
    const saved = JSON.parse(localStorage.getItem(TIME_KEY) ?? "null");
    if (typeof saved?.hours === "string" && typeof saved?.minutes === "string") return saved;
  } catch {
    /* sin almacenamiento o dato corrupto: se empieza sin límite */
  }
  return { hours: "", minutes: "" };
}

/** Minutos a dedicar a tareas según lo escrito; undefined = sin límite. */
function budgetFrom(t: TimeInput): number | undefined {
  const min = Math.max(0, Math.round(Number(t.hours))) * 60 + Math.max(0, Math.round(Number(t.minutes)));
  return min > 0 ? min : undefined;
}

const PLANNER_INFO = (
  <>
    <section className="info-section">
      <h3>Cómo se organiza el día</h3>
      <p>
        El planificador reparte tus tareas pendientes y programadas entre los huecos libres del día, desde
        las 08:00 y sin hora de fin (hasta medianoche). Los eventos de la agenda son fijos y nunca se mueven. En cada hueco elige la tarea
        que mejor puntúa: urgencia de la fecha de vencimiento (40 %), prioridad (35 %) y lo bien que encaja
        en el hueco (25 %). Tras 90 min seguidos de tareas añade un descanso de 15 min.
      </p>
    </section>

    <section className="info-section">
      <h3>Tiempo para tareas</h3>
      <p>
        Indica en horas y minutos cuánto quieres dedicar a tareas. El planificador no pasa de ese total
        (los descansos no cuentan) y deja el resto como hueco libre. Si cabe una tarea, se coloca, por
        pequeña que sea. Con el campo vacío no hay límite y se rellena todo el día. Las tareas que se
        quedan fuera vuelven a Pendiente.
      </p>
    </section>

    <section className="info-section">
      <h3>Los botones</h3>
      <dl>
        <dt>Plan del día</dt>
        <dd>
          Crea el plan de hoy desde cero, empezando a las 08:00 y sin límite de tiempo para tareas: reparte
          todas las que caben hasta medianoche.
        </dd>
        <dt>Planificar desde ahora</dt>
        <dd>
          Pregunta cuánto tiempo quieres dedicar a tareas y reparte las pendientes desde la hora actual, sin
          pasar de ese tiempo. Lo que no quepa hoy pasa al día siguiente, desde las 08:00. Úsalo cuando cambie algo a mitad de día: una tarea urgente,
          una reunión nueva o un retraso.
        </dd>
      </dl>
      <p>
        Cada vez que usas «Plan del día» o «Planificar desde ahora» se crea un plan nuevo, y lo que cambia de hueco respecto al
        anterior se marca como «Reordenado».
      </p>
    </section>

    <section className="info-section">
      <h3>Cambiar el orden a mano</h3>
      <p>
        En «Tu día» puedes arrastrar una tarea o usar las flechas ↑ ↓. El plan se recoloca desde su primer
        bloque con tu orden, respetando los eventos fijos y el tiempo indicado. Los eventos y los descansos
        no se pueden mover. Si una tarea ya no cabe en su sitio, se coloca la siguiente de tu lista que sí.
      </p>
    </section>

    <section className="info-section">
      <h3>Qué no se planifica</h3>
      <p>
        Las tareas completadas, canceladas o en curso, y las que tienen una estimación de 0 min.
      </p>
    </section>
  </>
);

function greeting(d: Date): string {
  const h = d.getHours();
  return h < 6 ? "Buenas noches" : h < 13 ? "Buenos días" : h < 21 ? "Buenas tardes" : "Buenas noches";
}

export function TodayPage() {
  const plan = useApiData(() => plannerApi.today(), []);
  const projects = useApiData(() => projectsApi.list(), []);
  const inbox = useApiData(() => workItemsApi.inbox(), []);
  const [quickTitle, setQuickTitle] = useState("");
  const [time, setTime] = useState(readSavedTime);
  const [replanning, setReplanning] = useState(false);
  const [clearing, setClearing] = useState(false);
  const [editingTask, setEditingTask] = useState<WorkItemDto | null>(null);
  const [stepsTaskId, setStepsTaskId] = useState<number | null>(null);
  // Tiempo máximo a dedicar a tareas del plan actual, en minutos; undefined = sin límite.
  // Solo lo fija "Planificar desde ahora"; "Plan del día" lo borra.
  const budget = budgetFrom(time);

  function changeTime(next: TimeInput) {
    setTime(next);
    try {
      localStorage.setItem(TIME_KEY, JSON.stringify(next));
    } catch {
      /* sin almacenamiento: solo se pierde la preferencia */
    }
  }

  /** Plan completo desde las 08:00, sin límite de tiempo para tareas. */
  function generatePlan() {
    changeTime({ hours: "", minutes: "" });
    runAction(() => plannerApi.generate(), reloadAll);
  }

  function confirmReplan(next: TimeInput) {
    changeTime(next);
    setReplanning(false);
    runAction(() => plannerApi.replan(budgetFrom(next)), reloadAll);
  }

  const status = plan.status === "error" || projects.status === "error" || inbox.status === "error"
    ? "error"
    : plan.status === "loading" || projects.status === "loading" || inbox.status === "loading"
      ? "loading"
      : "ok";

  function reloadAll() {
    plan.reload();
    projects.reload();
    inbox.reload();
  }

  async function quickAdd() {
    const title = quickTitle.trim();
    if (!title) return;
    await runAction(() => workItemsApi.create({ title, priority: "Medium", estimatedHours: 0, estimatedMinutes: 30 }), () => {
      setQuickTitle("");
      inbox.reload();
    });
  }

  const now = new Date();
  const entries = plan.data?.entries ?? [];

  const year = now.getFullYear();
  const month = now.getMonth();
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const firstDayOfWeek = (new Date(year, month, 1).getDay() + 6) % 7;
  const monthName = now.toLocaleString("es-ES", { month: "long" });

  return (
    <main className="main main-wide">
      <Topbar title={greeting(now)} subtitle={fmtDateLong(now)} status={status} onRefresh={reloadAll} info={PLANNER_INFO} />

      {status === "error" && <ConnectionBanner message={plan.error || projects.error || inbox.error || ""} />}

      <div className="today-grid">
        <div className="today-main">
          <NowCard
            entries={entries}
            loading={plan.status === "loading"}
            pendingCount={inbox.data?.length ?? 0}
            pendingMinutes={(inbox.data ?? []).reduce((sum, t) => sum + totalMinutes(t), 0)}
            onGenerate={generatePlan}
            onReplan={() => setReplanning(true)}
            onClear={() => setClearing(true)}
          />

          <div className="section-head">
            <h2>Tu día</h2>
          </div>
          <Timeline
            entries={entries}
            loading={plan.status === "loading"}
            onReorder={(ids) => runAction(() => plannerApi.reorder(ids, budget), reloadAll)}
          />
        </div>

        <aside className="today-side">
          <div className="card glass">
            <h3>
              Proyectos <span>{projects.data ? `${projects.data.length} activos` : "—"}</span>
            </h3>
            {!projects.data || projects.data.length === 0 ? (
              <div className="empty-mini">Aún no tienes proyectos.</div>
            ) : (
              projects.data.slice(0, 5).map((p) => {
                const pct = Math.round(p.progressPercent);
                return (
                  <Link key={p.id} to={`/proyectos/${p.id}`} className="proj-row" style={{ ["--c" as string]: p.colorHex }}>
                    <div className="proj-ring" style={{ ["--deg" as string]: `${pct * 3.6}deg` }}>
                      <span>{pct}%</span>
                    </div>
                    <div className="proj-info">
                      <div className="proj-name">{p.name}</div>
                      <div className="proj-sub">
                        {p.completedWorkItems}/{p.totalWorkItems} tareas
                        {p.targetDate ? ` · ${new Date(p.targetDate).toLocaleDateString("es-ES", { day: "numeric", month: "short" })}` : ""}
                      </div>
                    </div>
                  </Link>
                );
              })
            )}
          </div>

          <div className="card glass">
            <h3>
              Bandeja <span>{inbox.data ? `${inbox.data.length} pendientes` : "—"}</span>
            </h3>
            {!inbox.data || inbox.data.length === 0 ? (
              <div className="empty-mini">La bandeja está vacía.</div>
            ) : (
              inbox.data.slice(0, 5).map((t) => (
                <div
                  key={t.id}
                  className="inbox-row clickable"
                  role="button"
                  tabIndex={0}
                  onClick={() => setEditingTask(t)}
                  onKeyDown={(e) => {
                    if (e.target === e.currentTarget && (e.key === "Enter" || e.key === " ")) {
                      e.preventDefault();
                      setEditingTask(t);
                    }
                  }}
                >
                  <span className="inbox-dot" />
                  <span className="inbox-title">{t.title}</span>
                  {t.subtasks.length > 0 && (
                    <button
                      className="tag tag-button"
                      aria-haspopup="dialog"
                      aria-label={`Ver pasos de "${t.title}"`}
                      onClick={(e) => {
                        e.stopPropagation();
                        setStepsTaskId(t.id);
                      }}
                    >
                      ☑ {t.subtasks.filter((s) => s.isCompleted).length}/{t.subtasks.length}
                    </button>
                  )}
                  <span className="mono inbox-est">{fmtDuration(totalMinutes(t))}</span>
                </div>
              ))
            )}
            <div className="quick-add">
              <input
                placeholder="Nueva tarea…"
                value={quickTitle}
                onChange={(e) => setQuickTitle(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && quickAdd()}
              />
              <button onClick={quickAdd}>Añadir</button>
            </div>
          </div>

          <div className="card glass">
            <h3 style={{ textTransform: "capitalize" }}>{monthName}</h3>
            <div className="mini-cal">
              {["L", "M", "X", "J", "V", "S", "D"].map((d) => (
                <span key={d} className="d" style={{ fontWeight: 600 }}>{d}</span>
              ))}
              {Array.from({ length: firstDayOfWeek }).map((_, i) => (
                <span key={`pad-${i}`} className="d pad" />
              ))}
              {Array.from({ length: daysInMonth }).map((_, i) => (
                <span key={i} className={"d" + (i + 1 === now.getDate() ? " today" : "")}>{i + 1}</span>
              ))}
            </div>
          </div>
        </aside>
      </div>

      {editingTask && (
        <TaskModal
          task={editingTask}
          projects={projects.data ?? []}
          onClose={() => setEditingTask(null)}
          onSaved={reloadAll}
        />
      )}

      {stepsTaskId !== null && inbox.data?.find((t) => t.id === stepsTaskId) && (
        <SubtasksModal
          task={inbox.data.find((t) => t.id === stepsTaskId)!}
          onClose={() => setStepsTaskId(null)}
          onChanged={inbox.reload}
        />
      )}

      {clearing && (
        <ConfirmModal
          title="Vaciar programadas"
          message="¿Vaciar el plan de hoy? Las tareas programadas volverán a la bandeja."
          confirmLabel="Vaciar"
          onConfirm={async () => {
            await plannerApi.clear();
            reloadAll();
          }}
          onCancel={() => setClearing(false)}
        />
      )}

      {replanning && <ReplanModal initial={time} onConfirm={confirmReplan} onCancel={() => setReplanning(false)} />}
    </main>
  );
}

async function runAction(action: () => Promise<unknown>, after: () => void) {
  try {
    await action();
    after();
  } catch (err) {
    alert("No se pudo completar la acción: " + (err instanceof Error ? err.message : String(err)));
  }
}

/** Resumen del plan de hoy: qué está planificado y qué queda pendiente, con las acciones del plan. */
function NowCard({
  entries,
  loading,
  pendingCount,
  pendingMinutes,
  onGenerate,
  onReplan,
  onClear,
}: {
  entries: PlanEntryDto[];
  loading: boolean;
  /** Tareas pendientes de planificar (la bandeja) y su duración total estimada. */
  pendingCount: number;
  pendingMinutes: number;
  onGenerate: () => void;
  onReplan: () => void;
  onClear: () => void;
}) {
  if (loading) {
    return (
      <section className="now-card">
        <div className="empty-state">
          <p>Cargando el plan de hoy…</p>
        </div>
      </section>
    );
  }

  const tasks = entries.filter((e) => e.kind === "Task");
  const plannedCount = tasks.length;
  const plannedMinutes = tasks.reduce(
    (sum, e) => sum + Math.round((new Date(e.end).getTime() - new Date(e.start).getTime()) / 60000),
    0,
  );
  const first = tasks.length > 0 ? new Date(Math.min(...tasks.map((e) => new Date(e.start).getTime()))) : null;
  const last = tasks.length > 0 ? new Date(Math.max(...tasks.map((e) => new Date(e.end).getTime()))) : null;
  const spills = first !== null && last !== null && first.toDateString() !== last.toDateString();
  const range = first && last ? `${fmtTime(first.toISOString())} — ${fmtTime(last.toISOString())}${spills ? " (+1 día)" : ""}` : null;

  const total = pendingCount + plannedCount;
  const pct = total > 0 ? plannedCount / total : 0;
  const circumference = 2 * Math.PI * 42;
  const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

  const planned = plannedCount > 0;
  const title = planned ? "Planificado" : pendingCount > 0 ? "Listo para planificar" : "Todo al día";
  const sub = planned
    ? `${plural(plannedCount, "tarea", "tareas")} (${fmtDuration(plannedMinutes)}) entre las ${fmtTime(first!.toISOString())} y las ${fmtTime(last!.toISOString())}${spills ? ", continúa al día siguiente" : ""}.`
    : pendingCount > 0
      ? `Tienes ${plural(pendingCount, "tarea pendiente", "tareas pendientes")} (${fmtDuration(pendingMinutes)}).`
      : "No hay tareas pendientes. Añade alguna desde la bandeja o usa «Plan del día».";

  return (
    <section className="now-card">
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 24, flexWrap: "wrap" }}>
        <div style={{ minWidth: 0, flex: "1 1 380px" }}>
          <div className="now-eyebrow">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <rect x="3" y="5" width="18" height="16" rx="2" />
              <path d="M3 10h18M8 3v4M16 3v4" />
            </svg>
            Plan de hoy
          </div>
          <h2 className="now-title">{title}</h2>
          <div className="now-sub">{sub}</div>
          <div className="chips">
            {planned && (
              <>
                <span className="chip">
                  <b>{plural(plannedCount, "tarea", "tareas")}</b> planificadas
                </span>
                <span className="chip">
                  <b>{fmtDuration(plannedMinutes)}</b> de trabajo
                </span>
              </>
            )}
            <span className="chip">
              <b>{plural(pendingCount, "tarea", "tareas")}</b> pendientes
            </span>
            {!planned && pendingMinutes > 0 && (
              <span className="chip">
                <b>{fmtDuration(pendingMinutes)}</b> por planificar
              </span>
            )}
          </div>
          <PlanActions divider primaryGenerate={!planned} onGenerate={onGenerate} onReplan={onReplan} onClear={onClear} />
        </div>
        <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 8 }}>
          <div style={{ position: "relative", width: 96, height: 96 }}>
            <svg width="96" height="96" viewBox="0 0 96 96" style={{ transform: "rotate(-90deg)" }}>
              <circle cx="48" cy="48" r="42" fill="none" stroke="#E5E5E5" strokeWidth="6" />
              <circle
                cx="48"
                cy="48"
                r="42"
                fill="none"
                stroke="#0A0A0A"
                strokeWidth="6"
                strokeLinecap="round"
                strokeDasharray={circumference}
                strokeDashoffset={circumference * (1 - pct)}
                style={{ transition: "stroke-dashoffset .4s linear" }}
              />
            </svg>
            <div style={{ position: "absolute", inset: 0, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center" }}>
              <span className="mono" style={{ fontSize: 18, fontWeight: 600 }}>
                {planned ? plannedCount : pendingCount}
              </span>
              <span style={{ fontSize: 12, color: "var(--muted)" }}>{planned ? "planificadas" : "pendientes"}</span>
            </div>
          </div>
          {range && (
            <div className="mono" style={{ fontSize: 12, color: "var(--muted)" }}>
              {range}
            </div>
          )}
        </div>
      </div>
    </section>
  );
}

/** Acciones sobre el plan del día: viven en la tarjeta superior para que no haya botones repetidos. */
function PlanActions({
  primaryGenerate = false,
  divider = false,
  onGenerate,
  onReplan,
  onClear,
}: {
  primaryGenerate?: boolean;
  divider?: boolean;
  onGenerate: () => void;
  onReplan: () => void;
  onClear: () => void;
}) {
  return (
    <div className={"plan-actions" + (divider ? " with-divider" : "")}>
      <button className={primaryGenerate ? "btn-primary" : "btn-secondary"} onClick={onGenerate}>
        Plan del día
      </button>
      <button className="btn-secondary" onClick={onReplan}>
        Planificar desde ahora
      </button>
      <button className="btn-ghost" onClick={onClear}>
        Vaciar programadas
      </button>
    </div>
  );
}

/** Pregunta cuánto tiempo dedicar a tareas y planifica desde la hora actual. */
function ReplanModal({
  initial,
  onConfirm,
  onCancel,
}: {
  initial: TimeInput;
  onConfirm: (time: TimeInput) => void;
  onCancel: () => void;
}) {
  const [draft, setDraft] = useState(initial);
  const budget = budgetFrom(draft);

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") onCancel();
    }
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onCancel]);

  return (
    <div className="modal-overlay" onClick={onCancel}>
      <div
        className="modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="replan-title"
        onClick={(e) => e.stopPropagation()}
        onKeyDown={(e) => {
          if (e.key === "Enter") onConfirm(draft);
        }}
      >
        <h2 id="replan-title">Planificar desde ahora</h2>
        <p className="confirm-message">
          Son las <b>{fmtTime(new Date().toISOString())}</b>. Se reparten tus tareas pendientes desde esta hora,
          respetando los eventos de la agenda. Lo que no quepa hoy pasa al día siguiente, desde las 08:00.
        </p>
        <TimeBudgetField time={draft} budget={budget} onChange={setDraft} />
        <div className="modal-actions">
          <button className="btn-secondary" onClick={onCancel}>
            Cancelar
          </button>
          <button className="btn-primary" onClick={() => onConfirm(draft)}>
            Planificar
          </button>
        </div>
      </div>
    </div>
  );
}

/** Tiempo que el usuario quiere dedicar a tareas: horas + minutos, sin límites. Vacío = sin límite. */
function TimeBudgetField({
  time,
  budget,
  onChange,
}: {
  time: TimeInput;
  budget?: number;
  onChange: (next: TimeInput) => void;
}) {
  return (
    <div className="budget-box">
      <div className="budget-text">
        <span className="budget-label">Tiempo para tareas</span>
        <span className="budget-hint">
          {budget ? `Máximo ${fmtDuration(budget)}` : "Sin límite"}
        </span>
      </div>
      <div className="budget-inputs">
        <label className="budget-unit">
          <input
            type="number"
            min={0}
            placeholder="0"
            aria-label="Horas para tareas"
            value={time.hours}
            onChange={(e) => onChange({ ...time, hours: e.target.value })}
          />
          <span>h</span>
        </label>
        <label className="budget-unit">
          <input
            type="number"
            min={0}
            placeholder="0"
            aria-label="Minutos para tareas"
            value={time.minutes}
            onChange={(e) => onChange({ ...time, minutes: e.target.value })}
          />
          <span>min</span>
        </label>
      </div>
    </div>
  );
}

/** Etiqueta de un día del plan: "Mañana" o "Sábado 24 de octubre". */
function planDayLabel(date: Date, today: Date): string {
  const days = Math.round(
    (new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime() -
      new Date(today.getFullYear(), today.getMonth(), today.getDate()).getTime()) /
      86400000,
  );
  if (days === 1) return "Mañana";
  return fmtDateLong(date);
}

function Timeline({
  entries,
  loading,
  onReorder,
}: {
  entries: PlanEntryDto[];
  loading: boolean;
  onReorder: (workItemIds: number[]) => void;
}) {
  const [dragId, setDragId] = useState<number | null>(null);
  const [overId, setOverId] = useState<number | null>(null);

  if (loading) {
    return (
      <div className="timeline">
        <div className="empty-state">
          <p>Cargando línea de tiempo…</p>
        </div>
      </div>
    );
  }

  if (entries.length === 0) {
    return (
      <div className="timeline">
        <div className="empty-state">
          <p>Todavía no hay nada planificado para hoy.</p>
        </div>
      </div>
    );
  }

  const now = new Date();
  let dividerShown = false;

  // Solo las tareas se pueden mover; los eventos fijos de la agenda no.
  const taskIds = entries.filter((e) => e.kind === "Task" && e.workItemId != null).map((e) => e.workItemId!);
  const canReorder = taskIds.length > 1;

  function moveTo(id: number, toIndex: number) {
    const next = taskIds.filter((x) => x !== id);
    next.splice(toIndex, 0, id);
    if (next.some((x, i) => x !== taskIds[i])) onReorder(next);
  }

  return (
    <div className="timeline">
      {entries.map((entry, index) => {
        const start = new Date(entry.start);
        const end = new Date(entry.end);
        const isCurrent = start <= now && now < end;
        // El plan puede seguir en días posteriores: se separa con el nombre del día.
        const previousDay = (index === 0 ? now : new Date(entries[index - 1].start)).toDateString();
        const showDay = start.toDateString() !== previousDay;
        const showDivider = !dividerShown && start > now;
        if (showDivider) dividerShown = true;

        const movableId = canReorder && entry.kind === "Task" ? entry.workItemId : null;
        const taskIndex = movableId != null ? taskIds.indexOf(movableId) : -1;

        const tag =
          entry.kind === "Task"
            ? entry.projectName || "Sin proyecto"
            : entry.kind === "CalendarEvent"
              ? "🔒 Fijo en agenda"
              : entry.kind === "Break"
                ? "Pausa"
                : null;

        return (
          <div key={entry.id}>
            {showDivider && (
              <div className="tl-now-divider">
                <span className="line" />
                <span className="lbl">AHORA · {now.toLocaleTimeString("es-ES", { hour: "2-digit", minute: "2-digit" })}</span>
                <span className="line" />
              </div>
            )}

            {showDay && <div className="tl-day">{planDayLabel(start, now)}</div>}

            {entry.kind === "Free" ? (
              <div className="tl-item">
                <div className="tl-time">{fmtTime(entry.start)}</div>
                <div className="tl-block free">{entry.reason || "Hueco libre"}</div>
              </div>
            ) : (
              <div className="tl-item">
                <div className="tl-time">{fmtTime(entry.start)}</div>
                <div className="tl-dot" style={isCurrent ? { background: "var(--signal)" } : undefined} />
                <div
                  className={
                    "tl-block" +
                    (isCurrent ? " current" : "") +
                    (entry.priority === "Urgent" ? " flagged" : "") +
                    (movableId != null ? " movable" : "") +
                    (movableId != null && movableId === dragId ? " dragging" : "") +
                    (movableId != null && movableId === overId && dragId !== overId ? " drag-over" : "")
                  }
                  draggable={movableId != null}
                  onDragStart={() => movableId != null && setDragId(movableId)}
                  onDragOver={(e) => {
                    if (dragId == null || movableId == null) return;
                    e.preventDefault();
                    setOverId(movableId);
                  }}
                  onDrop={(e) => {
                    e.preventDefault();
                    if (dragId != null && movableId != null) moveTo(dragId, taskIndex);
                    setDragId(null);
                    setOverId(null);
                  }}
                  onDragEnd={() => {
                    setDragId(null);
                    setOverId(null);
                  }}
                >
                  <div className="tl-main">
                    <div className="tl-name">{entry.title}</div>
                    <div className="tl-meta">
                      {tag && <span className="tag">{tag}</span>}
                      {entry.wasRescheduled && <span className="tag moved">↻ Reordenado</span>}
                    </div>
                  </div>
                  <div className="tl-side">
                    <div className="tl-dur mono">{fmtDuration(Math.round((end.getTime() - start.getTime()) / 60000))}</div>
                    {movableId != null && (
                      <div className="tl-move">
                        <button
                          className="btn-ghost"
                          aria-label={`Subir "${entry.title}"`}
                          disabled={taskIndex === 0}
                          onClick={() => moveTo(movableId, taskIndex - 1)}
                        >
                          ↑
                        </button>
                        <button
                          className="btn-ghost"
                          aria-label={`Bajar "${entry.title}"`}
                          disabled={taskIndex === taskIds.length - 1}
                          onClick={() => moveTo(movableId, taskIndex + 1)}
                        >
                          ↓
                        </button>
                      </div>
                    )}
                  </div>
                </div>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
