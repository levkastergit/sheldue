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
