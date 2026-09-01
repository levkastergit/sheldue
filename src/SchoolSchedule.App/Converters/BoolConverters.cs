using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SchoolSchedule.App.Converters;

/// <summary>true -> Visible, false -> Collapsed.</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

/// <summary>true -> Collapsed, false -> Visible (обратное к <see cref="BoolToVisibilityConverter"/>).</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not Visibility.Visible;
}

/// <summary>Отрицание bool — например, чтобы включить кнопку, пока идёт НЕ выполнение операции.</summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && !b;
}
