namespace SchoolSchedule.Core.Models;

/// <summary>
/// Тип/назначение кабинета (например, "Спортзал", "Лаборатория"). В отличие от
/// первой версии, список типов не зашит в код — администратор пополняет его сам
/// на странице "Кабинеты".
/// </summary>
public class RoomType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<Room> Rooms { get; set; } = [];
    public List<Subject> SubjectsRequiringThis { get; set; } = [];
}
