using Avalonia.Data.Converters;
using System;
using System.Collections;
using System.Globalization;

namespace Symphex.Converters
{
    public class SafeIndexConverter : IValueConverter
    {
        public static readonly SafeIndexConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is IList list && parameter is string indexStr && int.TryParse(indexStr, out int index))
            {
                if (index >= 0 && index < list.Count)
                {
                    return list[index];
                }
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
