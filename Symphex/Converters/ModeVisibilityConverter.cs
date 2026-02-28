using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Symphex.Converters
{
    public class BothModesOffConverter : IMultiValueConverter
    {
        public static readonly BothModesOffConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count >= 2 && values[0] is bool isResizeMode && values[1] is bool isMetadataMode)
            {
                return !isResizeMode && !isMetadataMode;
            }
            return true;
        }
    }
}
