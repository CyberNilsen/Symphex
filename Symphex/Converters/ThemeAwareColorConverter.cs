using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Globalization;

namespace Symphex.Converters
{
    public class ThemeAwareColorConverter : IValueConverter
    {
        // Button backgrounds
        public static readonly ThemeAwareColorConverter ButtonBackground = new() 
        { 
            LightColor = "#ffffff",  // White for light mode
            DarkColor = "#1a1a1a"    // Dark background for dark mode
        };
        
        public static readonly ThemeAwareColorConverter HoverBackground = new() 
        { 
            LightColor = "#d0d0d0",  // Much darker gray for light mode hover - very visible
            DarkColor = "#2a2a2a"    // Lighter gray for dark mode hover
        };
        
        public static readonly ThemeAwareColorConverter PressedBackground = new() 
        { 
            LightColor = "#b0b0b0",  // Even darker gray for pressed state in light mode
            DarkColor = "#3a3a3a"    // Even lighter for pressed state in dark mode
        };
        
        // Button borders
        public static readonly ThemeAwareColorConverter ButtonBorder = new() 
        { 
            LightColor = "#c0c0c0",  // Medium gray border for light mode
            DarkColor = "#404040"    // Dark border for dark mode
        };
        
        public static readonly ThemeAwareColorConverter HoverBorder = new() 
        { 
            LightColor = "#808080",  // Much darker border for light mode hover
            DarkColor = "#606060"    // Lighter border for dark mode hover
        };
        
        // Panel backgrounds
        public static readonly ThemeAwareColorConverter PanelBackground = new()
        {
            LightColor = "#ffffff",  // White for light mode
            DarkColor = "#0a0a0a"    // Very dark for dark mode
        };
        
        public static readonly ThemeAwareColorConverter CardBackground = new()
        {
            LightColor = "#fafafa",  // Off-white for light mode
            DarkColor = "#1a1a1a"    // Dark for dark mode
        };
        
        public static readonly ThemeAwareColorConverter SecondaryBackground = new()
        {
            LightColor = "#f5f5f5",  // Light gray for light mode
            DarkColor = "#151515"    // Slightly lighter dark for dark mode
        };
        
        // Text colors
        public static readonly ThemeAwareColorConverter PrimaryText = new()
        {
            LightColor = "#000000",  // Black for light mode
            DarkColor = "#ffffff"    // White for dark mode
        };
        
        public static readonly ThemeAwareColorConverter SecondaryText = new()
        {
            LightColor = "#666666",  // Gray for light mode
            DarkColor = "#cccccc"    // Light gray for dark mode
        };
        
        public static readonly ThemeAwareColorConverter TertiaryText = new()
        {
            LightColor = "#999999",  // Light gray for light mode
            DarkColor = "#888888"    // Medium gray for dark mode
        };

        public string? LightColor { get; set; }
        public string? DarkColor { get; set; }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not ThemeVariant themeVariant)
            {
                // Fallback: check Application.Current
                var app = Application.Current;
                if (app == null) return new SolidColorBrush(Color.Parse(DarkColor ?? "#1a1a1a"));
                themeVariant = app.ActualThemeVariant;
            }

            // Detect if OS is in light or dark mode
            var isLightMode = themeVariant == ThemeVariant.Light;
            var colorString = isLightMode ? LightColor : DarkColor;
            
            return new SolidColorBrush(Color.Parse(colorString ?? "#1a1a1a"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
