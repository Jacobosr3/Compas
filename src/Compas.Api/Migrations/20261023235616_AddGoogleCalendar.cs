using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalCalendarId",
                table: "CalendarEvents",
                type: "TEXT",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "CalendarEvents",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GoogleAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    RefreshToken = table.Column<string>(type: "TEXT", nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncError = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GoogleCalendarSelections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CalendarId = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ColorHex = table.Column<string>(type: "TEXT", maxLength: 9, nullable: true),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    Selected = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleCalendarSelections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_ExternalCalendarId_ExternalId",
                table: "CalendarEvents",
                columns: new[] { "ExternalCalendarId", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleCalendarSelections_CalendarId",
                table: "GoogleCalendarSelections",
                column: "CalendarId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoogleAccounts");

            migrationBuilder.DropTable(
                name: "GoogleCalendarSelections");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_ExternalCalendarId_ExternalId",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "ExternalCalendarId",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "CalendarEvents");
        }
    }
}
