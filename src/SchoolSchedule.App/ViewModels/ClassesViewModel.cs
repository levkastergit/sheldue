using SchoolSchedule.App.Helpers;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SchoolSchedule.App.Services;
using SchoolSchedule.Core.Models;
using SchoolSchedule.Data;

namespace SchoolSchedule.App.ViewModels;

public partial class ClassesViewModel : ObservableObject
{
    private readonly IDbContextFactory<SchoolScheduleDbContext> _contextFactory;
    private readonly ISnackbarService _snackbar;
    private SchoolScheduleDbContext? _context;

    public ObservableCollection<SchoolClass> Classes { get; } = [];

    /// <summary>Справочный список кабинетов для выпадающего списка "Классный кабинет".</summary>
    public ObservableCollection<Room> AllRooms { get; } = [];

    [ObservableProperty]
    private SchoolClass? _selectedClass;

    public ClassesViewModel(IDbContextFactory<SchoolScheduleDbContext> contextFactory, ISnackbarService snackbar)
    {
        _contextFactory = contextFactory;
        _snackbar = snackbar;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        _context?.Dispose();
        _context = await _contextFactory.CreateDbContextAsync();

        AllRooms.Clear();
        foreach (var room in await _context.Rooms.OrderBy(r => r.Name).ToListAsync())
            AllRooms.Add(room);

        Classes.Clear();
        foreach (var schoolClass in await _context.SchoolClasses
                     .Include(c => c.HomeRoom)
                     .OrderBy(c => c.Grade).ThenBy(c => c.Name)
                     .ToListAsync())
        {
            Classes.Add(schoolClass);
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (_context is null) return;

        var schoolClass = new SchoolClass { Name = "Новый класс", Shift = Shift.Первая, StudentCount = 25 };
        _context.SchoolClasses.Add(schoolClass);
        if (await TrySaveAsync("добавить класс"))
        {
            Classes.Add(schoolClass);
            SelectedClass = schoolClass;
        }
        else
        {
            _context.SchoolClasses.Remove(schoolClass);
        }
    }

    [RelayCommand]
    private async Task SaveClassAsync(SchoolClass schoolClass)
    {
        // Отдельного поля "параллель" в UI больше нет — номер параллели берём из начала названия
        // ("5А" -> 5), чтобы классы по-прежнему сортировались по возрастанию, а не по алфавиту
        // (иначе "10А" оказался бы раньше "5А").
        schoolClass.Grade = ParseGrade(schoolClass.Name);

        // Комбобокс биндится на HomeRoom (SelectedItem) напрямую, а не на HomeRoomId (SelectedValue) —
        // см. комментарий в RoomsViewModel.SaveRoomAsync про то, почему это надёжнее.
        await TrySaveAsync("сохранить класс");
    }

    private static int ParseGrade(string name)
    {
        var match = Regex.Match(name, @"^\d+");
        return match.Success ? int.Parse(match.Value) : 0;
    }

    [RelayCommand]
    private async Task DeleteAsync(SchoolClass? schoolClass)
    {
        schoolClass ??= SelectedClass;
        if (schoolClass is null || _context is null) return;

        _context.SchoolClasses.Remove(schoolClass);
        if (await TrySaveAsync("удалить класс — возможно, для него уже составлен учебный план или расписание"))
        {
            Classes.Remove(schoolClass);
        }
        else
        {
            _context.Entry(schoolClass).State = EntityState.Unchanged;
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
