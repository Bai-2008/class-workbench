using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassScheduleApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_Week_Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EndWeek",
                table: "CourseSchedules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartWeek",
                table: "CourseSchedules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // ── 数据回填：把既有的 WeekRange 文本解析成 StartWeek / EndWeek ──
            // 规则："X-Y" → (X, Y)；"X" → (X, X)；null / 空 / 无法解析 → (1, 20)。
            // SQLite 的 CAST('abc' AS INTEGER) 得 0，因此下面第二步统一把 0、越界、起止颠倒的行回退为 (1, 20)。
            migrationBuilder.Sql(
                """
                UPDATE "CourseSchedules"
                SET "StartWeek" = CAST(
                        CASE
                            WHEN INSTR(TRIM(COALESCE("WeekRange", '')), '-') > 0
                                THEN TRIM(SUBSTR(TRIM("WeekRange"), 1, INSTR(TRIM("WeekRange"), '-') - 1))
                            ELSE TRIM(COALESCE("WeekRange", ''))
                        END AS INTEGER),
                    "EndWeek" = CAST(
                        CASE
                            WHEN INSTR(TRIM(COALESCE("WeekRange", '')), '-') > 0
                                THEN TRIM(SUBSTR(TRIM("WeekRange"), INSTR(TRIM("WeekRange"), '-') + 1))
                            ELSE TRIM(COALESCE("WeekRange", ''))
                        END AS INTEGER);
                """);

            migrationBuilder.Sql(
                """
                UPDATE "CourseSchedules"
                SET "StartWeek" = 1,
                    "EndWeek" = 20
                WHERE "StartWeek" < 1
                   OR "StartWeek" > 25
                   OR "EndWeek" < 1
                   OR "EndWeek" > 25
                   OR "StartWeek" > "EndWeek";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndWeek",
                table: "CourseSchedules");

            migrationBuilder.DropColumn(
                name: "StartWeek",
                table: "CourseSchedules");
        }
    }
}
