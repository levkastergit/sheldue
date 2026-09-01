using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SchoolSchedule.Core.Models;

namespace SchoolSchedule.App.ViewModels;

/// <summary>
/// Одна строка списка "Назначения" — обёртка над ClassSubjectGroup с уже отфильтрованным
/// по предмету списком учителей, которых можно выбрать (только те, кто отмечен как вправе
/// вести этот предмет на странице "Учителя").
/// </summary>
public partial class AssignmentRow(ClassSubjectGroup group, IEnumerable<Teacher> qualifiedTeachers) : ObservableObject
{
    public ClassSubjectGroup Group { get; } = group;

    public string ClassName => Group.Class.Name;
    public string SubjectName => Group.Subject.Name;
    public string GroupLabel => Group.GroupLabel ?? "Весь класс";
    public int LessonsPerWeek => Group.LessonsPerWeek;

    public ObservableCollection<Teacher> QualifiedTeachers { get; } = new(qualifiedTeachers);

    public int? TeacherId
    {
        get => Group.TeacherId;
        set
        {
            if (Group.TeacherId == value) return;
            Group.TeacherId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TeacherName));
        }
    }

    public string TeacherName => QualifiedTeachers.FirstOrDefault(t => t.Id == TeacherId)?.FullName ?? "— не назначен —";
}
