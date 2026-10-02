# Compás — Frontend (React + TypeScript + Vite)

Cliente web de Compás, separado del backend, que consume la API por `fetch`.
Cuatro pantallas conectadas a datos reales:

- **Hoy** — panel "Ahora mismo", línea de tiempo del día, resumen de proyectos y bandeja.
- **Proyectos** — listado con progreso real, crear / editar / eliminar.
- **Agenda** — eventos fijos de calendario por día, crear / eliminar.
- **Bandeja** — todas las tareas, filtro por estado, crear / editar / completar / eliminar.

> ✅ Este proyecto **sí se ha compilado y probado** en el entorno donde se generó
> (`npm run build` sin errores, servidor de desarrollo arrancado y verificado).
> A diferencia del backend en .NET, aquí sí hay acceso a npm/Node.

## Puesta en marcha

Requisitos: [Node.js](https://nodejs.org) 20+.

```bash
npm install
npm run dev
```

Se abrirá en `http://localhost:5173` (o el puerto que indique la terminal).

## Conectar con el backend

Por defecto apunta a `http://localhost:5080` (el puerto fijado en el
`launchSettings.json` del backend). Dos formas de cambiarlo:

1. **En caliente**: icono ⚙️ arriba a la derecha en cualquier pantalla → escribe la URL → Guardar. Se recuerda entre sesiones (`localStorage`).
2. **En build time**: copia `.env.example` a `.env` y ajusta `VITE_API_BASE_URL`.

Recuerda arrancar primero el backend (`dotnet run` en `src/Compas.Api`) — si no,
verás el aviso "Sin conexión con la API" en la barra superior de cada pantalla.

## Estructura

```
src/
├── api/
│   ├── types.ts       # Tipos que reflejan los DTOs del backend
│   ├── client.ts      # fetch con manejo de URL base y errores
│   └── index.ts       # Funciones tipadas por endpoint (projectsApi, workItemsApi...)
├── components/        # Sidebar, Topbar, ConnectionBanner (compartidos)
├── hooks/
│   └── useApiData.ts  # Hook de carga + estado (loading/ok/error) + reload()
├── pages/
│   ├── TodayPage.tsx
│   ├── ProjectsPage.tsx
│   ├── AgendaPage.tsx
│   └── BandejaPage.tsx
├── styles/
│   ├── tokens.css     # Colores, tipografía (mismos tokens que el prototipo HTML)
│   └── global.css     # Layout, tarjetas, botones, formularios
├── utils/format.ts    # Formato de fechas/duraciones y traducciones de enums
├── App.tsx            # Rutas (react-router-dom)
└── main.tsx
```

## Compilar para producción

```bash
npm run build
```

Genera `dist/` con HTML/CSS/JS estáticos, listos para servir desde cualquier
sitio (o desde `wwwroot/` del backend, si prefieres servirlo todo junto — copia
el contenido de `dist/` ahí en vez del `compas.html` suelto).

## Notas / limitaciones actuales

- El mini-calendario del prototipo HTML original no se incluyó aquí como
  componente propio; la vista **Agenda** lo sustituye con un selector de fecha
  real conectado a `/api/calendarevents`.
- No hay autenticación (asume el mismo backend de un único usuario).
- Los formularios validan lo mínimo (título obligatorio, fin > inicio en
  eventos); el backend es la fuente de verdad para el resto de reglas.
