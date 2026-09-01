using SchoolSchedule.Core.Models;

namespace SchoolSchedule.Scheduling;

/// <summary>
/// Всё, что нужно солверу — вызывающая сторона (App) собирает один согласованный снимок из базы
/// (с нужными Include) и передаёт сюда; сам солвер в базу не ходит.
/// </summary>
public class ScheduleInput
{
    /// <summary>Кабинеты — с загруженными RoomType и AssignedTeachers.</summary>
    public required List<Room> Rooms { get; init; }

    /// <summary>Урочные слоты сетки (сгенерированы из ScheduleSettings через TimeSlotGenerator).</summary>
    public required List<TimeSlot> TimeSlots { get; init; }

    /// <summary>Строки учебного плана — с загруженными Class, Subject, Teacher.</summary>
    public required List<ClassSubjectGroup> Groups { get; init; }

    public required List<TeacherUnavailability> Unavailabilities { get; init; }

    /// <summary>Сколько солверу разрешено думать, прежде чем вернуть лучшее найденное решение
    /// или признать (либо не смочь опровергнуть) неразрешимость.</summary>
    public TimeSpan TimeLimit { get; init; } = TimeSpan.FromSeconds(45);
}
