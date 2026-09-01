using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolSchedule.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixClassSubjectGroupUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClassSubjectGroups_ClassId_SubjectId_GroupLabel",
                table: "ClassSubjectGroups");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjectGroups_ClassId_SubjectId",
                table: "ClassSubjectGroups",
                columns: new[] { "ClassId", "SubjectId" },
                unique: true,
                filter: "\"GroupLabel\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjectGroups_ClassId_SubjectId_GroupLabel",
                table: "ClassSubjectGroups",
                columns: new[] { "ClassId", "SubjectId", "GroupLabel" },
                unique: true,
                filter: "\"GroupLabel\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClassSubjectGroups_ClassId_SubjectId",
                table: "ClassSubjectGroups");

            migrationBuilder.DropIndex(
                name: "IX_ClassSubjectGroups_ClassId_SubjectId_GroupLabel",
                table: "ClassSubjectGroups");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjectGroups_ClassId_SubjectId_GroupLabel",
                table: "ClassSubjectGroups",
                columns: new[] { "ClassId", "SubjectId", "GroupLabel" },
                unique: true);
        }
    }
}
