import { useCallback, useEffect, useState } from "react";
import { ApiError } from "../api/client";

export type AsyncStatus = "loading" | "ok" | "error";

/**
 * Ejecuta `loader` al montar y expone datos + estado de conexión.
 * `reload()` se puede llamar tras cualquier acción de escritura (crear, editar, borrar)
 * para refrescar la vista con los datos reales del backend.
 */
export function useApiData<T>(loader: () => Promise<T>, deps: unknown[] = []) {
  const [data, setData] = useState<T | null>(null);
  const [status, setStatus] = useState<AsyncStatus>("loading");
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(() => {
    setStatus("loading");
    setError(null);
    loader()
      .then((result) => {
        setData(result);
        setStatus("ok");
      })
      .catch((err: unknown) => {
        setStatus("error");
        setError(err instanceof ApiError ? err.message : "Error desconocido");
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  useEffect(() => {
    reload();
  }, [reload]);

  return { data, status, error, reload };
}
