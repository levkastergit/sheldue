using SchoolSchedule.App.ViewModels;
using SchoolSchedule.Core.Models;
using SchoolSchedule.Tests.TestSupport;

namespace SchoolSchedule.Tests;

public class ScheduleViewModelTests
{
    [Fact]
    public async Task Load_creates_and_persists_default_settings_when_none_exist()
    {
        // Регрессия: у ScheduleSettings.Id раньше был C#-дефолт "= 1", из-за чего проверка
        // "ещё не сохранено" (Id == 0) никогда не срабатывала — новые настройки выглядели уже
        // сохранёнными в памяти, но строка в базе так и не появлялась.
        using var factory = new SqliteTestContextFactory();
        var vm = new ScheduleViewModel(factory, new FakeSnackbarService());

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(6, vm.DaysPerWeek);
        Assert.Equal(7, vm.PeriodsPerDayShift1);

        await using var check = factory.CreateDbContext();
        var saved = Assert.Single(check.ScheduleSettings);
        Assert.True(saved.Id > 0);
        Assert.Equal(6, saved.DaysPerWeek);
    }

    [Fact]
    public async Task Save_settings_persists_changes_and_regenerates_time_slots()
    {
        using var factory = new SqliteTestContextFactory();
        var vm = new ScheduleViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);

        vm.DaysPerWeek = 5;
        vm.PeriodsPerDayShift1 = 4;
        await vm.SaveSettingsCommand.ExecuteAsync(null);

