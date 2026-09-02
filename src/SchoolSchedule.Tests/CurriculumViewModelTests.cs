using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SchoolSchedule.App.ViewModels;
using SchoolSchedule.Core.Models;
using SchoolSchedule.Tests.TestSupport;

namespace SchoolSchedule.Tests;

public class CurriculumViewModelTests
{
    private static async Task<(int classId, int subjectId)> SeedClassAndSubjectAsync(SqliteTestContextFactory factory)
    {
        await using var context = factory.CreateDbContext();
        var schoolClass = new SchoolClass { Name = "5А", Grade = 5, Shift = Shift.Первая, StudentCount = 25 };
        var subject = new Subject { Name = "Математика" };
        context.SchoolClasses.Add(schoolClass);
        context.Subjects.Add(subject);
        await context.SaveChangesAsync();
        return (schoolClass.Id, subject.Id);
    }

    [Fact]
    public async Task Load_populates_reference_lists_and_selects_first_class()
    {
        using var factory = new SqliteTestContextFactory();
        await SeedClassAndSubjectAsync(factory);

        var vm = new CurriculumViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Single(vm.AllClasses);
        Assert.Single(vm.AllSubjects);
        Assert.NotNull(vm.SelectedClass);
    }

    [Fact]
    public async Task Add_edit_and_reload_round_trips_lessons_per_week()
    {
        using var factory = new SqliteTestContextFactory();
        var (classId, subjectId) = await SeedClassAndSubjectAsync(factory);
        var snackbar = new FakeSnackbarService();

        var vm = new CurriculumViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        var group = Assert.Single(vm.Groups);
        group.LessonsPerWeek = 5;
        await vm.SaveGroupCommand.ExecuteAsync(group);

        var vm2 = new CurriculumViewModel(factory, snackbar);
        await vm2.LoadCommand.ExecuteAsync(null);
        var reloaded = Assert.Single(vm2.Groups);
        Assert.Equal(5, reloaded.LessonsPerWeek);
        Assert.Equal(classId, reloaded.ClassId);
        Assert.Equal(subjectId, reloaded.SubjectId);
    }

    [Fact]
    public async Task New_row_defaults_to_at_most_one_lesson_per_day_and_no_pairing()
    {
        using var factory = new SqliteTestContextFactory();
        await SeedClassAndSubjectAsync(factory);
        var vm = new CurriculumViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.AddCommand.ExecuteAsync(null);

        var group = Assert.Single(vm.Groups);
        Assert.Equal(1, group.MaxLessonsPerDay);
        Assert.False(group.PairedLessons);
    }

    [Fact]
    public async Task Max_lessons_per_day_and_paired_lessons_round_trip()
    {
        using var factory = new SqliteTestContextFactory();
        await SeedClassAndSubjectAsync(factory);
        var snackbar = new FakeSnackbarService();

        var vm = new CurriculumViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        var group = Assert.Single(vm.Groups);
        group.LessonsPerWeek = 4;
        group.MaxLessonsPerDay = 2;
        group.PairedLessons = true;
        await vm.SaveGroupCommand.ExecuteAsync(group);

        var vm2 = new CurriculumViewModel(factory, snackbar);
        await vm2.LoadCommand.ExecuteAsync(null);
        var reloaded = Assert.Single(vm2.Groups);
        Assert.Equal(2, reloaded.MaxLessonsPerDay);
        Assert.True(reloaded.PairedLessons);
    }

    [Fact]
    public async Task Adding_a_second_time_picks_a_different_not_yet_used_subject()
    {
        // Раньше "Добавить" всегда подставлял первый по алфавиту предмет, поэтому вторая
        // попытка добавить строку падала на уникальном индексе (тот же предмет уже есть в
        // плане) прежде, чем пользователь успевал выбрать нужный предмет в самой строке.
        using var factory = new SqliteTestContextFactory();
        await using (var seed = factory.CreateDbContext())
        {
            var schoolClass = new SchoolClass { Name = "5А", Grade = 5, Shift = Shift.Первая, StudentCount = 25 };
            seed.SchoolClasses.Add(schoolClass);
            seed.Subjects.AddRange(new Subject { Name = "Математика" }, new Subject { Name = "Русский язык" });
            await seed.SaveChangesAsync();
        }
        var snackbar = new FakeSnackbarService();

        var vm = new CurriculumViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.AddCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        Assert.Empty(snackbar.Messages);
        Assert.Equal(2, vm.Groups.Count);
        Assert.Equal(2, vm.Groups.Select(g => g.SubjectId).Distinct().Count());
    }

