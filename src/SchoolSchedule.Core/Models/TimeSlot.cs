namespace SchoolSchedule.Core.Models;

/// <summary>
/// Единственная строка настроек сетки расписания (singleton, Id всегда 1).
/// Из неё генерируется набор <see cref="TimeSlot"/>.
/// </summary>
public class ScheduleSettings
{
    public int Id { get; set; } = 1;

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
