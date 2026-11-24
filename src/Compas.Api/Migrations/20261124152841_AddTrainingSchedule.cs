using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CalendarEventId",
                table: "WorkoutSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "WorkoutSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartTime",
                table: "WorkoutSessions",
                type: "TEXT",
                maxLength: 5,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExerciseRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExerciseName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RepMin = table.Column<int>(type: "INTEGER", nullable: false),
                    RepMax = table.Column<int>(type: "INTEGER", nullable: false),
                    IncrementKg = table.Column<double>(type: "REAL", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainingSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    DayOfWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSlots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseRules_UserId",
                table: "ExerciseRules",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSlots_UserId_DayOfWeek",
                table: "TrainingSlots",
                columns: new[] { "UserId", "DayOfWeek" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExerciseRules");

            migrationBuilder.DropTable(
                name: "TrainingSlots");

            migrationBuilder.DropColumn(
                name: "CalendarEventId",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "WorkoutSessions");
        }
    }
}
