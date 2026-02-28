using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Velopack;

namespace Symphex.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private const string CURRENT_VERSION = "1.4.0";

        [ObservableProperty]
        private string currentVersionText = $"Current Version: v{CURRENT_VERSION}";

        [ObservableProperty]
        private string updateStatusText = "Click 'Check for Updates' to see if a new version is available.";

        [ObservableProperty]
        private bool isCheckingForUpdates = false;

        [ObservableProperty]
        private bool isUpdateAvailable = false;

        [ObservableProperty]
        private bool enableAlbumArtDownload = true;

        [ObservableProperty]
        private bool skipThumbnailDownload = false;

        [ObservableProperty]
        private string selectedThumbnailSize = "Medium Quality (600x600)";

        [ObservableProperty]
        private bool enableArtworkSelection = true; // Default ON

        [ObservableProperty]
        private int artworkSelectionTimeout = 5; // 5 seconds gives users time to decide

        [ObservableProperty]
        private double albumArtSize = 600; // Default resize size

        public IRelayCommand? BackCommand { get; set; }
        
        private MainWindowViewModel? _mainWindowViewModel;
        public MainWindowViewModel? MainWindowViewModel
        {
            get => _mainWindowViewModel;
            set => _mainWindowViewModel = value;
        }

        [ObservableProperty]
        private List<string> thumbnailSizeOptions = new List<string>
        {
            "Maximum Quality",
            "High Quality (1200x1200)",
            "Medium Quality (600x600)",
            "Low Quality (300x300)"
        };

        [ObservableProperty]
        private string selectedAudioFormat = "MP3";

        [ObservableProperty]
        private List<string> audioFormatOptions = new List<string>
        {
            "MP3",
            "FLAC (Lossless)",
            "WAV (Uncompressed)",
            "M4A (AAC)",
            "Opus",
            "Vorbis (OGG)"
        };

        [ObservableProperty]
        private string selectedBitrate = "320";

        [ObservableProperty]
        private List<string> bitrateOptions = new List<string>();

        // Show bitrate selector only for lossy formats
        public bool ShowBitrateSelector => SelectedAudioFormat == "MP3" || 
                                           SelectedAudioFormat == "M4A (AAC)" || 
                                           SelectedAudioFormat == "Opus" || 
                                           SelectedAudioFormat == "Vorbis (OGG)";

        // Orange warning: Album art disabled but thumbnails enabled
        public bool ShowAlbumArtDisabledWarning => !EnableAlbumArtDownload && SkipThumbnailDownload;

        // Red warning: Both album art and thumbnails disabled
        public bool ShowNoArtworkWarning => !EnableAlbumArtDownload && !SkipThumbnailDownload;

        public SettingsViewModel()
        {
            CurrentVersionText = $"Current Version: v{CURRENT_VERSION}";
            UpdateBitrateOptions(); // Initialize bitrate options
            LoadSettings();
        }

        private async Task CheckForUpdatesOnStartup()
        {
            try
            {
                await Task.Delay(2000); // Wait 2 seconds after startup
                
                var updateManager = new Velopack.UpdateManager("https://github.com/CyberNilsen/Symphex/releases/latest/download");
                var updateInfo = await updateManager.CheckForUpdatesAsync();

                if (updateInfo != null)
                {
                    UpdateStatusText = $"Update available: v{updateInfo.TargetFullRelease.Version}";
                    IsUpdateAvailable = true;
                    
                    // Show update dialog to user
                    var result = await ShowUpdateDialog(updateInfo.TargetFullRelease.Version.ToString());
                    
                    if (result)
                    {
                        UpdateStatusText = "Downloading update...";
                        IsCheckingForUpdates = true;
                        
                        await updateManager.DownloadUpdatesAsync(updateInfo);
                        
                        UpdateStatusText = "Update downloaded! Restarting...";
                        await Task.Delay(1000); // Brief pause to show message
                        
                        updateManager.ApplyUpdatesAndRestart(updateInfo);
                    }
                    else
                    {
                        UpdateStatusText = $"Update available: v{updateInfo.TargetFullRelease.Version} (skipped)";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsViewModel] Startup update check failed: {ex.Message}");
                // Silently fail - don't bother the user on startup
            }
            finally
            {
                IsCheckingForUpdates = false;
            }
        }

        private void LoadSettings()
        {
            try
            {
                var settings = Services.SettingsService.LoadSettings();
                EnableAlbumArtDownload = settings.EnableAlbumArtDownload;
                SkipThumbnailDownload = settings.SkipThumbnailDownload;
                SelectedThumbnailSize = settings.SelectedThumbnailSize;
                EnableArtworkSelection = settings.EnableArtworkSelection;
                ArtworkSelectionTimeout = settings.ArtworkSelectionTimeout;
                AlbumArtSize = settings.AlbumArtSize;
                
                // Migrate old format names to new ones
                SelectedAudioFormat = MigrateAudioFormat(settings.SelectedAudioFormat);
                SelectedBitrate = settings.SelectedBitrate;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsViewModel] Error loading settings: {ex.Message}");
            }
        }

        private string MigrateAudioFormat(string oldFormat)
        {
            // Migrate old format names to new simplified names
            return oldFormat switch
            {
                "MP3 (Best Quality)" => "MP3",
                "MP3 (320kbps)" => "MP3",
                "AAC (256kbps)" => "M4A (AAC)",
                "M4A (256kbps)" => "M4A (AAC)",
                "M4A (AAC - Best Quality)" => "M4A (AAC)",
                "Opus (192kbps)" => "Opus",
                "Opus (Best Quality)" => "Opus",
                "Vorbis (192kbps)" => "Vorbis (OGG)",
                "Vorbis (OGG - Best Quality)" => "Vorbis (OGG)",
                _ => AudioFormatOptions.Contains(oldFormat) ? oldFormat : "MP3" // Default to MP3 if unknown
            };
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new Services.UserSettings
                {
                    EnableAlbumArtDownload = EnableAlbumArtDownload,
                    SkipThumbnailDownload = SkipThumbnailDownload,
                    SelectedThumbnailSize = SelectedThumbnailSize,
                    EnableArtworkSelection = EnableArtworkSelection,
                    ArtworkSelectionTimeout = ArtworkSelectionTimeout,
                    AlbumArtSize = AlbumArtSize,
                    SelectedAudioFormat = SelectedAudioFormat,
                    SelectedBitrate = SelectedBitrate
                };
                Services.SettingsService.SaveSettings(settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsViewModel] Error saving settings: {ex.Message}");
            }
        }

        partial void OnEnableAlbumArtDownloadChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowNoArtworkWarning));
            OnPropertyChanged(nameof(ShowAlbumArtDisabledWarning));
            SaveSettings();
        }

        partial void OnSkipThumbnailDownloadChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowNoArtworkWarning));
            OnPropertyChanged(nameof(ShowAlbumArtDisabledWarning));
            SaveSettings();
        }

        partial void OnSelectedThumbnailSizeChanged(string value)
        {
            SaveSettings();
        }

        partial void OnEnableArtworkSelectionChanged(bool value)
        {
            SaveSettings();
        }

        partial void OnArtworkSelectionTimeoutChanged(int value)
        {
            SaveSettings();
        }

        partial void OnAlbumArtSizeChanged(double value)
        {
            SaveSettings();
        }

        [RelayCommand]
        private void ResetAlbumArtSize()
        {
            AlbumArtSize = 600; // Reset to default
        }

        [RelayCommand]
        private void ResetAllSettings()
        {
            EnableAlbumArtDownload = true;
            SkipThumbnailDownload = true; 
            SelectedThumbnailSize = "Medium Quality (600x600)";
            EnableArtworkSelection = true;
            ArtworkSelectionTimeout = 5;
            AlbumArtSize = 600;
            SelectedAudioFormat = "MP3";
            SelectedBitrate = "320";
            
            SaveSettings();
            
            // Update MainWindowViewModel if available
            if (_mainWindowViewModel != null)
            {
                _mainWindowViewModel.ReloadSettings();
            }
        }

        partial void OnSelectedAudioFormatChanged(string value)
        {
            UpdateBitrateOptions();
            OnPropertyChanged(nameof(ShowBitrateSelector));
            SaveSettings();
            
            // Immediately update MainWindowViewModel if available
            if (_mainWindowViewModel != null)
            {
                _mainWindowViewModel.SelectedAudioFormat = value;
            }
        }

        partial void OnSelectedBitrateChanged(string value)
        {
            SaveSettings();
            
            // Immediately update MainWindowViewModel if available
            if (_mainWindowViewModel != null)
            {
                _mainWindowViewModel.SelectedBitrate = value;
            }
        }

        private void UpdateBitrateOptions()
        {
            BitrateOptions = SelectedAudioFormat switch
            {
                "MP3" => new List<string> { "128", "192", "256", "320" },
                "M4A (AAC)" => new List<string> { "128", "192", "256", "320" },
                "Opus" => new List<string> { "96", "128", "160", "192" },
                "Vorbis (OGG)" => new List<string> { "128", "192", "256", "320" },
                _ => new List<string>()
            };

            // Always default to highest quality (last item in list)
            if (BitrateOptions.Count > 0)
            {
                if (!BitrateOptions.Contains(SelectedBitrate))
                {
                    SelectedBitrate = BitrateOptions.Last(); // Default to highest quality
                }
            }
        }

        [RelayCommand]
        private async Task CheckForUpdates()
        {
            try
            {
                IsCheckingForUpdates = true;
                UpdateStatusText = "Checking for updates...";
                IsUpdateAvailable = false;

                var updateManager = new Velopack.UpdateManager("https://github.com/CyberNilsen/Symphex/releases/latest/download");

                var updateInfo = await updateManager.CheckForUpdatesAsync();

                if (updateInfo == null)
                {
                    UpdateStatusText = "You're running the latest version!";
                    IsUpdateAvailable = false;
                }
                else
                {
                    UpdateStatusText = $"Update available: v{updateInfo.TargetFullRelease.Version}";
                    IsUpdateAvailable = true;

                    // Ask user if they want to download and install
                    var result = await ShowUpdateDialog(updateInfo.TargetFullRelease.Version.ToString());
                    
                    if (result)
                    {
                        UpdateStatusText = "Downloading update...";
                        await updateManager.DownloadUpdatesAsync(updateInfo);
                        
                        UpdateStatusText = "Update downloaded! Restarting...";
                        updateManager.ApplyUpdatesAndRestart(updateInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText = $"Error checking for updates: {ex.Message}";
                Debug.WriteLine($"[SettingsViewModel] Update check error: {ex}");
            }
            finally
            {
                IsCheckingForUpdates = false;
            }
        }

        private async Task<bool> ShowUpdateDialog(string version)
        {
            try
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var dialog = new Window
                    {
                        Title = "Update Available",
                        Width = 400,
                        Height = 200,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        CanResize = false
                    };

                    var result = false;
                    var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };
                    
                    panel.Children.Add(new TextBlock 
                    { 
                        Text = $"A new version (v{version}) is available!",
                        FontSize = 16,
                        Margin = new Avalonia.Thickness(0, 0, 0, 20)
                    });
                    
                    panel.Children.Add(new TextBlock 
                    { 
                        Text = "Would you like to download and install it now?",
                        Margin = new Avalonia.Thickness(0, 0, 0, 20)
                    });

                    var buttonPanel = new StackPanel 
                    { 
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
                    };

                    var yesButton = new Button 
                    { 
                        Content = "Yes", 
                        Width = 80,
                        Margin = new Avalonia.Thickness(0, 0, 10, 0)
                    };
                    yesButton.Click += (s, e) => { result = true; dialog.Close(); };

                    var noButton = new Button 
                    { 
                        Content = "No", 
                        Width = 80 
                    };
                    noButton.Click += (s, e) => { result = false; dialog.Close(); };

                    buttonPanel.Children.Add(yesButton);
                    buttonPanel.Children.Add(noButton);
                    panel.Children.Add(buttonPanel);

                    dialog.Content = panel;

                    await dialog.ShowDialog(desktop.MainWindow!);
                    return result;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsViewModel] Error showing update dialog: {ex}");
                return false;
            }
        }
    }
}