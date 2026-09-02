using SchoolSchedule.App.Helpers;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SchoolSchedule.App.Services;
using SchoolSchedule.Core.Models;
using SchoolSchedule.Data;

namespace SchoolSchedule.App.ViewModels;

public partial class SubjectsViewModel : ObservableObject
{
    private readonly IDbContextFactory<SchoolScheduleDbContext> _contextFactory;
    private readonly ISnackbarService _snackbar;
    private SchoolScheduleDbContext? _context;

    public ObservableCollection<Subject> Subjects { get; } = [];

    /// <summary>Типы кабинетов + синтетическая запись Id=0 "Не важно" для сброса необязательного требования.</summary>
    public ObservableCollection<RoomType> RoomTypeOptions { get; } = [];

    [ObservableProperty]
    private Subject? _selectedSubject;

    public SubjectsViewModel(IDbContextFactory<SchoolScheduleDbContext> contextFactory, ISnackbarService snackbar)
    {
        _contextFactory = contextFactory;
        _snackbar = snackbar;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        _context?.Dispose();
        _context = await _contextFactory.CreateDbContextAsync();

        RoomTypeOptions.Clear();
        RoomTypeOptions.Add(NullToRoomTypeSentinelConverter.Sentinel);
        foreach (var roomType in await _context.RoomTypes.OrderBy(rt => rt.Name).ToListAsync())
            RoomTypeOptions.Add(roomType);

        Subjects.Clear();
        foreach (var subject in await _context.Subjects.Include(s => s.RequiredRoomType).OrderBy(s => s.Name).ToListAsync())
            Subjects.Add(subject);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (_context is null) return;

        // Имя уникально в базе — иначе повторное добавление сразу падало бы на дубликате, не давая
        // добавить второй предмет, пока не переименуешь первый.
        var name = UniqueNameHelper.NextAvailable("Новый предмет", Subjects.Select(s => s.Name));
        var subject = new Subject { Name = name };
        _context.Subjects.Add(subject);
        if (await TrySaveAsync("добавить предмет"))
        {
            Subjects.Add(subject);
            SelectedSubject = subject;
        }
        else
        {
            _context.Subjects.Remove(subject);
        }
    }

    [RelayCommand]
    private async Task SaveSubjectAsync(Subject subject)
    {
        // Комбобокс биндится на RequiredRoomType (SelectedItem) напрямую, а не на RequiredRoomTypeId
        // (SelectedValue) — см. комментарий в RoomsViewModel.SaveRoomAsync. Синтетическая запись
        // "Не важно" превращается обратно в null конвертером NullToRoomTypeSentinelConverter ещё на
        // этапе биндинга, так что здесь уже гарантированно корректное значение.
        await TrySaveAsync("сохранить предмет");
    }

    [RelayCommand]
    private async Task DeleteAsync(Subject? subject)
    {
        subject ??= SelectedSubject;
        if (subject is null || _context is null) return;

        _context.Subjects.Remove(subject);
        if (await TrySaveAsync("удалить предмет — возможно, он уже используется в учебном плане"))
        {
            Subjects.Remove(subject);
        }
        else
        {
            _context.Entry(subject).State = EntityState.Unchanged;
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
