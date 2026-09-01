using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SchoolSchedule.App.Helpers;
using SchoolSchedule.App.Services;
using SchoolSchedule.Core.Models;
using SchoolSchedule.Data;
using SchoolSchedule.Scheduling;

namespace SchoolSchedule.App.ViewModels;

/// <summary>
/// Расписание: настройки сетки (дни/уроки в смену), запуск генерации (CP-SAT, см.
/// SchoolSchedule.Scheduling.ScheduleSolver) и просмотр результата — по классам или по учителям.
/// </summary>
public partial class ScheduleViewModel : ObservableObject
{
    private readonly IDbContextFactory<SchoolScheduleDbContext> _contextFactory;
    private readonly ISnackbarService _snackbar;
    private SchoolScheduleDbContext? _context;

    private ScheduleSettings _settings = new();
    private List<TimeSlot> _timeSlots = [];
    private List<ScheduledLesson> _lessons = [];

    public ObservableCollection<SchoolClass> AllClasses { get; } = [];
    public ObservableCollection<Teacher> AllTeachers { get; } = [];
    public ObservableCollection<ScheduleGridRow> GridRows { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];

    [ObservableProperty]
    private string[] _dayHeaders = [];

    [ObservableProperty]
    private int _daysPerWeek = 6;

    [ObservableProperty]
    private int _periodsPerDayShift1 = 7;

    [ObservableProperty]
    private int _periodsPerDayShift2 = 7;

    [ObservableProperty]
    private bool _hasSecondShift;

    /// <summary>false — вид "по классам", true — "по учителям".</summary>
    [ObservableProperty]
    private bool _viewByTeacher;

    [ObservableProperty]
    private SchoolClass? _selectedClassForView;

    [ObservableProperty]
    private Teacher? _selectedTeacherForView;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _statusIsError;

    [ObservableProperty]
    private bool _hasGeneratedSchedule;

    public ScheduleViewModel(IDbContextFactory<SchoolScheduleDbContext> contextFactory, ISnackbarService snackbar)
    {
        _contextFactory = contextFactory;
        _snackbar = snackbar;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        _context?.Dispose();
        _context = await _contextFactory.CreateDbContextAsync();

        var existingSettings = await _context.ScheduleSettings.FirstOrDefaultAsync();
        _settings = existingSettings ?? new ScheduleSettings();
        if (existingSettings is null)
        {
            _context.ScheduleSettings.Add(_settings);
            await TrySaveAsync("создать настройки сетки расписания по умолчанию");

            // Сразу создаём и слоты сетки по этим дефолтным настройкам — иначе на чистой
            // установке «Сгенерировать» было бы недоступно, пока пользователь не зайдёт на
            // вкладку и не нажмёт «Сохранить настройки сетки» вручную, даже ничего не меняя.
            foreach (var slot in TimeSlotGenerator.Generate(_settings))
                _context.TimeSlots.Add(slot);
            await TrySaveAsync("создать урочные слоты по умолчанию");
        }

        DaysPerWeek = _settings.DaysPerWeek;
        PeriodsPerDayShift1 = _settings.PeriodsPerDayShift1;
        PeriodsPerDayShift2 = _settings.PeriodsPerDayShift2;
        HasSecondShift = _settings.HasSecondShift;

        AllClasses.Clear();
        foreach (var c in await _context.SchoolClasses.OrderBy(c => c.Grade).ThenBy(c => c.Name).ToListAsync())
            AllClasses.Add(c);

        AllTeachers.Clear();
        foreach (var t in await _context.Teachers.OrderBy(t => t.FullName).ToListAsync())
            AllTeachers.Add(t);

        SelectedClassForView ??= AllClasses.FirstOrDefault();
        SelectedTeacherForView ??= AllTeachers.FirstOrDefault();

        await ReloadScheduleDataAsync();
    }

