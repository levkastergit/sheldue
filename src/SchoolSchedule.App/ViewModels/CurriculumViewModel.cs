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

    /// <summary>"Все параллели" (обычный просмотр по SelectedClass) либо конкретная параллель —
    /// тогда таблица показывает сразу все классы этой параллели.</summary>
    public ObservableCollection<GradeFilterOption> GradeFilterOptions { get; } = [];

    [ObservableProperty]
    private GradeFilterOption? _selectedGradeFilter;

    public bool IsGradeFilterActive => SelectedGradeFilter?.Grade is not null;

    // --- "Применить ко многим классам сразу" ---
    public ObservableCollection<ClassSelection> BulkApplyClassSelections { get; } = [];
    public ObservableCollection<int> BulkApplyGrades { get; } = [];

    [ObservableProperty]
    private bool _isBulkApplyPopupOpen;

    [ObservableProperty]
    private Subject? _bulkApplySubject;

    [ObservableProperty]
    private string _bulkApplyLessonsPerWeek = string.Empty;

    [ObservableProperty]
    private string _bulkApplyMaxLessonsPerDay = "1";

    [ObservableProperty]
    private bool _bulkApplyPairedLessons;

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

        BulkApplyGrades.Clear();
        foreach (var grade in AllClasses.Select(c => c.Grade).Distinct().OrderBy(g => g))
            BulkApplyGrades.Add(grade);
        RebuildBulkApplyClassSelections();

        var previousGradeSelection = SelectedGradeFilter?.Grade;
        GradeFilterOptions.Clear();
        GradeFilterOptions.Add(new GradeFilterOption(null, "Все параллели"));
        foreach (var grade in BulkApplyGrades)
            GradeFilterOptions.Add(new GradeFilterOption(grade, $"{grade} класс"));
        SelectedGradeFilter = GradeFilterOptions.FirstOrDefault(o => o.Grade == previousGradeSelection) ?? GradeFilterOptions[0];

        SelectedClass ??= AllClasses.FirstOrDefault();
        await ReloadGroupsAsync();
    }

    private void RebuildBulkApplyClassSelections()
    {
        BulkApplyClassSelections.Clear();
        foreach (var schoolClass in AllClasses)
            BulkApplyClassSelections.Add(new ClassSelection(schoolClass, false));
    }

    partial void OnSelectedClassChanged(SchoolClass? value)
    {
        // Выбор конкретного класса — это выход из режима "вся параллель": иначе было бы неясно,
        // что сейчас показывает таблица.
        if (IsGradeFilterActive)
            SelectedGradeFilter = GradeFilterOptions.FirstOrDefault(o => o.Grade is null);
        else
            _ = ReloadGroupsAsync();
    }

    partial void OnSelectedGradeFilterChanged(GradeFilterOption? value)
    {
        OnPropertyChanged(nameof(IsGradeFilterActive));
        _ = ReloadGroupsAsync();
    }

    /// <summary>internal — чтобы тесты могли детерминированно дождаться перезагрузки при смене класса,
    /// не полагаясь на fire-and-forget вызов из OnSelectedClassChanged (см. TeachersViewModel).</summary>
    internal async Task ReloadGroupsAsync()
    {
        Groups.Clear();
        if (_context is null) return;

        if (SelectedGradeFilter?.Grade is { } grade)
        {
            // Вся параллель сразу — строки всех её классов в одной таблице (с колонкой "Класс").
            var gradeGroups = await _context.ClassSubjectGroups
                .Include(g => g.Subject)
                .Include(g => g.Class)
                .Where(g => g.Class.Grade == grade)
                .OrderBy(g => g.Class.Name).ThenBy(g => g.Subject.Name).ThenBy(g => g.GroupLabel)
                .ToListAsync();
            foreach (var group in gradeGroups)
                Groups.Add(group);
            return;
        }

        if (SelectedClass is null) return;

        var groups = await _context.ClassSubjectGroups
            .Include(g => g.Subject)
            .Include(g => g.Class)
            .Where(g => g.ClassId == SelectedClass.Id)
            .OrderBy(g => g.Subject.Name).ThenBy(g => g.GroupLabel)
            .ToListAsync();
        foreach (var group in groups)
            Groups.Add(group);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (IsGradeFilterActive)
        {
            _snackbar.Show("В режиме «вся параллель» неясно, к какому классу добавлять — выберите конкретный класс, либо используйте «Применить ко многим классам»");
            return;
        }
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

        // Подставляем первый ещё не занятый (без подгруппы) предмет, а не всегда первый по
        // алфавиту — иначе повторное нажатие "Добавить" каждый раз пытается вставить тот же
        // предмет, что уже есть в плане, и падает на уникальном индексе, не давая дойти до
        // выбора нужного предмета в самой строке.
        var usedSubjectIds = Groups.Where(g => g.GroupLabel is null).Select(g => g.SubjectId).ToHashSet();
        var subject = AllSubjects.FirstOrDefault(s => !usedSubjectIds.Contains(s.Id)) ?? AllSubjects[0];

        var group = new ClassSubjectGroup
        {
            ClassId = SelectedClass.Id,
            SubjectId = subject.Id,
            LessonsPerWeek = 1,
        };
        _context.ClassSubjectGroups.Add(group);
        if (await TrySaveAsync("добавить предмет в учебный план"))
        {
            group.Subject = subject;
            Groups.Add(group);
        }
        else
        {
            _context.ClassSubjectGroups.Remove(group);
        }
    }

    [RelayCommand]
    private void ToggleBulkApplyPopup()
    {
        if (!IsBulkApplyPopupOpen)
            RebuildBulkApplyClassSelections(); // сброс выбора при каждом открытии — предсказуемее, чем помнить прошлый раз
        IsBulkApplyPopupOpen = !IsBulkApplyPopupOpen;
    }

    [RelayCommand]
    private void SelectGrade(int grade)
    {
        foreach (var selection in BulkApplyClassSelections.Where(s => s.Grade == grade))
            selection.IsSelected = true;
    }

    [RelayCommand]
    private void ClearBulkSelection()
    {
        foreach (var selection in BulkApplyClassSelections)
            selection.IsSelected = false;
    }

    /// <summary>Ставит один и тот же предмет (весь класс, без подгруппы) с одними и теми же
    /// часами сразу нескольким классам — например, всем пятым сразу 5 уроков русского в неделю,
    /// вместо того чтобы заходить в каждый класс по отдельности. Если у класса эта строка уже
    /// есть — обновляет её, а не создаёт дубликат (безопасно применять повторно).</summary>
    [RelayCommand]
    private async Task BulkApplyAsync()
    {
        if (_context is null) return;

        var selectedClasses = BulkApplyClassSelections.Where(s => s.IsSelected).Select(s => s.Class).ToList();
        if (selectedClasses.Count == 0)
        {
            _snackbar.Show("Выберите хотя бы один класс");
            return;
        }
        if (BulkApplySubject is null)
        {
            _snackbar.Show("Выберите предмет");
            return;
        }
        if (!int.TryParse(BulkApplyLessonsPerWeek, out var lessonsPerWeek) || lessonsPerWeek < 1)
        {
            _snackbar.Show("«Уроков/нед.» — целое число не меньше 1");
            return;
        }
        if (!int.TryParse(BulkApplyMaxLessonsPerDay, out var maxLessonsPerDay) || maxLessonsPerDay < 1)
        {
            _snackbar.Show("«Уроков/день» — целое число не меньше 1");
            return;
        }

        var classIds = selectedClasses.Select(c => c.Id).ToHashSet();
        var existingByClassId = await _context.ClassSubjectGroups
            .Where(g => classIds.Contains(g.ClassId) && g.SubjectId == BulkApplySubject.Id && g.GroupLabel == null)
            .ToDictionaryAsync(g => g.ClassId);

        var createdCount = 0;
        var updatedCount = 0;
        foreach (var schoolClass in selectedClasses)
        {
            if (existingByClassId.TryGetValue(schoolClass.Id, out var existing))
            {
                existing.LessonsPerWeek = lessonsPerWeek;
                existing.MaxLessonsPerDay = maxLessonsPerDay;
                existing.PairedLessons = BulkApplyPairedLessons;
                updatedCount++;
            }
            else
            {
                _context.ClassSubjectGroups.Add(new ClassSubjectGroup
                {
                    ClassId = schoolClass.Id,
                    SubjectId = BulkApplySubject.Id,
                    LessonsPerWeek = lessonsPerWeek,
                    MaxLessonsPerDay = maxLessonsPerDay,
                    PairedLessons = BulkApplyPairedLessons,
                });
                createdCount++;
            }
        }

        if (await TrySaveAsync("применить предмет сразу к нескольким классам"))
        {
            IsBulkApplyPopupOpen = false;
            _snackbar.Show(updatedCount == 0
                ? $"Добавлено классам: {createdCount}"
                : $"Добавлено классам: {createdCount}, обновлено (уже было): {updatedCount}");

            // Если среди затронутых классов — тот, что сейчас открыт справа, сразу обновляем и его
            // таблицу, а не только базу, иначе выглядело бы, будто применение не сработало.
            if (SelectedClass is not null && classIds.Contains(SelectedClass.Id))
                await ReloadGroupsAsync();
        }
    }

    [RelayCommand]
    private async Task SaveGroupAsync(ClassSubjectGroup group)
    {
        // Комбобокс биндится на Subject (SelectedItem) напрямую, а не на SubjectId (SelectedValue) —
        // см. комментарий в RoomsViewModel.SaveRoomAsync про то, почему это надёжнее.
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
