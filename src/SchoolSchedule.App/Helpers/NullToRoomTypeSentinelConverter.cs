using System.Globalization;
using System.Windows.Data;
using SchoolSchedule.Core.Models;

namespace SchoolSchedule.App.Helpers;

/// <summary>
/// Для полей вида "необязательный требуемый тип кабинета": null (реальное значение "не важно")
/// нужно показывать в ComboBox как выбранную синтетическую запись-заглушку — просто SelectedItem
/// на null никогда не совпадёт ни с одним элементом ItemsSource, список окажется без выделения.
/// Sentinel — общий экземпляр (не создаётся заново при каждой загрузке), чтобы ссылочное
/// сравнение в WPF совпадало.
/// </summary>
public class NullToRoomTypeSentinelConverter : IValueConverter
{
    public static readonly RoomType Sentinel = new() { Id = 0, Name = "Не важно" };

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) => value ?? Sentinel;

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is RoomType { Id: 0 } ? null : value;
}
