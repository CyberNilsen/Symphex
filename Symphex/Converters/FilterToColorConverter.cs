using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Symphex.Converters
{
    public class FilterToColorConverter : IValueConverter
    {
        public static readonly FilterToColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string selectedFilter && parameter is string filterName)
            {
                return selectedFilter == filterName 
                    ? new SolidColorBrush(Color.Parse("#3b82f6")) 
                    : new SolidColorBrush(Color.Parse("#333333"));
            }
            return new SolidColorBrush(Color.Parse("#333333"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
