import { useEffect, useState } from "react";
import { Topbar } from "../components/Topbar";
import { ConnectionBanner } from "../components/ConnectionBanner";
import { ConfirmModal } from "../components/ConfirmModal";
import { useApiData } from "../hooks/useApiData";
import { profileApi } from "../api";
import type {
  BodyMeasurementDto,
  ExerciseRuleDto,
  LiftGoalDto,
  NutritionPhase,
  TrainingSlotDto,
  UserProfileDto,
} from "../api/types";
import { toDateInputValue } from "../utils/format";
import { errorText } from "../utils/errors";

const PHASES: { value: NutritionPhase; label: string; hint: string }[] = [
  { value: "Bulk", label: "Volumen", hint: "Superávit calórico" },
  { value: "Maintenance", label: "Mantenimiento", hint: "Calorías de mantenimiento" },
  { value: "Cut", label: "Definición", hint: "Déficit calórico" },
];

const ACTIVITY: { value: string; label: string }[] = [
  { value: "sedentary", label: "Sedentario" },
  { value: "light", label: "Ligera (1-3 días/sem)" },
  { value: "moderate", label: "Moderada (3-5 días/sem)" },
  { value: "active", label: "Alta (6-7 días/sem)" },
  { value: "very_active", label: "Muy alta (doble sesión o trabajo físico)" },
];

/** Texto de un campo numérico → número, o null si está vacío o no es válido. */
function num(text: string): number | null {
  const n = Number(text.replace(",", "."));
  return text.trim() === "" || !Number.isFinite(n) ? null : n;
}

const str = (n: number | null | undefined) => (n == null ? "" : String(n));

function fmtDay(date: string): string {
  return new Date(date + "T00:00:00").toLocaleDateString("es-ES", { day: "numeric", month: "short", year: "numeric" });
}

export function ProfilePage() {
  const overview = useApiData(() => profileApi.get(), []);
  const [error, setError] = useState<string | null>(null);

  return (
    <main className="main">
      <Topbar title="Perfil" subtitle="Objetivos y medidas para programar tus pesos" status={overview.status} onRefresh={overview.reload} />
      {overview.status === "error" && <ConnectionBanner message={overview.error || ""} />}
      {error && <p className="connector-error">{error}</p>}
      {overview.status === "loading" && (
        <div className="empty-state">
          <p>Cargando perfil…</p>
        </div>
      )}
      {overview.data && (
        <div className="profile-grid">
          <GoalCard
            profile={overview.data.profile}
            latest={overview.data.measurements[0] ?? null}
            onSaved={overview.reload}
            onError={setError}
          />
          <MeasurementsCard measurements={overview.data.measurements} onChanged={overview.reload} onError={setError} />
          <LiftGoalsCard goals={overview.data.liftGoals} onChanged={overview.reload} onError={setError} />
          <SlotsCard slots={overview.data.trainingSlots} onChanged={overview.reload} onError={setError} />
          <RulesCard rules={overview.data.exerciseRules} onChanged={overview.reload} onError={setError} />
        </div>
      )}
    </main>
  );
}

// ---------------- Fase, objetivo de peso y datos personales ----------------

