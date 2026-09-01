namespace SchoolSchedule.Scheduling;

public enum ScheduleGenerationStatus
{
    /// <summary>Все годные к расстановке строки учебного плана расставлены без конфликтов.</summary>
    Success,

    /// <summary>Расписание построено, но часть строк учебного плана в него не попала — см. Warnings
    /// (например, не назначен учитель, нет подходящего кабинета или общего слота для подгрупп).</summary>
    PartialSuccess,

    /// <summary>Солвер математически доказал: с текущими данными и ограничениями расписание без
    /// конфликтов построить невозможно.</summary>
    Infeasible,

    /// <summary>Солвер не успел ни найти решение, ни доказать неразрешимость за отведённое время.</summary>
    TimedOut,

    /// <summary>Не с чем работать (нет кабинетов, нет слотов сетки, либо ни одна строка учебного
    /// плана не готова к расстановке).</summary>
    NoData,
}

/// <summary>Один урок в результате генерации — привязка строки учебного плана к учителю/кабинету/слоту.</summary>
public record GeneratedLesson(int ClassSubjectGroupId, int TeacherId, int RoomId, int TimeSlotId);

public class ScheduleGenerationResult
{
    public ScheduleGenerationStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<GeneratedLesson> Lessons { get; init; } = [];

    /// <summary>Человекочитаемые причины, по которым те или иные строки учебного плана не попали
    /// в расписание.</summary>
    public List<string> Warnings { get; init; } = [];
}
