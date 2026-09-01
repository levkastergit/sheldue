using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SchoolSchedule.Core.Models;

namespace SchoolSchedule.App.ViewModels;

/// <summary>
/// Одна строка списка "Назначения" — обёртка над ClassSubjectGroup с уже отфильтрованным
/// по предмету списком учителей, которых можно выбрать (только те, кто отмечен как вправе
/// вести этот предмет на странице "Учителя").
/// </summary>
public partial class AssignmentRow : ObservableObject
{
    /// <summary>
    /// Синтетическая запись "не назначен" — общий статический экземпляр (не новый на каждую
    /// строку), чтобы ComboBox с SelectedItem матчил её по ссылке что при показе, что при выборе.
    /// </summary>
    public static readonly Teacher Unassigned = new() { Id = 0, FullName = "— не назначен —" };

    public ClassSubjectGroup Group { get; }

    public ObservableCollection<Teacher> QualifiedTeachers { get; }

    public AssignmentRow(ClassSubjectGroup group, IEnumerable<Teacher> qualifiedTeachers)
    {
        Group = group;
        QualifiedTeachers = new ObservableCollection<Teacher>(new[] { Unassigned }.Concat(qualifiedTeachers));
    }

    public string ClassName => Group.Class.Name;
    public string SubjectName => Group.Subject.Name;
    public string GroupLabel => Group.GroupLabel ?? "Весь класс";
    public int LessonsPerWeek => Group.LessonsPerWeek;

    public int? TeacherId => Group.TeacherId;

    /// <summary>
    /// Комбобокс в гриде биндится сюда (SelectedItem) напрямую, а не на TeacherId (SelectedValue) —
    /// у динамически создаваемого ComboBox с SelectedValue/SelectedValuePath список иногда не готов
    /// к моменту, когда WPF ищет совпадение по Id, из-за чего поле визуально пустело до клика мимо.
    /// SelectedItem матчит элемент по ссылке — такой гонки при инициализации не бывает (тот же баг
    /// и то же решение, что и в RoomsViewModel/ClassesViewModel/CurriculumViewModel/SubjectsViewModel).
    /// </summary>
    public Teacher SelectedTeacher
    {
        get => Group.Teacher ?? Unassigned;
        set
        {
            var normalized = ReferenceEquals(value, Unassigned) ? null : value;
            if (Group.Teacher == normalized) return;
            Group.Teacher = normalized;
            Group.TeacherId = normalized?.Id;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TeacherId));
            OnPropertyChanged(nameof(TeacherName));
        }
    }

    public string TeacherName => Group.Teacher?.FullName ?? "— не назначен —";
}
