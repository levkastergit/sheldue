using SchoolSchedule.Core.Models;

namespace SchoolSchedule.App.Helpers;

/// <summary>Готовые списки значений enum-ов для ItemsSource выпадающих списков в XAML (через x:Static).</summary>
public static class EnumSource
{
    public static Shift[] Shifts { get; } = Enum.GetValues<Shift>();

    public static SchoolDay[] SchoolDays { get; } = Enum.GetValues<SchoolDay>();
}
