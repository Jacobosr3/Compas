import { getApiBase } from "../api/client";

export function ConnectionBanner({ message }: { message: string }) {
  return (
    <div className="conn-banner">
      No consigo hablar con la API en <code>{getApiBase()}</code>. Comprueba que el backend está
      arrancado (<code>dotnet run</code> dentro de <code>src/Compas.Api</code>) y que el puerto
      coincide con el configurado (icono ⚙️ arriba a la derecha).
      <div style={{ marginTop: 6, color: "var(--muted)" }}>{message}</div>
    </div>
  );
}
