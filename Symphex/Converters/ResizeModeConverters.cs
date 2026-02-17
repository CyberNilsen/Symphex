using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Symphex.Converters
{
    public class ResizeModeToTextConverter : IMultiValueConverter
    {
        public static readonly ResizeModeToTextConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count > 0 && values[0] is bool isResizeMode)
            {
                return isResizeMode ? "Drag and drop files or folders here" : "Paste your music link here";
            }
            return "Paste your music link here";
        }
    }

    public class ResizeModeToWatermarkConverter : IMultiValueConverter
    {
        public static readonly ResizeModeToWatermarkConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count > 0 && values[0] is bool isResizeMode)
            {
                return isResizeMode 
                    ? "Drop music files or folders here to resize album art..." 
                    : "Paste YouTube/Spotify links here (multiple links supported)...";
            }
            return "Paste YouTube/Spotify links here (multiple links supported)...";
        }
    }

    public class ResizeModeToButtonTextConverter : IMultiValueConverter
    {
        public static readonly ResizeModeToButtonTextConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count > 0 && values[0] is bool isResizeMode)
            {
                return isResizeMode ? "🎨 Resize Images" : "🎵 Download My Music";
            }
            return "🎵 Download My Music";
        }
    }

    public class ResizeModeToOpenButtonTextConverter : IMultiValueConverter
    {
        public static readonly ResizeModeToOpenButtonTextConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count > 0 && values[0] is bool isResizeMode)
            {
                return isResizeMode ? "📁 Open Resized" : "📁 Open Downloads";
            }
            return "📁 Open Downloads";
        }
    }
}
