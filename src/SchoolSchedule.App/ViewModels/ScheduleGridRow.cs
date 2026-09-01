namespace SchoolSchedule.App.ViewModels;

/// <summary>Одна ячейка недельной сетки расписания — урок или пусто (SubjectName == null).</summary>
public class ScheduleCell
{
    public string? SubjectName { get; init; }

    /// <summary>По классам — ФИО учителя (+подгруппа); по учителям — класс (+подгруппа).</summary>
    public string? SecondaryLine { get; init; }

    public string? RoomName { get; init; }
}

/// <summary>Одна строка сетки — конкретный урок недели (смена+номер), с ячейкой на каждый день.</summary>
public class ScheduleGridRow(string periodLabel)
{
    public string PeriodLabel { get; } = periodLabel;

    public List<ScheduleCell> Days { get; } = [];
}
