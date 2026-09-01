namespace SchoolSchedule.Core.Models;

/// <summary>
/// Оверлей поверх базового расписания на конкретную дату — результат подбора замены.
/// Не изменяет ScheduledLesson: после отпуска/болезни расписание само возвращается к норме.
/// </summary>
public class Substitution
{
    public int Id { get; set; }

    public int ScheduledLessonId { get; set; }
    public ScheduledLesson ScheduledLesson { get; set; } = null!;

    public DateOnly Date { get; set; }
    public SubstitutionStatus Status { get; set; }

    public int? SubstituteTeacherId { get; set; }
    public Teacher? SubstituteTeacher { get; set; }

    public int? SubstituteRoomId { get; set; }
    public Room? SubstituteRoom { get; set; }

    public string? Note { get; set; }
}
