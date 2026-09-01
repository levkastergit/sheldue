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

        var room = new Room { Name = "Новый кабинет", Capacity = 25, RoomTypeId = AllRoomTypes[0].Id };
        _context.Rooms.Add(room);
        if (await TrySaveAsync("добавить кабинет"))
        {
            room.RoomType = AllRoomTypes[0];
            Rooms.Add(room);
            SelectedRoom = room;
        }
        else
        {
            _context.Rooms.Remove(room);
        }
    }

    [RelayCommand]
    private async Task SaveRoomAsync(Room room)
    {
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