    [Fact]
    public async Task Same_subject_twice_without_group_label_is_rejected_as_duplicate()
    {
        using var factory = new SqliteTestContextFactory();
        await SeedClassAndSubjectAsync(factory);
        var snackbar = new FakeSnackbarService();

        var vm = new CurriculumViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.AddCommand.ExecuteAsync(null); // предмет + null-подгруппа
        await vm.AddCommand.ExecuteAsync(null); // тот же предмет, тоже null -> нарушает уникальный индекс

        Assert.Single(vm.Groups);
        Assert.Single(snackbar.Messages);
    }

    [Fact]
    public async Task Same_subject_with_different_group_labels_is_allowed_as_subgroups()
    {
        using var factory = new SqliteTestContextFactory();
        await SeedClassAndSubjectAsync(factory);
        var snackbar = new FakeSnackbarService();

        var vm = new CurriculumViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.AddCommand.ExecuteAsync(null);
        vm.Groups[0].GroupLabel = "Группа 1";
        await vm.SaveGroupCommand.ExecuteAsync(vm.Groups[0]);

        await vm.AddCommand.ExecuteAsync(null);
        vm.Groups[1].GroupLabel = "Группа 2";
        await vm.SaveGroupCommand.ExecuteAsync(vm.Groups[1]);

        Assert.Empty(snackbar.Messages);
        Assert.Equal(2, vm.Groups.Count);
    }

    [Fact]
    public async Task Selecting_different_class_reloads_only_its_groups()
    {
        using var factory = new SqliteTestContextFactory();
        int class2Id, subjectId;
        await using (var context = factory.CreateDbContext())
        {
            var class1 = new SchoolClass { Name = "5А", Grade = 5, Shift = Shift.Первая, StudentCount = 25 };
            var class2 = new SchoolClass { Name = "5Б", Grade = 5, Shift = Shift.Первая, StudentCount = 24 };
            var subject = new Subject { Name = "Русский язык" };
            context.AddRange(class1, class2, subject);
            await context.SaveChangesAsync();

            context.ClassSubjectGroups.Add(new ClassSubjectGroup { ClassId = class1.Id, SubjectId = subject.Id, LessonsPerWeek = 4 });
            await context.SaveChangesAsync();
            class2Id = class2.Id;
            subjectId = subject.Id;
        }

        var vm = new CurriculumViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Single(vm.Groups); // выбран первый класс (5А), у него 1 предмет

        vm.SelectedClass = vm.AllClasses.Single(c => c.Id == class2Id);
        await vm.ReloadGroupsAsync(); // детерминированный аналог того, что делает OnSelectedClassChanged

        Assert.Empty(vm.Groups); // у 5Б учебный план пуст
    }

    [Fact]
    public async Task Delete_removes_group()
    {
        using var factory = new SqliteTestContextFactory();
        await SeedClassAndSubjectAsync(factory);
        var vm = new CurriculumViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        await vm.DeleteCommand.ExecuteAsync(vm.Groups[0]);

        Assert.Empty(vm.Groups);
    }

    [Fact]
    public async Task Changing_subject_updates_navigation_property_immediately_not_only_after_save_completes()
    {
        using var factory = new SqliteTestContextFactory();
        await SeedClassAndSubjectAsync(factory); // "Математика"
        await using (var seed = factory.CreateDbContext())
        {
            seed.Subjects.Add(new Subject { Name = "Литература" });
            await seed.SaveChangesAsync();
        }

        var vm = new CurriculumViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        var group = vm.Groups[0];
        var newSubject = vm.AllSubjects.First(s => s.Id != group.SubjectId);
        // Комбобокс в гриде биндится на Subject (SelectedItem) напрямую, а не на SubjectId.
        group.Subject = newSubject;

        vm.SaveGroupCommand.Execute(group);
        Assert.Equal(newSubject.Name, group.Subject?.Name);

        if (vm.SaveGroupCommand is IAsyncRelayCommand { ExecutionTask: { } task })
            await task;

        Assert.Equal(newSubject.Id, group.SubjectId);
    }

    private static async Task<(int class5AId, int class5BId, int class6AId, int subjectId)> SeedTwoGradesAndSubjectAsync(SqliteTestContextFactory factory)
    {
        await using var context = factory.CreateDbContext();
        var class5A = new SchoolClass { Name = "5А", Grade = 5, Shift = Shift.Первая, StudentCount = 25 };
        var class5B = new SchoolClass { Name = "5Б", Grade = 5, Shift = Shift.Первая, StudentCount = 24 };
        var class6A = new SchoolClass { Name = "6А", Grade = 6, Shift = Shift.Первая, StudentCount = 26 };
        var russian = new Subject { Name = "Русский язык" };
        context.AddRange(class5A, class5B, class6A, russian);
        await context.SaveChangesAsync();
        return (class5A.Id, class5B.Id, class6A.Id, russian.Id);
    }

