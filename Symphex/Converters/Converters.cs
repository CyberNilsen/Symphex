using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Symphex.Converters
{
    public class BoolToAlbumArtLabelConverter : IValueConverter
    {
        public static readonly BoolToAlbumArtLabelConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool hasRealAlbumArt)
            {
                return hasRealAlbumArt ? "ALBUM ART" : "THUMBNAIL";
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
                return hasRealAlbumArt ? "✅ Real Album Art Found" : "⚠️ Using Video Thumbnail";
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