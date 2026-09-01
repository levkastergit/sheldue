namespace SchoolSchedule.Core.Models;

/// <summary>Закрепление кабинета за учителем — кабинет может быть закреплён сразу за несколькими учителями.</summary>
public class RoomTeacherAssignment
{
    public int Id { get; set; }

    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
}
