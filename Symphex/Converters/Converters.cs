using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Symphex.Converters
{
    public class NumberToVisibilityConverter : IValueConverter
    {
        public static readonly NumberToVisibilityConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null) return false;

            switch (value)
            {
                case long longValue:
                    return longValue > 0;
                case int intValue:
                    return intValue > 0;
                case double doubleValue:
                    return doubleValue > 0;
                case float floatValue:
                    return floatValue > 0;
                case decimal decimalValue:
                    return decimalValue > 0;
                default:
                    if (long.TryParse(value.ToString(), out long parsedValue))
                    {
                        return parsedValue > 0;
                    }
                    return false;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public static class ObjectConverters
    {
        public static readonly IValueConverter IsNull = new FuncValueConverter<object?, bool>(x => x is null);
        public static readonly IValueConverter IsNotNull = new FuncValueConverter<object?, bool>(x => x is not null);
    }

    public static class StringConverters
    {
        public static readonly IValueConverter IsNotNullOrEmpty = new FuncValueConverter<object?, bool>(x => !string.IsNullOrEmpty(x?.ToString()));
        public static readonly IValueConverter IsNullOrEmpty = new FuncValueConverter<object?, bool>(x => string.IsNullOrEmpty(x?.ToString()));
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

    public class BoolToAlbumArtLabelConverter : IValueConverter
    {
        public static readonly BoolToAlbumArtLabelConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool hasRealAlbumArt)
            {
              
                return hasRealAlbumArt ? "ALBUM ART" : "";
            }
            return "SEARCHING";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToAlbumArtStatusConverter : IValueConverter
    {
        public static readonly BoolToAlbumArtStatusConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool hasRealAlbumArt)
            {
              
                return hasRealAlbumArt ? "✅ Real Album Art Found" : "";
            }
            return "🔍 Searching for Album Art...";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToAlbumArtStatusColorConverter : IValueConverter
    {
        public static readonly BoolToAlbumArtStatusColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool hasRealAlbumArt)
            {
                return hasRealAlbumArt ? new SolidColorBrush(Color.FromRgb(0, 150, 0)) : new SolidColorBrush(Color.FromRgb(200, 140, 0));
            }
            return new SolidColorBrush(Color.FromRgb(100, 100, 100));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}