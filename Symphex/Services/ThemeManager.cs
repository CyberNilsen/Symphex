using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace Symphex.Services
{
    public class ThemeManager : INotifyPropertyChanged
    {
        private static ThemeManager? _instance;
        
        public static ThemeManager Instance => _instance ??= new ThemeManager();
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        private ThemeManager()
        {
            // Subscribe to theme changes - with null check
            try
            {
                if (Application.Current != null)
                {
                    Application.Current.ActualThemeVariantChanged += (s, e) =>
                    {
                        OnPropertyChanged(nameof(IsDarkMode));
                        OnPropertyChanged(nameof(ButtonBackground));
                        OnPropertyChanged(nameof(ButtonForeground));
                        OnPropertyChanged(nameof(ButtonBorder));
                        OnPropertyChanged(nameof(HoverBackground));
                        OnPropertyChanged(nameof(HoverBorder));
                        OnPropertyChanged(nameof(PressedBackground));
                        OnPropertyChanged(nameof(CardBackground));
                        OnPropertyChanged(nameof(PageBackground));
                        OnPropertyChanged(nameof(SecondaryBackground));
                        OnPropertyChanged(nameof(PrimaryText));
                        OnPropertyChanged(nameof(SecondaryText));
                        OnPropertyChanged(nameof(TertiaryText));
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] Error subscribing to theme changes: {ex.Message}");
            }
        }
        
        public bool IsDarkMode
        {
            get
            {
                try
                {
                    return Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
                }
                catch
                {
                    return true; // Default to dark mode if error
                }
            }
        }
        
        // Button colors
        public IBrush ButtonBackground => new SolidColorBrush(
            IsDarkMode ? Color.Parse("#1a1a1a") : Color.Parse("#ffffff"));
        
        public IBrush ButtonForeground => new SolidColorBrush(
            IsDarkMode ? Color.Parse("#ffffff") : Color.Parse("#000000"));
        
        public IBrush ButtonBorder => new SolidColorBrush(
            IsDarkMode ? Color.Parse("#404040") : Color.Parse("#c0c0c0"));
        
        public IBrush HoverBackground => new SolidColorBrush(
            IsDarkMode ? Color.Parse("#2a2a2a") : Color.Parse("#d0d0d0"));
        
        public IBrush HoverBorder => new SolidColorBrush(
            IsDarkMode ? Color.Parse("#606060") : Color.Parse("#808080"));
        
        public IBrush PressedBackground => new SolidColorBrush(
            IsDarkMode ? Color.Parse("#3a3a3a") : Color.Parse("#b0b0b0"));
        
        // Card and panel backgrounds
        public IBrush CardBackground => new SolidColorBrush(
            IsDarkMode ? Color.Parse("#1a1a1a") : Color.Parse("#c0c0c0"));
        
        public IBrush PageBackground => new SolidColorBrush(
            IsDarkMode ? Color.Parse("#000000") : Color.Parse("#ffffff"));
        
        public IBrush SecondaryBackground => new SolidColorBrush(
            IsDarkMode ? Color.Parse("#0a0a0a") : Color.Parse("#fafafa"));
        
        // Text colors
        public IBrush PrimaryText => new SolidColorBrush(
            IsDarkMode ? Color.Parse("#ffffff") : Color.Parse("#000000"));
        
        public IBrush SecondaryText => new SolidColorBrush(
            IsDarkMode ? Color.Parse("#cccccc") : Color.Parse("#666666"));
        
        public IBrush TertiaryText => new SolidColorBrush(
            IsDarkMode ? Color.Parse("#888888") : Color.Parse("#999999"));
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
