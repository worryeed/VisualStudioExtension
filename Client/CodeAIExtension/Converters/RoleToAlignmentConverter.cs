using CodeAIExtension.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CodeAIExtension.Converters;

public class RoleToAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Role r && r == Role.User
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}