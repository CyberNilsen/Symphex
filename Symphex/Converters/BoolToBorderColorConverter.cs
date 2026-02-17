using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Symphex.Converters
{
    public class BoolToBorderColorConverter : IValueConverter
    {
        public static readonly BoolToBorderColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isActive && isActive)
            {
                return new SolidColorBrush(Color.Parse("#3b82f6")); // Bright blue when active
            }
            return new SolidColorBrush(Color.Parse("#404040")); // Gray when inactive
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
