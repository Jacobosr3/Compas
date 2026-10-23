import { useState } from "react";
import { useApiData } from "../hooks/useApiData";
import { fmtDuration, totalMinutes } from "../utils/format";
import { projectsApi, workItemsApi } from "../api";

export function RightRail() {
  const projects = useApiData(() => projectsApi.list(), []);
  const inbox = useApiData(() => workItemsApi.inbox(), []);
  const [quickTitle, setQuickTitle] = useState("");

  const now = new Date();
  const year = now.getFullYear();
  const month = now.getMonth();
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const firstDayOfWeek = (new Date(year, month, 1).getDay() + 6) % 7;
  const monthName = now.toLocaleString("es-ES", { month: "long" });

  async function quickAdd() {
    const title = quickTitle.trim();
    if (!title) return;
    const { workItemsApi: api } = await import("../api");
    await api.create({ title, priority: "Medium", estimatedHours: 0, estimatedMinutes: 30 });
    setQuickTitle("");
    inbox.reload();
  }

  return (
    <aside className="rail-right">

      <div className="card">
        <h3>Proyectos <span>{projects.data ? `${projects.data.length} activos` : "—"}</span></h3>
        {!projects.data || projects.data.length === 0 ? (
          <div className="empty-mini">Aún no tienes proyectos.</div>
        ) : (
          projects.data.map((p) => (
            <div key={p.id} className="proj">
              <div className="proj-top">
                <span className="p" style={{ display: "flex", alignItems: "center", gap: 6 }}>
                  <span style={{ width: 8, height: 8, borderRadius: 4, background: p.colorHex, display: "inline-block" }} />
                  {p.name}
                </span>
                <span className="n">{Math.round(p.progressPercent)}%</span>
              </div>
              <div className="bar"><i style={{ width: `${Math.round(p.progressPercent)}%` }} /></div>
            </div>
          ))
        )}
      </div>

      <div className="card">
        <h3>Bandeja <span>{inbox.data ? `${inbox.data.length} sin asignar` : "—"}</span></h3>
        {!inbox.data || inbox.data.length === 0 ? (
          <div className="empty-mini">La bandeja está vacía.</div>
        ) : (
          inbox.data.map((t) => (
            <div key={t.id} className="inbox-item">
              <span>{t.title}</span>
              <span className="est mono">{fmtDuration(totalMinutes(t))}</span>
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

      <div className="card">
        <h3 style={{ textTransform: "capitalize" }}>{monthName}</h3>
        <div className="mini-cal">
          {["L","M","X","J","V","S","D"].map(d => (
            <span key={d} className="d" style={{ color: "var(--muted-2)", fontWeight: 600 }}>{d}</span>
          ))}
          {Array.from({ length: firstDayOfWeek }).map((_, i) => (
            <span key={`pad-${i}`} className="d pad" />
          ))}
          {Array.from({ length: daysInMonth }).map((_, i) => {
            const day = i + 1;
            const isToday = day === now.getDate();
            return <span key={day} className={`d${isToday ? " today" : ""}`}>{day}</span>;
          })}
        </div>
      </div>

    </aside>
  );
}