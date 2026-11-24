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
  GoogleStatusDto,
  AuthStatusDto,
  WorkoutSessionDto,
  ApiTokenDto,
  CreatedApiTokenDto,
  ProfileOverviewDto,
  UserProfileDto,
  UpdateProfileInput,
  BodyMeasurementDto,
  BodyMeasurementInput,
  LiftGoalDto,
  LiftGoalInput,
  TrainingSlotDto,
  TrainingSlotInput,
  ExerciseRuleDto,
  ExerciseRuleInput,
  MealDto,
  MealCreateInput,
  MealConfirmInput,
  NutritionSummaryDto,
} from "./types";

export const authApi = {
  status: () => api.get<AuthStatusDto>("/api/auth/status"),
  login: (email: string, password: string, remember: boolean) =>
    api.post<AuthStatusDto>("/api/auth/login", { email, password, remember }),
  register: (email: string, password: string, displayName: string, remember: boolean) =>
    api.post<AuthStatusDto>("/api/auth/register", { email, password, displayName: displayName || null, remember }),
  logout: () => api.post<void>("/api/auth/logout"),
  /** Devuelve la dirección de Google a la que navegar para iniciar sesión; al terminar se vuelve a `returnUrl`. */
  googleStart: (returnUrl: string, remember: boolean) =>
    api.post<{ url: string }>("/api/auth/google", { returnUrl, remember }),
};

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
  /** Eventos de un rango de días (ambos incluidos), por ejemplo una semana. */
  range: (from: string, to: string) => api.get<CalendarEventDto[]>(`/api/calendarevents?from=${from}&to=${to}`),
  get: (id: number) => api.get<CalendarEventDto>(`/api/calendarevents/${id}`),
  create: (dto: CreateCalendarEventDto) => api.post<CalendarEventDto>("/api/calendarevents", dto),
  update: (id: number, dto: CreateCalendarEventDto) => api.put<CalendarEventDto>(`/api/calendarevents/${id}`, dto),
  remove: (id: number) => api.delete<void>(`/api/calendarevents/${id}`),
};

export const googleApi = {
  status: () => api.get<GoogleStatusDto>("/api/google/status"),
  /** Devuelve la dirección de Google a la que enviar al usuario para elegir cuenta. */
  connect: (returnUrl: string) => api.post<{ url: string }>("/api/google/connect", { returnUrl }),
  refreshCalendars: () => api.post<GoogleStatusDto>("/api/google/calendars/refresh"),
  saveSelection: (calendarIds: string[]) => api.put<GoogleStatusDto>("/api/google/calendars", { calendarIds }),
  sync: () => api.post<GoogleStatusDto>("/api/google/sync"),
  /** Sincroniza solo si hace falta (la API lo decide) y devuelve el estado; para el refresco automático. */
  refresh: () => api.post<GoogleStatusDto>("/api/google/refresh"),
  /** Calendario de Google donde se crean los eventos nuevos de Compás. */
  setTarget: (calendarId: string) => api.put<GoogleStatusDto>("/api/google/target", { calendarId }),
  disconnect: () => api.delete<GoogleStatusDto>("/api/google"),
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
  // Programa lo que queda de la semana (hasta el domingo); `projectIds` como en replan.
  week: (projectIds?: number[]) => api.post<DailyPlanDto>(`/api/planner/week${plannerQuery(undefined, projectIds)}`),
  reorder: (workItemIds: number[], minutes?: number, projectIds?: number[]) =>
    api.post<DailyPlanDto>(`/api/planner/reorder${plannerQuery(minutes, projectIds)}`, { workItemIds }),
};

export const workoutsApi = {
  list: (from?: string, to?: string) => {
    const params = new URLSearchParams();
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    const qs = params.toString();
    return api.get<WorkoutSessionDto[]>(`/api/workouts${qs ? `?${qs}` : ""}`);
  },
  setCompleted: (id: number, completed: boolean) => api.put<void>(`/api/workouts/${id}/completed`, { completed }),
  remove: (id: number) => api.delete<void>(`/api/workouts/${id}`),
};

/** Tokens personales del conector MCP con el que una automatización de Claude escribe en Compás. */
export const tokensApi = {
  list: () => api.get<ApiTokenDto[]>("/api/tokens"),
  create: (name: string) => api.post<CreatedApiTokenDto>("/api/tokens", { name }),
  /** Token de captura rápida (atajo del móvil): solo permite subir comidas con foto. */
  createQuick: (name: string) => api.post<CreatedApiTokenDto>("/api/tokens", { name, kind: "quick" }),
  revoke: (id: number) => api.delete<void>(`/api/tokens/${id}`),
};

/** Perfil, medidas corporales y objetivos de fuerza (los lee la automatización de Claude para programar los pesos). */
export const profileApi = {
  get: () => api.get<ProfileOverviewDto>("/api/profile"),
  update: (dto: UpdateProfileInput) => api.put<UserProfileDto>("/api/profile", dto),
  logMeasurement: (dto: BodyMeasurementInput) => api.post<BodyMeasurementDto>("/api/profile/measurements", dto),
  removeMeasurement: (id: number) => api.delete<void>(`/api/profile/measurements/${id}`),
  upsertLiftGoal: (dto: LiftGoalInput) => api.put<LiftGoalDto>("/api/profile/lift-goals", dto),
  removeLiftGoal: (id: number) => api.delete<void>(`/api/profile/lift-goals/${id}`),
  /** Sustituye todos los huecos semanales de entreno. */
  setTrainingSlots: (slots: TrainingSlotInput[]) => api.put<TrainingSlotDto[]>("/api/profile/training-slots", slots),
  upsertExerciseRules: (rules: ExerciseRuleInput[]) => api.put<ExerciseRuleDto[]>("/api/profile/exercise-rules", rules),
  removeExerciseRule: (id: number) => api.delete<void>(`/api/profile/exercise-rules/${id}`),
};

/** Comidas con foto: Claude estima alimentos y calorías por el conector MCP y el usuario las confirma aquí. */
export const mealsApi = {
  list: (from?: string, to?: string) => {
    const params = new URLSearchParams();
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    const qs = params.toString();
    return api.get<MealDto[]>(`/api/meals${qs ? `?${qs}` : ""}`);
  },
  create: (dto: MealCreateInput) => api.post<MealDto>("/api/meals", dto),
  confirm: (id: number, dto: MealConfirmInput) => api.put<MealDto>(`/api/meals/${id}/confirm`, dto),
  reanalyze: (id: number) => api.post<MealDto>(`/api/meals/${id}/reanalyze`),
  remove: (id: number) => api.delete<void>(`/api/meals/${id}`),
  photoUrl: (id: number) => `/api/meals/${id}/photo`,
  summary: (from?: string, to?: string) => {
    const params = new URLSearchParams();
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    const qs = params.toString();
    return api.get<NutritionSummaryDto>(`/api/meals/summary${qs ? `?${qs}` : ""}`);
  },
};
