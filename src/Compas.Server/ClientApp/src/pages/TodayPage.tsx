import { useMemo, useState } from "react";
import { Topbar } from "../components/Topbar";
import { ConnectionBanner } from "../components/ConnectionBanner";
import { useApiData } from "../hooks/useApiData";
import { plannerApi, projectsApi, workItemsApi } from "../api";
import type { PlanEntryDto } from "../api/types";
import { fmtDateLong, fmtDuration, fmtTime, priorityLabel } from "../utils/format";

export function TodayPage() {
  const plan = useApiData(() => plannerApi.today(), []);
  const projects = useApiData(() => projectsApi.list(), []);
  const inbox = useApiData(() => workItemsApi.inbox(), []);

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

  return (
    <main className="main">
      <Topbar title="Hola" subtitle={fmtDateLong(new Date())} status={status} onRefresh={reloadAll} />

      {status === "error" && <ConnectionBanner message={plan.error || projects.error || inbox.error || ""} />}

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

      <Timeline entries={plan.data?.entries ?? []} loading={plan.status === "loading"} />

      <div className="section-head">
        <h2>Resumen</h2>
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16 }}>
        <div className="card">
          <h3>
            Proyectos <span>{projects.data ? `${projects.data.length} activo(s)` : "—"}</span>
          </h3>
          {!projects.data || projects.data.length === 0 ? (
            <div className="empty-mini">Aún no tienes proyectos.</div>
          ) : (
            projects.data.slice(0, 4).map((p) => (
              <div key={p.id} style={{ marginBottom: 12 }}>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12.5, marginBottom: 6 }}>
                  <span style={{ fontWeight: 500 }}>{p.name}</span>
                  <span className="mono" style={{ color: "var(--muted)", fontSize: 11.5 }}>
                    {Math.round(p.progressPercent)}%
                  </span>
                </div>
                <div className="bar">
                  <i style={{ width: `${Math.round(p.progressPercent)}%` }} />
                </div>
              </div>
            ))
          )}
        </div>

        <div className="card">
          <h3>
            Bandeja <span>{inbox.data ? `${inbox.data.length} sin asignar` : "—"}</span>
          </h3>
          {!inbox.data || inbox.data.length === 0 ? (
            <div className="empty-mini">La bandeja está vacía.</div>
          ) : (
            inbox.data.slice(0, 5).map((t) => (
              <div
                key={t.id}
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  padding: "8px 0",
                  borderBottom: "1px solid var(--line)",
                  fontSize: 12.5,
                }}
              >
                <span>{t.title}</span>
                <span className="mono" style={{ color: "var(--muted-2)", fontSize: 10.5 }}>
                  {t.estimatedMinutes}m
                </span>
              </div>
            ))
          )}
        </div>
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
                📁 <b>{entry.projectName}</b>
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
              <circle cx="48" cy="48" r="42" fill="none" stroke="#2A2F3D" strokeWidth="6" />
              <circle
                cx="48"
                cy="48"
                r="42"
                fill="none"
                stroke="#7C9EFF"
                strokeWidth="6"
                strokeLinecap="round"
                strokeDasharray={circumference}
                strokeDashoffset={offset}
                style={{ transition: "stroke-dashoffset .4s linear" }}
              />
            </svg>
            <div style={{ position: "absolute", inset: 0, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center" }}>
              <span className="mono" style={{ fontSize: 16, fontWeight: 500 }}>
                {remainingMin}m
              </span>
              <span style={{ fontSize: 9.5, color: "var(--muted)" }}>restantes</span>
            </div>
          </div>
          <div className="mono" style={{ fontSize: 11.5, color: "var(--muted)" }}>
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
