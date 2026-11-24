import { useState } from "react";
import { tokensApi } from "../api";
import type { CreatedApiTokenDto } from "../api/types";
import { useApiData } from "../hooks/useApiData";
import { errorText } from "../utils/errors";
import { ConfirmModal } from "./ConfirmModal";

/** Tokens del conector MCP: la URL que se pega en Claude para que las automatizaciones lean y escriban en Compás. */
export function ConnectorCard() {
  const tokens = useApiData(() => tokensApi.list(), []);
  const [name, setName] = useState("Claude");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [created, setCreated] = useState<CreatedApiTokenDto | null>(null);
  const [copied, setCopied] = useState(false);
  const [revoking, setRevoking] = useState<number | null>(null);
  const [quick, setQuick] = useState<CreatedApiTokenDto | null>(null);
  const [quickCopied, setQuickCopied] = useState<string | null>(null);

  async function create() {
    setBusy(true);
    setError(null);
    try {
      setCreated(await tokensApi.create(name.trim() || "Claude"));
      setCopied(false);
      tokens.reload();
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  async function createQuick() {
    setBusy(true);
    setError(null);
    try {
      setQuick(await tokensApi.createQuick("Atajo de comida"));
      setQuickCopied(null);
      tokens.reload();
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  async function copyQuick(label: string, text: string) {
    try {
      await navigator.clipboard.writeText(text);
      setQuickCopied(label);
    } catch {
      setError("No se pudo copiar; selecciona el texto y cópialo a mano.");
    }
  }

  async function copy(text: string) {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
    } catch {
      setError("No se pudo copiar; selecciona la dirección y cópiala a mano.");
    }
  }

  async function revoke(id: number) {
    setRevoking(null);
    try {
      await tokensApi.revoke(id);
      tokens.reload();
    } catch (err) {
      setError(errorText(err));
    }
  }

  return (
    <section className="card connector-card">
      <h3>Conector para Claude</h3>
      <p className="connector-help">
        Crea una dirección privada y pégala como conector personalizado (MCP) en Claude. Con ella las automatizaciones leen
        tu perfil, ajustan las reglas de progresión y analizan tus comidas. Quien tenga la dirección puede leer y cambiar
        tus datos: revócala si se filtra.
      </p>

      {created && (
        <div className="connector-new">
          <strong>Dirección del conector (solo se muestra ahora)</strong>
          <input readOnly value={created.mcpUrl} onFocus={(e) => e.currentTarget.select()} aria-label="Dirección del conector" />
          <div className="actions-inline">
            <button className="btn-primary" onClick={() => copy(created.mcpUrl)}>
              {copied ? "Copiada" : "Copiar"}
            </button>
            <button className="btn-ghost" onClick={() => setCreated(null)}>
              Ocultar
            </button>
          </div>
        </div>
      )}

      <div className="connector-create">
        <input value={name} maxLength={100} onChange={(e) => setName(e.target.value)} aria-label="Nombre del token" />
        <button className="btn-secondary" disabled={busy} onClick={create}>
          {busy ? "Creando…" : "Crear token"}
        </button>
      </div>

      <h3>Atajo de comida (iPhone)</h3>
      <p className="connector-help">
        Crea un token solo para subir fotos de comidas desde un atajo del móvil: no sirve para el conector de Claude ni
        para leer tus datos. La foto queda pendiente en Comidas y se estima en la siguiente pasada de la automatización.
      </p>

      {quick && quick.quickMealUrl && (
        <div className="connector-new">
          <strong>Datos para el atajo (el token solo se muestra ahora)</strong>
          <label className="connector-help" htmlFor="quick-url">Dirección (URL)</label>
          <input id="quick-url" readOnly value={quick.quickMealUrl} onFocus={(e) => e.currentTarget.select()} />
          <label className="connector-help" htmlFor="quick-auth">Cabecera Authorization</label>
          <input id="quick-auth" readOnly value={`Bearer ${quick.value}`} onFocus={(e) => e.currentTarget.select()} />
          <div className="actions-inline">
            <button className="btn-primary" onClick={() => copyQuick("url", quick.quickMealUrl!)}>
              {quickCopied === "url" ? "URL copiada" : "Copiar URL"}
            </button>
            <button className="btn-primary" onClick={() => copyQuick("auth", `Bearer ${quick.value}`)}>
              {quickCopied === "auth" ? "Cabecera copiada" : "Copiar cabecera"}
            </button>
            <button className="btn-ghost" onClick={() => setQuick(null)}>
              Ocultar
            </button>
          </div>
        </div>
      )}

      <div className="connector-create">
        <button className="btn-secondary" disabled={busy} onClick={createQuick}>
          {busy ? "Creando…" : "Crear token del atajo"}
        </button>
      </div>

      {error && <p className="connector-error">{error}</p>}

      {tokens.status === "loading" && <p className="connector-help">Cargando…</p>}
      {tokens.status === "error" && <p className="connector-error">{tokens.error}</p>}
      {tokens.status === "ok" && tokens.data?.length === 0 && <p className="connector-help">Todavía no hay tokens.</p>}
      <ul className="connector-list">
        {tokens.data?.map((t) => (
          <li key={t.id}>
            <span>
              <b>{t.name}</b> <code>{t.prefix}…</code>
              <small>
                {t.lastUsedAt ? `Último uso: ${new Date(t.lastUsedAt + "Z").toLocaleString("es-ES")}` : "Sin usar"}
              </small>
            </span>
            <button className="btn-ghost" onClick={() => setRevoking(t.id)}>
              Revocar
            </button>
          </li>
        ))}
      </ul>

      {revoking !== null && (
        <ConfirmModal
          title="Revocar token"
          message="Las automatizaciones que usen esta dirección dejarán de poder acceder a Compás."
          confirmLabel="Revocar"
          onConfirm={() => revoke(revoking)}
          onCancel={() => setRevoking(null)}
        />
      )}
    </section>
  );
}
