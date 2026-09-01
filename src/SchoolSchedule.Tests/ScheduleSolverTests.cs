using SchoolSchedule.Core.Models;
using SchoolSchedule.Scheduling;

namespace SchoolSchedule.Tests;

/// <summary>
/// Солвер работает на чистых POCO (не ходит в базу сам), поэтому тесты строят ScheduleInput
/// вручную — без SQLite. Время солвера намеренно небольшое (пара секунд), т.к. все сценарии тут
/// маленькие и либо решаются почти мгновенно, либо заведомо неразрешимы.
/// </summary>
public class ScheduleSolverTests
{
    private static readonly TimeSpan ShortLimit = TimeSpan.FromSeconds(5);

    private static RoomType OrdinaryType(int id = 1) => new() { Id = id, Name = "Обычный" };

    /// <summary>
    /// TimeSlotGenerator сам не проставляет Id — это делает EF при сохранении новых сущностей
    /// в базу. В тестах эмулируем это вручную, иначе все слоты остались бы с Id=0, и по итоговому
    /// GeneratedLesson.TimeSlotId было бы невозможно (да и не нужно солверу) отличить один слот
    /// от другого — солвер сам работает по позиции в списке, а не по Id.
    /// </summary>
    private static List<TimeSlot> GenerateSlotsWithIds(ScheduleSettings settings)
    {
        var slots = TimeSlotGenerator.Generate(settings);
        for (var i = 0; i < slots.Count; i++)
            slots[i].Id = i + 1;
        return slots;
    }

    private static Room MakeRoom(int id, RoomType type, params Teacher[] pinnedTo)
    {
        var room = new Room { Id = id, Name = $"Каб.{id}", Capacity = 30, RoomTypeId = type.Id, RoomType = type };
        room.AssignedTeachers = pinnedTo.Select(t => new RoomTeacherAssignment { RoomId = id, TeacherId = t.Id, Teacher = t }).ToList();
        return room;
    }

    private static ClassSubjectGroup MakeGroup(int id, SchoolClass cls, Subject subject, Teacher? teacher, int lessonsPerWeek, string? groupLabel = null) => new()
    {
        Id = id,
        ClassId = cls.Id,
        Class = cls,
        SubjectId = subject.Id,
        Subject = subject,
        TeacherId = teacher?.Id,
        Teacher = teacher,
        LessonsPerWeek = lessonsPerWeek,
        GroupLabel = groupLabel,
    };

    [Fact]
    public void Simple_feasible_case_schedules_every_lesson_without_conflicts()
    {
        var classA = new SchoolClass { Id = 1, Name = "5А", Grade = 5, Shift = Shift.Первая, StudentCount = 25 };
        var math = new Subject { Id = 1, Name = "Математика" };
        var russian = new Subject { Id = 2, Name = "Русский язык" };
        var teacher1 = new Teacher { Id = 1, FullName = "Иванова И.И." };
        var teacher2 = new Teacher { Id = 2, FullName = "Петрова П.П." };
        var ordinary = OrdinaryType();
        var room = MakeRoom(1, ordinary);

        var settings = new ScheduleSettings { DaysPerWeek = 5, PeriodsPerDayShift1 = 6 };
        var timeSlots = GenerateSlotsWithIds(settings);

        var input = new ScheduleInput
        {
            Rooms = [room],
            TimeSlots = timeSlots,
            Groups =
            [
                MakeGroup(1, classA, math, teacher1, 3),
                MakeGroup(2, classA, russian, teacher2, 2),
            ],
            Unavailabilities = [],
            TimeLimit = ShortLimit,
        };

        var result = new ScheduleSolver().Generate(input);

        Assert.Equal(ScheduleGenerationStatus.Success, result.Status);
        Assert.Empty(result.Warnings);
        Assert.Equal(5, result.Lessons.Count);

        // Класс не занят дважды в один слот.
        var slotsUsed = result.Lessons.Select(l => l.TimeSlotId).ToList();
        Assert.Equal(slotsUsed.Count, slotsUsed.Distinct().Count());
    }

    [Fact]
    public void Subject_with_required_room_type_only_uses_a_matching_room()
    {
        var classA = new SchoolClass { Id = 1, Name = "5А", Grade = 5, Shift = Shift.Первая, StudentCount = 25 };
        var ordinary = OrdinaryType(1);
        var gymType = new RoomType { Id = 2, Name = "Спортзал" };
        var pe = new Subject { Id = 1, Name = "Физкультура", RequiredRoomTypeId = gymType.Id, RequiredRoomType = gymType };
        var teacher = new Teacher { Id = 1, FullName = "Сидоров С.С." };

        var ordinaryRoom = MakeRoom(1, ordinary);
        var gym = MakeRoom(2, gymType);

        var settings = new ScheduleSettings { DaysPerWeek = 5, PeriodsPerDayShift1 = 6 };
        var input = new ScheduleInput
        {
            Rooms = [ordinaryRoom, gym],
            TimeSlots = GenerateSlotsWithIds(settings),
            Groups = [MakeGroup(1, classA, pe, teacher, 2)],
            Unavailabilities = [],
            TimeLimit = ShortLimit,
        };

        var result = new ScheduleSolver().Generate(input);

        Assert.Equal(ScheduleGenerationStatus.Success, result.Status);
        Assert.All(result.Lessons, l => Assert.Equal(gym.Id, l.RoomId));
    }

