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
    public async Task Add_defaults_to_the_ordinary_room_type_not_the_alphabetically_first_one()
    {
        using var factory = new SqliteTestContextFactory();
        var vm = new RoomsViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        // "Актовый зал" сортируется раньше "Обычный" по алфавиту — если бы дефолт брался
        // по порядку списка, а не по имени, тест поймал бы это.
        Assert.Contains(vm.AllRoomTypes, rt => rt.Name == "Актовый зал");

        await vm.AddCommand.ExecuteAsync(null);

        Assert.Equal("Обычный", vm.Rooms[0].RoomType.Name);
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
    public async Task Adding_repeatedly_gives_each_room_a_different_default_name()
    {
        // Регрессия: раньше "Добавить" всегда подставлял буквально "Новый кабинет" — второе
        // нажатие сразу падало на уникальном индексе, не давая добавить второй кабинет, пока не
        // переименуешь первый вручную.
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();
        var vm = new RoomsViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.AddCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        Assert.Empty(snackbar.Messages);
        Assert.Equal(3, vm.Rooms.Count);
        Assert.Equal(3, vm.Rooms.Select(r => r.Name).Distinct().Count());
        Assert.Contains(vm.Rooms, r => r.Name == "Новый кабинет");
        Assert.Contains(vm.Rooms, r => r.Name == "Новый кабинет 2");
        Assert.Contains(vm.Rooms, r => r.Name == "Новый кабинет 3");
    }

    [Fact]
    public async Task Renaming_to_an_existing_name_is_still_rejected_and_reported_via_snackbar()
    {
        // Автоподбор свободного имени касается только "Добавить" — если пользователь сам
        // переименует кабинет в уже занятое имя, это по-прежнему должно быть отклонено.
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();
        var vm = new RoomsViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.AddCommand.ExecuteAsync(null); // "Новый кабинет"
        await vm.AddCommand.ExecuteAsync(null); // "Новый кабинет 2"

        vm.Rooms[1].Name = "Новый кабинет";
        await vm.SaveRoomCommand.ExecuteAsync(vm.Rooms[1]);

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
        // Комбобокс в гриде биндится на RoomType (SelectedItem) напрямую, а не на RoomTypeId
        // (SelectedValue) — именно так и симулируем то, что реально делает UI. Проверяем, что
        // RoomTypeId (FK) на самом деле обновляется в базе после сохранения.
        using var factory = new SqliteTestContextFactory();
        var vm = new RoomsViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.AddCommand.ExecuteAsync(null);

        var room = vm.Rooms[0];
        var newType = vm.AllRoomTypes.First(rt => rt.Id != room.RoomTypeId);
        room.RoomType = newType;

        vm.SaveRoomCommand.Execute(room);
        Assert.Equal(newType.Name, room.RoomType?.Name);

        if (vm.SaveRoomCommand is IAsyncRelayCommand { ExecutionTask: { } task })
            await task;

        Assert.Equal(newType.Id, room.RoomTypeId);
    }

    [Fact]
    public async Task Bulk_add_creates_a_room_for_every_number_in_the_range()
    {
        using var factory = new SqliteTestContextFactory();
        var vm = new RoomsViewModel(factory, new FakeSnackbarService());
        await vm.LoadCommand.ExecuteAsync(null);

        vm.BulkAddFrom = "101";
        vm.BulkAddTo = "105";
        await vm.BulkAddCommand.ExecuteAsync(null);

        Assert.Equal(5, vm.Rooms.Count);
        for (var n = 101; n <= 105; n++)
            Assert.Contains(vm.Rooms, r => r.Name == n.ToString());
        Assert.All(vm.Rooms, r => Assert.Equal("Обычный", r.RoomType.Name));
        Assert.False(vm.IsBulkAddPopupOpen);

        var vm2 = new RoomsViewModel(factory, new FakeSnackbarService());
        await vm2.LoadCommand.ExecuteAsync(null);
        Assert.Equal(5, vm2.Rooms.Count);
    }

    [Fact]
    public async Task Bulk_add_accepts_the_range_reversed_and_skips_already_existing_numbers()
    {
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();
        var vm = new RoomsViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.BulkAddFrom = "3";
        vm.BulkAddTo = "1";
        await vm.BulkAddCommand.ExecuteAsync(null);
        Assert.Equal(3, vm.Rooms.Count);

        // Повторно с тем же диапазоном — все номера уже заняты, второй раз ничего не добавляется.
        vm.BulkAddFrom = "1";
        vm.BulkAddTo = "3";
        await vm.BulkAddCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Rooms.Count);
        Assert.Contains(snackbar.Messages, m => m.Contains("уже существуют"));
    }

    [Fact]
    public async Task Bulk_add_rejects_a_non_numeric_or_missing_range()
    {
        using var factory = new SqliteTestContextFactory();
        var snackbar = new FakeSnackbarService();
        var vm = new RoomsViewModel(factory, snackbar);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.BulkAddFrom = "101";
        vm.BulkAddTo = "";
        await vm.BulkAddCommand.ExecuteAsync(null);

        Assert.Empty(vm.Rooms);
        Assert.Single(snackbar.Messages);
    }
}
