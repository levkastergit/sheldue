using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SchoolSchedule.App.Converters;

/// <summary>null -> Collapsed, значение есть -> Visible.</summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>null -> Visible, значение есть -> Collapsed (обратное к <see cref="NullToVisibilityConverter"/>).</summary>
public class NullToInverseVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
