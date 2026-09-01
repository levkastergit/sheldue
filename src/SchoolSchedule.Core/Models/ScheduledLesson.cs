namespace SchoolSchedule.Core.Models;

/// <summary>
/// Один урок в базовом шаблоне "обычной недели" — результат генерации расписания.
/// TeacherId — снимок учителя на момент генерации (не смотрит "вживую" на ClassSubjectGroup.TeacherId,
/// чтобы смена назначения не портила уже посчитанное расписание без явной перегенерации).
/// </summary>
public class ScheduledLesson
{
    public int Id { get; set; }

    public int ClassSubjectGroupId { get; set; }
    public ClassSubjectGroup ClassSubjectGroup { get; set; } = null!;

    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public int TimeSlotId { get; set; }
    public TimeSlot TimeSlot { get; set; } = null!;

    public List<Substitution> Substitutions { get; set; } = [];
}
