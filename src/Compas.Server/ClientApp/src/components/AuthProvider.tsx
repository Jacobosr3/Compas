import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { authApi } from "../api";
import { UNAUTHORIZED_EVENT } from "../api/client";
import type { AuthStatusDto } from "../api/types";
import { AuthContext, type AuthContextValue, type AuthState } from "../hooks/useAuth";
import { errorText } from "../utils/errors";

/** Consulta la sesión al abrir la app y la mantiene: si la API responde 401, vuelve a la pantalla de acceso. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>("loading");
  const [info, setInfo] = useState<AuthStatusDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  const apply = useCallback((status: AuthStatusDto) => {
    setInfo(status);
    setError(null);
    setState(status.authenticated ? "authenticated" : "anonymous");
  }, []);

  const reload = useCallback(() => {
    setState("loading");
    authApi
      .status()
      .then(apply)
      .catch((err: unknown) => {
        setError(errorText(err));
        setState("error");
      });
  }, [apply]);

  useEffect(() => {
    // Tras iniciar sesión con Google la API vuelve con ?auth=ok: se quita para que no quede en la dirección.
    const query = new URLSearchParams(window.location.search);
    if (query.get("auth") === "ok") window.history.replaceState(window.history.state, "", window.location.pathname);
    reload();
  }, [reload]);

  // Una respuesta 401 de cualquier llamada significa que la sesión caducó o se cerró en otra pestaña.
  useEffect(() => {
    const onUnauthorized = () => {
      authApi.status().then(apply).catch(() => setState("anonymous"));
    };
    window.addEventListener(UNAUTHORIZED_EVENT, onUnauthorized);
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, onUnauthorized);
  }, [apply]);

  const logout = useCallback(async () => {
    await authApi.logout();
    // Se vuelve a leer el estado para saber si ahora se ofrece crear cuenta, etc.
    apply(await authApi.status());
  }, [apply]);

  const value = useMemo<AuthContextValue>(
    () => ({ state, info, email: info?.authenticated ? info.email : null, error, reload, setSession: apply, logout }),
    [state, info, error, reload, apply, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
