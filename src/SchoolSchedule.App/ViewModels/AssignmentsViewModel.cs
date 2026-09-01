using SchoolSchedule.App.Helpers;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SchoolSchedule.App.Services;
using SchoolSchedule.Core.Models;
using SchoolSchedule.Data;

namespace SchoolSchedule.App.ViewModels;

/// <summary>
/// Назначения: какой учитель ведёт каждую строку учебного плана (класс/подгруппа + предмет).
/// Список учителей в каждой строке ограничен теми, кто отмечен как вправе вести этот предмет
/// (страница "Учителя") — так пропадает половина ошибок ручного ввода.
/// </summary>
public partial class AssignmentsViewModel : ObservableObject
{
    private readonly IDbContextFactory<SchoolScheduleDbContext> _contextFactory;
    private readonly ISnackbarService _snackbar;
    private SchoolScheduleDbContext? _context;
    private List<AssignmentRow> _allRows = [];

    public ObservableCollection<AssignmentRow> Rows { get; } = [];
    public ObservableCollection<SchoolClass> AllClasses { get; } = [];

    /// <summary>null = показать все классы.</summary>
    [ObservableProperty]
    private SchoolClass? _classFilter;

    public AssignmentsViewModel(IDbContextFactory<SchoolScheduleDbContext> contextFactory, ISnackbarService snackbar)
    {
        _contextFactory = contextFactory;
        _snackbar = snackbar;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        _context?.Dispose();
        _context = await _contextFactory.CreateDbContextAsync();

        AllClasses.Clear();
        foreach (var schoolClass in await _context.SchoolClasses.OrderBy(c => c.Grade).ThenBy(c => c.Name).ToListAsync())
            AllClasses.Add(schoolClass);

        var teachers = await _context.Teachers
            .Include(t => t.TeacherSubjects)
            .ToListAsync();

        var groups = await _context.ClassSubjectGroups
            .Include(g => g.Class)
            .Include(g => g.Subject)
            .Include(g => g.Teacher)
            .OrderBy(g => g.Class.Grade).ThenBy(g => g.Class.Name)
            .ThenBy(g => g.Subject.Name).ThenBy(g => g.GroupLabel)
            .ToListAsync();

        _allRows = groups
            .Select(group =>
            {
                var qualified = teachers.Where(t => t.TeacherSubjects.Any(ts => ts.SubjectId == group.SubjectId));
                return new AssignmentRow(group, qualified);
            })
            .ToList();

        ApplyFilter();
    }

    partial void OnClassFilterChanged(SchoolClass? value) => ApplyFilter();

    [RelayCommand]
    private void ClearClassFilter() => ClassFilter = null;

    private void ApplyFilter()
    {
        Rows.Clear();
        var filtered = ClassFilter is null
            ? _allRows
            : _allRows.Where(r => r.Group.ClassId == ClassFilter.Id);
        foreach (var row in filtered)
            Rows.Add(row);
    }

    [RelayCommand]
    private async Task SaveAsync(AssignmentRow row)
    {
        if (_context is null) return;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _snackbar.Show(DbErrorFormatter.Format("сохранить назначение", ex));
        }
    }
}
