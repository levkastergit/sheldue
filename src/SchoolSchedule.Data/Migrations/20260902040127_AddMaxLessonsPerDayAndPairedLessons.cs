using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolSchedule.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxLessonsPerDayAndPairedLessons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxLessonsPerDay",
                table: "ClassSubjectGroups",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "PairedLessons",
                table: "ClassSubjectGroups",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxLessonsPerDay",
                table: "ClassSubjectGroups");

            migrationBuilder.DropColumn(
                name: "PairedLessons",
                table: "ClassSubjectGroups");
        }
    }
}
