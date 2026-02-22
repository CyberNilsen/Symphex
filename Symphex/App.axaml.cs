using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Symphex.ViewModels;
using Symphex.Views;

namespace Symphex
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                try
                {
                    DisableAvaloniaDataAnnotationValidation();
                    
                    var mainWindow = new MainWindow();
                    
                    try
                    {
                        mainWindow.DataContext = new MainWindowViewModel();
                        
                        // Start invisible
                        mainWindow.Opacity = 0;
                        desktop.MainWindow = mainWindow;
                        
                        // Fade in after 200ms
                        System.Threading.Tasks.Task.Run(async () =>
                        {
                            await System.Threading.Tasks.Task.Delay(200);
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                mainWindow.Opacity = 1;
                            });
                        });
                    }
                    catch (Exception vmEx)
                    {
                        LogError("ViewModel creation failed", vmEx);
                        desktop.MainWindow = CreateErrorWindow(vmEx);
                    }
                }
                catch (Exception ex)
                {
                    LogError("Window creation failed", ex);
                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop2)
                    {
                        desktop2.MainWindow = CreateErrorWindow(ex);
                    }
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void LogError(string message, Exception ex)
        {
            try
            {
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Symphex", "error.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"[{DateTime.Now}] {message}: {ex}\n\n");
            }
            catch { /* Ignore logging errors */ }
        }

        private Window CreateErrorWindow(Exception ex)
        {
            var window = new Window
            {
                Title = "Symphex - Error",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 10
            };

            stackPanel.Children.Add(new TextBlock
            {
                Text = "Symphex failed to start",
                FontSize = 20,
                FontWeight = Avalonia.Media.FontWeight.Bold
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = "An error occurred during startup. Error details have been saved to:",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });

            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Symphex", "error.log");

            stackPanel.Children.Add(new TextBlock
            {
                Text = logPath,
                FontFamily = new Avalonia.Media.FontFamily("Consolas"),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = $"\nError: {ex.Message}",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Foreground = Avalonia.Media.Brushes.Red
            });

            var scrollViewer = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = ex.ToString(),
                    FontFamily = new Avalonia.Media.FontFamily("Consolas"),
                    FontSize = 10,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                }
            };

            stackPanel.Children.Add(scrollViewer);

            window.Content = stackPanel;
            return window;
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}