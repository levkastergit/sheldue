using System.Globalization;
using System.Windows.Data;

namespace SchoolSchedule.App.Converters;

/// <summary>
/// Связывает RadioButton.IsChecked с одним значением enum-свойства через ConverterParameter —
/// сколько RadioButton, столько и значений enum, у каждого свой ConverterParameter (имя значения).
/// </summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is not null && parameter is string parameterString && value.ToString() == parameterString;

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && parameter is string parameterString ? Enum.Parse(targetType, parameterString) : Binding.DoNothing;
}
