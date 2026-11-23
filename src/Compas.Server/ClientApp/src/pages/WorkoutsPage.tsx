import { useState } from "react";
import { Topbar } from "../components/Topbar";
import { ConnectionBanner } from "../components/ConnectionBanner";
import { ConnectorCard } from "../components/ConnectorCard";
import { useApiData } from "../hooks/useApiData";
import { workoutsApi } from "../api";
import type { WorkoutSessionDto, WorkoutSetDto } from "../api/types";
import { toDateInputValue } from "../utils/format";
import { errorText } from "../utils/errors";

const SET_LABEL: Record<string, string> = { warmup: "calent.", dropset: "dropset", failure: "al fallo" };

function fmtSet(s: WorkoutSetDto): string {
  const load = s.weightKg != null ? `${s.weightKg} kg` : "peso corporal";
  const reps = s.reps != null ? ` × ${s.reps}` : "";
  const tag = SET_LABEL[s.type];
  return `${load}${reps}${tag ? ` (${tag})` : ""}`;
}

function fmtDay(date: string): string {
  const d = new Date(date + "T00:00:00");
  return d.toLocaleDateString("es-ES", { weekday: "long", day: "numeric", month: "long" });
}

export function WorkoutsPage() {
  // Desde la semana pasada hacia delante: lo próximo es lo que se consulta en el gimnasio.
  const from = (() => {
    const d = new Date();
    d.setDate(d.getDate() - 7);
    return toDateInputValue(d);
  })();
  const today = toDateInputValue(new Date());
  const workouts = useApiData(() => workoutsApi.list(from), [from]);
  const [error, setError] = useState<string | null>(null);

  async function toggle(w: WorkoutSessionDto) {
    setError(null);
    try {
      await workoutsApi.setCompleted(w.id, !w.completed);
      workouts.reload();
    } catch (err) {
      setError(errorText(err));
    }
  }

  return (
    <main className="main">
      <Topbar title="Entrenos" subtitle="Sesiones y pesos programados" status={workouts.status} onRefresh={workouts.reload} />

      {workouts.status === "error" && <ConnectionBanner message={workouts.error || ""} />}
      {error && <p className="connector-error">{error}</p>}

      {workouts.status === "loading" && (
        <div className="empty-state">
          <p>Cargando entrenos…</p>
        </div>
      )}

      {workouts.status === "ok" && workouts.data?.length === 0 && (
        <div className="empty-state">
          <p>Todavía no hay entrenos programados. Cuando la automatización de Claude los escriba, aparecerán aquí.</p>
        </div>
      )}

      <div className="workout-list">
        {workouts.data?.map((w) => (
          <article key={w.id} className={"card workout-card" + (w.completed ? " done" : "")}>
            <header>
              <div>
                <h3>{w.title}</h3>
                <small>
                  {fmtDay(w.date)}
                  {w.date === today ? " · hoy" : ""}
                </small>
              </div>
              <label className="workout-check">
                <input type="checkbox" checked={w.completed} onChange={() => toggle(w)} />
                Hecho
              </label>
            </header>
            {w.notes && <p className="connector-help">{w.notes}</p>}
            {w.exercises.map((ex, i) => (
              <section key={i} className="workout-ex">
                <b>{ex.name}</b>
                {ex.notes && <small> — {ex.notes}</small>}
                <ol>
                  {ex.sets.map((s, j) => (
                    <li key={j}>{fmtSet(s)}</li>
                  ))}
                </ol>
              </section>
            ))}
          </article>
        ))}
      </div>

      <ConnectorCard />
    </main>
  );
}
