namespace SchoolSchedule.Core.Models;

/// <summary>Период отсутствия учителя (болезнь, отпуск и т.п.) — запускает подбор замен.</summary>
public class Absence
{
    public int Id { get; set; }

    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public AbsenceReason Reason { get; set; }
    public string? Note { get; set; }
}
