using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace SchoolSchedule.App.Views;

/// <summary>
/// Заглушка для разделов, которые ещё не реализованы (появятся в следующих фазах разработки).
/// </summary>
public partial class PlaceholderPage : UserControl
{
    public static readonly DependencyProperty SectionTitleProperty =
        DependencyProperty.Register(nameof(SectionTitle), typeof(string), typeof(PlaceholderPage), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(PackIconKind), typeof(PlaceholderPage), new PropertyMetadata(PackIconKind.Information));

    public string SectionTitle
    {
        get => (string)GetValue(SectionTitleProperty);
        set => SetValue(SectionTitleProperty, value);
    }

    public PackIconKind Icon
    {
        get => (PackIconKind)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public PlaceholderPage()
    {
        InitializeComponent();
    }
}
