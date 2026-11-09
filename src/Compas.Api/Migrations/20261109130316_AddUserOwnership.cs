using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GoogleCalendarSelections_CalendarId",
                table: "GoogleCalendarSelections");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "WorkItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "PlanRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "GoogleCalendarSelections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "GoogleAccounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "CalendarEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "AppUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_UserId",
                table: "WorkItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_UserId",
                table: "Projects",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanRuns_UserId",
                table: "PlanRuns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GoogleCalendarSelections_UserId_CalendarId",
                table: "GoogleCalendarSelections",
                columns: new[] { "UserId", "CalendarId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleAccounts_UserId",
                table: "GoogleAccounts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_UserId",
                table: "CalendarEvents",
                column: "UserId");

            // Los datos que ya existían pasan a la primera cuenta (la del propietario, que era la única que podía entrar).
            // Si todavía no hay ninguna cuenta se quedan sin propietario (UserId 0) y los reclama la primera que se cree.
            foreach (var table in new[] { "Projects", "WorkItems", "CalendarEvents", "PlanRuns", "GoogleAccounts", "GoogleCalendarSelections" })
            {
                migrationBuilder.Sql(
                    $"UPDATE \"{table}\" SET \"UserId\" = (SELECT MIN(\"Id\") FROM \"AppUsers\") " +
                    "WHERE \"UserId\" = 0 AND EXISTS (SELECT 1 FROM \"AppUsers\");");
            }

            // Google verifica el correo: las cuentas ya vinculadas a Google quedan como verificadas.
            migrationBuilder.Sql("UPDATE \"AppUsers\" SET \"EmailVerified\" = 1 WHERE \"GoogleSubject\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_UserId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_Projects_UserId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_PlanRuns_UserId",
                table: "PlanRuns");

            migrationBuilder.DropIndex(
                name: "IX_GoogleCalendarSelections_UserId_CalendarId",
                table: "GoogleCalendarSelections");

            migrationBuilder.DropIndex(
                name: "IX_GoogleAccounts_UserId",
                table: "GoogleAccounts");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_UserId",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "PlanRuns");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "GoogleCalendarSelections");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "GoogleAccounts");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "EmailVerified",
                table: "AppUsers");

            migrationBuilder.CreateIndex(
                name: "IX_GoogleCalendarSelections_CalendarId",
                table: "GoogleCalendarSelections",
                column: "CalendarId",
                unique: true);
        }
    }
}
