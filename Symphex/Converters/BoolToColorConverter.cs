using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Symphex.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public static readonly BoolToColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isActive && isActive)
            {
                return new SolidColorBrush(Color.Parse("#1e40af")); // Blue when active
            }
            return new SolidColorBrush(Color.Parse("#1a1a1a")); // Dark when inactive
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
