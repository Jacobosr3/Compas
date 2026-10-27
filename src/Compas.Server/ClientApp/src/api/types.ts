// Tipos que reflejan exactamente los DTOs del backend (Compas.Api/Dtos).
// Mantenerlos sincronizados si cambian los records de C#.

export type Priority = "Low" | "Medium" | "High" | "Urgent";
export type ProjectStatus = "Active" | "OnHold" | "Completed" | "Archived";
export type WorkItemStatus =
  | "Pending"
  | "Scheduled"
  | "Completed"
  | "Cancelled";

export type PlanEntryKind =
  | "Task"
  | "CalendarEvent"
  | "Break"
  | "Free";

export interface ProjectDto {
  id: number;
  name: string;
  description: string | null;
  colorHex: string;
  status: ProjectStatus;
  progressPercent: number;
  totalWorkItems: number;
  completedWorkItems: number;
}

export interface CreateProjectDto {
  name: string;
  description?: string | null;
  colorHex?: string | null;
}

export interface UpdateProjectDto {
  name: string;
  description: string | null;
  colorHex: string;
  status: ProjectStatus;
}

export interface WorkItemDto {
  id: number;
  title: string;
  description: string | null;
  projectId: number | null;
  projectName: string | null;
  priority: Priority;
  estimatedHours: number;
  estimatedMinutes: number;
  dueDate: string | null;
  status: WorkItemStatus;
  scheduledStart: string | null;
  scheduledEnd: string | null;
  subtasks: SubtaskDto[];
}

export interface SubtaskDto {
  id: number;
  title: string;
  isCompleted: boolean;
}

export interface CreateWorkItemDto {
  title: string;
  description?: string | null;
  projectId?: number | null;
  priority: Priority;
  estimatedHours: number;
  estimatedMinutes: number;
  dueDate?: string | null;
}

export interface UpdateWorkItemDto {
  title: string;
  description: string | null;
  projectId: number | null;
  priority: Priority;
  estimatedHours: number;
  estimatedMinutes: number;
  dueDate: string | null;
  status: WorkItemStatus;
}

export interface CalendarEventDto {
  id: number;
  title: string;
  startTime: string;
  endTime: string;
  isAllDay: boolean;
  notes: string | null;
  /** "Manual" o "Import" (traído de Google Calendar). */
  source: string;
  /** El evento está vinculado a Google Calendar: los cambios se guardan también allí. */
  googleLinked: boolean;
  /** Aviso al crear: el evento se guardó en Compás pero no se pudo crear en Google. */
  syncWarning?: string | null;
  /** Color (#rrggbb) del calendario de Google del evento; null si no está vinculado a Google. */
  colorHex?: string | null;
  /** Id del calendario de Google donde está el evento; null si solo existe en Compás. */
  calendarId?: string | null;
}

export interface GoogleCalendarDto {
  id: string;
  summary: string;
  colorHex: string | null;
  isPrimary: boolean;
  selected: boolean;
  /** Se pueden crear y editar eventos en este calendario. */
  canEdit: boolean;
}

export interface GoogleStatusDto {
  /** Hay credenciales de Google configuradas en la API. */
  configured: boolean;
  connected: boolean;
  email: string | null;
  lastSyncAt: string | null;
  lastSyncError: string | null;
  /** La cuenta dio permiso para crear y editar eventos (no solo leerlos). */
  canWrite: boolean;
  /** Calendario donde se crean los eventos nuevos de Compás. */
  defaultCalendarId: string | null;
  calendars: GoogleCalendarDto[];
}

export interface CreateCalendarEventDto {
  title: string;
  startTime: string;
  endTime: string;
  isAllDay: boolean;
  notes?: string | null;
  /** Calendario de Google donde crearlo: omitido = el de por defecto; "" = solo en Compás. */
  calendarId?: string | null;
}

export interface PlanEntryDto {
  id: number;
  kind: PlanEntryKind;
  title: string;
  start: string;
  end: string;
  projectName: string | null;
  priority: Priority | null;
  reason: string | null;
  wasRescheduled: boolean;
  isCurrent: boolean;
  workItemId: number | null;
  calendarEventId: number | null;
}

export interface DailyPlanDto {
  date: string;
  generatedAt: string;
  entries: PlanEntryDto[];
  nowEntry: PlanEntryDto | null;
}
