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

    /// <summary>
    /// Сколько солверу разрешено думать, прежде чем вернуть лучшее найденное решение или признать
    /// (либо не смочь опровергнуть) неразрешимость. Это верхний потолок, а не обязательное
    /// ожидание — для небольшой школы решение находится за секунды независимо от лимита; большой
    /// лимит нужен только когда данных много (крупная школа: 25+ классов, 50+ учителей). Замер на
    /// синтетическом сценарии близкого масштаба (15 классов, 28 учителей, 405 уроков/нед., с
    /// минимизацией окон) — около 4.5 минут до решения без единого окна у учителей, поэтому
    /// дефолт взят с запасом.
    /// </summary>
    public TimeSpan TimeLimit { get; init; } = TimeSpan.FromMinutes(6);
}
