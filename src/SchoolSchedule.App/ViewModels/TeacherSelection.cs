using CommunityToolkit.Mvvm.ComponentModel;
using SchoolSchedule.Core.Models;

namespace SchoolSchedule.App.ViewModels;

/// <summary>Строка чек-листа "за какими учителями закреплён этот кабинет" в детальной панели кабинета.</summary>
public partial class TeacherSelection(Teacher teacher, bool isSelected) : ObservableObject
{
    public Teacher Teacher { get; } = teacher;
    public string Name => Teacher.FullName;

    [ObservableProperty]
    private bool _isSelected = isSelected;
}
