using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Symphex.ViewModels;
using Symphex.Views;
using NetSparkleUpdater;
using NetSparkleUpdater.SignatureVerifiers;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.Events;

namespace Symphex
{
    public partial class App : Application
    {
        private SparkleUpdater? _sparkle;
        
        // Make SparkleUpdater accessible to other parts of the app
        public static SparkleUpdater? Updater { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };

                // Initialize NetSparkle updater
                InitializeUpdater(desktop.MainWindow);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void InitializeUpdater(Window mainWindow)
        {
            // App cast URL points to the latest release on GitHub
            // The build script will update this URL for each release
            var appcastUrl = "https://github.com/CyberNilsen/Symphex/releases/latest/download/appcast.xml";
            var publicKey = "5dp/dwKhNPanJBzgWAkaPxGatnSKHR79odzSwFtyYTI=";

            _sparkle = new SparkleUpdater(
                appcastUrl,
                new Ed25519Checker(SecurityMode.Strict, publicKey)
            )
            {
                UIFactory = new NetSparkleUpdater.UI.Avalonia.UIFactory(mainWindow.Icon),
                RelaunchAfterUpdate = true // Set to true if you want auto-restart after update
            };

            // Make it accessible globally
            Updater = _sparkle;

            // Optional: Subscribe to events for logging
            _sparkle.UpdateDetected += (sender, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"Update detected: {e.AppCastItems?.FirstOrDefault()?.Version}");
            };

            // Start checking for updates (checks once, then periodically)
            _sparkle.StartLoop(true);
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