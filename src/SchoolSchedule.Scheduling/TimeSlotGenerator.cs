using SchoolSchedule.Core.Models;

namespace SchoolSchedule.Scheduling;

/// <summary>
/// Строит набор <see cref="TimeSlot"/> из настроек сетки (<see cref="ScheduleSettings"/>): для
/// каждой используемой смены — DaysPerWeek дней × соответствующее число уроков в день, с
/// фиксированной длительностью урока и перемены. Время начала — разумные дефолты для обычной
/// школы (смена 1 — с утра, смена 2 — после обеда); при необходимости несложно вынести в настройки.
/// </summary>
public static class TimeSlotGenerator
{
    private static readonly TimeSpan LessonLength = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan BreakLength = TimeSpan.FromMinutes(10);
    private static readonly TimeOnly Shift1Start = new(8, 30);
    private static readonly TimeOnly Shift2Start = new(13, 30);

    public static List<TimeSlot> Generate(ScheduleSettings settings)
    {
        var slots = new List<TimeSlot>();
        AddShift(slots, Shift.Первая, settings.DaysPerWeek, settings.PeriodsPerDayShift1, Shift1Start);
        if (settings.HasSecondShift)
            AddShift(slots, Shift.Вторая, settings.DaysPerWeek, settings.PeriodsPerDayShift2, Shift2Start);
        return slots;
    }

    private static void AddShift(List<TimeSlot> slots, Shift shift, int daysPerWeek, int periodsPerDay, TimeOnly start)
    {
        for (var day = 1; day <= daysPerWeek; day++)
        {
            var current = start;
            for (var period = 1; period <= periodsPerDay; period++)
            {
                var end = current.Add(LessonLength);
                slots.Add(new TimeSlot
                {
                    Shift = shift,
                    Day = (SchoolDay)day,
                    PeriodNumber = period,
                    StartTime = current,
                    EndTime = end,
                });
                current = end.Add(BreakLength);
            }
        }
    }
}
