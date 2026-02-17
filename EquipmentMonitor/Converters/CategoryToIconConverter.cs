using System.Globalization;
using System.Windows.Data;
using EquipmentMonitor.Models;

namespace EquipmentMonitor.Converters;

public class CategoryToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DeviceCategory.Server => "\uE839",
            DeviceCategory.Printer => "\uE749",
            DeviceCategory.PC => "\uE7F4",
            _ => "\uE7F4"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
