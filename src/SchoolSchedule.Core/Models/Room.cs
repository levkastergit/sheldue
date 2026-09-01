namespace SchoolSchedule.Core.Models;

/// <summary>Кабинет школы.</summary>
public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }

    public int RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    public List<ScheduledLesson> ScheduledLessons { get; set; } = [];

    /// <summary>Учителя, за которыми закреплён этот кабинет (может быть несколько).</summary>
    public List<RoomTeacherAssignment> AssignedTeachers { get; set; } = [];
}
