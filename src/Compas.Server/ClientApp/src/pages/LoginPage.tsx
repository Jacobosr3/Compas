import { useEffect, useState, type FormEvent } from "react";
import { authApi } from "../api";
import type { AuthStatusDto } from "../api/types";
import { errorText } from "../utils/errors";

type Mode = "login" | "register";

/**
 * Pantalla de acceso: correo y contraseña, o «Continuar con Google» (que además deja conectado el calendario).
 * «Recordar este dispositivo» decide si la sesión sobrevive al cierre del navegador.
 */
export function LoginPage({
  info,
  onAuthenticated,
}: {
  info: AuthStatusDto | null;
  onAuthenticated: (status: AuthStatusDto) => void;
}) {
  const registrationOpen = info?.registrationOpen ?? false;
  // Si todavía no hay ninguna cuenta, lo normal es crearla.
  const [mode, setMode] = useState<Mode>(registrationOpen ? "register" : "login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [remember, setRemember] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Cuando se cierra la sesión o se crea la cuenta, la opción de «Crear cuenta» aparece o desaparece.
  const canRegister = registrationOpen;
  const effectiveMode: Mode = canRegister ? mode : "login";

  // Al volver de Google con un fallo, la API deja el motivo en la dirección (?auth=error&message=…).
  useEffect(() => {
    const query = new URLSearchParams(window.location.search);
    if (query.has("auth")) {
      if (query.get("auth") === "error") setError(query.get("message") || "No se pudo iniciar sesión con Google.");
      window.history.replaceState(window.history.state, "", window.location.pathname);
    }
  }, []);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (busy) return;
    setError(null);
    setBusy(true);
    try {
      const status =
        effectiveMode === "register"
          ? await authApi.register(email, password, displayName, remember)
          : await authApi.login(email, password, remember);
      onAuthenticated(status);
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  async function continueWithGoogle() {
    if (busy) return;
    setError(null);
    setBusy(true);
    try {
      // La API devuelve la dirección de Google; al terminar, Google vuelve a la API y esta redirige aquí.
      const { url } = await authApi.googleStart(window.location.origin + "/", remember);
      window.location.href = url;
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  const registering = effectiveMode === "register";

  return (
    <main className="auth-page">
      <div className="auth-card">
        <div className="brand">C</div>
        <h1>{registering ? "Crea tu cuenta" : "Inicia sesión"}</h1>
        <p className="auth-sub">
          {registering
            ? "Tu planificador personal. Esta será la cuenta propietaria de Compás."
            : "Entra para ver tu plan, tus proyectos y tu agenda."}
        </p>

        {info?.googleConfigured && (
          <>
            <button type="button" className="btn-secondary auth-google" onClick={continueWithGoogle} disabled={busy}>
              <svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true">
                <path fill="#4285F4" d="M22.5 12.27c0-.79-.07-1.54-.2-2.27H12v4.3h5.9a5.05 5.05 0 01-2.19 3.31v2.75h3.54c2.07-1.91 3.25-4.72 3.25-8.09z" />
                <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.54-2.75c-.98.66-2.24 1.05-3.74 1.05-2.88 0-5.32-1.94-6.19-4.56H2.15v2.84A11 11 0 0012 23z" />
                <path fill="#FBBC05" d="M5.81 14.08a6.6 6.6 0 010-4.16V7.08H2.15a11 11 0 000 9.84l3.66-2.84z" />
                <path fill="#EA4335" d="M12 5.38c1.62 0 3.07.56 4.21 1.65l3.15-3.15C17.45 2.09 14.97 1 12 1A11 11 0 002.15 7.08l3.66 2.84C6.68 7.32 9.12 5.38 12 5.38z" />
              </svg>
              Continuar con Google
            </button>
            <p className="auth-hint">Con Google también se conecta tu calendario.</p>
            <div className="auth-divider">
              <span>o con tu correo</span>
            </div>
          </>
        )}

        <form onSubmit={submit} noValidate>
          {registering && (
            <div className="field">
              <label htmlFor="auth-name">Nombre (opcional)</label>
              <input id="auth-name" name="name" autoComplete="name" value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
            </div>
          )}
          <div className="field">
            <label htmlFor="auth-email">Correo</label>
            <input
              id="auth-email"
              name="email"
              type="email"
              autoComplete="username"
              inputMode="email"
              autoFocus
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="auth-password">Contraseña</label>
            <input
              id="auth-password"
              name="password"
              type="password"
              autoComplete={registering ? "new-password" : "current-password"}
              required
              minLength={registering ? 10 : undefined}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
            {registering && <span className="auth-hint">Mínimo 10 caracteres.</span>}
          </div>

          <label className="auth-remember">
            <input type="checkbox" checked={remember} onChange={(e) => setRemember(e.target.checked)} />
            Recordar este dispositivo
          </label>

          {error && (
            <div className="auth-error" role="alert">
              {error}
            </div>
          )}

          <button type="submit" className="btn-primary auth-submit" disabled={busy || !email || !password}>
            {busy ? "Un momento…" : registering ? "Crear cuenta" : "Entrar"}
          </button>
        </form>

        {canRegister && (
          <p className="auth-switch">
            {registering ? "¿Ya tienes cuenta?" : "¿Aún no tienes cuenta?"}{" "}
            <button
              type="button"
              className="link-btn"
              onClick={() => {
                setMode(registering ? "login" : "register");
                setError(null);
              }}
            >
              {registering ? "Inicia sesión" : "Créala"}
            </button>
          </p>
        )}
      </div>
    </main>
  );
}
