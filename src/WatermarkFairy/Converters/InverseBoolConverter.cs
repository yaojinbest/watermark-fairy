using System.Globalization;
using System.Windows.Data;

namespace WatermarkFairy.Converters;

/// <summary>
/// bool 取反转换器（true → false, false → true）
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? false : true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? false : true;
    }
}