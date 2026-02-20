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

namespace Symphex.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private const string CURRENT_VERSION = "1.3.0";

        [ObservableProperty]
        private string currentVersionText = $"Current Version: v{CURRENT_VERSION}";

        [ObservableProperty]
        private string updateStatusText = "NetSparkle checks for updates automatically on startup.";

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
        private void CheckForUpdates()
        {
            try
            {
                IsCheckingForUpdates = true;
                UpdateStatusText = "Checking for updates...";
                IsUpdateAvailable = false;

                // Use NetSparkle to check for updates
                var updater = App.Updater;
                
                if (updater == null)
                {
                    UpdateStatusText = "Update system not initialized.";
                    IsCheckingForUpdates = false;
                    return;
                }

                // Check for updates on UI thread (required for macOS)
                // NetSparkle will handle the async operations internally
                updater.CheckForUpdatesAtUserRequest();

                // NetSparkle will show its own UI, so we just update our status
                UpdateStatusText = "Update check complete. NetSparkle will show a dialog if updates are available.";
            }
            catch (Exception ex)
            {
                UpdateStatusText = $"Error checking for updates: {ex.Message}";
            }
            finally
            {
                IsCheckingForUpdates = false;
            }
        }
    }
}