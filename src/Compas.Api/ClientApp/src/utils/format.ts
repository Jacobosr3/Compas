export function fmtDuration(mins: number): string {
  if (mins < 60) return `${mins}m`;
  const h = Math.floor(mins / 60);
  const m = mins % 60;
  return m === 0 ? `${h}h` : `${h}h ${m}m`;
}

export function fmtTime(iso: string): string {
  return new Date(iso).toLocaleTimeString("es-ES", { hour: "2-digit", minute: "2-digit" });
}

export function fmtDate(iso: string): string {
  return new Date(iso).toLocaleDateString("es-ES", { day: "numeric", month: "short", year: "numeric" });
}

export function fmtDateLong(date: Date): string {
  const label = new Intl.DateTimeFormat("es-ES", { weekday: "long", day: "numeric", month: "long" }).format(date);
  return label.charAt(0).toUpperCase() + label.slice(1);
}

export function toDateInputValue(date: Date): string {
  return date.toISOString().slice(0, 10);
}

export function toDateTimeLocalValue(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export const priorityLabel: Record<string, string> = {
  Low: "Baja",
  Medium: "Media",
  High: "Alta",
  Urgent: "Urgente",
};

export const statusLabel: Record<string, string> = {
  Pending: "Pendiente",
  Scheduled: "Programada",
  InProgress: "En curso",
  Completed: "Completada",
  Cancelled: "Cancelada",
};

export const projectStatusLabel: Record<string, string> = {
  Active: "Activo",
  OnHold: "En pausa",
  Completed: "Completado",
  Archived: "Archivado",
};
