import { useEffect, useMemo, useState } from "react";
import { ApiError } from "../api/client";
import { googleApi } from "../api";
import { ConfirmModal } from "./ConfirmModal";
import { useApiData } from "../hooks/useApiData";
import { errorText } from "../utils/errors";

function fmtAgo(iso: string): string {
  const minutes = Math.round((Date.now() - new Date(iso).getTime()) / 60000);
  if (minutes < 1) return "hace un momento";
  if (minutes < 60) return `hace ${minutes} min`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `hace ${hours} h`;
  return new Date(iso).toLocaleString("es-ES", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
}

function GoogleMark() {
  return (
    <span className="google-mark" aria-hidden="true">
      G
    </span>
  );
}

type Notice = { kind: "ok" | "error"; text: string };

const OPEN_KEY = "compas.googleCalendarsOpen";

function readOpenPreference(): boolean | null {
  try {
    const saved = localStorage.getItem(OPEN_KEY);
    return saved === "1" ? true : saved === "0" ? false : null;
  } catch {
    return null; // sin almacenamiento: se decide automáticamente
  }
}

/**
 * Conexión con Google Calendar. Desactivado por defecto: el botón "Google Calendar" lleva a Google
 * para elegir la cuenta; después se eligen los calendarios que se incluyen en Compás.
 */
export function GoogleCalendarCard({ onSynced }: { onSynced: () => void }) {
  const google = useApiData(() => googleApi.status(), []);
  const status = google.data;

  const [busy, setBusy] = useState<string | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [selection, setSelection] = useState<Set<string> | null>(null);
  const [showSetup, setShowSetup] = useState(false);
  const [confirmDisconnect, setConfirmDisconnect] = useState(false);
  // Lista de calendarios desplegada o recogida. null = automático: abierta hasta la primera sincronización.
  const [listOpen, setListOpen] = useState<boolean | null>(readOpenPreference);

  function changeListOpen(next: boolean) {
    setListOpen(next);
    try {
      localStorage.setItem(OPEN_KEY, next ? "1" : "0");
    } catch {
      /* sin almacenamiento: solo se pierde la preferencia */
    }
  }

  // Resultado de volver de Google (?google=connected | error&message=...).
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const result = params.get("google");
    if (!result) return;
    if (result === "connected") {
      setNotice({ kind: "ok", text: "Cuenta conectada. Elige qué calendarios quieres incluir en Compás." });
    } else {
      setNotice({ kind: "error", text: params.get("message") || "No se pudo conectar con Google." });
    }
    window.history.replaceState(null, "", window.location.pathname);
  }, []);

  const chosen = useMemo(
    () => selection ?? new Set((status?.calendars ?? []).filter((c) => c.selected).map((c) => c.id)),
    [selection, status],
  );
  const dirty =
    selection !== null &&
    (status?.calendars ?? []).some((c) => c.selected !== selection.has(c.id));

  async function run(name: string, action: () => Promise<unknown>, okText?: string) {
    setBusy(name);
    setNotice(null);
    try {
      await action();
      if (okText) setNotice({ kind: "ok", text: okText });
    } catch (err) {
      setNotice({ kind: "error", text: errorText(err) });
    } finally {
      setBusy(null);
      google.reload();
    }
  }

  /** Lleva al usuario a Google, que le pide elegir la cuenta. */
  async function connect() {
    setBusy("connect");
    setNotice(null);
    try {
      const { url } = await googleApi.connect(window.location.origin + "/agenda");
      window.location.href = url;
    } catch (err) {
      setBusy(null);
      if (err instanceof ApiError && err.status === 409) setShowSetup(true);
      else setNotice({ kind: "error", text: errorText(err) });
    }
  }

  function toggle(id: string) {
    const next = new Set(chosen);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    setSelection(next);
  }

  const connected = status?.connected ?? false;
  const open = listOpen ?? !status?.lastSyncAt;
  const includedNames = (status?.calendars ?? []).filter((c) => chosen.has(c.id)).map((c) => c.summary);

  return (
    <section className={"card google-card" + (connected ? "" : " off")}>
      <div className="google-head">
        <div className="google-title">
          <GoogleMark />
          <div>
            <h3>Google Calendar</h3>
            <span className="google-state">
              {google.status === "error"
                ? "Sin conexión con la API"
                : !status
                  ? "Cargando…"
                  : connected
                    ? `Conectado · ${status.email}`
                    : "Desactivado"}
            </span>
          </div>
        </div>

        {status && !connected && (
          <button className="btn-secondary google-btn" onClick={connect} disabled={busy !== null}>
            <GoogleMark />
            {busy === "connect" ? "Abriendo Google…" : "Google Calendar"}
          </button>
        )}

        {connected && (
          <div className="google-actions">
            <button className="btn-secondary" onClick={() => run("sync", async () => { await googleApi.sync(); onSynced(); }, "Eventos sincronizados.")} disabled={busy !== null}>
              {busy === "sync" ? "Sincronizando…" : "Sincronizar ahora"}
            </button>
            <button className="btn-ghost" onClick={connect} disabled={busy !== null}>
              Cambiar de cuenta
            </button>
            <button className="btn-danger" onClick={() => setConfirmDisconnect(true)} disabled={busy !== null}>
              Desconectar
            </button>
          </div>
        )}
      </div>

      {status && !connected && (
        <p className="google-help">
          Conecta tu cuenta para que Compás tenga en cuenta tus reuniones al planificar. Google te pedirá elegir la cuenta
          y después podrás escoger qué calendarios incluir. Solo se pide acceso de lectura.
        </p>
      )}

      {notice && <div className={"google-notice " + notice.kind}>{notice.text}</div>}

      {connected && status && !status.canWrite && (
        <div className="google-notice">
          Compás solo puede <b>leer</b> tu calendario. Para crear y editar eventos en Google desde Compás hace falta un permiso más.{" "}
          <button className="link-btn" onClick={connect} disabled={busy !== null}>
            Dar permiso de escritura
          </button>
        </div>
      )}


      {connected && status && (
        <>
          <div className="google-list-head">
            <button className="google-toggle" aria-expanded={open} onClick={() => changeListOpen(!open)}>
              <span className={"google-chevron" + (open ? " open" : "")} aria-hidden="true">
                ›
              </span>
              Calendarios que se incluyen
              <span className="google-count">
                {chosen.size} de {status.calendars.length}
              </span>
            </button>
            {open && (
              <button className="link-btn" onClick={() => run("refresh", async () => { await googleApi.refreshCalendars(); setSelection(null); })} disabled={busy !== null}>
                {busy === "refresh" ? "Actualizando…" : "Actualizar lista"}
              </button>
            )}
          </div>

          {!open && (
            <div className="google-summary">
              {includedNames.length === 0 ? (
                <span className="google-state">Ningún calendario incluido</span>
              ) : (
                includedNames.map((name) => (
                  <span className="tag" key={name}>
                    {name}
                  </span>
                ))
              )}
            </div>
          )}

          {/* Siempre montada para animar la altura al recoger o desplegar. */}
          <div className={"google-collapse" + (open ? " open" : "")} aria-hidden={!open}>
            <div className="google-calendars">
              {status.calendars.length === 0 && <div className="empty-mini">Esta cuenta no tiene calendarios disponibles.</div>}
              {status.calendars.map((c) => (
                <label key={c.id} className={"google-cal" + (chosen.has(c.id) ? " on" : "")}>
                  <input type="checkbox" checked={chosen.has(c.id)} onChange={() => toggle(c.id)} tabIndex={open ? 0 : -1} />
                  <span className="google-dot" style={{ background: c.colorHex ?? "var(--muted)" }} />
                  <span className="google-cal-name">{c.summary}</span>
                  {c.isPrimary && <span className="tag">Principal</span>}
                </label>
              ))}
            </div>
          </div>

          <div className="google-foot">
            <span className={status.lastSyncError ? "google-error" : "google-state"}>
              {status.lastSyncError
                ? status.lastSyncError
                : status.lastSyncAt
                  ? `Última sincronización ${fmtAgo(status.lastSyncAt)}`
                  : "Aún no se ha sincronizado"}
            </span>
            <button
              className="btn-primary"
              disabled={!dirty || busy !== null}
              onClick={() =>
                run(
                  "save",
                  async () => {
                    await googleApi.saveSelection([...chosen]);
                    setSelection(null);
                    changeListOpen(false); // ya elegidos: se recoge la lista para ahorrar espacio
                    onSynced();
                  },
                  "Calendarios guardados y eventos sincronizados.",
                )
              }
            >
              {busy === "save" ? "Guardando…" : "Guardar y sincronizar"}
            </button>
          </div>
        </>
      )}

      {confirmDisconnect && (
        <ConfirmModal
          title="Desconectar Google Calendar"
          message="Se revocará el permiso y se quitarán de Compás los eventos importados de Google. Los eventos que ya están en Google no se borran allí, y los que creaste en Compás se conservan."
          confirmLabel="Desconectar"
          onConfirm={async () => {
            await googleApi.disconnect();
            setSelection(null);
            google.reload();
            onSynced();
          }}
          onCancel={() => setConfirmDisconnect(false)}
        />
      )}

      {showSetup && <GoogleSetupModal onClose={() => setShowSetup(false)} />}
    </section>
  );
}

