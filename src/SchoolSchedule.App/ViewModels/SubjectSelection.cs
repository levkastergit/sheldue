using CommunityToolkit.Mvvm.ComponentModel;
using SchoolSchedule.Core.Models;

namespace SchoolSchedule.App.ViewModels;

/// <summary>Строка чек-листа "какие предметы учитель вправе вести" в детальной панели учителя.</summary>
public partial class SubjectSelection(Subject subject, bool isSelected) : ObservableObject
{
    public Subject Subject { get; } = subject;
    public string Name => Subject.Name;

    [ObservableProperty]
    private bool _isSelected = isSelected;
}
