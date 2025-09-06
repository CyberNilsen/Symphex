using Avalonia.Controls;
using Avalonia.Platform;
using Symphex.ViewModels;
using System;

namespace Symphex.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = new SettingsViewModel();

            // Center the settings window when it opens
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Size window based on screen dimensions
            SetWindowSizeBasedOnScreen();
        }

        private void SetWindowSizeBasedOnScreen()
        {
            try
            {
                // Get the primary screen
                var screen = Screens.Primary;

                if (screen != null)
                {
                    // Get screen working area (excludes taskbar)
                    var workingArea = screen.WorkingArea;

                    // Calculate window size as percentage of screen
                    // Use 35% of screen width and 40% of screen height for settings
                    double targetWidth = workingArea.Width * 0.35;
                    double targetHeight = workingArea.Height * 0.40;

                    // Set constraints for settings window
                    const double MIN_WIDTH = 550;
                    const double MIN_HEIGHT = 550;
                    const double MAX_WIDTH = 650;   // Slightly larger than current
                    const double MAX_HEIGHT = 650;  // Slightly larger than current

                    // Apply constraints
                    Width = Math.Max(MIN_WIDTH, Math.Min(MAX_WIDTH, targetWidth));
                    Height = Math.Max(MIN_HEIGHT, Math.Min(MAX_HEIGHT, targetHeight));

                    // Set minimum window size to match your XAML
                    MinWidth = MIN_WIDTH;
                    MinHeight = MIN_HEIGHT;
                }
                else
                {
                    // Fallback to your current XAML dimensions
                    Width = 550;
                    Height = 550;
                    MinWidth = 550;
                    MinHeight = 550;
                }
            }
            catch (Exception)
            {
                // Fallback to your current XAML dimensions
                Width = 550;
                Height = 550;
                MinWidth = 550;
                MinHeight = 550;
            }
        }
    }
}