/** Explica cómo crear las credenciales de Google (hacen falta una sola vez). */
function GoogleSetupModal({ onClose }: { onClose: () => void }) {
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") onClose();
    }
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal google-setup" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
        <h2>Configurar Google Calendar</h2>
        <p className="confirm-message">
          Compás necesita unas credenciales de Google para poder pedirte permiso. Se crean una sola vez, gratis:
        </p>
        <ol className="google-steps">
          <li>
            En <b>console.cloud.google.com</b> crea un proyecto y activa la <b>Google Calendar API</b>.
          </li>
          <li>
            En <b>Pantalla de consentimiento</b> elige «Externo» y añade tu cuenta como usuario de prueba.
          </li>
          <li>
            En <b>Credenciales</b> crea un <b>ID de cliente OAuth</b> de tipo «Aplicación web» y, como URI de redireccionamiento
            autorizado, pon <code>http://localhost:5080/api/google/callback</code>.
          </li>
          <li>
            Guarda el ID y el secreto en la API (PowerShell, desde la carpeta del proyecto) y reinicia la API:
            <pre>{`dotnet user-secrets set "Google:ClientId" "TU_ID" --project src/Compas.Api
dotnet user-secrets set "Google:ClientSecret" "TU_SECRETO" --project src/Compas.Api`}</pre>
          </li>
        </ol>
        <div className="modal-actions">
          <button className="btn-primary" onClick={onClose}>
            Entendido
          </button>
        </div>
      </div>
    </div>
  );
}
