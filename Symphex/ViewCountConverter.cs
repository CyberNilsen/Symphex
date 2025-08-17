using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Symphex.Converters
{
    public class NumberToVisibilityConverter : IValueConverter
    {
        public static readonly NumberToVisibilityConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is long number && number > 0)
            {
                return true;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}