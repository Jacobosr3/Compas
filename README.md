# Compás — API del Planificador Inteligente

Backend en ASP.NET Core (.NET 9) del planificador inteligente. Toda la lógica vive
en la API: el cliente (web, escritorio, móvil...) es solo una vista sobre ella.

> ⚠️ **Nota sobre este código**: se ha escrito íntegramente a mano en un entorno sin
> SDK de .NET ni acceso a NuGet, así que no ha podido compilarse ni ejecutarse aquí.
> Sigue los pasos de abajo en tu máquina y, si algo no compila, dime el error exacto
> y lo corrijo.

## Stack

- ASP.NET Core Web API (.NET 9)
- Entity Framework Core 9
- SQLite para desarrollo / uso personal
- PostgreSQL como opción para cuando exista versión online (cambio de un valor de configuración, sin tocar código)
- Swagger / OpenAPI para explorar y probar la API

## Estructura

```
Compas/
├── Compas.sln
└── src/Compas.Api/
    ├── Controllers/       # Endpoints REST
    ├── Data/               # DbContext + factoría de diseño para EF Core
    ├── Models/             # Entidades: Project, WorkItem, CalendarEvent, PlanRun, PlanEntry
    ├── Dtos/               # Contratos de entrada/salida de la API
    ├── Services/           # IPlanningEngine (el "núcleo IA"), motor heurístico, PlannerService
    ├── Program.cs
    └── appsettings.json
```

### El motor de planificación

`IPlanningEngine` es el punto de extensión pensado para que la IA sea el núcleo real
del producto: hoy lo implementa `HeuristicPlanningEngine` (puntuación transparente por
urgencia + prioridad + encaje del hueco disponible), pero se puede sustituir por una
implementación que llame a un LLM sin tocar controladores ni el resto del sistema —
solo hace falta registrar otra clase en `Program.cs`:

```csharp
builder.Services.AddScoped<IPlanningEngine, TuMotorConLLM>();
```

Cada vez que se genera un plan se guarda como un `PlanRun` con sus `PlanEntry`,
lo que permite comparar contra la ejecución anterior y marcar qué bloques se
`WasRescheduled` (se movieron) — la base para que la interfaz muestre "↻ Movido
desde las 15:00", como en el prototipo.

## Puesta en marcha (SQLite, desarrollo)

