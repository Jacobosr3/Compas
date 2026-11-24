// Tipos que reflejan exactamente los DTOs del backend (Compas.Api/Dtos).
// Mantenerlos sincronizados si cambian los records de C#.

/** Estado de la sesión y de las opciones de acceso (Compas.Api/Dtos/AuthDtos.cs). */
export interface AuthStatusDto {
  authenticated: boolean;
  email: string | null;
  displayName: string | null;
  registrationOpen: boolean;
  googleConfigured: boolean;
}

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

export interface WorkoutSetDto {
  type: string;
  weightKg: number | null;
  reps: number | null;
}

export interface WorkoutExerciseDto {
  name: string;
  notes: string | null;
  sets: WorkoutSetDto[];
}

export interface WorkoutSessionDto {
  id: number;
  title: string;
  /** Día en formato yyyy-MM-dd. */
  date: string;
  /** Hora de inicio HH:mm (con ella la sesión está también en el calendario); null = solo el día. */
  startTime: string | null;
  durationMinutes: number | null;
  notes: string | null;
  completed: boolean;
  source: string | null;
  externalId: string | null;
  exercises: WorkoutExerciseDto[];
}

export interface ApiTokenDto {
  id: number;
  name: string;
  prefix: string;
  createdAt: string;
  lastUsedAt: string | null;
}

export interface CreatedApiTokenDto {
  token: ApiTokenDto;
  /** Token en claro: solo se recibe al crearlo. */
  value: string;
  mcpUrl: string;
}

export type NutritionPhase = "Bulk" | "Maintenance" | "Cut";

export interface UserProfileDto {
  heightCm: number | null;
  birthDate: string | null;
  sex: string | null;
  activityLevel: string | null;
  phase: NutritionPhase;
  phaseStartDate: string | null;
  targetWeightKg: number | null;
  targetDate: string | null;
  notes: string | null;
}

export interface BodyMeasurementDto {
  id: number;
  date: string;
  weightKg: number | null;
  bodyFatPct: number | null;
  waistCm: number | null;
  chestCm: number | null;
  hipCm: number | null;
  armCm: number | null;
  thighCm: number | null;
  notes: string | null;
}

export interface LiftGoalDto {
  id: number;
  exerciseName: string;
  hevyExerciseTemplateId: string | null;
  targetWeightKg: number;
  targetReps: number;
  targetDate: string | null;
  notes: string | null;
  achieved: boolean;
}

export interface TrainingSlotDto {
  id: number;
  /** 1 = lunes ... 7 = domingo. */
  dayOfWeek: number;
  startTime: string;
  durationMinutes: number;
  label: string | null;
}

export interface TrainingSlotInput {
  dayOfWeek: number;
  startTime: string;
  durationMinutes?: number | null;
  label?: string | null;
}

export interface ExerciseRuleDto {
  id: number;
  exerciseName: string;
  repMin: number;
  repMax: number;
  incrementKg: number;
  notes: string | null;
}

export interface ExerciseRuleInput {
  exerciseName: string;
  repMin: number;
  repMax: number;
  incrementKg: number;
  notes?: string | null;
}

export interface ProfileOverviewDto {
  profile: UserProfileDto;
  measurements: BodyMeasurementDto[];
  liftGoals: LiftGoalDto[];
  trainingSlots: TrainingSlotDto[];
  exerciseRules: ExerciseRuleDto[];
}

/** Actualización parcial: solo se cambian los campos con valor. */
export interface UpdateProfileInput {
  heightCm?: number | null;
  birthDate?: string | null;
  sex?: string | null;
  activityLevel?: string | null;
  phase?: NutritionPhase | null;
  phaseStartDate?: string | null;
  targetWeightKg?: number | null;
  targetDate?: string | null;
  notes?: string | null;
}

export interface BodyMeasurementInput {
  date: string;
  weightKg?: number | null;
  bodyFatPct?: number | null;
  waistCm?: number | null;
  chestCm?: number | null;
  hipCm?: number | null;
  armCm?: number | null;
  thighCm?: number | null;
  notes?: string | null;
}

export interface LiftGoalInput {
  exerciseName: string;
  targetWeightKg: number;
  targetReps?: number | null;
  hevyExerciseTemplateId?: string | null;
  targetDate?: string | null;
  notes?: string | null;
  achieved?: boolean | null;
}

export type MealStatus = "Pending" | "Proposed" | "Confirmed";
export type MealType = "breakfast" | "lunch" | "snack" | "dinner" | "other";

export interface MealItemDto {
  name: string;
  quantity: string | null;
  calories: number;
  proteinG: number | null;
  carbsG: number | null;
  fatG: number | null;
}

export interface MealDto {
  id: number;
  date: string;
  time: string | null;
  mealType: MealType | null;
  note: string | null;
  hasPhoto: boolean;
  status: MealStatus;
  items: MealItemDto[];
  totalCalories: number;
  proteinG: number | null;
  carbsG: number | null;
  fatG: number | null;
  analysisNotes: string | null;
  createdAt: string;
  analyzedAt: string | null;
  confirmedAt: string | null;
}

export interface MealCreateInput {
  date: string;
  time?: string | null;
  mealType?: MealType | null;
  note?: string | null;
  /** Foto ya reducida, en base64 sin prefijo `data:`. */
  photoBase64?: string | null;
  photoContentType?: string | null;
}

export interface MealConfirmInput {
  items?: MealItemDto[] | null;
  mealType?: MealType | null;
  date?: string | null;
  time?: string | null;
  note?: string | null;
}

export interface NutritionDayDto {
  date: string;
  meals: number;
  calories: number;
  proteinG: number;
  carbsG: number;
  fatG: number;
}

export interface NutritionTargetsDto {
  weightKg: number | null;
  bmr: number | null;
  maintenanceCalories: number | null;
  targetCalories: number | null;
  phase: NutritionPhase;
  note: string | null;
}

export interface NutritionSummaryDto {
  from: string;
  to: string;
  days: NutritionDayDto[];
  averageCalories: number;
  pendingMeals: number;
  proposedMeals: number;
  targets: NutritionTargetsDto;
}
