namespace SchoolSchedule.Core.Models;

/// <summary>Учитель.</summary>
public class Teacher
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;

    /// <summary>Максимальная нагрузка в часах/уроках в неделю (для контроля при генерации и заменах).</summary>
    public int MaxLessonsPerWeek { get; set; } = 30;

    public List<TeacherSubject> TeacherSubjects { get; set; } = [];
    public List<TeacherUnavailability> Unavailabilities { get; set; } = [];
    public List<ClassSubjectGroup> ClassSubjectGroups { get; set; } = [];
    public List<ScheduledLesson> ScheduledLessons { get; set; } = [];
    public List<Absence> Absences { get; set; } = [];

    /// <summary>Кабинеты, закреплённые за этим учителем.</summary>
    public List<RoomTeacherAssignment> AssignedRooms { get; set; } = [];
}

/// <summary>Связь "учитель вправе вести предмет" (многие-ко-многим).</summary>
public class TeacherSubject
{
    public int Id { get; set; }

    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
}

/// <summary>Регулярное окно недоступности учителя (например, не может по вторникам 1-м уроком).</summary>
public class TeacherUnavailability
{
    public int Id { get; set; }

    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    public SchoolDay Day { get; set; }
    public int PeriodNumber { get; set; }
}
