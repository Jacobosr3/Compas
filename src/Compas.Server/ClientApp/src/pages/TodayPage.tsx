import { useMemo, useState } from "react";
import { Topbar } from "../components/Topbar";
import { ConnectionBanner } from "../components/ConnectionBanner";
import { useApiData } from "../hooks/useApiData";
import { plannerApi, projectsApi, workItemsApi } from "../api";
import type { PlanEntryDto } from "../api/types";
import { fmtDateLong, fmtDuration, totalMinutes, fmtTime, priorityLabel } from "../utils/format";

function greeting(d: Date): string {
  const h = d.getHours();
  return h < 6 ? "Buenas noches" : h < 13 ? "Buenos días" : h < 21 ? "Buenas tardes" : "Buenas noches";
}

export function TodayPage() {
  const plan = useApiData(() => plannerApi.today(), []);
  const projects = useApiData(() => projectsApi.list(), []);
  const inbox = useApiData(() => workItemsApi.inbox(), []);
  const [quickTitle, setQuickTitle] = useState("");

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
  const tasks = entries.filter((e) => e.kind === "Task");
  const plannedMin = tasks.reduce((n, e) => n + Math.round((new Date(e.end).getTime() - new Date(e.start).getTime()) / 60000), 0);
  const inboxMin = (inbox.data ?? []).reduce((n, t) => n + totalMinutes(t), 0);

  const year = now.getFullYear();
  const month = now.getMonth();
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const firstDayOfWeek = (new Date(year, month, 1).getDay() + 6) % 7;
  const monthName = now.toLocaleString("es-ES", { month: "long" });

  return (
    <main className="main main-wide">
      <Topbar title={greeting(now)} subtitle={fmtDateLong(now)} status={status} onRefresh={reloadAll} />

      {status === "error" && <ConnectionBanner message={plan.error || projects.error || inbox.error || ""} />}

      <div className="stat-row">
        <div className="stat">
          <span className="stat-ico a">◷</span>
          <div>
            <div className="stat-num">{tasks.length}</div>
            <div className="stat-lbl">tareas hoy · {fmtDuration(plannedMin)}</div>
          </div>
        </div>
        <div className="stat">
          <span className="stat-ico b">▤</span>
          <div>
            <div className="stat-num">{inbox.data?.length ?? "—"}</div>
            <div className="stat-lbl">en bandeja · {fmtDuration(inboxMin)}</div>
          </div>
        </div>
        <div className="stat">
          <span className="stat-ico c">◆</span>
          <div>
            <div className="stat-num">{projects.data?.length ?? "—"}</div>
            <div className="stat-lbl">proyectos activos</div>
          </div>
        </div>
      </div>

      <div className="today-grid">
        <div className="today-main">
          <NowCard entry={plan.data?.nowEntry ?? null} loading={plan.status === "loading"} onChanged={reloadAll} />

          <div className="section-head">
            <h2>Tu día</h2>
            <div className="actions-inline">
              <button className="link-btn" onClick={() => runAction(() => plannerApi.generate(), reloadAll)}>
                Generar plan del día
              </button>
              <button className="link-btn" onClick={() => runAction(() => plannerApi.replan(), reloadAll)}>
                Replanificar desde ahora
              </button>
            </div>
          </div>
          <Timeline entries={entries} loading={plan.status === "loading"} />
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
                  <div key={p.id} className="proj-row" style={{ ["--c" as string]: p.colorHex }}>
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
                  </div>
                );
              })
            )}
          </div>

          <div className="card glass">
            <h3>
              Bandeja <span>{inbox.data ? `${inbox.data.length} sin asignar` : "—"}</span>
            </h3>
            {!inbox.data || inbox.data.length === 0 ? (
              <div className="empty-mini">La bandeja está vacía.</div>
            ) : (
              inbox.data.slice(0, 5).map((t) => (
                <div key={t.id} className="inbox-row">
                  <span className="inbox-dot" />
                  <span className="inbox-title">{t.title}</span>
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

function NowCard({
  entry,
  loading,
  onChanged,
}: {
  entry: PlanEntryDto | null;
  loading: boolean;
  onChanged: () => void;
}) {
  const [, forceTick] = useState(0);

  // Refresca el anillo de progreso cada 30s sin volver a pedir datos.
  useMemo(() => {
    const id = setInterval(() => forceTick((n) => n + 1), 30000);
    return () => clearInterval(id);
  }, []);

  if (loading) {
    return (
      <section className="now-card">
        <div className="empty-state">
          <p>Cargando el plan de hoy…</p>
        </div>
      </section>
    );
  }

  if (!entry) {
    return (
      <section className="now-card">
        <div className="empty-state">
          <p>No hay ninguna tarea planificada para este momento.</p>
          <button
            className="btn-primary"
            onClick={() => runAction(() => plannerApi.generate(), onChanged)}
          >
            Generar plan del día
          </button>
        </div>
      </section>
    );
  }

  const start = new Date(entry.start);
  const end = new Date(entry.end);
  const now = new Date();
  const totalMs = end.getTime() - start.getTime();
  const elapsedMs = Math.min(Math.max(now.getTime() - start.getTime(), 0), totalMs);
  const remainingMin = Math.max(0, Math.round((end.getTime() - now.getTime()) / 60000));
  const pct = totalMs > 0 ? elapsedMs / totalMs : 0;
  const circumference = 2 * Math.PI * 42;
  const offset = circumference * (1 - pct);
  const isHighPriority = entry.priority === "High" || entry.priority === "Urgent";

  async function startTask() {
    if (!entry?.workItemId) return;
    const item = await workItemsApi.get(entry.workItemId);
    await workItemsApi.update(entry.workItemId, {
      title: item.title,
      description: item.description,
      projectId: item.projectId,
      priority: item.priority,
      estimatedHours: item.estimatedHours,
      estimatedMinutes: item.estimatedMinutes,
      dueDate: item.dueDate,
      status: "InProgress",
    });
  }

  return (
    <section className="now-card">
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 24, flexWrap: "wrap" }}>
        <div style={{ minWidth: 0, flex: "1 1 380px" }}>
          <div className="now-eyebrow">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M13 2L4 14h6l-1 8 9-12h-6l1-8z" />
            </svg>
            Ahora mismo
          </div>
          <h2 className="now-title">{entry.title}</h2>
          {entry.reason && <div className="now-sub">{entry.reason}</div>}
          <div className="chips">
            <span className="chip">
              ⏱ <b>{fmtDuration(Math.round(totalMs / 60000))}</b> de bloque
            </span>
            {isHighPriority && (
              <span className="chip flag">
                🔺 Prioridad <b>{priorityLabel[entry.priority ?? ""] ?? entry.priority}</b>
              </span>
            )}
            {entry.projectName && (
              <span className="chip">
                <b>{entry.projectName}</b>
              </span>
            )}
          </div>
          <div className="actions">
            <button
              className="btn-primary"
              disabled={!entry.workItemId}
              onClick={() => runAction(startTask, onChanged)}
            >
              Empezar ahora
            </button>
            <button className="btn-secondary" onClick={() => runAction(() => plannerApi.replan(), onChanged)}>
              Ver otra sugerencia
            </button>
          </div>
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
                strokeDashoffset={offset}
                style={{ transition: "stroke-dashoffset .4s linear" }}
              />
            </svg>
            <div style={{ position: "absolute", inset: 0, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center" }}>
              <span className="mono" style={{ fontSize: 18, fontWeight: 600 }}>
                {remainingMin}m
              </span>
              <span style={{ fontSize: 12, color: "var(--muted)" }}>restantes</span>
            </div>
          </div>
          <div className="mono" style={{ fontSize: 12, color: "var(--muted)" }}>
            {fmtTime(entry.start)} — {fmtTime(entry.end)}
          </div>
        </div>
      </div>
    </section>
  );
}

function Timeline({ entries, loading }: { entries: PlanEntryDto[]; loading: boolean }) {
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

  return (
    <div className="timeline">
      {entries.map((entry) => {
        const start = new Date(entry.start);
        const end = new Date(entry.end);
        const isCurrent = start <= now && now < end;
        const showDivider = !dividerShown && start > now;
        if (showDivider) dividerShown = true;

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
                    "tl-block" + (isCurrent ? " current" : "") + (entry.priority === "Urgent" ? " flagged" : "")
                  }
                >
                  <div className="tl-main">
                    <div className="tl-name">{entry.title}</div>
                    <div className="tl-meta">
                      {tag && <span className="tag">{tag}</span>}
                      {entry.wasRescheduled && <span className="tag moved">↻ Reordenado</span>}
                    </div>
                  </div>
                  <div className="tl-dur mono">{fmtDuration(Math.round((end.getTime() - start.getTime()) / 60000))}</div>
                </div>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
