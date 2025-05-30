using CodeAIExtension.Models;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CodeAIExtension.Converters;

public class RoleToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Role role
            ? role == Role.User
                ? Brushes.LightBlue
                : role == Role.Assistant
                    ? Brushes.LightGray
                    : Brushes.LightCoral
            : Brushes.Transparent;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
