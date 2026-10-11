using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassScheduleApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_AttendanceCourseName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CourseName",
                table: "Attendances",
                type: "TEXT",
                nullable: true);

            // 存量回填：按当前课程名补齐历史考勤的课程名快照
            migrationBuilder.Sql(
                "UPDATE Attendances SET CourseName = (SELECT c.CourseName FROM Courses c WHERE c.Id = Attendances.CourseId) " +
                "WHERE CourseName IS NULL AND CourseId IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CourseName",
                table: "Attendances");
        }
    }
}
