import { createContext, useContext } from "react";
import type { AuthStatusDto } from "../api/types";

export type AuthState = "loading" | "error" | "anonymous" | "authenticated";

export interface AuthContextValue {
  state: AuthState;
  /** Estado devuelto por la API (opciones de acceso: crear cuenta, Google). */
  info: AuthStatusDto | null;
  email: string | null;
  error: string | null;
  reload: () => void;
  /** Guarda el resultado de un inicio de sesión o registro correcto. */
  setSession: (status: AuthStatusDto) => void;
  logout: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth debe usarse dentro de <AuthProvider>.");
  return ctx;
}