    [Fact]
    public void Teacher_unavailability_window_is_never_scheduled_into()
    {
        var classA = new SchoolClass { Id = 1, Name = "5А", Grade = 5, Shift = Shift.Первая, StudentCount = 25 };
        var math = new Subject { Id = 1, Name = "Математика" };
        var teacher = new Teacher { Id = 1, FullName = "Иванова И.И." };
        var ordinary = OrdinaryType();
        var room = MakeRoom(1, ordinary);

        // Единственный слот смены 1, понедельник, 1-й урок — недоступен учителю.
        var settings = new ScheduleSettings { DaysPerWeek = 1, PeriodsPerDayShift1 = 1 };
        var timeSlots = GenerateSlotsWithIds(settings);

        var input = new ScheduleInput
        {
            Rooms = [room],
            TimeSlots = timeSlots,
            Groups = [MakeGroup(1, classA, math, teacher, 1)],
            Unavailabilities = [new TeacherUnavailability { TeacherId = teacher.Id, Day = SchoolDay.Понедельник, PeriodNumber = 1 }],
            TimeLimit = ShortLimit,
        };

        var result = new ScheduleSolver().Generate(input);

        // Единственный слот заблокирован недоступностью — расставить урок некуда.
        Assert.Equal(ScheduleGenerationStatus.NoData, result.Status);
        Assert.Contains(result.Warnings, w => w.Contains("нет ни одного урочного слота"));
    }

    [Fact]
    public void Room_pinned_to_a_teacher_is_not_used_by_a_different_teacher()
    {
        var classA = new SchoolClass { Id = 1, Name = "5А", Grade = 5, Shift = Shift.Первая, StudentCount = 25 };
        var subject = new Subject { Id = 1, Name = "Информатика" };
        var pinnedTeacher = new Teacher { Id = 1, FullName = "Закреплённый" };
        var otherTeacher = new Teacher { Id = 2, FullName = "Другой" };
        var ordinary = OrdinaryType();

        var pinnedRoom = MakeRoom(1, ordinary, pinnedTeacher); // закреплён только за pinnedTeacher
        var openRoom = MakeRoom(2, ordinary); // ни за кем не закреплён — открыт всем

        var settings = new ScheduleSettings { DaysPerWeek = 5, PeriodsPerDayShift1 = 6 };
        var input = new ScheduleInput
        {
            Rooms = [pinnedRoom, openRoom],
            TimeSlots = GenerateSlotsWithIds(settings),
            Groups = [MakeGroup(1, classA, subject, otherTeacher, 2)],
            Unavailabilities = [],
            TimeLimit = ShortLimit,
        };

        var result = new ScheduleSolver().Generate(input);

        Assert.Equal(ScheduleGenerationStatus.Success, result.Status);
        Assert.All(result.Lessons, l => Assert.Equal(openRoom.Id, l.RoomId));
    }

    [Fact]
    public void Subgroups_of_the_same_subject_share_the_time_slot_but_get_different_teachers_and_rooms()
    {
        var classA = new SchoolClass { Id = 1, Name = "7А", Grade = 7, Shift = Shift.Первая, StudentCount = 26 };
        var english = new Subject { Id = 1, Name = "Английский язык" };
        var teacher1 = new Teacher { Id = 1, FullName = "Группа 1" };
        var teacher2 = new Teacher { Id = 2, FullName = "Группа 2" };
        var ordinary = OrdinaryType();
        var room1 = MakeRoom(1, ordinary);
        var room2 = MakeRoom(2, ordinary);

        var settings = new ScheduleSettings { DaysPerWeek = 5, PeriodsPerDayShift1 = 6 };
        var input = new ScheduleInput
        {
            Rooms = [room1, room2],
            TimeSlots = GenerateSlotsWithIds(settings),
            Groups =
            [
                MakeGroup(1, classA, english, teacher1, 3, "Группа 1"),
                MakeGroup(2, classA, english, teacher2, 3, "Группа 2"),
            ],
            Unavailabilities = [],
            TimeLimit = ShortLimit,
        };

        var result = new ScheduleSolver().Generate(input);

        Assert.Equal(ScheduleGenerationStatus.Success, result.Status);
        Assert.Equal(6, result.Lessons.Count);

        var byGroup1 = result.Lessons.Where(l => l.ClassSubjectGroupId == 1).OrderBy(l => l.TimeSlotId).ToList();
        var byGroup2 = result.Lessons.Where(l => l.ClassSubjectGroupId == 2).OrderBy(l => l.TimeSlotId).ToList();
        Assert.Equal(3, byGroup1.Count);
        Assert.Equal(3, byGroup2.Count);

        // Подгруппы идут в один и тот же набор слотов (параллельно)...
        Assert.Equal(byGroup1.Select(l => l.TimeSlotId), byGroup2.Select(l => l.TimeSlotId));
        // ...но в каждом слоте — разные кабинеты (разные физические помещения одновременно).
        for (var i = 0; i < 3; i++)
            Assert.NotEqual(byGroup1[i].RoomId, byGroup2[i].RoomId);
    }

