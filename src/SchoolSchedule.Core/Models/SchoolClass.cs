namespace SchoolSchedule.Core.Models;

/// <summary>Класс (например, "5А").</summary>
public class SchoolClass
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Grade { get; set; }
    public Shift Shift { get; set; } = Shift.Первая;
    public int StudentCount { get; set; }

    public int? HomeRoomId { get; set; }
    public Room? HomeRoom { get; set; }

    public List<ClassSubjectGroup> SubjectGroups { get; set; } = [];
}
