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
} from "./types";

export const projectsApi = {
  list: () => api.get<ProjectDto[]>("/api/projects"),
  get: (id: number) => api.get<ProjectDto>(`/api/projects/${id}`),
  create: (dto: CreateProjectDto) => api.post<ProjectDto>("/api/projects", dto),
  update: (id: number, dto: UpdateProjectDto) => api.put<void>(`/api/projects/${id}`, dto),
  remove: (id: number) => api.delete<void>(`/api/projects/${id}`),
};

export const workItemsApi = {
  list: (status?: WorkItemStatus) =>
    api.get<WorkItemDto[]>(`/api/workitems${status ? `?status=${status}` : ""}`),
  inbox: () => api.get<WorkItemDto[]>("/api/workitems/inbox"),
  get: (id: number) => api.get<WorkItemDto>(`/api/workitems/${id}`),
  create: (dto: CreateWorkItemDto) => api.post<WorkItemDto>("/api/workitems", dto),
  update: (id: number, dto: UpdateWorkItemDto) => api.put<void>(`/api/workitems/${id}`, dto),
  remove: (id: number) => api.delete<void>(`/api/workitems/${id}`),
};

export const calendarEventsApi = {
  list: (date?: string) => api.get<CalendarEventDto[]>(`/api/calendarevents${date ? `?date=${date}` : ""}`),
  get: (id: number) => api.get<CalendarEventDto>(`/api/calendarevents/${id}`),
  create: (dto: CreateCalendarEventDto) => api.post<CalendarEventDto>("/api/calendarevents", dto),
  remove: (id: number) => api.delete<void>(`/api/calendarevents/${id}`),
};

export const plannerApi = {
  today: () => api.get<DailyPlanDto>("/api/planner/today"),
  generate: (date?: string) => api.post<DailyPlanDto>(`/api/planner/generate${date ? `?date=${date}` : ""}`),
  replan: () => api.post<DailyPlanDto>("/api/planner/replan"),
};
