using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Symphex.Converters
{
    public static class ObjectConverters
    {
        public static readonly IValueConverter IsNull = new FuncValueConverter<object?, bool>(x => x is null);
        public static readonly IValueConverter IsNotNull = new FuncValueConverter<object?, bool>(x => x is not null);
    }

    public static class StringConverters
    {
        public static readonly IValueConverter IsNotNullOrEmpty = new FuncValueConverter<string?, bool>(x => !string.IsNullOrEmpty(x));
        public static readonly IValueConverter IsNullOrEmpty = new FuncValueConverter<string?, bool>(x => string.IsNullOrEmpty(x));
    }

    public class PercentageToWidthConverter : IValueConverter
    {
        public static readonly PercentageToWidthConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
               
                return (percentage / 100.0) * 200;
            }
            return 0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}