import { api } from "./client";
import type {
  ProjectDto,
  CreateProjectDto,
  UpdateProjectDto,
  WorkItemDto,
  CreateWorkItemDto,
  UpdateWorkItemDto,
  CalendarEventDto,
  CreateCalendarEventDto,
  DailyPlanDto,
  WorkItemStatus,
  SubtaskDto,
} from "./types";

export const projectsApi = {
  list: () => api.get<ProjectDto[]>("/api/projects"),
  get: (id: number) => api.get<ProjectDto>(`/api/projects/${id}`),
  create: (dto: CreateProjectDto) => api.post<ProjectDto>("/api/projects", dto),
  update: (id: number, dto: UpdateProjectDto) => api.put<void>(`/api/projects/${id}`, dto),
  remove: (id: number) => api.delete<void>(`/api/projects/${id}`),
};

export const workItemsApi = {
  list: (status?: WorkItemStatus, projectId?: number) => {
    const params = new URLSearchParams();
    if (status) params.set("status", status);
    if (projectId !== undefined) params.set("projectId", String(projectId));
    const qs = params.toString();
    return api.get<WorkItemDto[]>(`/api/workitems${qs ? `?${qs}` : ""}`);
  },
  inbox: () => api.get<WorkItemDto[]>("/api/workitems/inbox"),
  get: (id: number) => api.get<WorkItemDto>(`/api/workitems/${id}`),
  create: (dto: CreateWorkItemDto) => api.post<WorkItemDto>("/api/workitems", dto),
  update: (id: number, dto: UpdateWorkItemDto) => api.put<void>(`/api/workitems/${id}`, dto),
  remove: (id: number) => api.delete<void>(`/api/workitems/${id}`),
  addSubtask: (id: number, title: string) =>
    api.post<SubtaskDto>(`/api/workitems/${id}/subtasks`, { title }),
  updateSubtask: (id: number, subtaskId: number, title: string, isCompleted: boolean) =>
    api.put<void>(`/api/workitems/${id}/subtasks/${subtaskId}`, { title, isCompleted }),
  reorderSubtasks: (id: number, subtaskIds: number[]) =>
    api.put<void>(`/api/workitems/${id}/subtasks/order`, { subtaskIds }),
  removeSubtask: (id: number, subtaskId: number) =>
    api.delete<void>(`/api/workitems/${id}/subtasks/${subtaskId}`),
};

export const calendarEventsApi = {
  list: (date?: string) => api.get<CalendarEventDto[]>(`/api/calendarevents${date ? `?date=${date}` : ""}`),
  get: (id: number) => api.get<CalendarEventDto>(`/api/calendarevents/${id}`),
  create: (dto: CreateCalendarEventDto) => api.post<CalendarEventDto>("/api/calendarevents", dto),
  remove: (id: number) => api.delete<void>(`/api/calendarevents/${id}`),
};

function plannerQuery(minutes?: number, projectIds?: number[]): string {
  const params = new URLSearchParams();
  if (minutes) params.set("minutes", String(minutes));
  projectIds?.forEach((id) => params.append("projectIds", String(id)));
  const qs = params.toString();
  return qs ? `?${qs}` : "";
}

export const plannerApi = {
  today: () => api.get<DailyPlanDto>("/api/planner/today"),
  // `minutes`: tiempo máximo a dedicar a tareas; sin él se rellena todo el día.
  generate: (minutes?: number, date?: string) => {
    const params = new URLSearchParams();
    if (date) params.set("date", date);
    if (minutes) params.set("minutes", String(minutes));
    const qs = params.toString();
    return api.post<DailyPlanDto>(`/api/planner/generate${qs ? `?${qs}` : ""}`);
  },
  // `projectIds`: solo tareas de esos proyectos (0 = sin proyecto); vacío = todos.
  replan: (minutes?: number, projectIds?: number[]) =>
    api.post<DailyPlanDto>(`/api/planner/replan${plannerQuery(minutes, projectIds)}`),
  clear: () => api.post<DailyPlanDto>("/api/planner/clear"),
  reorder: (workItemIds: number[], minutes?: number, projectIds?: number[]) =>
    api.post<DailyPlanDto>(`/api/planner/reorder${plannerQuery(minutes, projectIds)}`, { workItemIds }),
};
