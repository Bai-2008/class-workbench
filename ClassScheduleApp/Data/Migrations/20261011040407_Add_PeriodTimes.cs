using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClassScheduleApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_PeriodTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PeriodTimes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PeriodNo = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodTimes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "PeriodTimes",
                columns: new[] { "Id", "EndTime", "PeriodNo", "StartTime" },
                values: new object[,]
                {
                    { 1, new TimeOnly(9, 15, 0), 1, new TimeOnly(8, 30, 0) },
                    { 2, new TimeOnly(10, 5, 0), 2, new TimeOnly(9, 20, 0) },
                    { 3, new TimeOnly(11, 10, 0), 3, new TimeOnly(10, 25, 0) },
                    { 4, new TimeOnly(12, 0, 0), 4, new TimeOnly(11, 15, 0) },
                    { 5, new TimeOnly(14, 45, 0), 5, new TimeOnly(14, 0, 0) },
                    { 6, new TimeOnly(15, 35, 0), 6, new TimeOnly(14, 50, 0) },
                    { 7, new TimeOnly(16, 40, 0), 7, new TimeOnly(15, 55, 0) },
                    { 8, new TimeOnly(17, 30, 0), 8, new TimeOnly(16, 45, 0) },
                    { 9, new TimeOnly(19, 15, 0), 9, new TimeOnly(18, 30, 0) },
                    { 10, new TimeOnly(20, 5, 0), 10, new TimeOnly(19, 20, 0) },
                    { 11, new TimeOnly(20, 55, 0), 11, new TimeOnly(20, 10, 0) },
                    { 12, new TimeOnly(21, 45, 0), 12, new TimeOnly(21, 0, 0) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodTimes_PeriodNo",
                table: "PeriodTimes",
                column: "PeriodNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PeriodTimes");
        }
    }
}
