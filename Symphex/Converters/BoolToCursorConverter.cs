using Avalonia.Data.Converters;
using Avalonia.Input;
using System;
using System.Globalization;

namespace Symphex.Converters
{
    public class BoolToCursorConverter : IValueConverter
    {
        public static readonly BoolToCursorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isResizeMode && isResizeMode)
            {
                return new Cursor(StandardCursorType.SizeNorthSouth); // Vertical resize cursor
            }
            return new Cursor(StandardCursorType.Arrow); // Default cursor
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