Requisitos: [.NET 9 SDK](https://dotnet.microsoft.com/download).

```bash
cd src/Compas.Api

# 1. Restaurar dependencias
dotnet restore

# 2. Instalar la herramienta de EF Core (una sola vez, global)
dotnet tool install --global dotnet-ef

# 3. Crear la migración inicial a partir de los modelos
dotnet ef migrations add InitialCreate

# 4. Crear/actualizar la base de datos SQLite (compas.db)
dotnet ef database update

# 5. Arrancar la API
dotnet run
```

La API quedará en algo como `https://localhost:5001` (revisa la consola para el
puerto exacto) y Swagger en `https://localhost:5001/swagger`.

En modo `Development`, `Program.cs` aplica las migraciones pendientes
automáticamente al arrancar, así que el paso 4 es opcional una vez tengas
migraciones creadas.

## Cambiar a PostgreSQL

Cuando tengas una instancia de PostgreSQL (local o en la nube):

1. Edita `appsettings.json` (o mejor, `appsettings.Production.json` / variables de entorno):
   ```json
   "Database": { "Provider": "Postgres" },
   "ConnectionStrings": {
     "Postgres": "Host=tu-host;Port=5432;Database=compas;Username=...;Password=..."
   }
   ```
2. Genera las migraciones para Postgres si aún no existen o si difieren de las de SQLite
   (EF Core puede necesitar migraciones separadas por proveedor si usas tipos de datos
   muy específicos de cada motor; con el modelo actual no debería hacer falta).
3. `dotnet ef database update` contra esa cadena de conexión.

No hace falta tocar nada del código de dominio ni de los controladores: el único
punto de cambio es esa opción `Database:Provider`.

## Frontend conectado

Este backend viene acompañado de `compas.html` (fuera de esta carpeta, en la
entrega del chat): un cliente estático en HTML/JS puro que consume la API por
`fetch` — `/api/planner/today`, `/api/projects`, `/api/workitems/inbox`, etc.
No requiere Node ni build: se abre el archivo directamente en el navegador.

Para que funcione:

1. Arranca el backend (`dotnet run` dentro de `src/Compas.Api`). Con el
   `launchSettings.json` incluido, quedará en **`http://localhost:5080`**
   (HTTP, no HTTPS, para evitar la fricción de aceptar certificados de
   desarrollo solo para probar el frontend).
2. Abre `compas.html` en el navegador.
3. Si tu backend corre en otra URL o puerto, pulsa el icono de engranaje (⚙️)
   en la esquina superior derecha del frontend e introduce la URL correcta
   (se guarda en `localStorage`, así que solo hace falta la primera vez).
4. Si ves el aviso "Sin conexión con la API", comprueba que el backend está
   arrancado y que CORS no está bloqueando la petición (por defecto el backend
   permite cualquier origen en desarrollo, ver `Program.cs`).

Qué hace el frontend con datos reales:

- **Panel "Ahora mismo"**: usa `nowEntry` de `GET /api/planner/today` — título,
  motivo (`reason`), prioridad, y el anillo de tiempo restante calculado a partir
  de `start`/`end` reales.
- **Botón "Empezar ahora"**: hace `PUT /api/workitems/{id}` cambiando el estado a `InProgress`.
- **Botón "Ver otra sugerencia"**: llama a `POST /api/planner/replan`.
- **"Generar plan del día"**: llama a `POST /api/planner/generate`.
- **Línea de tiempo**: renderiza los `entries` del plan (tareas, eventos fijos,
  descansos y huecos libres) en orden, con la etiqueta "↻ Reordenado" cuando
  `wasRescheduled` es `true`.
- **Proyectos**: `GET /api/projects` con su progreso real.
- **Bandeja**: `GET /api/workitems/inbox`, con un campo para crear tareas nuevas (`POST /api/workitems`).
- **Mini calendario**: se genera con el mes real desde JavaScript (todavía no
  marca días con eventos — eso necesitaría un endpoint de calendario por rango
  de mes, que no existe aún).

## Endpoints principales

| Método | Ruta                          | Descripción                                              |
|--------|-------------------------------|-----------------------------------------------------------|
| GET    | `/api/projects`               | Lista proyectos con progreso calculado                    |
| POST   | `/api/projects`                | Crea un proyecto                                          |
| PUT    | `/api/projects/{id}`           | Actualiza un proyecto                                     |
| DELETE | `/api/projects/{id}`           | Elimina un proyecto                                        |
| GET    | `/api/workitems?status=Pending`| Lista tareas, opcionalmente filtradas por estado          |
| GET    | `/api/workitems/inbox`         | Tareas sin programar (la "bandeja")                        |
| POST   | `/api/workitems`                | Crea una tarea                                             |
| PUT    | `/api/workitems/{id}`           | Actualiza una tarea (incl. marcarla completada)            |
| GET    | `/api/calendarevents?date=...`  | Eventos fijos de un día                                     |
| POST   | `/api/calendarevents`           | Crea un evento fijo                                         |
| GET    | `/api/planner/today`           | Plan de hoy (lo genera si no existe) + bloque "ahora"       |
| POST   | `/api/planner/generate?date=...`| Regenera el plan completo de una fecha                    |
| POST   | `/api/planner/replan`          | Replanifica el resto del día de hoy a partir de este instante |

## Siguientes pasos sugeridos

- Autenticación (hoy la API asume un único usuario / uso personal).
- Persistir preferencias de jornada (horario laboral, duración de descansos) por usuario en vez de en `appsettings.json`.
- Sustituir `HeuristicPlanningEngine` por un motor que llame a un LLM, manteniendo la misma interfaz.
- Sincronización real de calendario (Google Calendar / Outlook) como nuevo `EventSource`.
