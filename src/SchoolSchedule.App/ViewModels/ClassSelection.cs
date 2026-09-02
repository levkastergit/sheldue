using CommunityToolkit.Mvvm.ComponentModel;
using SchoolSchedule.Core.Models;

namespace SchoolSchedule.App.ViewModels;

/// <summary>Строка чек-листа классов в попапе "Применить ко многим классам сразу" на странице
/// "Учебный план".</summary>
public partial class ClassSelection(SchoolClass schoolClass, bool isSelected) : ObservableObject
{
    public SchoolClass Class { get; } = schoolClass;
    public string Name => Class.Name;
    public int Grade => Class.Grade;

    [ObservableProperty]
    private bool _isSelected = isSelected;
}

/// <summary>Один пункт фильтра "показать целиком эту параллель" на странице "Учебный план" —
/// Grade == null означает "все параллели" (обычный просмотр по одному выбранному классу).</summary>
public class GradeFilterOption(int? grade, string label)
{
    public int? Grade { get; } = grade;
    public string Label { get; } = label;
}
