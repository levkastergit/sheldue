using SchoolSchedule.App.ViewModels;
using SchoolSchedule.Core.Models;
using SchoolSchedule.Tests.TestSupport;

namespace SchoolSchedule.Tests;

public class TeachersViewModelTests
{
    private static async Task<int> SeedSubjectAsync(SqliteTestContextFactory factory, string name)
    {
        await using var context = factory.CreateDbContext();
        var subject = new Subject { Name = name };
        context.Subjects.Add(subject);
        await context.SaveChangesAsync();
        return subject.Id;
    }

    [Fact]
    public async Task Add_teacher_and_edit_basic_fields_persists()
    {
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();
        var vm = new TeachersViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.AddCommand.ExecuteAsync(null);
        var teacher = vm.SelectedTeacher!;
        teacher.FullName = "Иванова Мария Петровна";
        teacher.MaxLessonsPerWeek = 24;
        await vm.SaveTeacherCommand.ExecuteAsync(teacher);

        var vm2 = new TeachersViewModel(factory, snackbar);
        await vm2.LoadCommand.ExecuteAsync(null);
        var reloaded = Assert.Single(vm2.Teachers);
        Assert.Equal("Иванова Мария Петровна", reloaded.FullName);
        Assert.Equal(24, reloaded.MaxLessonsPerWeek);
    }

    [Fact]
    public async Task Selecting_teacher_builds_subject_checklist_from_all_subjects()
    {
        using var factory = new SqliteTestContextFactory();
        await SeedSubjectAsync(factory, "Математика");
        await SeedSubjectAsync(factory, "Русский язык");

        var snackbar = new FakeSnackbarService();
        var vm = new TeachersViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.SubjectSelections.Count);
        Assert.All(vm.SubjectSelections, s => Assert.False(s.IsSelected));
    }

    [Fact]
    public async Task Toggling_subject_on_persists_TeacherSubject_and_off_removes_it()
    {
        using var factory = new SqliteTestContextFactory();
        await SeedSubjectAsync(factory, "Математика");

        var snackbar = new FakeSnackbarService();
        var vm = new TeachersViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);
        var teacherId = vm.SelectedTeacher!.Id;

        // Отдельный экземпляр SubjectSelection (не из vm.SubjectSelections) — тот, что во ViewModel,
        // уже подписан на PropertyChanged и сам асинхронно дёргает ToggleSubjectAsync при смене IsSelected;
        // вызывая метод напрямую здесь, мы проверяем его детерминированно, без гонки с этой подпиской.
        var subject = vm.SubjectSelections[0].Subject;

        await vm.ToggleSubjectAsync(new SubjectSelection(subject, true));

        await using (var check = factory.CreateDbContext())
        {
            var link = Assert.Single(check.TeacherSubjects);
            Assert.Equal(teacherId, link.TeacherId);
        }

        await vm.ToggleSubjectAsync(new SubjectSelection(subject, false));

        await using (var check = factory.CreateDbContext())
        {
            Assert.Empty(check.TeacherSubjects);
        }
    }

    [Fact]
    public async Task Add_and_remove_unavailability_window()
    {
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();
        var vm = new TeachersViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        vm.NewUnavailabilityDay = SchoolDay.Вторник;
        vm.NewUnavailabilityPeriod = 1;
        await vm.AddUnavailabilityCommand.ExecuteAsync(null);

        var entry = Assert.Single(vm.Unavailabilities);
        Assert.Equal(SchoolDay.Вторник, entry.Day);
        Assert.Equal(1, entry.PeriodNumber);

        await vm.RemoveUnavailabilityCommand.ExecuteAsync(entry);
        Assert.Empty(vm.Unavailabilities);

        await using var check = factory.CreateDbContext();
        Assert.Empty(check.TeacherUnavailabilities);
    }

    [Fact]
    public async Task Delete_teacher_removes_it()
    {
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();
        var vm = new TeachersViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);
        var teacher = vm.Teachers[0];

        await vm.DeleteCommand.ExecuteAsync(teacher);

        Assert.Empty(vm.Teachers);
    }
}
