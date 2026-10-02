// Tipos que reflejan exactamente los DTOs del backend (Compas.Api/Dtos).
// Mantenerlos sincronizados si cambian los records de C#.

export type Priority = "Low" | "Medium" | "High" | "Urgent";
export type ProjectStatus = "Active" | "OnHold" | "Completed" | "Archived";
export type WorkItemStatus = "Pending" | "Scheduled" | "InProgress" | "Completed" | "Cancelled";
export type PlanEntryKind = "Task" | "CalendarEvent" | "Break" | "Free";

export interface ProjectDto {
  id: number;
  name: string;
  description: string | null;
  colorHex: string;
  status: ProjectStatus;
  targetDate: string | null;
  progressPercent: number;
  totalWorkItems: number;
  completedWorkItems: number;
}

export interface CreateProjectDto {
  name: string;
  description?: string | null;
  colorHex?: string | null;
  targetDate?: string | null;
}

export interface UpdateProjectDto {
  name: string;
  description: string | null;
  colorHex: string;
  status: ProjectStatus;
  targetDate: string | null;
}

export interface WorkItemDto {
  id: number;
  title: string;
  description: string | null;
  projectId: number | null;
  projectName: string | null;
  priority: Priority;
  estimatedMinutes: number;
  dueDate: string | null;
  status: WorkItemStatus;
  scheduledStart: string | null;
  scheduledEnd: string | null;
}

export interface CreateWorkItemDto {
  title: string;
  description?: string | null;
  projectId?: number | null;
  priority: Priority;
  estimatedMinutes: number;
  dueDate?: string | null;
}

export interface UpdateWorkItemDto {
  title: string;
  description: string | null;
  projectId: number | null;
  priority: Priority;
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
}

export interface CreateCalendarEventDto {
  title: string;
  startTime: string;
  endTime: string;
  isAllDay: boolean;
  notes?: string | null;
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
