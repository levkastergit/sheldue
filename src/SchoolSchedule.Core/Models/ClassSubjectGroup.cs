namespace SchoolSchedule.Core.Models;

/// <summary>
/// Единица учебного плана и назначения: предмет для класса (или для подгруппы класса).
/// Если <see cref="GroupLabel"/> == null — предмет ведётся для всего класса целиком.
/// Если предмет делится на подгруппы (иностранный язык, информатика и т.п.) — для одного
/// (ClassId, SubjectId) заводится несколько записей с разными GroupLabel ("Группа 1", "Группа 2"),
/// которые при генерации расписания встают в один и тот же TimeSlot, но с разными
/// учителями/кабинетами.
/// </summary>
public class ClassSubjectGroup
{
    public int Id { get; set; }

    public int ClassId { get; set; }
    public SchoolClass Class { get; set; } = null!;

    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    /// <summary>null — весь класс; иначе название подгруппы ("Группа 1" и т.п.).</summary>
    public string? GroupLabel { get; set; }

    /// <summary>Сколько уроков этого предмета/подгруппы в неделю — данные учебного плана.</summary>
    public int LessonsPerWeek { get; set; }

    /// <summary>Учитель, назначенный вести — заполняется на шаге "Назначения", может быть не заполнен.</summary>
    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public List<ScheduledLesson> ScheduledLessons { get; set; } = [];
}
