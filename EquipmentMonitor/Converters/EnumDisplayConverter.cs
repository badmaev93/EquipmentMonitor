using System.Globalization;
using System.Windows.Data;
using EquipmentMonitor.Models;

namespace EquipmentMonitor.Converters;

public class EnumDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DeviceCategory.Server => "Сервер",
            DeviceCategory.Printer => "Принтер",
            DeviceCategory.PC => "Компьютер",
            DeviceStatus.Working => "Работает",
            DeviceStatus.Broken => "Сломано",
            DeviceStatus.Decommissioned => "Списано",
            _ => value?.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
