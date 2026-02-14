using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Symphex.Converters
{
    public class MultiBoolToVisibilityConverter : IMultiValueConverter
    {
        public static readonly MultiBoolToVisibilityConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            // Returns true only if all boolean values are false
            if (values == null || values.Count == 0)
                return true;

            return values.All(v => v is bool b && !b);
        }
    }
}
