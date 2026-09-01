using CommunityToolkit.Mvvm.Input;
using SchoolSchedule.App.ViewModels;
using SchoolSchedule.Core.Models;
using SchoolSchedule.Tests.TestSupport;

namespace SchoolSchedule.Tests;

public class AssignmentsViewModelTests
{
    private static async Task<(int class1Id, int class2Id, int mathId, int mathTeacherId, int litTeacherId)> SeedAsync(SqliteTestContextFactory factory)
    {
        await using var context = factory.CreateDbContext();

        var class1 = new SchoolClass { Name = "5А", Grade = 5, Shift = Shift.Первая, StudentCount = 25 };
        var class2 = new SchoolClass { Name = "5Б", Grade = 5, Shift = Shift.Первая, StudentCount = 24 };
        var math = new Subject { Name = "Математика" };
        var literature = new Subject { Name = "Литература" };
        var mathTeacher = new Teacher { FullName = "Иванова И.И." };
        var litTeacher = new Teacher { FullName = "Петрова П.П." };
        context.AddRange(class1, class2, math, literature, mathTeacher, litTeacher);
        await context.SaveChangesAsync();

        context.TeacherSubjects.Add(new TeacherSubject { TeacherId = mathTeacher.Id, SubjectId = math.Id });
        context.TeacherSubjects.Add(new TeacherSubject { TeacherId = litTeacher.Id, SubjectId = literature.Id });
        context.ClassSubjectGroups.Add(new ClassSubjectGroup { ClassId = class1.Id, SubjectId = math.Id, LessonsPerWeek = 5 });
        context.ClassSubjectGroups.Add(new ClassSubjectGroup { ClassId = class2.Id, SubjectId = literature.Id, LessonsPerWeek = 3 });
        await context.SaveChangesAsync();

        return (class1.Id, class2.Id, math.Id, mathTeacher.Id, litTeacher.Id);
    }

    [Fact]
    public async Task Load_builds_one_row_per_group_with_only_qualified_teachers()
    {
        using var factory = new SqliteTestContextFactory();
        var (class1Id, _, mathId, mathTeacherId, litTeacherId) = await SeedAsync(factory);

        var vm = new AssignmentsViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Rows.Count);

        var mathRow = vm.Rows.Single(r => r.Group.ClassId == class1Id);
        Assert.Equal("Математика", mathRow.SubjectName);
        Assert.Equal("5А", mathRow.ClassName);
        Assert.Equal("Весь класс", mathRow.GroupLabel);
        // Список включает синтетическую запись "не назначен" + только учителя математики —
        // не учителя литературы.
        Assert.Equal(2, mathRow.QualifiedTeachers.Count);
        Assert.Contains(mathRow.QualifiedTeachers, t => t.Id == mathTeacherId);
        Assert.DoesNotContain(mathRow.QualifiedTeachers, t => t.Id == litTeacherId);
    }

    [Fact]
    public async Task Setting_teacher_and_saving_persists_and_updates_display_name()
    {
        using var factory = new SqliteTestContextFactory();
        var (class1Id, _, _, mathTeacherId, _) = await SeedAsync(factory);
        var snackbar = new FakeSnackbarService();

        var vm = new AssignmentsViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);
        var row = vm.Rows.Single(r => r.Group.ClassId == class1Id);

        Assert.Equal("— не назначен —", row.TeacherName);

        // Комбобокс в гриде биндится на SelectedTeacher (SelectedItem) напрямую, а не на TeacherId
        // (SelectedValue) — так и симулируем то, что реально делает UI.
        row.SelectedTeacher = row.QualifiedTeachers.Single(t => t.Id == mathTeacherId);
        await vm.SaveCommand.ExecuteAsync(row);

        Assert.Equal("Иванова И.И.", row.TeacherName);
        Assert.Empty(snackbar.Messages);

        var vm2 = new AssignmentsViewModel(factory, snackbar);
        await vm2.LoadCommand.ExecuteAsync(null);
        var reloaded = vm2.Rows.Single(r => r.Group.ClassId == class1Id);
        Assert.Equal(mathTeacherId, reloaded.TeacherId);
    }

    [Fact]
    public async Task Class_filter_narrows_rows_and_clear_restores_all()
    {
        using var factory = new SqliteTestContextFactory();
        var (class1Id, _, _, _, _) = await SeedAsync(factory);

        var vm = new AssignmentsViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);

        vm.ClassFilter = vm.AllClasses.Single(c => c.Id == class1Id);
        Assert.Single(vm.Rows);
        Assert.All(vm.Rows, r => Assert.Equal(class1Id, r.Group.ClassId));

        vm.ClearClassFilterCommand.Execute(null);
        Assert.Equal(2, vm.Rows.Count);
    }

    [Fact]
    public async Task Changing_teacher_updates_navigation_property_immediately_not_only_after_save_completes()
    {
        using var factory = new SqliteTestContextFactory();
        var (class1Id, _, _, mathTeacherId, _) = await SeedAsync(factory);

        var vm = new AssignmentsViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        var row = vm.Rows.Single(r => r.Group.ClassId == class1Id);
        var mathTeacher = row.QualifiedTeachers.Single(t => t.Id == mathTeacherId);

        row.SelectedTeacher = mathTeacher;

        vm.SaveCommand.Execute(row);
        Assert.Equal(mathTeacher.FullName, row.TeacherName);

        if (vm.SaveCommand is IAsyncRelayCommand { ExecutionTask: { } task })
            await task;

        Assert.Equal(mathTeacherId, row.TeacherId);
    }

    [Fact]
    public async Task Selecting_the_unassigned_sentinel_clears_the_teacher()
    {
        using var factory = new SqliteTestContextFactory();
        var (class1Id, _, _, mathTeacherId, _) = await SeedAsync(factory);

        var vm = new AssignmentsViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        var row = vm.Rows.Single(r => r.Group.ClassId == class1Id);
        row.SelectedTeacher = row.QualifiedTeachers.Single(t => t.Id == mathTeacherId);
        await vm.SaveCommand.ExecuteAsync(row);

        row.SelectedTeacher = AssignmentRow.Unassigned;
        await vm.SaveCommand.ExecuteAsync(row);

        Assert.Null(row.TeacherId);
        Assert.Equal("— не назначен —", row.TeacherName);
    }
}
