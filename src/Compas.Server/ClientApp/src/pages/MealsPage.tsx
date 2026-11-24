import { useRef, useState } from "react";
import { Topbar } from "../components/Topbar";
import { ProfileTabs } from "../components/ProfileTabs";
import { ConnectionBanner } from "../components/ConnectionBanner";
import { ConfirmModal } from "../components/ConfirmModal";
import { useApiData } from "../hooks/useApiData";
import { mealsApi } from "../api";
import type { MealDto, MealItemDto, MealType, NutritionSummaryDto } from "../api/types";
import { toDateInputValue } from "../utils/format";
import { errorText } from "../utils/errors";

const MEAL_TYPES: { value: MealType; label: string }[] = [
  { value: "breakfast", label: "Desayuno" },
  { value: "lunch", label: "Comida" },
  { value: "snack", label: "Merienda / snack" },
  { value: "dinner", label: "Cena" },
  { value: "other", label: "Otra" },
];
const TYPE_LABEL = Object.fromEntries(MEAL_TYPES.map((t) => [t.value, t.label])) as Record<string, string>;
const STATUS_LABEL = { Pending: "Esperando a Claude", Proposed: "Por revisar", Confirmed: "Confirmada" } as const;

const MAX_SIDE = 1280;

function fmtDay(date: string): string {
  return new Date(date + "T00:00:00").toLocaleDateString("es-ES", { weekday: "long", day: "numeric", month: "long" });
}

const n = (v: number | null | undefined) => (v == null ? "" : String(Math.round(v * 10) / 10));

/** Reduce la foto en el navegador (lado máximo 1280 px, JPEG) para que pese poco al subirla y al analizarla. */
async function shrinkImage(file: File): Promise<{ base64: string; contentType: string; preview: string }> {
  let bitmap: ImageBitmap;
  try {
    bitmap = await createImageBitmap(file);
  } catch {
    throw new Error("No se pudo leer la imagen. Usa una foto JPEG, PNG o WebP.");
  }
  const scale = Math.min(1, MAX_SIDE / Math.max(bitmap.width, bitmap.height));
  const canvas = document.createElement("canvas");
  canvas.width = Math.max(1, Math.round(bitmap.width * scale));
  canvas.height = Math.max(1, Math.round(bitmap.height * scale));
  const ctx = canvas.getContext("2d");
  if (!ctx) throw new Error("El navegador no puede procesar la imagen.");
  ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  bitmap.close();
  const dataUrl = canvas.toDataURL("image/jpeg", 0.8);
  return { base64: dataUrl.slice(dataUrl.indexOf(",") + 1), contentType: "image/jpeg", preview: dataUrl };
}

export function MealsPage() {
  const today = toDateInputValue(new Date());
  const from = (() => {
    const d = new Date();
    d.setDate(d.getDate() - 14);
    return toDateInputValue(d);
  })();
  const meals = useApiData(() => mealsApi.list(from), [from]);
  const summary = useApiData(() => mealsApi.summary(), []);
  const [error, setError] = useState<string | null>(null);

  function reload() {
    meals.reload();
    summary.reload();
  }

  // Agrupa por día manteniendo el orden (la API devuelve lo más reciente primero).
  const days: { date: string; meals: MealDto[] }[] = [];
  for (const m of meals.data ?? []) {
    const last = days[days.length - 1];
    if (last && last.date === m.date) last.meals.push(m);
    else days.push({ date: m.date, meals: [m] });
  }

  return (
    <main className="main">
      <Topbar title="Comidas" subtitle="Foto, estimación de Claude y tu confirmación" status={meals.status} onRefresh={reload} />
      <ProfileTabs />

      {meals.status === "error" && <ConnectionBanner message={meals.error || ""} />}
      {error && <p className="connector-error">{error}</p>}

      <div className="meal-grid">
        <UploadCard defaultDate={today} onCreated={reload} onError={setError} />
        <SummaryCard summary={summary.data} status={summary.status} />
      </div>

      {meals.status === "loading" && (
        <div className="empty-state">
          <p>Cargando comidas…</p>
        </div>
      )}

      {meals.status === "ok" && days.length === 0 && (
        <div className="empty-state">
          <p>Todavía no hay comidas. Sube una foto y Claude estimará los alimentos y las calorías.</p>
        </div>
      )}

      <div className="meal-list">
        {days.map((day) => {
          const confirmed = day.meals.filter((m) => m.status === "Confirmed");
          const total = confirmed.reduce((acc, m) => acc + m.totalCalories, 0);
          return (
            <section key={day.date} className="meal-list">
              <h2 className="meal-day">
                {fmtDay(day.date)}
                {day.date === today ? " · hoy" : ""}
                {confirmed.length > 0 ? ` · ${Math.round(total)} kcal confirmadas` : ""}
              </h2>
              {day.meals.map((m) => (
                <MealCard key={m.id} meal={m} onChanged={reload} onError={setError} />
              ))}
            </section>
          );
        })}
      </div>
    </main>
  );
}

