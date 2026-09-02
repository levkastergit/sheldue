using CommunityToolkit.Mvvm.Input;
using SchoolSchedule.App.ViewModels;
using SchoolSchedule.Core.Models;
using SchoolSchedule.Tests.TestSupport;

namespace SchoolSchedule.Tests;

public class SubjectsViewModelTests
{
    [Fact]
    public async Task Add_edit_delete_round_trip_persists_correctly()
    {
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();
        var vm = new SubjectsViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.AddCommand.ExecuteAsync(null);
        var sportsHall = vm.RoomTypeOptions.Single(rt => rt.Name == "Спортзал");
        var subject = vm.Subjects[0];
        subject.Name = "Физкультура";
        // Комбобокс в гриде биндится на RequiredRoomType (SelectedItem) напрямую, а не на
        // RequiredRoomTypeId — так и симулируем то, что реально делает UI.
        subject.RequiredRoomType = sportsHall;
        await vm.SaveSubjectCommand.ExecuteAsync(subject);

        var vm2 = new SubjectsViewModel(factory, snackbar);
        await vm2.LoadCommand.ExecuteAsync(null);
        var reloaded = Assert.Single(vm2.Subjects);
        Assert.Equal("Физкультура", reloaded.Name);
        Assert.Equal("Спортзал", reloaded.RequiredRoomType?.Name);

        await vm2.DeleteCommand.ExecuteAsync(reloaded);
        Assert.Empty(vm2.Subjects);

        var vm3 = new SubjectsViewModel(factory, snackbar);
        await vm3.LoadCommand.ExecuteAsync(null);
        Assert.Empty(vm3.Subjects);
    }

    [Fact]
    public async Task RequiredRoomType_can_be_left_unset()
    {
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();
        var vm = new SubjectsViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.AddCommand.ExecuteAsync(null);
        Assert.Null(vm.Subjects[0].RequiredRoomType);
    }

    [Fact]
    public async Task Changing_required_room_type_updates_navigation_property_immediately_not_only_after_save_completes()
    {
        using var factory = new SqliteTestContextFactory();
        var vm = new SubjectsViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        var subject = vm.Subjects[0];
        var sportsHall = vm.RoomTypeOptions.Single(rt => rt.Name == "Спортзал");
        subject.RequiredRoomType = sportsHall;

        vm.SaveSubjectCommand.Execute(subject);
        Assert.Equal("Спортзал", subject.RequiredRoomType?.Name);

        if (vm.SaveSubjectCommand is IAsyncRelayCommand { ExecutionTask: { } task })
            await task;

        Assert.Equal(sportsHall.Id, subject.RequiredRoomTypeId);
    }

    [Fact]
    public async Task Adding_repeatedly_gives_each_subject_a_different_default_name()
    {
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();
        var vm = new SubjectsViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.AddCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        Assert.Empty(snackbar.Messages);
        Assert.Equal(2, vm.Subjects.Count);
        Assert.Contains(vm.Subjects, s => s.Name == "Новый предмет");
        Assert.Contains(vm.Subjects, s => s.Name == "Новый предмет 2");
    }
}
