using SchoolSchedule.App.Helpers;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SchoolSchedule.App.Services;
using SchoolSchedule.Core.Models;
using SchoolSchedule.Data;

namespace SchoolSchedule.App.ViewModels;

public partial class RoomsViewModel : ObservableObject
{
    private readonly IDbContextFactory<SchoolScheduleDbContext> _contextFactory;
    private readonly ISnackbarService _snackbar;
    private SchoolScheduleDbContext? _context;
    private List<Teacher> _allTeachers = [];

    public ObservableCollection<Room> Rooms { get; } = [];
    public ObservableCollection<RoomType> AllRoomTypes { get; } = [];
    public ObservableCollection<TeacherSelection> TeacherSelections { get; } = [];

    [ObservableProperty]
    private Room? _selectedRoom;

    [ObservableProperty]
    private bool _isRoomTypesPopupOpen;

    [ObservableProperty]
    private string _newRoomTypeName = string.Empty;

    [ObservableProperty]
    private bool _isBulkAddPopupOpen;

    [ObservableProperty]
    private string _bulkAddFrom = string.Empty;

    [ObservableProperty]
    private string _bulkAddTo = string.Empty;

    public RoomsViewModel(IDbContextFactory<SchoolScheduleDbContext> contextFactory, ISnackbarService snackbar)
    {
        _contextFactory = contextFactory;
        _snackbar = snackbar;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        _context?.Dispose();
        _context = await _contextFactory.CreateDbContextAsync();

        AllRoomTypes.Clear();
        foreach (var roomType in await _context.RoomTypes.OrderBy(rt => rt.Name).ToListAsync())
            AllRoomTypes.Add(roomType);

        _allTeachers = await _context.Teachers.OrderBy(t => t.FullName).ToListAsync();

        Rooms.Clear();
        var rooms = await _context.Rooms
            .Include(r => r.RoomType)
            .Include(r => r.AssignedTeachers).ThenInclude(a => a.Teacher)
            .OrderBy(r => r.Name)
            .ToListAsync();
        foreach (var room in rooms)
            Rooms.Add(room);

        RebuildTeacherSelections(SelectedRoom);
    }

    partial void OnSelectedRoomChanged(Room? value) => RebuildTeacherSelections(value);

    private void RebuildTeacherSelections(Room? room)
    {
        TeacherSelections.Clear();
        if (room is null) return;

        var assignedIds = room.AssignedTeachers.Select(a => a.TeacherId).ToHashSet();
        foreach (var teacher in _allTeachers)
            AttachTeacherSelection(new TeacherSelection(teacher, assignedIds.Contains(teacher.Id)));
    }

    private void AttachTeacherSelection(TeacherSelection selection)
    {
        selection.PropertyChanged += async (_, args) =>
        {
            if (args.PropertyName == nameof(TeacherSelection.IsSelected))
                await ToggleTeacherAssignmentAsync(selection);
        };
        TeacherSelections.Add(selection);
    }

    /// <summary>internal — чтобы тесты могли детерминированно дождаться завершения (см. TeachersViewModel).</summary>
    internal async Task ToggleTeacherAssignmentAsync(TeacherSelection selection)
    {
        if (_context is null || SelectedRoom is null) return;

        if (selection.IsSelected)
        {
            var link = new RoomTeacherAssignment { RoomId = SelectedRoom.Id, TeacherId = selection.Teacher.Id };
            _context.RoomTeacherAssignments.Add(link);
            if (!await TrySaveAsync("закрепить учителя за кабинетом"))
            {
                _context.RoomTeacherAssignments.Remove(link);
                RebuildTeacherSelections(SelectedRoom);
            }
        }
        else
        {
            var link = SelectedRoom.AssignedTeachers.FirstOrDefault(a => a.TeacherId == selection.Teacher.Id);
            if (link is null) return;

            _context.RoomTeacherAssignments.Remove(link);
            if (!await TrySaveAsync("снять закрепление учителя"))
            {
                _context.Entry(link).State = EntityState.Unchanged;
                RebuildTeacherSelections(SelectedRoom);
            }
        }
    }

    [RelayCommand]
    private void ToggleRoomTypesPopup() => IsRoomTypesPopupOpen = !IsRoomTypesPopupOpen;

    [RelayCommand]
    private async Task AddRoomTypeAsync()
    {
        if (_context is null) return;
        var name = NewRoomTypeName.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var roomType = new RoomType { Name = name };
        _context.RoomTypes.Add(roomType);
        if (await TrySaveAsync("добавить тип кабинета"))
        {
            AllRoomTypes.Add(roomType);
            NewRoomTypeName = string.Empty;
        }
        else
        {
            _context.RoomTypes.Remove(roomType);
        }
    }

