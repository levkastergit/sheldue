namespace SchoolSchedule.Core.Models;

/// <summary>Учебный предмет.</summary>
public class Subject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Если задано — урок можно ставить только в кабинет такого типа (например, физкультура → Спортзал).</summary>
    public int? RequiredRoomTypeId { get; set; }
    public RoomType? RequiredRoomType { get; set; }

    public List<TeacherSubject> TeacherSubjects { get; set; } = [];
    public List<ClassSubjectGroup> ClassSubjectGroups { get; set; } = [];
}
