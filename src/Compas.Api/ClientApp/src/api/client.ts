// En desarrollo, Vite redirige /api al backend vía proxy (vite.config.ts).
// En producción (build estático servido por .NET), /api la resuelve el propio servidor.
// En ambos casos la URL base es relativa: no hay que configurar nada.
const API_BASE = "";

export class ApiError extends Error {
  status?: number;

  constructor(message: string, status?: number) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let res: Response;
  try {
    res = await fetch(API_BASE + path, {
      ...init,
      headers: { "Content-Type": "application/json", ...(init?.headers || {}) },
    });
  } catch {
    throw new ApiError("No se pudo conectar con la API. ¿Está el backend arrancado?");
  }

  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new ApiError(`${res.status} ${res.statusText}${text ? " — " + text : ""}`, res.status);
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "POST", body: body !== undefined ? JSON.stringify(body) : undefined }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "PUT", body: body !== undefined ? JSON.stringify(body) : undefined }),
  delete: <T>(path: string) => request<T>(path, { method: "DELETE" }),
};

// Función vacía para mantener compatibilidad con Topbar (ya no hace falta configurar nada)
export function getApiBase(): string { return "http://localhost:5080"; }
export function setApiBase(_url: string): void { /* no-op */ }
