using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassScheduleApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_ScoreEntry_StudentName_And_SetNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScoreEntries_Students_StudentId",
                table: "ScoreEntries");

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "ScoreEntries",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "StudentName",
                table: "ScoreEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ScoreEntries_Students_StudentId",
                table: "ScoreEntries",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScoreEntries_Students_StudentId",
                table: "ScoreEntries");

            migrationBuilder.DropColumn(
                name: "StudentName",
                table: "ScoreEntries");

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "ScoreEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ScoreEntries_Students_StudentId",
                table: "ScoreEntries",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
