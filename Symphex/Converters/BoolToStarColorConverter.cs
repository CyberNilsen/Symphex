using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Symphex.Converters
{
    public class BoolToStarColorConverter : IValueConverter
    {
        public static readonly BoolToStarColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isFavorite)
            {
                return isFavorite 
                    ? new SolidColorBrush(Color.Parse("#fbbf24")) // Gold for favorited
                    : new SolidColorBrush(Color.Parse("#666666")); // Gray for not favorited
            }
            return new SolidColorBrush(Color.Parse("#666666"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
