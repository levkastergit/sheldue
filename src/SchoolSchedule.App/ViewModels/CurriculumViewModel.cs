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
/// Учебный план: для выбранного класса — какие предметы и сколько уроков в неделю.
/// Если предмет делится на подгруппы, для него заводится несколько строк с разным GroupLabel
/// (например, "Группа 1"/"Группа 2") — это тот же ClassSubjectGroup, что и в "Назначениях".
/// </summary>
public partial class CurriculumViewModel : ObservableObject
{
    private readonly IDbContextFactory<SchoolScheduleDbContext> _contextFactory;
    private readonly ISnackbarService _snackbar;
    private SchoolScheduleDbContext? _context;

    public ObservableCollection<SchoolClass> AllClasses { get; } = [];
    public ObservableCollection<Subject> AllSubjects { get; } = [];
    public ObservableCollection<ClassSubjectGroup> Groups { get; } = [];

    [ObservableProperty]
    private SchoolClass? _selectedClass;

    public CurriculumViewModel(IDbContextFactory<SchoolScheduleDbContext> contextFactory, ISnackbarService snackbar)
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

        AllSubjects.Clear();
        foreach (var subject in await _context.Subjects.OrderBy(s => s.Name).ToListAsync())
            AllSubjects.Add(subject);

        SelectedClass ??= AllClasses.FirstOrDefault();
        await ReloadGroupsAsync();
    }

    partial void OnSelectedClassChanged(SchoolClass? value) => _ = ReloadGroupsAsync();

    /// <summary>internal — чтобы тесты могли детерминированно дождаться перезагрузки при смене класса,
    /// не полагаясь на fire-and-forget вызов из OnSelectedClassChanged (см. TeachersViewModel).</summary>
    internal async Task ReloadGroupsAsync()
    {
        Groups.Clear();
        if (_context is null || SelectedClass is null) return;

        var groups = await _context.ClassSubjectGroups
            .Include(g => g.Subject)
            .Where(g => g.ClassId == SelectedClass.Id)
            .OrderBy(g => g.Subject.Name).ThenBy(g => g.GroupLabel)
            .ToListAsync();
        foreach (var group in groups)
            Groups.Add(group);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (_context is null || SelectedClass is null)
        {
            _snackbar.Show("Сначала выберите класс");
            return;
        }
        if (AllSubjects.Count == 0)
        {
            _snackbar.Show("Сначала добавьте хотя бы один предмет на странице «Предметы»");
            return;
        }

        var group = new ClassSubjectGroup
        {
            ClassId = SelectedClass.Id,
            SubjectId = AllSubjects[0].Id,
            LessonsPerWeek = 1,
        };
        _context.ClassSubjectGroups.Add(group);
        if (await TrySaveAsync("добавить предмет в учебный план"))
        {
            group.Subject = AllSubjects[0];
            Groups.Add(group);
        }
        else
        {
            _context.ClassSubjectGroups.Remove(group);
        }
    }

    [RelayCommand]
    private async Task SaveGroupAsync(ClassSubjectGroup group)
    {
        await TrySaveAsync("сохранить учебный план");
    }

    [RelayCommand]
    private async Task DeleteAsync(ClassSubjectGroup group)
    {
        if (_context is null) return;

        _context.ClassSubjectGroups.Remove(group);
        if (await TrySaveAsync("удалить строку учебного плана — возможно, на неё уже назначен учитель или составлено расписание"))
        {
            Groups.Remove(group);
        }
        else
        {
            _context.Entry(group).State = EntityState.Unchanged;
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
