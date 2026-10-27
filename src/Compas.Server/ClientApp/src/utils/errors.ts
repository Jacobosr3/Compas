import { ApiError } from "../api/client";

/** Mensaje limpio de un error de la API (quita el "409 Conflict — " del principio). */
export function errorText(err: unknown): string {
  if (err instanceof ApiError) {
    const i = err.message.indexOf(" — ");
    return i >= 0 ? err.message.slice(i + 3) : err.message;
  }
  return err instanceof Error ? err.message : String(err);
}
