using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Symphex.Converters
{
    public class BoolToPlayPauseIconConverter : IValueConverter
    {
        public static readonly BoolToPlayPauseIconConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is bool isPlaying && isPlaying ? "⏸" : "▶";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class VolumeToIconConverter : IValueConverter
    {
        public static readonly VolumeToIconConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is float volume)
            {
                if (volume == 0) return "🔇";
                if (volume < 33) return "🔈";
                if (volume < 66) return "🔉";
                return "🔊";
            }
            return "🔉";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToAlbumArtStatusConverter : IValueConverter
    {
        public static readonly BoolToAlbumArtStatusConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is bool hasRealAlbumArt && hasRealAlbumArt ? "Album Art" : "Thumbnail";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}