-- Orden de las subtareas: equivalente en Postgres/Supabase de la migración EF "AddSubtaskOrder".
-- Las subtareas existentes quedan con 0 y se siguen mostrando por Id hasta que se reordenen.
alter table "WorkItems" add column if not exists "SortOrder" integer not null default 0;
