using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchoolSchedule.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoomTypeEntityAndTeacherCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Teachers");

            migrationBuilder.RenameColumn(
                name: "RequiredRoomType",
                table: "Subjects",
                newName: "RequiredRoomTypeId");

            migrationBuilder.RenameColumn(
                name: "RoomType",
                table: "Rooms",
                newName: "RoomTypeId");

            migrationBuilder.CreateTable(
                name: "RoomTeacherAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoomId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeacherId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomTeacherAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomTeacherAssignments_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomTeacherAssignments_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomTypes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "RoomTypes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Обычный" },
                    { 2, "Спортзал" },
                    { 3, "Лаборатория" },
                    { 4, "Компьютерный класс" },
                    { 5, "Актовый зал" },
                    { 6, "Мастерская" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_RequiredRoomTypeId",
                table: "Subjects",
                column: "RequiredRoomTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_RoomTypeId",
                table: "Rooms",
                column: "RoomTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomTeacherAssignments_RoomId_TeacherId",
                table: "RoomTeacherAssignments",
                columns: new[] { "RoomId", "TeacherId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomTeacherAssignments_TeacherId",
                table: "RoomTeacherAssignments",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomTypes_Name",
                table: "RoomTypes",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Rooms_RoomTypes_RoomTypeId",
                table: "Rooms",
                column: "RoomTypeId",
                principalTable: "RoomTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subjects_RoomTypes_RequiredRoomTypeId",
                table: "Subjects",
                column: "RequiredRoomTypeId",
                principalTable: "RoomTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_RoomTypes_RoomTypeId",
                table: "Rooms");

            migrationBuilder.DropForeignKey(
                name: "FK_Subjects_RoomTypes_RequiredRoomTypeId",
                table: "Subjects");

            migrationBuilder.DropTable(
                name: "RoomTeacherAssignments");

            migrationBuilder.DropTable(
                name: "RoomTypes");

            migrationBuilder.DropIndex(
                name: "IX_Subjects_RequiredRoomTypeId",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_RoomTypeId",
                table: "Rooms");

            migrationBuilder.RenameColumn(
                name: "RequiredRoomTypeId",
                table: "Subjects",
                newName: "RequiredRoomType");

            migrationBuilder.RenameColumn(
                name: "RoomTypeId",
                table: "Rooms",
                newName: "RoomType");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Teachers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Teachers",
                type: "TEXT",
                nullable: true);
        }
    }
}
