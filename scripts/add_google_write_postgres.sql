-- Google Calendar de lectura y escritura: equivalente en Postgres/Supabase de la migración EF "AddGoogleWrite".
-- CanWrite: la cuenta dio permiso para crear/editar eventos. DefaultCalendarId: calendario donde se crean los
-- eventos nuevos de Compás. CanEdit: el usuario puede editar ese calendario (propietario o editor).
alter table "GoogleAccounts" add column if not exists "CanWrite" boolean not null default false;
alter table "GoogleAccounts" add column if not exists "DefaultCalendarId" varchar(400) null;
alter table "GoogleCalendarSelections" add column if not exists "CanEdit" boolean not null default false;