function GoalCard({
  profile,
  latest,
  onSaved,
  onError,
}: {
  profile: UserProfileDto;
  latest: BodyMeasurementDto | null;
  onSaved: () => void;
  onError: (message: string | null) => void;
}) {
  const [phase, setPhase] = useState<NutritionPhase>(profile.phase);
  const [target, setTarget] = useState(str(profile.targetWeightKg));
  const [targetDate, setTargetDate] = useState(profile.targetDate ?? "");
  const [height, setHeight] = useState(str(profile.heightCm));
  const [birth, setBirth] = useState(profile.birthDate ?? "");
  const [sex, setSex] = useState(profile.sex ?? "");
  const [activity, setActivity] = useState(profile.activityLevel ?? "");
  const [notes, setNotes] = useState(profile.notes ?? "");
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);

  // Si el perfil cambia desde fuera (otra pestaña o la automatización) y se recarga, el formulario lo refleja.
  useEffect(() => {
    setPhase(profile.phase);
    setTarget(str(profile.targetWeightKg));
    setTargetDate(profile.targetDate ?? "");
    setHeight(str(profile.heightCm));
    setBirth(profile.birthDate ?? "");
    setSex(profile.sex ?? "");
    setActivity(profile.activityLevel ?? "");
    setNotes(profile.notes ?? "");
    // Clave por contenido: recargar sin cambios (p. ej. tras registrar una medida) no pisa lo que se esté editando.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [JSON.stringify(profile)]);

  async function save() {
    setBusy(true);
    setSaved(false);
    onError(null);
    try {
      await profileApi.update({
        phase,
        targetWeightKg: num(target),
        targetDate: targetDate || null,
        heightCm: num(height),
        birthDate: birth || null,
        sex: sex || null,
        activityLevel: activity || null,
        notes,
      });
      setSaved(true);
      onSaved();
    } catch (err) {
      onError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  const goal = num(target);
  const diff = latest?.weightKg != null && goal != null ? Math.round((goal - latest.weightKg) * 10) / 10 : null;

  return (
    <section className="card">
      <h3>Fase y objetivo</h3>
      <div className="segmented" role="radiogroup" aria-label="Fase actual">
        {PHASES.map((p) => (
          <button
            key={p.value}
            role="radio"
            aria-checked={p.value === phase}
            title={p.hint}
            className={p.value === phase ? "btn-secondary" : "btn-ghost"}
            onClick={() => setPhase(p.value)}
          >
            {p.label}
          </button>
        ))}
      </div>
      {profile.phaseStartDate && <p className="connector-help">En esta fase desde el {fmtDay(profile.phaseStartDate)}.</p>}

      <div className="form-grid">
        <div className="field">
          <label htmlFor="pf-target">Peso objetivo (kg)</label>
          <input id="pf-target" inputMode="decimal" value={target} onChange={(e) => setTarget(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="pf-tdate">Fecha objetivo</label>
          <input id="pf-tdate" type="date" value={targetDate} onChange={(e) => setTargetDate(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="pf-height">Estatura (cm)</label>
          <input id="pf-height" inputMode="decimal" value={height} onChange={(e) => setHeight(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="pf-birth">Fecha de nacimiento</label>
          <input id="pf-birth" type="date" value={birth} onChange={(e) => setBirth(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="pf-sex">Sexo</label>
          <select id="pf-sex" value={sex} onChange={(e) => setSex(e.target.value)}>
            <option value="">Sin indicar</option>
            <option value="male">Hombre</option>
            <option value="female">Mujer</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="pf-act">Actividad diaria</label>
          <select id="pf-act" value={activity} onChange={(e) => setActivity(e.target.value)}>
            <option value="">Sin indicar</option>
            {ACTIVITY.map((a) => (
              <option key={a.value} value={a.value}>
                {a.label}
              </option>
            ))}
          </select>
        </div>
      </div>
      <div className="field">
        <label htmlFor="pf-notes">Notas (lesiones, preferencias, material disponible…)</label>
        <textarea id="pf-notes" rows={3} maxLength={2000} value={notes} onChange={(e) => setNotes(e.target.value)} />
      </div>

      {diff != null && latest?.weightKg != null && (
        <p className="connector-help">
          Último peso: {latest.weightKg} kg ({fmtDay(latest.date)}).{" "}
          {diff === 0 ? "Ya estás en tu peso objetivo." : `Te faltan ${diff > 0 ? "+" : ""}${diff} kg para el objetivo.`}
        </p>
      )}

      <div className="actions-inline">
        <button className="btn-primary" disabled={busy} onClick={save}>
          {busy ? "Guardando…" : "Guardar"}
        </button>
        {saved && <span className="connector-help">Guardado.</span>}
      </div>
    </section>
  );
}

// ---------------- Medidas corporales ----------------

function MeasurementsCard({
  measurements,
  onChanged,
  onError,
}: {
  measurements: BodyMeasurementDto[];
  onChanged: () => void;
  onError: (message: string | null) => void;
}) {
  const empty = { weight: "", fat: "", waist: "", chest: "", hip: "", arm: "", thigh: "" };
  const [date, setDate] = useState(toDateInputValue(new Date()));
  const [f, setF] = useState(empty);
  const [busy, setBusy] = useState(false);
  const [deleting, setDeleting] = useState<BodyMeasurementDto | null>(null);

  const fields: { key: keyof typeof empty; label: string }[] = [
    { key: "weight", label: "Peso (kg)" },
    { key: "fat", label: "Grasa (%)" },
    { key: "waist", label: "Cintura (cm)" },
    { key: "chest", label: "Pecho (cm)" },
    { key: "hip", label: "Cadera (cm)" },
    { key: "arm", label: "Brazo (cm)" },
    { key: "thigh", label: "Muslo (cm)" },
  ];

  async function add() {
    setBusy(true);
    onError(null);
    try {
      await profileApi.logMeasurement({
        date,
        weightKg: num(f.weight),
        bodyFatPct: num(f.fat),
        waistCm: num(f.waist),
        chestCm: num(f.chest),
        hipCm: num(f.hip),
        armCm: num(f.arm),
        thighCm: num(f.thigh),
      });
      setF(empty);
      onChanged();
    } catch (err) {
      onError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  async function remove(m: BodyMeasurementDto) {
    setDeleting(null);
    try {
      await profileApi.removeMeasurement(m.id);
      onChanged();
    } catch (err) {
      onError(errorText(err));
    }
  }

  const cell = (n: number | null) => (n == null ? "—" : n);

  return (
    <section className="card">
      <h3>Peso y medidas</h3>
      <div className="form-grid">
        <div className="field">
          <label htmlFor="ms-date">Fecha</label>
          <input id="ms-date" type="date" value={date} onChange={(e) => setDate(e.target.value)} />
        </div>
        {fields.map((x) => (
          <div className="field" key={x.key}>
            <label htmlFor={`ms-${x.key}`}>{x.label}</label>
            <input id={`ms-${x.key}`} inputMode="decimal" value={f[x.key]} onChange={(e) => setF({ ...f, [x.key]: e.target.value })} />
          </div>
        ))}
      </div>
      <button className="btn-secondary" disabled={busy || !date} onClick={add}>
        {busy ? "Guardando…" : "Registrar"}
      </button>

      {measurements.length === 0 ? (
        <p className="connector-help">Todavía no hay medidas. Registra al menos tu peso actual.</p>
      ) : (
        <div className="table-scroll">
          <table className="measure-table">
            <thead>
              <tr>
                <th>Fecha</th>
                <th>Peso</th>
                <th>Grasa</th>
                <th>Cintura</th>
                <th>Pecho</th>
                <th>Cadera</th>
                <th>Brazo</th>
                <th>Muslo</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {measurements.map((m) => (
                <tr key={m.id}>
                  <td>{fmtDay(m.date)}</td>
                  <td>{cell(m.weightKg)}</td>
                  <td>{cell(m.bodyFatPct)}</td>
                  <td>{cell(m.waistCm)}</td>
                  <td>{cell(m.chestCm)}</td>
                  <td>{cell(m.hipCm)}</td>
                  <td>{cell(m.armCm)}</td>
                  <td>{cell(m.thighCm)}</td>
                  <td>
                    <button className="btn-ghost" aria-label={`Borrar el registro del ${fmtDay(m.date)}`} onClick={() => setDeleting(m)}>
                      ✕
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {deleting && (
        <ConfirmModal
          title="Borrar registro"
          message={`Se borrará el registro del ${fmtDay(deleting.date)}.`}
          onConfirm={() => remove(deleting)}
          onCancel={() => setDeleting(null)}
        />
      )}
    </section>
  );
}

// ---------------- Objetivos de fuerza ----------------

function LiftGoalsCard({
  goals,
  onChanged,
  onError,
}: {
  goals: LiftGoalDto[];
  onChanged: () => void;
  onError: (message: string | null) => void;
}) {
  const [name, setName] = useState("");
  const [kg, setKg] = useState("");
  const [reps, setReps] = useState("1");
  const [date, setDate] = useState("");
  const [busy, setBusy] = useState(false);
  const [deleting, setDeleting] = useState<LiftGoalDto | null>(null);

  async function add() {
    const weight = num(kg);
    if (!name.trim() || weight == null) {
      onError("Indica el ejercicio y el peso objetivo.");
      return;
    }
    setBusy(true);
    onError(null);
    try {
      await profileApi.upsertLiftGoal({
        exerciseName: name.trim(),
        targetWeightKg: weight,
        targetReps: num(reps) ?? 1,
        targetDate: date || null,
      });
      setName("");
      setKg("");
      setReps("1");
      setDate("");
      onChanged();
    } catch (err) {
      onError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  async function toggle(g: LiftGoalDto) {
    try {
      await profileApi.upsertLiftGoal({ exerciseName: g.exerciseName, targetWeightKg: g.targetWeightKg, achieved: !g.achieved });
      onChanged();
    } catch (err) {
      onError(errorText(err));
    }
  }

  async function remove(g: LiftGoalDto) {
    setDeleting(null);
    try {
      await profileApi.removeLiftGoal(g.id);
      onChanged();
    } catch (err) {
      onError(errorText(err));
    }
  }

  return (
    <section className="card">
      <h3>Objetivos de fuerza</h3>
      <p className="connector-help">Usa el nombre del ejercicio igual que en Hevy. Volver a guardar el mismo ejercicio actualiza su objetivo.</p>
      <div className="form-grid">
        <div className="field">
          <label htmlFor="lg-name">Ejercicio</label>
          <input id="lg-name" maxLength={200} value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="lg-kg">Peso (kg)</label>
          <input id="lg-kg" inputMode="decimal" value={kg} onChange={(e) => setKg(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="lg-reps">Repeticiones</label>
          <input id="lg-reps" inputMode="numeric" value={reps} onChange={(e) => setReps(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="lg-date">Fecha límite</label>
          <input id="lg-date" type="date" value={date} onChange={(e) => setDate(e.target.value)} />
        </div>
      </div>
      <button className="btn-secondary" disabled={busy} onClick={add}>
        {busy ? "Guardando…" : "Añadir objetivo"}
      </button>

      {goals.length === 0 ? (
        <p className="connector-help">Todavía no hay objetivos de fuerza.</p>
      ) : (
        <ul className="connector-list goal-list">
          {goals.map((g) => (
            <li key={g.id} className={g.achieved ? "done" : ""}>
              <span>
                <b>{g.exerciseName}</b> — {g.targetWeightKg} kg × {g.targetReps}
                {g.targetDate && <small>Antes del {fmtDay(g.targetDate)}</small>}
              </span>
              <span className="actions-inline">
                <label className="workout-check">
                  <input type="checkbox" checked={g.achieved} onChange={() => toggle(g)} />
                  Conseguido
                </label>
                <button className="btn-ghost" aria-label={`Borrar el objetivo de ${g.exerciseName}`} onClick={() => setDeleting(g)}>
                  ✕
                </button>
              </span>
            </li>
          ))}
        </ul>
      )}

      {deleting && (
        <ConfirmModal
          title="Borrar objetivo"
          message={`Se borrará el objetivo de ${deleting.exerciseName}.`}
          onConfirm={() => remove(deleting)}
          onCancel={() => setDeleting(null)}
        />
      )}
    </section>
  );
}

// ---------------- Disponibilidad semanal ----------------

const DAYS = ["Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo"];

function SlotsCard({
  slots,
  onChanged,
  onError,
}: {
  slots: TrainingSlotDto[];
  onChanged: () => void;
  onError: (message: string | null) => void;
}) {
  const [day, setDay] = useState(1);
  const [start, setStart] = useState("18:00");
  const [duration, setDuration] = useState("75");
  const [label, setLabel] = useState("");
  const [busy, setBusy] = useState(false);

  /** Los huecos se guardan como lista completa: cada cambio envía la lista nueva entera. */
  async function save(next: { dayOfWeek: number; startTime: string; durationMinutes: number; label: string | null }[]) {
    setBusy(true);
    onError(null);
    try {
      await profileApi.setTrainingSlots(next);
      onChanged();
    } catch (err) {
      onError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  const current = slots.map((s) => ({ dayOfWeek: s.dayOfWeek, startTime: s.startTime, durationMinutes: s.durationMinutes, label: s.label }));

  async function add() {
    if (!start) {
      onError("Indica la hora de inicio.");
      return;
    }
    await save([...current, { dayOfWeek: day, startTime: start, durationMinutes: num(duration) ?? 75, label: label.trim() || null }]);
    setLabel("");
  }

  return (
    <section className="card">
      <h3>Cuándo puedo entrenar</h3>
      <p className="connector-help">
        La automatización coloca tus entrenos dentro de estos huecos, evitando lo que ya tengas en el calendario, y los crea también en
        Google Calendar. Las horas son las de tu zona (Europe/Madrid).
      </p>
      <div className="form-grid">
        <div className="field">
          <label htmlFor="sl-day">Día</label>
          <select id="sl-day" value={day} onChange={(e) => setDay(Number(e.target.value))}>
            {DAYS.map((d, i) => (
              <option key={d} value={i + 1}>
                {d}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="sl-start">Hora de inicio</label>
          <input id="sl-start" type="time" value={start} onChange={(e) => setStart(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="sl-dur">Minutos disponibles</label>
          <input id="sl-dur" inputMode="numeric" value={duration} onChange={(e) => setDuration(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="sl-label">Sesión preferida (opcional)</label>
          <input id="sl-label" maxLength={50} placeholder="Push, Pull, Leg…" value={label} onChange={(e) => setLabel(e.target.value)} />
        </div>
      </div>
      <button className="btn-secondary" disabled={busy} onClick={add}>
        {busy ? "Guardando…" : "Añadir hueco"}
      </button>

      {slots.length === 0 ? (
        <p className="connector-help">Todavía no hay huecos. Añade al menos los días en los que sueles entrenar.</p>
      ) : (
        <ul className="connector-list goal-list">
          {slots.map((s) => (
            <li key={s.id}>
              <span>
                <b>{DAYS[s.dayOfWeek - 1]}</b> a las {s.startTime} · hasta {s.durationMinutes} min
                {s.label && <small>Preferido: {s.label}</small>}
              </span>
              <button
                className="btn-ghost"
                disabled={busy}
                aria-label={`Quitar el hueco del ${DAYS[s.dayOfWeek - 1]} a las ${s.startTime}`}
                onClick={() => save(current.filter((_, i) => slots[i].id !== s.id))}
              >
                ✕
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

// ---------------- Reglas de progresión por ejercicio ----------------

function RulesCard({
  rules,
  onChanged,
  onError,
}: {
  rules: ExerciseRuleDto[];
  onChanged: () => void;
  onError: (message: string | null) => void;
}) {
  const [name, setName] = useState("");
  const [repMin, setRepMin] = useState("8");
  const [repMax, setRepMax] = useState("12");
  const [inc, setInc] = useState("2.5");
  const [busy, setBusy] = useState(false);
  const [deleting, setDeleting] = useState<ExerciseRuleDto | null>(null);

  async function add() {
    const lo = num(repMin);
    const hi = num(repMax);
    const step = num(inc);
    if (!name.trim() || lo == null || hi == null || step == null) {
      onError("Indica el ejercicio, el rango de repeticiones y el salto de peso.");
      return;
    }
    setBusy(true);
    onError(null);
    try {
      await profileApi.upsertExerciseRules([{ exerciseName: name.trim(), repMin: lo, repMax: hi, incrementKg: step }]);
      setName("");
      onChanged();
    } catch (err) {
      onError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  async function remove(r: ExerciseRuleDto) {
    setDeleting(null);
    try {
      await profileApi.removeExerciseRule(r.id);
      onChanged();
    } catch (err) {
      onError(errorText(err));
    }
  }

  return (
    <section className="card">
      <h3>Reglas de progresión</h3>
      <p className="connector-help">
        Al completar las repeticiones máximas en todas las series se sube el salto indicado; por debajo del mínimo se mantiene o se baja el peso.
        Claude puede crearlas a partir de tu historial de Hevy; aquí las revisas y las ajustas.
      </p>
      <div className="form-grid">
        <div className="field">
          <label htmlFor="rl-name">Ejercicio</label>
          <input id="rl-name" maxLength={200} value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="rl-min">Reps mín.</label>
          <input id="rl-min" inputMode="numeric" value={repMin} onChange={(e) => setRepMin(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="rl-max">Reps máx.</label>
          <input id="rl-max" inputMode="numeric" value={repMax} onChange={(e) => setRepMax(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="rl-inc">Salto (kg)</label>
          <input id="rl-inc" inputMode="decimal" value={inc} onChange={(e) => setInc(e.target.value)} />
        </div>
      </div>
      <button className="btn-secondary" disabled={busy} onClick={add}>
        {busy ? "Guardando…" : "Guardar regla"}
      </button>

      {rules.length === 0 ? (
        <p className="connector-help">Todavía no hay reglas.</p>
      ) : (
        <div className="table-scroll">
          <table className="measure-table">
            <thead>
              <tr>
                <th>Ejercicio</th>
                <th>Reps</th>
                <th>Salto</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rules.map((r) => (
                <tr key={r.id}>
                  <td>{r.exerciseName}</td>
                  <td>
                    {r.repMin}-{r.repMax}
                  </td>
                  <td>{r.incrementKg} kg</td>
                  <td>
                    <button className="btn-ghost" aria-label={`Borrar la regla de ${r.exerciseName}`} onClick={() => setDeleting(r)}>
                      ✕
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {deleting && (
        <ConfirmModal
          title="Borrar regla"
          message={`Se borrará la regla de ${deleting.exerciseName}.`}
          onConfirm={() => remove(deleting)}
          onCancel={() => setDeleting(null)}
        />
      )}
    </section>
  );
}