function UploadCard({
  defaultDate,
  onCreated,
  onError,
}: {
  defaultDate: string;
  onCreated: () => void;
  onError: (msg: string | null) => void;
}) {
  const fileRef = useRef<HTMLInputElement>(null);
  const [photo, setPhoto] = useState<{ base64: string; contentType: string; preview: string } | null>(null);
  const [date, setDate] = useState(defaultDate);
  const [time, setTime] = useState("");
  const [mealType, setMealType] = useState<MealType | "">("");
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);

  async function pick(file: File | undefined) {
    onError(null);
    if (!file) return;
    try {
      setPhoto(await shrinkImage(file));
    } catch (err) {
      setPhoto(null);
      onError(errorText(err));
    }
  }

  async function submit() {
    onError(null);
    setBusy(true);
    try {
      await mealsApi.create({
        date,
        time: time || null,
        mealType: mealType || null,
        note: note.trim() || null,
        photoBase64: photo?.base64 ?? null,
        photoContentType: photo?.contentType ?? null,
      });
      setPhoto(null);
      setNote("");
      setTime("");
      setMealType("");
      if (fileRef.current) fileRef.current.value = "";
      onCreated();
    } catch (err) {
      onError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="card">
      <h3>Añadir comida</h3>
      <p className="connector-help">
        Sube una foto del plato. La automatización de Claude la analiza y te propone los alimentos y las calorías; tú los revisas y confirmas.
      </p>
      <div className="form-grid">
        <div className="field">
          <label htmlFor="meal-photo">Foto</label>
          <input id="meal-photo" ref={fileRef} type="file" accept="image/*" capture="environment" onChange={(e) => pick(e.target.files?.[0])} />
        </div>
        <div className="field">
          <label htmlFor="meal-date">Día</label>
          <input id="meal-date" type="date" value={date} max={defaultDate} onChange={(e) => setDate(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="meal-time">Hora (opcional)</label>
          <input id="meal-time" type="time" value={time} onChange={(e) => setTime(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="meal-type">Tipo (opcional)</label>
          <select id="meal-type" value={mealType} onChange={(e) => setMealType(e.target.value as MealType | "")}>
            <option value="">Que lo deduzca Claude</option>
            {MEAL_TYPES.map((t) => (
              <option key={t.value} value={t.value}>
                {t.label}
              </option>
            ))}
          </select>
        </div>
      </div>
      <div className="field">
        <label htmlFor="meal-note">Nota para Claude (opcional)</label>
        <textarea
          id="meal-note"
          rows={2}
          maxLength={500}
          value={note}
          placeholder="Ej.: arroz de 80 g en crudo, con dos cucharadas de aceite"
          onChange={(e) => setNote(e.target.value)}
        />
      </div>
      {photo && <img className="meal-preview" src={photo.preview} alt="Vista previa de la comida" />}
      <div className="actions-inline" style={{ marginTop: 12 }}>
        <button className="btn-primary" onClick={submit} disabled={busy || !date || (!photo && !note.trim())}>
          {busy ? "Subiendo…" : "Guardar comida"}
        </button>
      </div>
    </section>
  );
}

function SummaryCard({ summary, status }: { summary: NutritionSummaryDto | null | undefined; status: string }) {
  if (status === "loading") return <section className="card"><p className="connector-help">Cargando resumen…</p></section>;
  if (!summary) return null;
  const t = summary.targets;
  return (
    <section className="card">
      <h3>Últimos 7 días</h3>
      <div className="meal-stats">
        <div>
          <b>{Math.round(summary.averageCalories)}</b>
          <small>kcal/día (media, confirmadas)</small>
        </div>
        <div>
          <b>{t.targetCalories != null ? Math.round(t.targetCalories) : "—"}</b>
          <small>objetivo ({t.phase})</small>
        </div>
        <div>
          <b>{t.maintenanceCalories != null ? Math.round(t.maintenanceCalories) : "—"}</b>
          <small>mantenimiento estimado</small>
        </div>
      </div>
      {t.note && <p className="connector-help">{t.note}</p>}
      {(summary.pendingMeals > 0 || summary.proposedMeals > 0) && (
        <p className="connector-help">
          {summary.pendingMeals > 0 && `${summary.pendingMeals} esperando a Claude. `}
          {summary.proposedMeals > 0 && `${summary.proposedMeals} por revisar: solo cuentan las confirmadas.`}
        </p>
      )}
      {summary.days.length > 0 && (
        <div className="table-scroll">
          <table className="measure-table">
            <thead>
              <tr>
                <th>Día</th>
                <th>kcal</th>
                <th>P</th>
                <th>H</th>
                <th>G</th>
              </tr>
            </thead>
            <tbody>
              {summary.days.map((d) => (
                <tr key={d.date}>
                  <td>{d.date.slice(5)}</td>
                  <td>{d.calories}</td>
                  <td>{d.proteinG}</td>
                  <td>{d.carbsG}</td>
                  <td>{d.fatG}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

type EditItem = { name: string; quantity: string; calories: string; proteinG: string; carbsG: string; fatG: string };

const toEdit = (i: MealItemDto): EditItem => ({
  name: i.name,
  quantity: i.quantity ?? "",
  calories: n(i.calories),
  proteinG: n(i.proteinG),
  carbsG: n(i.carbsG),
  fatG: n(i.fatG),
});

const num = (s: string): number | null => (s.trim() === "" || Number.isNaN(Number(s)) ? null : Number(s));

function MealCard({ meal, onChanged, onError }: { meal: MealDto; onChanged: () => void; onError: (msg: string | null) => void }) {
  const [editing, setEditing] = useState(false);
  const [items, setItems] = useState<EditItem[]>([]);
  const [busy, setBusy] = useState(false);
  const [deleting, setDeleting] = useState(false);

  function startEdit() {
    setItems(meal.items.length > 0 ? meal.items.map(toEdit) : [{ name: "", quantity: "", calories: "", proteinG: "", carbsG: "", fatG: "" }]);
    setEditing(true);
  }

  function setItem(i: number, patch: Partial<EditItem>) {
    setItems((prev) => prev.map((it, idx) => (idx === i ? { ...it, ...patch } : it)));
  }

  async function run(action: () => Promise<unknown>) {
    onError(null);
    setBusy(true);
    try {
      await action();
      setEditing(false);
      onChanged();
    } catch (err) {
      onError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  function confirm() {
    const payload: MealItemDto[] = items
      .filter((it) => it.name.trim() !== "")
      .map((it) => ({
        name: it.name.trim(),
        quantity: it.quantity.trim() || null,
        calories: num(it.calories) ?? 0,
        proteinG: num(it.proteinG),
        carbsG: num(it.carbsG),
        fatG: num(it.fatG),
      }));
    return run(() => mealsApi.confirm(meal.id, { items: payload }));
  }

  const title = [meal.mealType ? TYPE_LABEL[meal.mealType] : "Comida", meal.time].filter(Boolean).join(" · ");
  const canEdit = meal.status !== "Pending";

  return (
    <article className="card meal-card">
      {meal.hasPhoto ? <img src={mealsApi.photoUrl(meal.id)} alt={`Foto: ${title}`} loading="lazy" /> : <div className="meal-preview" aria-hidden />}
      <div>
        <header>
          <h3>{title}</h3>
          <span className={"meal-badge " + meal.status.toLowerCase()}>{STATUS_LABEL[meal.status]}</span>
        </header>
        {meal.note && <small>Tu nota: {meal.note}</small>}

        {meal.status === "Pending" && <p className="connector-help">Claude la analizará en su próxima ejecución.</p>}

        {!editing && meal.items.length > 0 && (
          <>
            <ul className="meal-items">
              {meal.items.map((it, i) => (
                <li key={i}>
                  {it.name}
                  {it.quantity ? ` (${it.quantity})` : ""} — {Math.round(it.calories)} kcal
                </li>
              ))}
            </ul>
            <p className="meal-total">
              {Math.round(meal.totalCalories)} kcal
              {meal.proteinG != null ? ` · P ${Math.round(meal.proteinG)} g` : ""}
              {meal.carbsG != null ? ` · H ${Math.round(meal.carbsG)} g` : ""}
              {meal.fatG != null ? ` · G ${Math.round(meal.fatG)} g` : ""}
            </p>
            {meal.analysisNotes && <small>Notas de Claude: {meal.analysisNotes}</small>}
          </>
        )}

        {editing && (
          <div>
            {items.map((it, i) => (
              <div key={i}>
                <div className="meal-edit-row">
                  <input aria-label="Alimento" placeholder="Alimento" value={it.name} onChange={(e) => setItem(i, { name: e.target.value })} />
                  <input aria-label="Cantidad" placeholder="Cantidad" value={it.quantity} onChange={(e) => setItem(i, { quantity: e.target.value })} />
                  <input aria-label="Calorías" placeholder="kcal" inputMode="decimal" value={it.calories} onChange={(e) => setItem(i, { calories: e.target.value })} />
                </div>
                <div className="meal-edit-macros">
                  <input aria-label="Proteínas (g)" placeholder="P g" inputMode="decimal" value={it.proteinG} onChange={(e) => setItem(i, { proteinG: e.target.value })} />
                  <input aria-label="Hidratos (g)" placeholder="H g" inputMode="decimal" value={it.carbsG} onChange={(e) => setItem(i, { carbsG: e.target.value })} />
                  <input aria-label="Grasas (g)" placeholder="G g" inputMode="decimal" value={it.fatG} onChange={(e) => setItem(i, { fatG: e.target.value })} />
                  <button className="btn-ghost" onClick={() => setItems((prev) => prev.filter((_, idx) => idx !== i))} aria-label="Quitar alimento">
                    ✕
                  </button>
                </div>
              </div>
            ))}
            <div className="actions-inline" style={{ marginTop: 8 }}>
              <button className="btn-ghost" onClick={() => setItems((prev) => [...prev, { name: "", quantity: "", calories: "", proteinG: "", carbsG: "", fatG: "" }])}>
                + Añadir alimento
              </button>
            </div>
          </div>
        )}

        <div className="actions-inline" style={{ marginTop: 10 }}>
          {editing ? (
            <>
              <button className="btn-primary" onClick={confirm} disabled={busy || items.every((it) => it.name.trim() === "")}>
                {busy ? "Guardando…" : "Confirmar"}
              </button>
              <button className="btn-secondary" onClick={() => setEditing(false)} disabled={busy}>
                Cancelar
              </button>
            </>
          ) : (
            <>
              {meal.status === "Proposed" && (
                <button className="btn-primary" onClick={() => run(() => mealsApi.confirm(meal.id, {}))} disabled={busy}>
                  Confirmar
                </button>
              )}
              {canEdit && (
                <button className="btn-secondary" onClick={startEdit} disabled={busy}>
                  Corregir
                </button>
              )}
              {canEdit && meal.hasPhoto && (
                <button className="btn-ghost" onClick={() => run(() => mealsApi.reanalyze(meal.id))} disabled={busy}>
                  Que Claude la vuelva a analizar
                </button>
              )}
              <button className="btn-ghost" onClick={() => setDeleting(true)} disabled={busy}>
                Borrar
              </button>
            </>
          )}
        </div>
      </div>
      {deleting && (
        <ConfirmModal
          title="Borrar comida"
          message="Se borrará la comida y su foto. No se puede deshacer."
          onConfirm={() => mealsApi.remove(meal.id).then(onChanged)}
          onCancel={() => setDeleting(false)}
        />
      )}
    </article>
  );
}