    [Fact]
    public void Not_enough_rooms_for_simultaneous_classes_is_reported_as_infeasible()
    {
        var classA = new SchoolClass { Id = 1, Name = "5А", Grade = 5, Shift = Shift.Первая, StudentCount = 25 };
        var classB = new SchoolClass { Id = 2, Name = "5Б", Grade = 5, Shift = Shift.Первая, StudentCount = 24 };
        var math = new Subject { Id = 1, Name = "Математика" };
        var teacher1 = new Teacher { Id = 1, FullName = "Иванова И.И." };
        var teacher2 = new Teacher { Id = 2, FullName = "Петрова П.П." };
        var ordinary = OrdinaryType();
        var room = MakeRoom(1, ordinary); // единственный кабинет на двоих одновременно

        // Единственный слот на всю неделю — обоим классам просто некуда встать порознь.
        var settings = new ScheduleSettings { DaysPerWeek = 1, PeriodsPerDayShift1 = 1 };
        var input = new ScheduleInput
        {
            Rooms = [room],
            TimeSlots = GenerateSlotsWithIds(settings),
            Groups =
            [
                MakeGroup(1, classA, math, teacher1, 1),
                MakeGroup(2, classB, math, teacher2, 1),
            ],
            Unavailabilities = [],
            TimeLimit = ShortLimit,
        };

        var result = new ScheduleSolver().Generate(input);

        Assert.Equal(ScheduleGenerationStatus.Infeasible, result.Status);
    }

    [Fact]
    public void Group_without_an_assigned_teacher_is_skipped_and_reported_as_a_warning()
    {
        var classA = new SchoolClass { Id = 1, Name = "5А", Grade = 5, Shift = Shift.Первая, StudentCount = 25 };
        var math = new Subject { Id = 1, Name = "Математика" };
        var russian = new Subject { Id = 2, Name = "Русский язык" };
        var teacher = new Teacher { Id = 1, FullName = "Иванова И.И." };
        var ordinary = OrdinaryType();
        var room = MakeRoom(1, ordinary);

        var settings = new ScheduleSettings { DaysPerWeek = 5, PeriodsPerDayShift1 = 6 };
        var input = new ScheduleInput
        {
            Rooms = [room],
            TimeSlots = GenerateSlotsWithIds(settings),
            Groups =
            [
                MakeGroup(1, classA, math, teacher, 2),
                MakeGroup(2, classA, russian, null, 2), // учитель не назначен
            ],
            Unavailabilities = [],
            TimeLimit = ShortLimit,
        };

        var result = new ScheduleSolver().Generate(input);

        Assert.Equal(ScheduleGenerationStatus.PartialSuccess, result.Status);
        Assert.Equal(2, result.Lessons.Count);
        Assert.Single(result.Warnings);
        Assert.Contains("не назначен учитель", result.Warnings[0]);
    }

    [Fact]
    public void No_rooms_at_all_returns_no_data_status()
    {
        var classA = new SchoolClass { Id = 1, Name = "5А", Grade = 5, Shift = Shift.Первая, StudentCount = 25 };
        var math = new Subject { Id = 1, Name = "Математика" };
        var teacher = new Teacher { Id = 1, FullName = "Иванова И.И." };

        var settings = new ScheduleSettings { DaysPerWeek = 5, PeriodsPerDayShift1 = 6 };
        var input = new ScheduleInput
        {
            Rooms = [],
            TimeSlots = GenerateSlotsWithIds(settings),
            Groups = [MakeGroup(1, classA, math, teacher, 2)],
            Unavailabilities = [],
            TimeLimit = ShortLimit,
        };

        var result = new ScheduleSolver().Generate(input);

        Assert.Equal(ScheduleGenerationStatus.NoData, result.Status);
    }
}