    private async Task ReloadScheduleDataAsync()
    {
        if (_context is null) return;

        _timeSlots = await _context.TimeSlots.ToListAsync();
        _lessons = await _context.ScheduledLessons
            .Include(l => l.ClassSubjectGroup).ThenInclude(g => g.Class)
            .Include(l => l.ClassSubjectGroup).ThenInclude(g => g.Subject)
            .Include(l => l.Teacher)
            .Include(l => l.Room)
            .Include(l => l.TimeSlot)
            .ToListAsync();

        HasGeneratedSchedule = _lessons.Count > 0;

        RebuildDayHeaders();
        RebuildGrid();
    }

    partial void OnViewByTeacherChanged(bool value) => RebuildGrid();
    partial void OnSelectedClassForViewChanged(SchoolClass? value) => RebuildGrid();
    partial void OnSelectedTeacherForViewChanged(Teacher? value) => RebuildGrid();

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (_context is null) return;

        if (DaysPerWeek is < 1 or > 6)
        {
            _snackbar.Show("Дней в неделю — от 1 до 6");
            return;
        }
        if (PeriodsPerDayShift1 < 1 || (HasSecondShift && PeriodsPerDayShift2 < 1))
        {
            _snackbar.Show("Число уроков в смену должно быть не меньше 1");
            return;
        }

        // Сетка меняется — уже сгенерированное расписание ей больше не соответствует. Сначала
        // удаляем уроки и старые слоты (в таком порядке из-за внешнего ключа) и сохраняем...
        _context.ScheduledLessons.RemoveRange(await _context.ScheduledLessons.ToListAsync());
        _context.TimeSlots.RemoveRange(await _context.TimeSlots.ToListAsync());

        _settings.DaysPerWeek = DaysPerWeek;
        _settings.PeriodsPerDayShift1 = PeriodsPerDayShift1;
        _settings.PeriodsPerDayShift2 = PeriodsPerDayShift2;
        _settings.HasSecondShift = HasSecondShift;

        if (!await TrySaveAsync("сохранить настройки сетки расписания"))
            return;

        // ...а затем отдельным сохранением вставляем новые слоты — если делать это в одном
        // SaveChanges с удалением, есть риск конфликта уникального индекса (Смена+День+Урок) между
        // ещё не удалённой старой строкой и уже добавляемой новой с тем же сочетанием.
        foreach (var slot in TimeSlotGenerator.Generate(_settings))
            _context.TimeSlots.Add(slot);
        await TrySaveAsync("создать урочные слоты новой сетки");

        _snackbar.Show("Настройки сохранены, сетка пересчитана. Нажмите «Сгенерировать расписание» ниже.");
        await ReloadScheduleDataAsync();
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (_context is null) return;

