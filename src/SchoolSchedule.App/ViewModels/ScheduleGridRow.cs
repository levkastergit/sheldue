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

/// <summary>
/// Одна строка общего расписания (вид "Общее") — конкретные день+смена+урок, с ячейкой на
/// каждый класс (в отличие от ScheduleGridRow, где по дням — здесь по классам, а день+урок уже
/// зафиксированы в самой строке).
/// </summary>
public class OverviewGridRow(string rowLabel)
{
    public string RowLabel { get; } = rowLabel;

    public List<ScheduleCell> ClassCells { get; } = [];
}

/// <summary>Какой срез недельного расписания сейчас отображается на вкладке.</summary>
public enum ScheduleViewMode
{
    /// <summary>Один класс — дни по горизонтали, уроки по вертикали.</summary>
    ByClass,

    /// <summary>Один учитель — дни по горизонтали, уроки по вертикали.</summary>
    ByTeacher,

    /// <summary>Все классы сразу — классы по горизонтали, день+урок по вертикали.</summary>
    Overview,
}
