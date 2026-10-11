using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassScheduleApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_ScheduleAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduleAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Semester = table.Column<string>(type: "TEXT", nullable: false),
                    AdjustmentType = table.Column<int>(type: "INTEGER", nullable: false),
                    CourseId = table.Column<int>(type: "INTEGER", nullable: true),
                    AffectedDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    StartPeriod = table.Column<int>(type: "INTEGER", nullable: true),
                    EndPeriod = table.Column<int>(type: "INTEGER", nullable: true),
                    FollowWeekday = table.Column<int>(type: "INTEGER", nullable: true),
                    NewLocation = table.Column<string>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleAdjustments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleAdjustments_CourseId",
                table: "ScheduleAdjustments",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleAdjustments_Semester_AffectedDate",
                table: "ScheduleAdjustments",
                columns: new[] { "Semester", "AffectedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleAdjustments");
        }
    }
}