        await using var check = factory.CreateDbContext();
        var saved = Assert.Single(check.ScheduleSettings);
        Assert.Equal(5, saved.DaysPerWeek);
        Assert.Equal(4, saved.PeriodsPerDayShift1);
        Assert.Equal(20, check.TimeSlots.Count()); // 5 дней × 4 урока
    }

    [Fact]
    public async Task Save_settings_clears_a_previously_generated_schedule()
    {
        using var factory = new SqliteTestContextFactory();
        var vm = new ScheduleViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        await SeedSchedulableCurriculumAsync(factory);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.GenerateCommand.ExecuteAsync(null);
        Assert.True(vm.HasGeneratedSchedule);

        vm.DaysPerWeek = 5;
        await vm.SaveSettingsCommand.ExecuteAsync(null);

        await using var check = factory.CreateDbContext();
        Assert.Empty(check.ScheduledLessons);
    }

    [Fact]
    public async Task Generate_end_to_end_persists_lessons_and_populates_the_class_grid()
    {
        using var factory = new SqliteTestContextFactory();
        var (classId, _, teacherId) = await SeedSchedulableCurriculumAsync(factory);

        var vm = new ScheduleViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.GenerateCommand.ExecuteAsync(null);

        Assert.True(vm.HasGeneratedSchedule);
        Assert.False(vm.StatusIsError);
        Assert.Empty(vm.Warnings);

        await using var check = factory.CreateDbContext();
        Assert.Equal(3, check.ScheduledLessons.Count());

        // Сетка отображения по умолчанию — по классам, для первого класса; хотя бы одна ячейка
        // с уроком должна появиться.
        vm.SelectedClassForView = vm.AllClasses.Single(c => c.Id == classId);
        Assert.Contains(vm.GridRows, row => row.Days.Any(d => d.SubjectName is not null));

        // Те же уроки должны быть видны и в виде "по учителям".
        vm.ViewMode = ScheduleViewMode.ByTeacher;
        vm.SelectedTeacherForView = vm.AllTeachers.Single(t => t.Id == teacherId);
        var cells = vm.GridRows.SelectMany(row => row.Days).Where(d => d.SubjectName is not null).ToList();
        Assert.Equal(3, cells.Count);
        Assert.All(cells, c =>
        {
            Assert.Equal("Математика", c.SubjectName);
            Assert.Equal("5А", c.SecondaryLine);
            Assert.Equal("101", c.RoomName);
        });

        // И в общем расписании (все классы сразу) — тот же урок в столбце "5А".
        vm.ViewMode = ScheduleViewMode.Overview;
        var overviewCells = vm.OverviewRows.SelectMany(row => row.ClassCells).Where(c => c.SubjectName is not null).ToList();
        Assert.Equal(3, overviewCells.Count);
        Assert.All(overviewCells, c =>
        {
            Assert.Equal("Математика", c.SubjectName);
            Assert.Equal("Иванова И.И.", c.SecondaryLine);
            Assert.Equal("101", c.RoomName);
        });
    }

    [Fact]
    public async Task Overview_grid_places_each_classs_lessons_in_that_classs_own_column()
    {
        using var factory = new SqliteTestContextFactory();
        int classAId, classBId;
        await using (var context = factory.CreateDbContext())
        {
            var room = new Room { Name = "101", Capacity = 25, RoomTypeId = 1 };
            var classA = new SchoolClass { Name = "5А", Grade = 5, Shift = Shift.Первая, StudentCount = 25 };
            var classB = new SchoolClass { Name = "5Б", Grade = 5, Shift = Shift.Первая, StudentCount = 24 };
            var math = new Subject { Name = "Математика" };
            var teacherA = new Teacher { FullName = "Иванова И.И." };
            var teacherB = new Teacher { FullName = "Петрова П.П." };
            context.AddRange(room, classA, classB, math, teacherA, teacherB);
            await context.SaveChangesAsync();

            context.ClassSubjectGroups.AddRange(
                new ClassSubjectGroup { ClassId = classA.Id, SubjectId = math.Id, TeacherId = teacherA.Id, LessonsPerWeek = 2 },
                new ClassSubjectGroup { ClassId = classB.Id, SubjectId = math.Id, TeacherId = teacherB.Id, LessonsPerWeek = 2 });
            await context.SaveChangesAsync();
            classAId = classA.Id;
            classBId = classB.Id;
        }

        var vm = new ScheduleViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.GenerateCommand.ExecuteAsync(null);
        Assert.True(vm.HasGeneratedSchedule);

        vm.ViewMode = ScheduleViewMode.Overview;

        var classIndexA = vm.AllClasses.ToList().FindIndex(c => c.Id == classAId);
        var classIndexB = vm.AllClasses.ToList().FindIndex(c => c.Id == classBId);
        Assert.NotEqual(classIndexA, classIndexB);

        var columnA = vm.OverviewRows.Select(r => r.ClassCells[classIndexA]).Where(c => c.SubjectName is not null).ToList();
        var columnB = vm.OverviewRows.Select(r => r.ClassCells[classIndexB]).Where(c => c.SubjectName is not null).ToList();

        Assert.Equal(2, columnA.Count);
        Assert.Equal(2, columnB.Count);
        Assert.All(columnA, c => Assert.Equal("Иванова И.И.", c.SecondaryLine));
        Assert.All(columnB, c => Assert.Equal("Петрова П.П.", c.SecondaryLine));
    }

    [Fact]
    public async Task Selecting_a_specific_day_filters_all_views_to_that_day_only()
    {
        using var factory = new SqliteTestContextFactory();
        await SeedSchedulableCurriculumAsync(factory);

        var vm = new ScheduleViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.GenerateCommand.ExecuteAsync(null);
        Assert.True(vm.HasGeneratedSchedule);

        // По умолчанию — "Все дни": полный набор дней в шапке и в каждой строке.
        Assert.Equal(vm.DaysPerWeek, vm.DayHeaders.Length);
        Assert.All(vm.GridRows, row => Assert.Equal(vm.DaysPerWeek, row.Days.Count));

        var tuesday = vm.DayFilterOptions.Single(o => o.Day == SchoolDay.Вторник);
        vm.SelectedDayFilter = tuesday;

        Assert.Single(vm.DayHeaders);
        Assert.Equal("Вт", vm.DayHeaders[0]);
        Assert.All(vm.GridRows, row => Assert.Single(row.Days));

        vm.ViewMode = ScheduleViewMode.Overview;
        Assert.NotEmpty(vm.OverviewRows);
        Assert.All(vm.OverviewRows, row => Assert.StartsWith("Вт", row.RowLabel));

        // Возврат к "Все дни" восстанавливает полную сетку.
        vm.SelectedDayFilter = vm.DayFilterOptions.Single(o => o.Day is null);
        vm.ViewMode = ScheduleViewMode.ByClass;
        Assert.Equal(vm.DaysPerWeek, vm.DayHeaders.Length);
        Assert.All(vm.GridRows, row => Assert.Equal(vm.DaysPerWeek, row.Days.Count));
    }

    [Fact]
    public async Task Generate_without_any_teacher_assignment_reports_a_friendly_warning_and_no_data_status()
    {
        using var factory = new SqliteTestContextFactory();
        await using (var seed = factory.CreateDbContext())
        {
            var room = new Room { Name = "101", Capacity = 25, RoomTypeId = 1 };
            var cls = new SchoolClass { Name = "5А", Grade = 5, Shift = Shift.Первая, StudentCount = 25 };
            var subject = new Subject { Name = "Математика" };
            seed.AddRange(room, cls, subject);
            await seed.SaveChangesAsync();
            seed.ClassSubjectGroups.Add(new ClassSubjectGroup { ClassId = cls.Id, SubjectId = subject.Id, LessonsPerWeek = 2 });
            await seed.SaveChangesAsync();
        }

        var vm = new ScheduleViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.GenerateCommand.ExecuteAsync(null);

        Assert.False(vm.HasGeneratedSchedule);
        Assert.True(vm.StatusIsError);
        Assert.Single(vm.Warnings);
        Assert.Contains("не назначен учитель", vm.Warnings[0]);
    }

    private static async Task<(int classId, int subjectId, int teacherId)> SeedSchedulableCurriculumAsync(SqliteTestContextFactory factory)
    {
        await using var context = factory.CreateDbContext();

        var room = new Room { Name = "101", Capacity = 25, RoomTypeId = 1 };
        var cls = new SchoolClass { Name = "5А", Grade = 5, Shift = Shift.Первая, StudentCount = 25 };
        var subject = new Subject { Name = "Математика" };
        var teacher = new Teacher { FullName = "Иванова И.И." };
        context.AddRange(room, cls, subject, teacher);
        await context.SaveChangesAsync();

        context.ClassSubjectGroups.Add(new ClassSubjectGroup
        {
            ClassId = cls.Id,
            SubjectId = subject.Id,
            TeacherId = teacher.Id,
            LessonsPerWeek = 3,
        });
        await context.SaveChangesAsync();

        return (cls.Id, subject.Id, teacher.Id);
    }
}
