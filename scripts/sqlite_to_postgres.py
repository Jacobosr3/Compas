"""Genera un script SQL con los datos de compas.db (SQLite) para cargarlos en Postgres/Supabase.

Uso (desde la raíz del repo):
    python scripts/sqlite_to_postgres.py                 # escribe scripts/compas_data.sql
    python scripts/sqlite_to_postgres.py ruta/compas.db salida.sql

- Conserva los ids originales y es idempotente: si se ejecuta dos veces no duplica filas
  (INSERT ... ON CONFLICT DO NOTHING).
- Al final reajusta las secuencias de los ids para que las filas nuevas no choquen.
- Las tablas deben existir ya en Postgres (esquema inicial de Supabase).
"""
import sqlite3
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DB = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "src" / "Compas.Api" / "compas.db"
OUT = Path(sys.argv[2]) if len(sys.argv) > 2 else ROOT / "scripts" / "compas_data.sql"

# Orden de carga: primero las tablas de las que dependen las demás (claves foráneas).
TABLES = ["Projects", "CalendarEvents", "WorkItems", "PlanRuns", "PlanEntries"]
BOOLEAN_COLUMNS = {"IsAllDay", "WasRescheduled"}


def literal(column: str, value) -> str:
    if value is None:
        return "NULL"
    if column in BOOLEAN_COLUMNS:
        return "true" if value else "false"
    if isinstance(value, (int, float)):
        return str(value)
    return "'" + str(value).replace("'", "''") + "'"


def main() -> None:
    if not DB.exists():
        sys.exit(f"No se encuentra la base de datos SQLite: {DB}")

    con = sqlite3.connect(DB)
    lines = ["-- Datos exportados de compas.db", "begin;"]
    total = 0

    for table in TABLES:
        cur = con.execute(f'select * from "{table}" order by "Id"')
        columns = [d[0] for d in cur.description]
        cols_sql = ", ".join(f'"{c}"' for c in columns)
        rows = cur.fetchall()
        total += len(rows)
        for row in rows:
            values = ", ".join(literal(c, v) for c, v in zip(columns, row))
            lines.append(f'insert into "{table}" ({cols_sql}) overriding system value values ({values}) on conflict ("Id") do nothing;')
        print(f"{table}: {len(rows)} filas")

    for table in TABLES:
        lines.append(
            f"select setval(pg_get_serial_sequence('\"{table}\"', 'Id'), "
            f"coalesce((select max(\"Id\") from \"{table}\"), 0) + 1, false);"
        )

    lines.append("commit;")
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"\n{total} filas -> {OUT}")


if __name__ == "__main__":
    main()
