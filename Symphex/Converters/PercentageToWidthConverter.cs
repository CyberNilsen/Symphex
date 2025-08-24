using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Symphex.Converters
{
    public class ProgressToWidthConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double progress && progress >= 0)
            {
                // Convert percentage (0-100) to width for progress bar
                // Assuming container width of 300px, adjust as needed
                double width = Math.Max(8, (progress / 100.0) * 300);
                return width;
            }
            return 8.0; // Minimum width
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}