using MaterialDesignThemes.Wpf;

namespace SchoolSchedule.App.ViewModels;

/// <summary>Один пункт бокового меню навигации.</summary>
public class NavigationItem
{
    public required string Title { get; init; }
    public required PackIconKind Icon { get; init; }

    /// <summary>Секция меню (используется для группировки в сайдбаре) — например, "СПРАВОЧНИКИ".</summary>
    public required string Group { get; init; }

    /// <summary>Содержимое, которое показывается справа при выборе пункта (пока — заглушка, экраны появятся в следующих фазах).</summary>
    public required object Page { get; init; }
}
