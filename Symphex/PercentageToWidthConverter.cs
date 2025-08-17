using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Symphex.Converters;

public class PercentageToWidthConverter : IValueConverter
{
    public static readonly PercentageToWidthConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double percentage)
        {
            // Convert percentage (0-100) to a width relative to the container
            // This assumes the container has a fixed width of 200px for simplicity
            // You might want to make this more dynamic based on actual container size
            return (percentage / 100.0) * 200;
        }

        return 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}