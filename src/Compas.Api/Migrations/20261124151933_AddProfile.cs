using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BodyMeasurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WeightKg = table.Column<double>(type: "REAL", nullable: true),
                    BodyFatPct = table.Column<double>(type: "REAL", nullable: true),
                    WaistCm = table.Column<double>(type: "REAL", nullable: true),
                    ChestCm = table.Column<double>(type: "REAL", nullable: true),
                    HipCm = table.Column<double>(type: "REAL", nullable: true),
                    ArmCm = table.Column<double>(type: "REAL", nullable: true),
                    ThighCm = table.Column<double>(type: "REAL", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BodyMeasurements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiftGoals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExerciseName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    HevyExerciseTemplateId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TargetWeightKg = table.Column<double>(type: "REAL", nullable: false),
                    TargetReps = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Achieved = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiftGoals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    HeightCm = table.Column<double>(type: "REAL", nullable: true),
                    BirthDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Sex = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    ActivityLevel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Phase = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PhaseStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TargetWeightKg = table.Column<double>(type: "REAL", nullable: true),
                    TargetDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BodyMeasurements_UserId_Date",
                table: "BodyMeasurements",
                columns: new[] { "UserId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiftGoals_UserId",
                table: "LiftGoals",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UserId",
                table: "UserProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BodyMeasurements");

            migrationBuilder.DropTable(
                name: "LiftGoals");

            migrationBuilder.DropTable(
                name: "UserProfiles");
        }
    }
}
