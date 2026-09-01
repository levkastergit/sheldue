using CommunityToolkit.Mvvm.Input;
using SchoolSchedule.App.ViewModels;
using SchoolSchedule.Core.Models;
using SchoolSchedule.Tests.TestSupport;

namespace SchoolSchedule.Tests;

public class RoomsViewModelTests
{
    [Fact]
    public async Task Add_creates_room_with_defaults_and_persists()
    {
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();
        var vm = new RoomsViewModel(factory, snackbar);

        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        Assert.Single(vm.Rooms);
        Assert.True(vm.Rooms[0].Id > 0);

        // Перечитываем с нуля — убеждаемся, что запись реально в БД, а не только в памяти ViewModel.
        var vm2 = new RoomsViewModel(factory, snackbar);
        await vm2.LoadCommand.ExecuteAsync(null);
        Assert.Single(vm2.Rooms);
    }

    [Fact]
    public async Task Editing_name_and_saving_persists_the_change()
    {
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();
        var vm = new RoomsViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        var laboratory = vm.AllRoomTypes.Single(rt => rt.Name == "Лаборатория");
        var room = vm.Rooms[0];
        room.Name = "Кабинет физики";
        room.Capacity = 30;
        room.RoomTypeId = laboratory.Id;
        await vm.SaveRoomCommand.ExecuteAsync(room);

        var vm2 = new RoomsViewModel(factory, snackbar);
        await vm2.LoadCommand.ExecuteAsync(null);
        var reloaded = Assert.Single(vm2.Rooms);
        Assert.Equal("Кабинет физики", reloaded.Name);
        Assert.Equal(30, reloaded.Capacity);
        Assert.Equal("Лаборатория", reloaded.RoomType.Name);
    }

    [Fact]
    public async Task Delete_removes_room_from_collection_and_database()
    {
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();
        var vm = new RoomsViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);
        var room = vm.Rooms[0];

        await vm.DeleteCommand.ExecuteAsync(room);

        Assert.Empty(vm.Rooms);
        var vm2 = new RoomsViewModel(factory, snackbar);
        await vm2.LoadCommand.ExecuteAsync(null);
        Assert.Empty(vm2.Rooms);
    }

    [Fact]
    public async Task Duplicate_name_is_rejected_and_reported_via_snackbar()
    {
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();
        var vm = new RoomsViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.AddCommand.ExecuteAsync(null); // "Новый кабинет"
        await vm.AddCommand.ExecuteAsync(null); // тоже "Новый кабинет" -> нарушение уникального индекса

        Assert.Single(vm.Rooms); // второй кабинет не добавился в коллекцию
        Assert.Single(snackbar.Messages);
        Assert.Contains("уже используется", snackbar.Messages[0]);
    }

    [Fact]
    public async Task Room_types_are_seeded_by_default_and_new_ones_can_be_added_and_removed()
    {
        using var factory = new SqliteTestContextFactory();
        var vm = new RoomsViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.AllRoomTypes.Count >= 6); // стартовый набор из миграции

        vm.NewRoomTypeName = "Кабинет робототехники";
        await vm.AddRoomTypeCommand.ExecuteAsync(null);
        var added = Assert.Single(vm.AllRoomTypes, rt => rt.Name == "Кабинет робототехники");

        await vm.DeleteRoomTypeCommand.ExecuteAsync(added);
        Assert.DoesNotContain(vm.AllRoomTypes, rt => rt.Name == "Кабинет робототехники");
    }

    [Fact]
    public async Task Assigning_teacher_to_room_persists_and_unassigning_removes_it()
    {
        using var factory = new SqliteTestContextFactory();
        int teacherId;
        await using (var seed = factory.CreateDbContext())
        {
            var seededTeacher = new Teacher { FullName = "Сидоров С.С." };
            seed.Teachers.Add(seededTeacher);
            await seed.SaveChangesAsync();
            teacherId = seededTeacher.Id;
        }

        var vm = new RoomsViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        var teacher = Assert.Single(vm.TeacherSelections).Teacher;
        Assert.False(vm.TeacherSelections[0].IsSelected);

        // Отдельный несвязанный экземпляр — тот, что во ViewModel, уже подписан на PropertyChanged
        // и сам асинхронно дёргает ToggleTeacherAssignmentAsync при смене IsSelected (см. TeachersViewModelTests).
        await vm.ToggleTeacherAssignmentAsync(new TeacherSelection(teacher, true));

        await using (var check = factory.CreateDbContext())
        {
            var link = Assert.Single(check.RoomTeacherAssignments);
            Assert.Equal(teacherId, link.TeacherId);
        }

        await vm.ToggleTeacherAssignmentAsync(new TeacherSelection(teacher, false));

        await using (var check = factory.CreateDbContext())
        {
            Assert.Empty(check.RoomTeacherAssignments);
        }
    }

    [Fact]
    public async Task Changing_room_type_updates_navigation_property_immediately_not_only_after_save_completes()
    {
        // Регрессия: смена RoomTypeId в комбобоксе сама по себе не обновляет навигационное свойство
        // RoomType, а ячейка "Тип кабинета" в гриде читает именно его — без синхронного фикса в
        // SaveRoomAsync ячейка на долю секунды показывала старое/пустое значение до завершения
        // асинхронного сохранения (то, что видел пользователь до перехода на другую вкладку и обратно).
        using var factory = new SqliteTestContextFactory();
        var vm = new RoomsViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        var room = vm.Rooms[0];
        var newType = vm.AllRoomTypes.First(rt => rt.Id != room.RoomTypeId);
        room.RoomTypeId = newType.Id;

        vm.SaveRoomCommand.Execute(room);
        // Проверяем сразу, без await — именно это видит WPF при синхронном возврате в режим
        // отображения ячейки сразу после RowEditEnding.
        Assert.Equal(newType.Name, room.RoomType?.Name);

        if (vm.SaveRoomCommand is IAsyncRelayCommand { ExecutionTask: { } task })
            await task;
    }
}
