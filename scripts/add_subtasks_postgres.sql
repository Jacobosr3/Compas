-- Subtareas: equivalente en Postgres/Supabase de la migración EF "AddSubtasks".
alter table "WorkItems" add column if not exists "ParentId" integer null;
create index if not exists "IX_WorkItems_ParentId" on "WorkItems" ("ParentId");
alter table "WorkItems"
  add constraint "FK_WorkItems_WorkItems_ParentId"
  foreign key ("ParentId") references "WorkItems" ("Id") on delete cascade;
