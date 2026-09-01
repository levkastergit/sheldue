using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolSchedule.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Capacity = table.Column<int>(type: "INTEGER", nullable: false),
                    RoomType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DaysPerWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodsPerDayShift1 = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodsPerDayShift2 = table.Column<int>(type: "INTEGER", nullable: false),
                    HasSecondShift = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RequiredRoomType = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teachers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    MaxLessonsPerWeek = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teachers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Shift = table.Column<int>(type: "INTEGER", nullable: false),
                    Day = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeSlots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolClasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Grade = table.Column<int>(type: "INTEGER", nullable: false),
                    Shift = table.Column<int>(type: "INTEGER", nullable: false),
                    StudentCount = table.Column<int>(type: "INTEGER", nullable: false),
                    HomeRoomId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolClasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolClasses_Rooms_HomeRoomId",
                        column: x => x.HomeRoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Absences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeacherId = table.Column<int>(type: "INTEGER", nullable: false),
                    DateFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DateTo = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Reason = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Absences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Absences_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeacherSubjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeacherId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubjectId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherSubjects_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeacherSubjects_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeacherUnavailabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeacherId = table.Column<int>(type: "INTEGER", nullable: false),
                    Day = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherUnavailabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherUnavailabilities_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassSubjectGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClassId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    GroupLabel = table.Column<string>(type: "TEXT", nullable: true),
                    LessonsPerWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    TeacherId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassSubjectGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassSubjectGroups_SchoolClasses_ClassId",
                        column: x => x.ClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassSubjectGroups_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSubjectGroups_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledLessons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClassSubjectGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeacherId = table.Column<int>(type: "INTEGER", nullable: false),
                    RoomId = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeSlotId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledLessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledLessons_ClassSubjectGroups_ClassSubjectGroupId",
                        column: x => x.ClassSubjectGroupId,
                        principalTable: "ClassSubjectGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduledLessons_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledLessons_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledLessons_TimeSlots_TimeSlotId",
                        column: x => x.TimeSlotId,
                        principalTable: "TimeSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Substitutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScheduledLessonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SubstituteTeacherId = table.Column<int>(type: "INTEGER", nullable: true),
                    SubstituteRoomId = table.Column<int>(type: "INTEGER", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Substitutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Substitutions_Rooms_SubstituteRoomId",
                        column: x => x.SubstituteRoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Substitutions_ScheduledLessons_ScheduledLessonId",
                        column: x => x.ScheduledLessonId,
                        principalTable: "ScheduledLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Substitutions_Teachers_SubstituteTeacherId",
                        column: x => x.SubstituteTeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Absences_TeacherId",
                table: "Absences",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjectGroups_ClassId_SubjectId_GroupLabel",
                table: "ClassSubjectGroups",
                columns: new[] { "ClassId", "SubjectId", "GroupLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjectGroups_SubjectId",
                table: "ClassSubjectGroups",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSubjectGroups_TeacherId",
                table: "ClassSubjectGroups",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_Name",
                table: "Rooms",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledLessons_ClassSubjectGroupId",
                table: "ScheduledLessons",
                column: "ClassSubjectGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledLessons_RoomId",
                table: "ScheduledLessons",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledLessons_TeacherId",
                table: "ScheduledLessons",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledLessons_TimeSlotId",
                table: "ScheduledLessons",
                column: "TimeSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolClasses_HomeRoomId",
                table: "SchoolClasses",
                column: "HomeRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolClasses_Name",
                table: "SchoolClasses",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_Name",
                table: "Subjects",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Substitutions_ScheduledLessonId_Date",
                table: "Substitutions",
                columns: new[] { "ScheduledLessonId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Substitutions_SubstituteRoomId",
                table: "Substitutions",
                column: "SubstituteRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Substitutions_SubstituteTeacherId",
                table: "Substitutions",
                column: "SubstituteTeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubjects_SubjectId",
                table: "TeacherSubjects",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubjects_TeacherId_SubjectId",
                table: "TeacherSubjects",
                columns: new[] { "TeacherId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherUnavailabilities_TeacherId",
                table: "TeacherUnavailabilities",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeSlots_Shift_Day_PeriodNumber",
                table: "TimeSlots",
                columns: new[] { "Shift", "Day", "PeriodNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Absences");

            migrationBuilder.DropTable(
                name: "ScheduleSettings");

            migrationBuilder.DropTable(
                name: "Substitutions");

            migrationBuilder.DropTable(
                name: "TeacherSubjects");

            migrationBuilder.DropTable(
                name: "TeacherUnavailabilities");

            migrationBuilder.DropTable(
                name: "ScheduledLessons");

            migrationBuilder.DropTable(
                name: "ClassSubjectGroups");

            migrationBuilder.DropTable(
                name: "TimeSlots");

            migrationBuilder.DropTable(
                name: "SchoolClasses");

            migrationBuilder.DropTable(
                name: "Subjects");

            migrationBuilder.DropTable(
                name: "Teachers");

            migrationBuilder.DropTable(
                name: "Rooms");
        }
    }
}
