namespace SchoolSchedule.Core.Models;

/// <summary>
/// Единственная строка настроек сетки расписания (singleton, Id всегда 1).
/// Из неё генерируется набор <see cref="TimeSlot"/>.
/// </summary>
public class ScheduleSettings
{
    // Без явного дефолта у Id (обычный 0, как у всех остальных сущностей) — это чисто
    // in-memory значение нового объекта до сохранения; реальный Id всегда назначает
    // автоинкремент SQLite. Явный "= 1" здесь раньше ломал проверку "ещё не сохранено"
    // (Id == 0) в ScheduleViewModel — новые настройки выглядели уже сохранёнными, хотя
    // в базе строки не было.
    public int Id { get; set; }

    public int DaysPerWeek { get; set; } = 6;
    public int PeriodsPerDayShift1 { get; set; } = 7;
    public int PeriodsPerDayShift2 { get; set; } = 7;

    /// <summary>Используется ли вторая смена в школе.</summary>
    public bool HasSecondShift { get; set; }
}

/// <summary>Один урочный слот в недельной сетке: смена + день + номер урока (+ время, для отображения).</summary>
public class TimeSlot
{
    public int Id { get; set; }

    public Shift Shift { get; set; }
    public SchoolDay Day { get; set; }
    public int PeriodNumber { get; set; }

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public List<ScheduledLesson> ScheduledLessons { get; set; } = [];
}
