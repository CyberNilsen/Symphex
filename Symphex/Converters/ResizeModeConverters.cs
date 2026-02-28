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
            if (values.Count > 1 && values[0] is bool isResizeMode && values[1] is bool isMetadataMode)
            {
                if (isMetadataMode) return "Transfer metadata between files";
                if (isResizeMode) return "Drag and drop files or folders here";
                return "Paste your music link here";
            }
            if (values.Count > 0 && values[0] is bool resize)
            {
                return resize ? "Drag and drop files or folders here" : "Paste your music link here";
            }
            return "Paste your music link here";
        }
    }

    public class ResizeModeToWatermarkConverter : IMultiValueConverter
    {
        public static readonly ResizeModeToWatermarkConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count > 1 && values[0] is bool isResizeMode && values[1] is bool isMetadataMode)
            {
                if (isMetadataMode) return "Drop source files (with metadata) in green zone, target files in orange zone...";
                if (isResizeMode) return "Drop music files or folders here to resize album art...";
                return "Paste YouTube/Spotify links here (multiple links supported)...";
            }
            if (values.Count > 0 && values[0] is bool resize)
            {
                return resize 
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
            if (values.Count > 1 && values[0] is bool isResizeMode && values[1] is bool isMetadataMode)
            {
                if (isMetadataMode) return "🎵 Enhance Metadata";
                if (isResizeMode) return "🎨 Resize Images";
                return "🎵 Download My Music";
            }
            if (values.Count > 0 && values[0] is bool resize)
            {
                return resize ? "🎨 Resize Images" : "🎵 Download My Music";
            }
            return "🎵 Download My Music";
        }
    }

    public class ResizeModeToOpenButtonTextConverter : IMultiValueConverter
    {
        public static readonly ResizeModeToOpenButtonTextConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count > 1 && values[0] is bool isResizeMode && values[1] is bool isMetadataMode)
            {
                if (isMetadataMode) return "📁 Open Downloads";
                if (isResizeMode) return "📁 Open Resized";
                return "📁 Open Downloads";
            }
            if (values.Count > 0 && values[0] is bool resize)
            {
                return resize ? "📁 Open Resized" : "📁 Open Downloads";
            }
            return "📁 Open Downloads";
        }
    }
}
