using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Symphex.Converters
{
    /// <summary>
    /// Converts boolean to "Searching..." or "ALBUM ART" text for metadata display
    /// </summary>
    public class BoolToSearchingTextConverter : IValueConverter
    {
        public static readonly BoolToSearchingTextConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool showMetadata)
            {
                return showMetadata ? "Searching..." : "ALBUM ART";
            }
            return "ALBUM ART";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean to album art label text (REAL ART vs GENERATED)
    /// </summary>
    public class BoolToAlbumArtLabelConverter : IValueConverter
    {
        public static readonly BoolToAlbumArtLabelConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool hasRealAlbumArt)
            {
                return hasRealAlbumArt ? "REAL ART" : "GENERATED";
            }
            return "ALBUM ART";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean to album art status text for badge
    /// </summary>
    public class BoolToAlbumArtStatusConverter : IValueConverter
    {
        public static readonly BoolToAlbumArtStatusConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool hasRealAlbumArt)
            {
                return hasRealAlbumArt ? "✓ Real Album Art" : "⚠ Generated Art";
            }
            return "Album Art";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean to background color for album art status
    /// </summary>
    public class BoolToAlbumArtStatusColorConverter : IValueConverter
    {
        public static readonly BoolToAlbumArtStatusColorConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool hasRealAlbumArt)
            {
                // Green for real album art, orange for generated
                return hasRealAlbumArt
                    ? new SolidColorBrush(Color.FromRgb(34, 197, 94))   // Green-500
                    : new SolidColorBrush(Color.FromRgb(249, 115, 22)); // Orange-500
            }
            return new SolidColorBrush(Color.FromRgb(107, 114, 128)); // Gray-500
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts number to visibility (visible if > 0)
    /// </summary>
    public class NumberToVisibilityConverter : IValueConverter
    {
        public static readonly NumberToVisibilityConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue > 0 ? true : false;
            }
            if (value is long longValue)
            {
                return longValue > 0 ? true : false;
            }
            if (value is double doubleValue)
            {
                return doubleValue > 0 ? true : false;
            }
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts percentage (0-100) to width for progress bar
    /// Assumes a parent width context - you may need to adjust this based on your layout
    /// </summary>
    public class PercentageToWidthConverter : IValueConverter
    {
        public static readonly PercentageToWidthConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                // This assumes a relative width calculation
                // You might need to bind this to the actual parent width
                // For now, returning a percentage-based width
                return Math.Max(0, Math.Min(percentage, 100));
            }
            return 0.0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}