        IsGenerating = true;
        StatusMessage = null;
        StatusIsError = false;
        Warnings.Clear();
        try
        {
            var rooms = await _context.Rooms
                .Include(r => r.RoomType)
                .Include(r => r.AssignedTeachers)
                .ToListAsync();
            var timeSlots = await _context.TimeSlots.ToListAsync();
            var groups = await _context.ClassSubjectGroups
                .Include(g => g.Class)
                .Include(g => g.Subject)
                .Include(g => g.Teacher)
                .ToListAsync();
            var unavailabilities = await _context.TeacherUnavailabilities.ToListAsync();

            var input = new ScheduleInput
            {
                Rooms = rooms,
                TimeSlots = timeSlots,
                Groups = groups,
                Unavailabilities = unavailabilities,
            };

            // CP-SAT солвер синхронный и может занять заметное время — уводим на фоновый поток,
            // чтобы UI не подвисал во время генерации.
            var result = await Task.Run(() => new ScheduleSolver().Generate(input));

            StatusMessage = result.Message;
            StatusIsError = result.Status is ScheduleGenerationStatus.Infeasible
                or ScheduleGenerationStatus.TimedOut
                or ScheduleGenerationStatus.NoData;
            foreach (var w in result.Warnings)
                Warnings.Add(w);

            if (result.Status is ScheduleGenerationStatus.Success or ScheduleGenerationStatus.PartialSuccess)
            {
                _context.ScheduledLessons.RemoveRange(await _context.ScheduledLessons.ToListAsync());
                if (!await TrySaveAsync("очистить прежнее расписание перед сохранением нового"))
                    return;

                foreach (var lesson in result.Lessons)
                {
                    _context.ScheduledLessons.Add(new ScheduledLesson
                    {
                        ClassSubjectGroupId = lesson.ClassSubjectGroupId,
                        TeacherId = lesson.TeacherId,
                        RoomId = lesson.RoomId,
                        TimeSlotId = lesson.TimeSlotId,
                    });
                }
                await TrySaveAsync("сохранить сгенерированное расписание");
                await ReloadScheduleDataAsync();
            }
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private static readonly string[] DayShortNames = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб"];

    private void RebuildDayHeaders() => DayHeaders = DayShortNames.Take(Math.Max(_settings.DaysPerWeek, 1)).ToArray();

    private void RebuildGrid()
    {
        GridRows.Clear();
        if (_settings.DaysPerWeek <= 0) return;

        var days = Enumerable.Range(1, _settings.DaysPerWeek).Select(d => (SchoolDay)d).ToList();

        if (!ViewByTeacher)
        {
            var cls = SelectedClassForView;
            if (cls is null) return;

            var shiftSlots = _timeSlots.Where(t => t.Shift == cls.Shift).ToList();
            var periods = shiftSlots.Select(t => t.PeriodNumber).Distinct().OrderBy(p => p).ToList();
            var classLessons = _lessons.Where(l => l.ClassSubjectGroup.ClassId == cls.Id).ToList();

            foreach (var period in periods)
            {
                var row = new ScheduleGridRow($"{period} урок");
                foreach (var day in days)
                {
                    var slot = shiftSlots.FirstOrDefault(t => t.PeriodNumber == period && t.Day == day);
                    var lesson = slot is null ? null : classLessons.FirstOrDefault(l => l.TimeSlotId == slot.Id);
                    row.Days.Add(BuildCell(lesson, showClass: false));
                }
                GridRows.Add(row);
            }
        }
        else
        {
            var teacher = SelectedTeacherForView;
            if (teacher is null) return;

            var teacherLessons = _lessons.Where(l => l.TeacherId == teacher.Id).ToList();
            var relevantShifts = teacherLessons.Select(l => l.TimeSlot.Shift).Distinct().ToList();
            if (relevantShifts.Count == 0)
                relevantShifts = [Shift.Первая];

            var shiftSlots = _timeSlots.Where(t => relevantShifts.Contains(t.Shift)).ToList();
            var rows = shiftSlots
                .Select(t => (t.Shift, t.PeriodNumber))
                .Distinct()
                .OrderBy(r => r.Shift).ThenBy(r => r.PeriodNumber)
                .ToList();

            foreach (var (shift, period) in rows)
            {
                var label = relevantShifts.Count > 1 ? $"{period} ур. ({shift})" : $"{period} урок";
                var row = new ScheduleGridRow(label);
                foreach (var day in days)
                {
                    var slot = shiftSlots.FirstOrDefault(t => t.Shift == shift && t.PeriodNumber == period && t.Day == day);
                    var lesson = slot is null ? null : teacherLessons.FirstOrDefault(l => l.TimeSlotId == slot.Id);
                    row.Days.Add(BuildCell(lesson, showClass: true));
                }
                GridRows.Add(row);
            }
        }
    }

    private static ScheduleCell BuildCell(ScheduledLesson? lesson, bool showClass)
    {
        if (lesson is null) return new ScheduleCell();

        var label = lesson.ClassSubjectGroup.GroupLabel is null ? "" : $" ({lesson.ClassSubjectGroup.GroupLabel})";
        var secondary = showClass
            ? lesson.ClassSubjectGroup.Class.Name + label
            : lesson.Teacher.FullName + label;

        return new ScheduleCell
        {
            SubjectName = lesson.ClassSubjectGroup.Subject.Name,
            SecondaryLine = secondary,
            RoomName = lesson.Room.Name,
        };
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
