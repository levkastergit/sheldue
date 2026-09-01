using SchoolSchedule.App.ViewModels;
using SchoolSchedule.Core.Models;
using SchoolSchedule.Tests.TestSupport;

namespace SchoolSchedule.Tests;

public class ClassesViewModelTests
{
    [Fact]
    public async Task Load_populates_AllRooms_for_the_home_room_dropdown()
    {
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();

        await using (var seed = factory.CreateDbContext())
        {
            seed.Rooms.Add(new Room { Name = "101", Capacity = 28, RoomTypeId = 1 }); // 1 = "Обычный" (сидируется миграцией)
            await seed.SaveChangesAsync();
        }

        var vm = new ClassesViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Single(vm.AllRooms);
        Assert.Equal("101", vm.AllRooms[0].Name);
    }

    [Fact]
    public async Task Add_class_and_assign_home_room_persists()
    {
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();

        int roomId;
        await using (var seed = factory.CreateDbContext())
        {
            var room = new Room { Name = "205", Capacity = 26, RoomTypeId = 1 };
            seed.Rooms.Add(room);
            await seed.SaveChangesAsync();
            roomId = room.Id;
        }

        var vm = new ClassesViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        // Отдельного поля "параллель" больше нет в UI — номер параллели должен вычисляться
        // из названия класса автоматически при сохранении.
        var schoolClass = vm.Classes[0];
        schoolClass.Name = "7Б";
        schoolClass.Shift = Shift.Вторая;
        schoolClass.StudentCount = 27;
        schoolClass.HomeRoomId = roomId;
        await vm.SaveClassCommand.ExecuteAsync(schoolClass);

        var vm2 = new ClassesViewModel(factory, snackbar);
        await vm2.LoadCommand.ExecuteAsync(null);
        var reloaded = Assert.Single(vm2.Classes);
        Assert.Equal("7Б", reloaded.Name);
        Assert.Equal(7, reloaded.Grade);
        Assert.Equal(Shift.Вторая, reloaded.Shift);
        Assert.Equal(roomId, reloaded.HomeRoomId);
        Assert.Equal("205", reloaded.HomeRoom?.Name);
    }

    [Theory]
    [InlineData("5А", 5)]
    [InlineData("10А", 10)]
    [InlineData("11Б", 11)]
    [InlineData("Без номера", 0)]
    public async Task Grade_is_parsed_from_leading_digits_of_the_name(string name, int expectedGrade)
    {
        using var factory = new SqliteTestContextFactory();
        var vm = new ClassesViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        var schoolClass = vm.Classes[0];
        schoolClass.Name = name;
        await vm.SaveClassCommand.ExecuteAsync(schoolClass);

        Assert.Equal(expectedGrade, schoolClass.Grade);
    }

    [Fact]
    public async Task Delete_class_removes_it()
    {
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();
        var vm = new ClassesViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        await vm.DeleteCommand.ExecuteAsync(vm.Classes[0]);

        Assert.Empty(vm.Classes);
    }
}