    [RelayCommand]
    private async Task DeleteRoomTypeAsync(RoomType roomType)
    {
        if (_context is null) return;

        _context.RoomTypes.Remove(roomType);
        if (await TrySaveAsync("удалить тип кабинета — возможно, он уже используется"))
        {
            AllRoomTypes.Remove(roomType);
        }
        else
        {
            _context.Entry(roomType).State = EntityState.Unchanged;
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (_context is null) return;
        if (AllRoomTypes.Count == 0)
        {
            _snackbar.Show("Сначала добавьте хотя бы один тип кабинета");
            return;
        }

        // По умолчанию — тип "Обычный" (он есть в стартовом наборе миграции), а не первый по
        // алфавиту (им может оказаться, например, "Актовый зал").
        var defaultType = AllRoomTypes.FirstOrDefault(rt => rt.Name == "Обычный") ?? AllRoomTypes[0];
        // Имя уникально в базе — если "Новый кабинет" уже есть, подбираем "Новый кабинет 2" и т.д.,
        // иначе повторное добавление сразу падало бы на дубликате, не давая добавить второй кабинет,
        // пока не переименуешь первый.
        var name = UniqueNameHelper.NextAvailable("Новый кабинет", Rooms.Select(r => r.Name));
        var room = new Room { Name = name, Capacity = 25, RoomTypeId = defaultType.Id };
        _context.Rooms.Add(room);
        if (await TrySaveAsync("добавить кабинет"))
        {
            room.RoomType = defaultType;
            Rooms.Add(room);
            SelectedRoom = room;
        }
        else
        {
            _context.Rooms.Remove(room);
        }
    }

    [RelayCommand]
    private void ToggleBulkAddPopup() => IsBulkAddPopupOpen = !IsBulkAddPopupOpen;

    /// <summary>Добавляет сразу несколько кабинетов с номерами "от" и "до" включительно (например,
    /// 101..110 — 10 кабинетов). Номера, которые уже заняты существующим кабинетом, пропускаются
    /// (а не обрывают всю операцию) — про них сообщается отдельно после добавления остальных.</summary>
    [RelayCommand]
    private async Task BulkAddAsync()
    {
        if (_context is null) return;

        if (!int.TryParse(BulkAddFrom, out var from) || !int.TryParse(BulkAddTo, out var to))
        {
            _snackbar.Show("Укажите номера «от» и «до» — только целые числа");
            return;
        }
        if (from > to)
            (from, to) = (to, from);
        if (to - from + 1 > 200)
        {
            _snackbar.Show("Слишком большой диапазон — не больше 200 кабинетов за раз");
            return;
        }
        if (AllRoomTypes.Count == 0)
        {
            _snackbar.Show("Сначала добавьте хотя бы один тип кабинета");
            return;
        }

        var defaultType = AllRoomTypes.FirstOrDefault(rt => rt.Name == "Обычный") ?? AllRoomTypes[0];
        var existingNames = Rooms.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = new List<Room>();
        var skipped = new List<string>();

        for (var n = from; n <= to; n++)
        {
            var name = n.ToString();
            if (!existingNames.Add(name))
            {
                skipped.Add(name);
                continue;
            }
            var room = new Room { Name = name, Capacity = 25, RoomTypeId = defaultType.Id, RoomType = defaultType };
            _context.Rooms.Add(room);
            added.Add(room);
        }

        if (added.Count == 0)
        {
            _snackbar.Show("Все кабинеты в этом диапазоне уже существуют");
            return;
        }

        if (await TrySaveAsync($"добавить кабинеты {from}–{to}"))
        {
            foreach (var room in added)
                Rooms.Add(room);
            SelectedRoom = added[^1];
            IsBulkAddPopupOpen = false;
            BulkAddFrom = string.Empty;
            BulkAddTo = string.Empty;

            _snackbar.Show(skipped.Count == 0
                ? $"Добавлено кабинетов: {added.Count}"
                : $"Добавлено кабинетов: {added.Count}. Уже существовали и пропущены: {string.Join(", ", skipped.Take(10))}{(skipped.Count > 10 ? "…" : "")}");
        }
        else
        {
            foreach (var room in added)
                _context.Rooms.Remove(room);
        }
    }

    [RelayCommand]
    private async Task SaveRoomAsync(Room room)
    {
        // Комбобокс в гриде биндится на RoomType (SelectedItem) напрямую, а не на RoomTypeId
        // (SelectedValue) — так у динамически создаваемого ComboBox не бывает ситуации "ItemsSource
        // ещё не готов к моменту, когда SelectedValue пытается найти совпадение", из-за которой
        // список визуально показывал пустоту при входе в редактирование. EF Core сам корректно
        // выставит RoomTypeId из уже установленного RoomType при сохранении.
        await TrySaveAsync("сохранить кабинет");
    }

    [RelayCommand]
    private async Task DeleteAsync(Room? room)
    {
        room ??= SelectedRoom;
        if (room is null || _context is null) return;

        _context.Rooms.Remove(room);
        if (await TrySaveAsync("удалить кабинет — возможно, он используется в расписании или как классный кабинет"))
        {
            Rooms.Remove(room);
        }
        else
        {
            _context.Entry(room).State = EntityState.Unchanged;
        }
    }

    private async Task<bool> TrySaveAsync(string actionDescription)
    {
        if (_context is null) return false;
        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex)
        {
            _snackbar.Show(DbErrorFormatter.Format(actionDescription, ex));
            return false;
        }
    }
}
