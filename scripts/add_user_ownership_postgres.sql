-- Datos por usuario: equivalente en Postgres/Supabase de la migración EF "AddUserOwnership".
-- Cada proyecto, tarea, evento, plan y la conexión con Google pasan a pertenecer a un usuario (UserId).
-- Los datos que ya existían se asignan a la primera cuenta (la del propietario, que era la única que podía entrar).
-- Si todavía no hay ninguna cuenta quedan con UserId 0 (sin propietario) y los reclama la primera que se cree.
-- Es idempotente: se puede volver a ejecutar sin duplicar nada.

alter table "Projects" add column if not exists "UserId" integer not null default 0;
alter table "WorkItems" add column if not exists "UserId" integer not null default 0;
alter table "CalendarEvents" add column if not exists "UserId" integer not null default 0;
alter table "PlanRuns" add column if not exists "UserId" integer not null default 0;
alter table "GoogleAccounts" add column if not exists "UserId" integer not null default 0;
alter table "GoogleCalendarSelections" add column if not exists "UserId" integer not null default 0;
alter table "AppUsers" add column if not exists "EmailVerified" boolean not null default false;

update "Projects" set "UserId" = (select min("Id") from "AppUsers") where "UserId" = 0 and exists (select 1 from "AppUsers");
update "WorkItems" set "UserId" = (select min("Id") from "AppUsers") where "UserId" = 0 and exists (select 1 from "AppUsers");
update "CalendarEvents" set "UserId" = (select min("Id") from "AppUsers") where "UserId" = 0 and exists (select 1 from "AppUsers");
update "PlanRuns" set "UserId" = (select min("Id") from "AppUsers") where "UserId" = 0 and exists (select 1 from "AppUsers");
update "GoogleAccounts" set "UserId" = (select min("Id") from "AppUsers") where "UserId" = 0 and exists (select 1 from "AppUsers");
update "GoogleCalendarSelections" set "UserId" = (select min("Id") from "AppUsers") where "UserId" = 0 and exists (select 1 from "AppUsers");

-- Google verifica el correo: las cuentas ya vinculadas a Google quedan como verificadas.
update "AppUsers" set "EmailVerified" = true where "GoogleSubject" is not null;

create index if not exists "IX_Projects_UserId" on "Projects" ("UserId");
create index if not exists "IX_WorkItems_UserId" on "WorkItems" ("UserId");
create index if not exists "IX_CalendarEvents_UserId" on "CalendarEvents" ("UserId");
create index if not exists "IX_PlanRuns_UserId" on "PlanRuns" ("UserId");
create index if not exists "IX_GoogleAccounts_UserId" on "GoogleAccounts" ("UserId");

-- Cada usuario tiene su propia lista de calendarios: la unicidad pasa a ser por (usuario, calendario).
drop index if exists "IX_GoogleCalendarSelections_CalendarId";
create unique index if not exists "IX_GoogleCalendarSelections_UserId_CalendarId"
  on "GoogleCalendarSelections" ("UserId", "CalendarId");