    [Fact]
    public async Task SelectGrade_checks_only_classes_of_that_grade()
    {
        using var factory = new SqliteTestContextFactory();
        await SeedTwoGradesAndSubjectAsync(factory);
        var vm = new CurriculumViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SelectGradeCommand.Execute(5);

        Assert.True(vm.BulkApplyClassSelections.Single(s => s.Name == "5А").IsSelected);
        Assert.True(vm.BulkApplyClassSelections.Single(s => s.Name == "5Б").IsSelected);
        Assert.False(vm.BulkApplyClassSelections.Single(s => s.Name == "6А").IsSelected);
    }

    [Fact]
    public async Task BulkApply_creates_the_same_subject_for_every_selected_class_but_not_others()
    {
        using var factory = new SqliteTestContextFactory();
        var (class5AId, class5BId, class6AId, subjectId) = await SeedTwoGradesAndSubjectAsync(factory);
        var vm = new CurriculumViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SelectGradeCommand.Execute(5);
        vm.BulkApplySubject = vm.AllSubjects.Single(s => s.Id == subjectId);
        vm.BulkApplyLessonsPerWeek = "5";
        vm.BulkApplyMaxLessonsPerDay = "1";
        await vm.BulkApplyCommand.ExecuteAsync(null);

        Assert.False(vm.IsBulkApplyPopupOpen);

        await using var check = factory.CreateDbContext();
        var groups = await check.ClassSubjectGroups.ToListAsync();
        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, g => g.ClassId == class5AId && g.LessonsPerWeek == 5);
        Assert.Contains(groups, g => g.ClassId == class5BId && g.LessonsPerWeek == 5);
        Assert.DoesNotContain(groups, g => g.ClassId == class6AId);
    }

    [Fact]
    public async Task BulkApply_updates_an_existing_row_instead_of_duplicating_it()
    {
        using var factory = new SqliteTestContextFactory();
        var (class5AId, class5BId, _, subjectId) = await SeedTwoGradesAndSubjectAsync(factory);
        await using (var seed = factory.CreateDbContext())
        {
            seed.ClassSubjectGroups.Add(new ClassSubjectGroup { ClassId = class5AId, SubjectId = subjectId, LessonsPerWeek = 3 });
            await seed.SaveChangesAsync();
        }

        var vm = new CurriculumViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectGradeCommand.Execute(5);
        vm.BulkApplySubject = vm.AllSubjects.Single(s => s.Id == subjectId);
        vm.BulkApplyLessonsPerWeek = "5";
        vm.BulkApplyMaxLessonsPerDay = "2";
        vm.BulkApplyPairedLessons = true;
        await vm.BulkApplyCommand.ExecuteAsync(null);

        await using var check = factory.CreateDbContext();
        var groups = await check.ClassSubjectGroups.ToListAsync();
        // Всё ещё по одной строке на класс — существующая обновилась, а не задвоилась.
        Assert.Equal(2, groups.Count);
        var class5AGroup = groups.Single(g => g.ClassId == class5AId);
        Assert.Equal(5, class5AGroup.LessonsPerWeek);
        Assert.Equal(2, class5AGroup.MaxLessonsPerDay);
        Assert.True(class5AGroup.PairedLessons);
    }

    [Fact]
    public async Task BulkApply_immediately_refreshes_the_currently_open_class()
    {
        using var factory = new SqliteTestContextFactory();
        var (class5AId, _, _, subjectId) = await SeedTwoGradesAndSubjectAsync(factory);
        var vm = new CurriculumViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedClass = vm.AllClasses.Single(c => c.Id == class5AId);
        await vm.ReloadGroupsAsync();
        Assert.Empty(vm.Groups);

        vm.SelectGradeCommand.Execute(5);
        vm.BulkApplySubject = vm.AllSubjects.Single(s => s.Id == subjectId);
        vm.BulkApplyLessonsPerWeek = "5";
        await vm.BulkApplyCommand.ExecuteAsync(null);

        var reloaded = Assert.Single(vm.Groups);
        Assert.Equal(5, reloaded.LessonsPerWeek);
    }

    [Fact]
    public async Task BulkApply_without_selecting_a_class_reports_a_friendly_error()
    {
        using var factory = new SqliteTestContextFactory();
        var (_, _, _, subjectId) = await SeedTwoGradesAndSubjectAsync(factory);
        var snackbar = new FakeSnackbarService();
        var vm = new CurriculumViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.BulkApplySubject = vm.AllSubjects.Single(s => s.Id == subjectId);
        vm.BulkApplyLessonsPerWeek = "5";
        await vm.BulkApplyCommand.ExecuteAsync(null);

        Assert.Single(snackbar.Messages);
        await using var check = factory.CreateDbContext();
        Assert.Empty(await check.ClassSubjectGroups.ToListAsync());
    }

    [Fact]
    public async Task BulkApply_without_selecting_a_subject_reports_a_friendly_error()
    {
        using var factory = new SqliteTestContextFactory();
        await SeedTwoGradesAndSubjectAsync(factory);
        var snackbar = new FakeSnackbarService();
        var vm = new CurriculumViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SelectGradeCommand.Execute(5);
        vm.BulkApplyLessonsPerWeek = "5";
        await vm.BulkApplyCommand.ExecuteAsync(null);

        Assert.Single(snackbar.Messages);
    }

    [Fact]
    public async Task Grade_filter_options_list_all_parallels_plus_the_all_option()
    {
        using var factory = new SqliteTestContextFactory();
        await SeedTwoGradesAndSubjectAsync(factory);
        var vm = new CurriculumViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.GradeFilterOptions.Count); // "Все параллели" + 5 + 6
        Assert.Contains(vm.GradeFilterOptions, o => o.Grade is null);
        Assert.Contains(vm.GradeFilterOptions, o => o.Grade == 5);
        Assert.Contains(vm.GradeFilterOptions, o => o.Grade == 6);
        Assert.False(vm.IsGradeFilterActive);
    }

    [Fact]
    public async Task Selecting_a_grade_shows_groups_from_every_class_of_that_grade_and_no_others()
    {
        using var factory = new SqliteTestContextFactory();
        var (class5AId, class5BId, class6AId, subjectId) = await SeedTwoGradesAndSubjectAsync(factory);
        await using (var seed = factory.CreateDbContext())
        {
            seed.ClassSubjectGroups.Add(new ClassSubjectGroup { ClassId = class5AId, SubjectId = subjectId, LessonsPerWeek = 5 });
            seed.ClassSubjectGroups.Add(new ClassSubjectGroup { ClassId = class5BId, SubjectId = subjectId, LessonsPerWeek = 4 });
            seed.ClassSubjectGroups.Add(new ClassSubjectGroup { ClassId = class6AId, SubjectId = subjectId, LessonsPerWeek = 3 });
            await seed.SaveChangesAsync();
        }

        var vm = new CurriculumViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SelectedGradeFilter = vm.GradeFilterOptions.Single(o => o.Grade == 5);
        await vm.ReloadGroupsAsync();

        Assert.True(vm.IsGradeFilterActive);
        Assert.Equal(2, vm.Groups.Count);
        Assert.Contains(vm.Groups, g => g.ClassId == class5AId);
        Assert.Contains(vm.Groups, g => g.ClassId == class5BId);
        Assert.DoesNotContain(vm.Groups, g => g.ClassId == class6AId);
        // Класс подгружен (для колонки "Класс" в таблице), а не остаётся null.
        Assert.All(vm.Groups, g => Assert.NotNull(g.Class));
    }

    [Fact]
    public async Task Selecting_a_specific_class_exits_grade_filter_mode()
    {
        using var factory = new SqliteTestContextFactory();
        var (_, _, class6AId, _) = await SeedTwoGradesAndSubjectAsync(factory);
        var vm = new CurriculumViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        // LoadAsync уже сам выбрал первый класс по умолчанию (5А) — берём заведомо другой класс
        // (6А), чтобы присваивание SelectedClass ниже реально изменило значение и запустило
        // OnSelectedClassChanged (сеттер [ObservableProperty] не срабатывает при том же значении).

        vm.SelectedGradeFilter = vm.GradeFilterOptions.Single(o => o.Grade == 5);
        await vm.ReloadGroupsAsync();
        Assert.True(vm.IsGradeFilterActive);

        // Сброс SelectedGradeFilter происходит синхронно внутри сеттера SelectedClass (сама
        // перезагрузка Groups — уже отдельный fire-and-forget, но это здесь не проверяется).
        vm.SelectedClass = vm.AllClasses.Single(c => c.Id == class6AId);

        Assert.False(vm.IsGradeFilterActive);
        Assert.Null(vm.SelectedGradeFilter?.Grade);
    }

    [Fact]
    public async Task Add_is_blocked_while_grade_filter_is_active()
    {
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();
        await SeedTwoGradesAndSubjectAsync(factory);
        var vm = new CurriculumViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SelectedGradeFilter = vm.GradeFilterOptions.Single(o => o.Grade == 5);
        await vm.ReloadGroupsAsync();

        await vm.AddCommand.ExecuteAsync(null);

        Assert.Single(snackbar.Messages);
        await using var check = factory.CreateDbContext();
        Assert.Empty(await check.ClassSubjectGroups.ToListAsync());
    }
}
