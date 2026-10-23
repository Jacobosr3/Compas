-- Quita la fecha de "Próximo hito" de los proyectos: equivalente en Postgres/Supabase de la
-- migración EF "RemoveProjectTargetDate". Borra los datos de la columna.
alter table "Projects" drop column if exists "TargetDate";
