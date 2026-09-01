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
        subject.RequiredRoomTypeId = sportsHall.Id;
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
    public async Task Selecting_the_synthetic_none_option_saves_as_null()
    {
        using var factory = new SqliteTestContextFactory();
        var vm = new SubjectsViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        var subject = vm.Subjects[0];
        subject.RequiredRoomTypeId = 0; // "Не важно" в комбобоксе — синтетический Id
        await vm.SaveSubjectCommand.ExecuteAsync(subject);

        Assert.Null(subject.RequiredRoomTypeId);
    }
}
