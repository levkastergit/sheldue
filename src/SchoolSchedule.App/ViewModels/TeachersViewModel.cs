using SchoolSchedule.App.Helpers;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SchoolSchedule.App.Services;
using SchoolSchedule.Core.Models;
using SchoolSchedule.Data;

namespace SchoolSchedule.App.ViewModels;

public partial class TeachersViewModel : ObservableObject
{
    private readonly IDbContextFactory<SchoolScheduleDbContext> _contextFactory;
    private readonly ISnackbarService _snackbar;
    private SchoolScheduleDbContext? _context;

    public ObservableCollection<Teacher> Teachers { get; } = [];
    public ObservableCollection<SubjectSelection> SubjectSelections { get; } = [];
    public ObservableCollection<TeacherUnavailability> Unavailabilities { get; } = [];

    [ObservableProperty]
    private Teacher? _selectedTeacher;

    [ObservableProperty]
    private SchoolDay _newUnavailabilityDay = SchoolDay.Понедельник;

    [ObservableProperty]
    private int _newUnavailabilityPeriod = 1;

    public TeachersViewModel(IDbContextFactory<SchoolScheduleDbContext> contextFactory, ISnackbarService snackbar)
    {
        _contextFactory = contextFactory;
        _snackbar = snackbar;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        _context?.Dispose();
        _context = await _contextFactory.CreateDbContextAsync();

        Teachers.Clear();
        var teachers = await _context.Teachers
            .Include(t => t.TeacherSubjects).ThenInclude(ts => ts.Subject)
            .Include(t => t.Unavailabilities)
            .OrderBy(t => t.FullName)
            .ToListAsync();
        foreach (var teacher in teachers)
            Teachers.Add(teacher);

        SelectedTeacher = Teachers.FirstOrDefault();
        await RebuildDetailAsync(SelectedTeacher);
    }

    partial void OnSelectedTeacherChanged(Teacher? value) => _ = RebuildDetailAsync(value);

    private async Task RebuildDetailAsync(Teacher? teacher)
    {
        Unavailabilities.Clear();
        SubjectSelections.Clear();
        if (teacher is null || _context is null) return;

        foreach (var unavailability in teacher.Unavailabilities.OrderBy(u => u.Day).ThenBy(u => u.PeriodNumber))
            Unavailabilities.Add(unavailability);

        var allSubjects = await _context.Subjects.OrderBy(s => s.Name).ToListAsync();
        var assignedSubjectIds = teacher.TeacherSubjects.Select(ts => ts.SubjectId).ToHashSet();

        foreach (var subject in allSubjects)
            AttachSubjectSelection(new SubjectSelection(subject, assignedSubjectIds.Contains(subject.Id)));
    }

    private void AttachSubjectSelection(SubjectSelection selection)
    {
        selection.PropertyChanged += async (_, args) =>
        {
            if (args.PropertyName == nameof(SubjectSelection.IsSelected))
                await ToggleSubjectAsync(selection);
        };
        SubjectSelections.Add(selection);
    }

    /// <summary>internal (не private) — чтобы юнит-тесты могли детерминированно дождаться завершения,
    /// не полагаясь на fire-and-forget подписку PropertyChanged.</summary>
    internal async Task ToggleSubjectAsync(SubjectSelection selection)
    {
        if (_context is null || SelectedTeacher is null) return;

        // Навигационные коллекции (SelectedTeacher.TeacherSubjects) не трогаем руками: раз оба конца связи
        // уже отслеживаются одним и тем же контекстом, EF Core сам синхронизирует их при Add/Remove на DbSet
        // (fixup по внешнему ключу). Ручное дублирование приводило к тому, что запись оставалась в списке
        // дважды и последующий Remove не долетал до базы — см. TeachersViewModelTests.
        if (selection.IsSelected)
        {
            var link = new TeacherSubject { TeacherId = SelectedTeacher.Id, SubjectId = selection.Subject.Id };
            _context.TeacherSubjects.Add(link);
            if (!await TrySaveAsync("назначить предмет учителю"))
            {
                _context.TeacherSubjects.Remove(link);
                await RebuildDetailAsync(SelectedTeacher);
            }
        }
        else
        {
            var link = SelectedTeacher.TeacherSubjects.FirstOrDefault(ts => ts.SubjectId == selection.Subject.Id);
            if (link is null) return;

            _context.TeacherSubjects.Remove(link);
            if (!await TrySaveAsync("снять предмет с учителя"))
            {
                _context.Entry(link).State = EntityState.Unchanged;
                await RebuildDetailAsync(SelectedTeacher);
            }
        }
    }

    [RelayCommand]
    private async Task AddUnavailabilityAsync()
    {
        if (_context is null || SelectedTeacher is null) return;
        if (NewUnavailabilityPeriod < 1)
        {
            _snackbar.Show("Номер урока должен быть не меньше 1");
            return;
        }

        var entry = new TeacherUnavailability
        {
            TeacherId = SelectedTeacher.Id,
            Day = NewUnavailabilityDay,
            PeriodNumber = NewUnavailabilityPeriod,
        };
        // SelectedTeacher.Unavailabilities руками не трогаем — EF Core сам синхронизирует навигационную
        // коллекцию через fixup по внешнему ключу (см. комментарий в ToggleSubjectAsync).
        _context.TeacherUnavailabilities.Add(entry);
        if (await TrySaveAsync("добавить окно недоступности"))
        {
            Unavailabilities.Add(entry);
        }
        else
        {
            _context.TeacherUnavailabilities.Remove(entry);
        }
    }

    [RelayCommand]
    private async Task RemoveUnavailabilityAsync(TeacherUnavailability entry)
    {
        if (_context is null || SelectedTeacher is null) return;

        _context.TeacherUnavailabilities.Remove(entry);
        if (await TrySaveAsync("удалить окно недоступности"))
        {
            Unavailabilities.Remove(entry);
        }
        else
        {
            _context.Entry(entry).State = EntityState.Unchanged;
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (_context is null) return;

        var teacher = new Teacher { FullName = "Новый учитель", MaxLessonsPerWeek = 30 };
        _context.Teachers.Add(teacher);
        if (await TrySaveAsync("добавить учителя"))
        {
            Teachers.Add(teacher);
            SelectedTeacher = teacher;
        }
        else
        {
            _context.Teachers.Remove(teacher);
        }
    }

    [RelayCommand]
    private async Task SaveTeacherAsync(Teacher teacher)
    {
        await TrySaveAsync("сохранить учителя");
    }

    [RelayCommand]
    private async Task DeleteAsync(Teacher? teacher)
    {
        teacher ??= SelectedTeacher;
        if (teacher is null || _context is null) return;

        _context.Teachers.Remove(teacher);
        if (await TrySaveAsync("удалить учителя — возможно, он уже назначен в учебном плане или расписании"))
        {
            Teachers.Remove(teacher);
        }
        else
        {
            _context.Entry(teacher).State = EntityState.Unchanged;
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
