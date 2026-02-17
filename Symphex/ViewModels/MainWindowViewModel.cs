﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CliWrap;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Symphex.Models;
using Symphex.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Symphex.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private ScrollViewer? _cliScrollViewer;

        public ScrollViewer? CliScrollViewer
        {
            get => _cliScrollViewer;
            set => SetProperty(ref _cliScrollViewer, value);
        }

        [ObservableProperty]
        private string downloadUrl = "";

        [ObservableProperty]
        private string statusText = "🎵 Ready to download music...";

        [ObservableProperty]
        private string cliOutput = "Symphex Music Downloader v1.2.3\n" +
                                   "=============================\n" +
                                   "Ready to process downloads...\n\n";

        [ObservableProperty]
        private double downloadProgress = 0;

        [ObservableProperty]
        private bool isDownloading = false;

        [ObservableProperty]
        private string downloadFolder = "";

        [ObservableProperty]
        private TrackInfo? currentTrack;

        [ObservableProperty]
        private bool showMetadata = false;

        [ObservableProperty]
        private bool isToastVisible = false;

        [ObservableProperty]
        private string toastMessage = "";

        [ObservableProperty]
        private List<string> pendingUrls = new List<string>();

        [ObservableProperty]
        private bool isBatchProcessing = false;

        [ObservableProperty]
        private int currentBatchIndex = 0;

        [ObservableProperty]
        private int totalBatchCount = 0;

        [ObservableProperty]
        private string currentBatchFilePath = "";

        [ObservableProperty]
        private int maxConcurrentDownloads = 8; // Adjustable based on system capability

        [ObservableProperty]
        private int activeDownloads = 0;

        [ObservableProperty]
        private List<Task> runningTasks = new List<Task>();

        [ObservableProperty]
        private TrackInfo? lastProcessedTrack;

        [ObservableProperty]
        private string currentProcessingUrl = "";

        [ObservableProperty]
        private bool enableAlbumArtDownload = true; // Default to enabled

        [ObservableProperty]
        private bool skipThumbnailDownload = true; // Default to TRUE = download thumbnails

        [ObservableProperty]
        private string selectedThumbnailSize = "Medium Quality (600x600)";

        [ObservableProperty]
        private bool enableArtworkSelection = true; // Default ON - let users choose artwork

        [ObservableProperty]
        private int artworkSelectionTimeout = 5; // 5 seconds gives users time

        [ObservableProperty]
        private bool isSelectingArtwork = false; // Currently showing artwork options

        [ObservableProperty]
        private int selectionCountdown = 0; // Countdown timer

        private ObservableCollection<Bitmap?> artworkOptions = new ObservableCollection<Bitmap?>(); // 4 artwork options to display
        public ObservableCollection<Bitmap?> ArtworkOptions => artworkOptions;

        [ObservableProperty]
        private List<string> thumbnailSizeOptions = new List<string>
        {
            "None (No Thumbnails)",
            "Low Quality (300x300)",
            "Medium Quality (600x600)",
            "High Quality (1200x1200)",
            "Maximum Quality"
        };

        [ObservableProperty]
        private string selectedAudioFormat = "MP3"; // Default format

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
        private string selectedBitrate = "320"; // Default bitrate for lossy formats

        [ObservableProperty]
        private string lastDownloadedFileInfo = "";

        [ObservableProperty]
        private bool showFileInfo = false;

        [ObservableProperty]
        private string artworkSizeInfo = "";

        [ObservableProperty]
        private object? currentView;

        [ObservableProperty]
        private bool isSettingsView = false;

        // Playlist Management
        [ObservableProperty]
        private bool isSidePanelOpen = false;

        [ObservableProperty]
        private ObservableCollection<SavedLink> savedLinks = new ObservableCollection<SavedLink>();

        [ObservableProperty]
        private ObservableCollection<SavedLink> filteredLinks = new ObservableCollection<SavedLink>();

        [ObservableProperty]
        private string selectedFilter = "All";

        private readonly PlaylistService playlistService = new();

        // Download History
        [ObservableProperty]
        private bool isHistoryPanelOpen = false;

        [ObservableProperty]
        private ObservableCollection<DownloadHistoryItem> downloadHistory = new ObservableCollection<DownloadHistoryItem>();

        [ObservableProperty]
        private ObservableCollection<DownloadHistoryItem> filteredHistory = new ObservableCollection<DownloadHistoryItem>();

        [ObservableProperty]
        private string historySearchText = "";

        private readonly DownloadHistoryService historyService = new();

        // Resize Mode for Album Art
        [ObservableProperty]
        private bool isResizeMode = false;

        [ObservableProperty]
        private double albumArtSize = 600; // Default size for resizing (600x600)

        [ObservableProperty]
        private string resizedFolderPath = "";

        [ObservableProperty]
        private ObservableCollection<string> queuedFiles = new ObservableCollection<string>();

        private readonly HttpClient httpClient = new();

        private readonly DependencyManager dependencyManager = new();

        private string YtDlpPath => dependencyManager.YtDlpPath;
        private string FfmpegPath => dependencyManager.FfmpegPath;

        private readonly AlbumArtSearchService albumArtSearchService;

        private DownloadService? _downloadService;

        public MainWindowViewModel()
        {
            CurrentTrack = new TrackInfo();
            SetupDownloadFolder();
            SetupResizedFolder();

            // Load saved settings
            LoadUserSettings();

            // Load saved links
            LoadSavedLinks();

            // Load download history
            LoadDownloadHistory();

            // Initialize the album art search service
            albumArtSearchService = new AlbumArtSearchService(httpClient);

            if (!Directory.Exists(DownloadFolder))
            {
                Directory.CreateDirectory(DownloadFolder);
            }

            // Start auto-installation in background
            _ = Task.Run(async () =>
            {
                await dependencyManager.AutoInstallDependencies();

                // Initialize download service after dependencies are ready
                InitializeDownloadService();
            });
        }

        private void InitializeDownloadService()
        {
            _downloadService = new DownloadService(
                DownloadFolder,
                YtDlpPath,
                FfmpegPath,
                albumArtSearchService,
                EnableAlbumArtDownload 

            );
            
            // Set audio format
            _downloadService.UpdateAudioFormat(SelectedAudioFormat);
            _downloadService.UpdateBitrate(SelectedBitrate);
            
            // Set up download completion callback for history tracking
            _downloadService.OnDownloadCompleted = (track, filePath) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    OnDownloadComplete(track, filePath);
                });
            };
        }

        partial void OnEnableAlbumArtDownloadChanged(bool value)
        {
            if (_downloadService != null)
            {
                _downloadService.UpdateAlbumArtSetting(value);
            }
            SaveUserSettings();
        }

        partial void OnSkipThumbnailDownloadChanged(bool value)
        {
            if (_downloadService != null)
            {
                _downloadService.UpdateThumbnailSettings(value);
            }
            SaveUserSettings();
        }

        partial void OnSelectedThumbnailSizeChanged(string value)
        {
            if (_downloadService != null)
            {
                _downloadService.UpdateThumbnailSize(value);
            }
            SaveUserSettings();
        }

        partial void OnEnableArtworkSelectionChanged(bool value)
        {
            SaveUserSettings();
        }

        partial void OnArtworkSelectionTimeoutChanged(int value)
        {
            SaveUserSettings();
        }

        private void LoadUserSettings()
        {
            try
            {
                var settings = SettingsService.LoadSettings();
                
                EnableAlbumArtDownload = settings.EnableAlbumArtDownload;
                SkipThumbnailDownload = settings.SkipThumbnailDownload;
                SelectedThumbnailSize = settings.SelectedThumbnailSize;
                EnableArtworkSelection = settings.EnableArtworkSelection;
                ArtworkSelectionTimeout = settings.ArtworkSelectionTimeout;
                AlbumArtSize = settings.AlbumArtSize;
                
                // Migrate old format names to new ones
                SelectedAudioFormat = MigrateAudioFormat(settings.SelectedAudioFormat);
                SelectedBitrate = settings.SelectedBitrate;

                Debug.WriteLine("[MainWindowViewModel] User settings loaded");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] Error loading settings: {ex.Message}");
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

        private void SaveUserSettings()
        {
            try
            {
                var settings = new UserSettings
                {
                    EnableAlbumArtDownload = this.EnableAlbumArtDownload,
                    SkipThumbnailDownload = this.SkipThumbnailDownload,
                    SelectedThumbnailSize = this.SelectedThumbnailSize,
                    EnableArtworkSelection = this.EnableArtworkSelection,
                    ArtworkSelectionTimeout = this.ArtworkSelectionTimeout,
                    AlbumArtSize = this.AlbumArtSize,
                    SelectedAudioFormat = this.SelectedAudioFormat,
                    SelectedBitrate = this.SelectedBitrate
                };

                SettingsService.SaveSettings(settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] Error saving settings: {ex.Message}");
            }
        }

        partial void OnAlbumArtSizeChanged(double value)
        {
            SaveUserSettings();
        }

        partial void OnSelectedAudioFormatChanged(string value)
        {
            SaveUserSettings();
            if (_downloadService != null)
            {
                _downloadService.UpdateAudioFormat(value);
            }
        }

        partial void OnSelectedBitrateChanged(string value)
        {
            SaveUserSettings();
            if (_downloadService != null)
            {
                _downloadService.UpdateBitrate(value);
            }
        }

        [RelayCommand]
        private void NavigateToSettings()
        {
            try
            {
                var settingsViewModel = new Symphex.ViewModels.SettingsViewModel
                {
                    EnableAlbumArtDownload = this.EnableAlbumArtDownload,
                    SkipThumbnailDownload = this.SkipThumbnailDownload,
                    SelectedThumbnailSize = this.SelectedThumbnailSize,
                    EnableArtworkSelection = this.EnableArtworkSelection,
                    ArtworkSelectionTimeout = this.ArtworkSelectionTimeout,
                    AlbumArtSize = this.AlbumArtSize,
                    SelectedAudioFormat = this.SelectedAudioFormat,
                    SelectedBitrate = this.SelectedBitrate,
                    MainWindowViewModel = this
                };

                // Set up back command
                settingsViewModel.BackCommand = new RelayCommand(NavigateToHome);

                CurrentView = settingsViewModel;
                IsSettingsView = true;
            }
            catch (Exception ex)
            {
                StatusText = $"Error opening settings: {ex.Message}";
            }
        }

        [RelayCommand]
        private void NavigateToHome()
        {
            try
            {
                // Copy settings back from the settings view model
                if (CurrentView is SettingsViewModel settingsViewModel)
                {
                    this.EnableAlbumArtDownload = settingsViewModel.EnableAlbumArtDownload;
                    this.SkipThumbnailDownload = settingsViewModel.SkipThumbnailDownload;
                    this.SelectedThumbnailSize = settingsViewModel.SelectedThumbnailSize;
                    this.EnableArtworkSelection = settingsViewModel.EnableArtworkSelection;
                    this.ArtworkSelectionTimeout = settingsViewModel.ArtworkSelectionTimeout;
                    this.AlbumArtSize = settingsViewModel.AlbumArtSize;
                    this.SelectedAudioFormat = settingsViewModel.SelectedAudioFormat;
                }

                CurrentView = null;
                IsSettingsView = false;
            }
            catch (Exception ex)
            {
                StatusText = $"Error navigating home: {ex.Message}";
            }
        }

        [RelayCommand]
        private void SelectArtwork(int index)
        {
            try
            {
                // -2 means no picture
                if (index == -2)
                {
                    if (CurrentTrack != null)
                    {
                        CurrentTrack.AlbumArt = null;
                        CurrentTrack.Thumbnail = null;
                        Debug.WriteLine("[SelectArtwork] User selected NO PICTURE");
                    }
                }
                // -1 means skip/use default
                else if (index == -1)
                {
                    Debug.WriteLine("[SelectArtwork] User skipped - using default");
                }
                else if (index >= 0 && index < ArtworkOptions.Count && ArtworkOptions[index] != null)
                {
                    // User selected this artwork
                    if (CurrentTrack != null)
                    {
                        CurrentTrack.AlbumArt = ArtworkOptions[index];
                        Debug.WriteLine($"[SelectArtwork] User selected artwork option {index + 1}");
                    }
                }

                // Hide selection overlay
                IsSelectingArtwork = false;
                SelectionCountdown = 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SelectArtwork] Error: {ex.Message}");
            }
        }

        private async Task ShowArtworkSelectionDialog()
        {
            try
            {
                // Clear and prepare 4 artwork options
                ArtworkOptions.Clear();
                
                Debug.WriteLine($"[ShowArtworkSelectionDialog] Starting - Album Art: {CurrentTrack?.AlbumArt != null}, Thumbnail: {CurrentTrack?.Thumbnail != null}");
                Debug.WriteLine($"[ShowArtworkSelectionDialog] Available thumbnails: {CurrentTrack?.AvailableThumbnails?.Count ?? 0}");
                
                // Option 1: Album art (if available), otherwise thumbnail
                var option1 = CurrentTrack?.AlbumArt ?? CurrentTrack?.Thumbnail;
                ArtworkOptions.Add(option1);
                Debug.WriteLine($"[ShowArtworkSelectionDialog] Option 1: {(option1 != null ? $"{option1.PixelSize.Width}x{option1.PixelSize.Height}" : "null")}");
                
                // Option 2: Thumbnail (if different from album art)
                if (CurrentTrack?.Thumbnail != null && CurrentTrack.Thumbnail != CurrentTrack.AlbumArt)
                {
                    ArtworkOptions.Add(CurrentTrack.Thumbnail);
                    Debug.WriteLine($"[ShowArtworkSelectionDialog] Option 2: Thumbnail {CurrentTrack.Thumbnail.PixelSize.Width}x{CurrentTrack.Thumbnail.PixelSize.Height}");
                }
                else
                {
                    ArtworkOptions.Add(null);
                    Debug.WriteLine("[ShowArtworkSelectionDialog] Option 2: null (no separate thumbnail)");
                }
                
                // Options 3-4: Try to load different quality thumbnails from available list
                if (CurrentTrack?.AvailableThumbnails != null && CurrentTrack.AvailableThumbnails.Count > 0)
                {
                    var thumbnails = CurrentTrack.AvailableThumbnails.OrderByDescending(t => t.Width).ToList();
                    Debug.WriteLine($"[ShowArtworkSelectionDialog] Found {thumbnails.Count} available thumbnails");
                    
                    int loaded = 0;
                    for (int i = 0; i < thumbnails.Count && loaded < 2; i++)
                    {
                        try
                        {
                            Debug.WriteLine($"[ShowArtworkSelectionDialog] Loading thumbnail {i + 1}: {thumbnails[i].Url}");
                            var bitmap = await LoadImageAsync(thumbnails[i].Url);
                            if (bitmap != null)
                            {
                                ArtworkOptions.Add(bitmap);
                                Debug.WriteLine($"[ShowArtworkSelectionDialog] Option {ArtworkOptions.Count}: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
                                loaded++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ShowArtworkSelectionDialog] Failed to load thumbnail {i + 1}: {ex.Message}");
                        }
                    }
                }
                
                // Fill remaining slots with nulls
                while (ArtworkOptions.Count < 4)
                {
                    ArtworkOptions.Add(null);
                    Debug.WriteLine($"[ShowArtworkSelectionDialog] Option {ArtworkOptions.Count}: null (filler)");
                }

                Debug.WriteLine($"[ShowArtworkSelectionDialog] Total options prepared: {ArtworkOptions.Count}");

                // Show the selection overlay
                IsSelectingArtwork = true;
                SelectionCountdown = ArtworkSelectionTimeout;

                // Start countdown timer
                for (int i = ArtworkSelectionTimeout; i > 0; i--)
                {
                    if (!IsSelectingArtwork) break; // User made a selection
                    
                    SelectionCountdown = i;
                    await Task.Delay(1000);
                }

                // Auto-select first option if user didn't choose
                if (IsSelectingArtwork)
                {
                    Debug.WriteLine("[ShowArtworkSelectionDialog] Timeout - auto-selecting first option");
                    IsSelectingArtwork = false;
                    SelectionCountdown = 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ShowArtworkSelectionDialog] Error: {ex.Message}");
                Debug.WriteLine($"[ShowArtworkSelectionDialog] Stack trace: {ex.StackTrace}");
                IsSelectingArtwork = false;
            }
        }

        private async Task<Bitmap?> LoadImageAsync(string url)
        {
            try
            {
                using var httpClient = new HttpClient();
                var imageBytes = await httpClient.GetByteArrayAsync(url);
                using var stream = new MemoryStream(imageBytes);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }

        private void SetupDownloadFolder()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                DownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Symphex Downloads");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                DownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music", "Symphex Downloads");
            }
            else
            {
                DownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music", "Symphex Downloads");
            }
        }

        private bool IsSpotifyUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            // More comprehensive Spotify URL detection
            return url.Contains("open.spotify.com/") ||
                   url.Contains("spotify.com/") ||
                   url.StartsWith("spotify:") ||
                   url.Contains("spotify://");
        }

        [ObservableProperty]
        private bool isProcessingSpotify = false;

        [ObservableProperty]
        private int totalSpotifyTracks = 0;

        [ObservableProperty]
        private int processedSpotifyTracks = 0;



        private async Task ProcessSpotifyDownload(string spotifyUrl)
        {
            try
            {
                CliOutput += $"Spotify URL detected: {spotifyUrl}\n";
                CliOutput += "Converting to YouTube search...\n";

                StatusText = "Spotify URL detected. Converting to YouTube search...";

                // Convert the Spotify URL to a search term
                string searchTerm = await ConvertSpotifyUrlToSearchTerm(spotifyUrl);

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    CliOutput += $"Converted to search: {searchTerm}\n";
                    StatusText = $"Searching YouTube for: {searchTerm}";

                    // IMPORTANT: Don't update DownloadUrl in batch mode
                    if (!IsBatchProcessing)
                    {
                        DownloadUrl = searchTerm;
                    }

                    // Process as a regular YouTube search with the converted term
                    await ProcessConvertedSpotifySearch(searchTerm);
                }
                else
                {
                    StatusText = "Could not convert Spotify URL. Skipping...";
                    CliOutput += "Conversion failed. Skipping this URL.\n";

                    if (IsBatchProcessing)
                    {
                        CliOutput += "Continuing to next URL in batch...\n";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error processing Spotify URL: {ex.Message}";
                CliOutput += $"Spotify processing error: {ex.Message}\n";

                if (IsBatchProcessing)
                {
                    CliOutput += "Continuing to next URL in batch...\n";
                }
            }
        }

        private async Task ProcessConvertedSpotifySearch(string searchTerm)
        {
            try
            {
                DownloadProgress = 0;
                ShowMetadata = false;
                CurrentTrack = new TrackInfo();

                StatusText = $"Searching YouTube for: {searchTerm}";
                CliOutput += $"YouTube search initiated: {searchTerm}\n";

                DownloadProgress = 5;

                var extractedTrack = await ExtractMetadata(searchTerm);

                if (extractedTrack != null)
                {
                    CurrentTrack = extractedTrack;
                    ShowMetadata = true;
                    StatusText = $"Found: {CurrentTrack.Title} by {CurrentTrack.Artist}";
                    CliOutput += $"Match found: {CurrentTrack.Title} by {CurrentTrack.Artist}\n";
                    DownloadProgress = 15;

                    // Proceed with actual download
                    await RealDownloadWithSearchTerm(searchTerm);
                }
                else
                {
                    StatusText = "No results found for converted Spotify search.";
                    CliOutput += "No YouTube results found for converted Spotify term.\n";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Converted search failed: {ex.Message}";
                CliOutput += $"Converted YouTube search error: {ex.Message}\n";
                throw;
            }
        }

        private async Task RealDownloadWithSearchTerm(string searchTerm)
        {
            try
            {
                DownloadProgress = 20;

                // Always treat this as a search since it came from Spotify conversion
                string fullUrl = $"ytsearch1:{searchTerm}";

                var output = new StringBuilder();
                var error = new StringBuilder();

                // Create filename based on cleaned metadata
                string filenameTemplate;
                if (CurrentTrack != null && !string.IsNullOrEmpty(CurrentTrack.Title) && !string.IsNullOrEmpty(CurrentTrack.Artist))
                {
                    string cleanTitle = SanitizeFilename(CurrentTrack.Title);
                    string cleanArtist = SanitizeFilename(CurrentTrack.Artist);
                    filenameTemplate = Path.Combine(DownloadFolder, $"{cleanArtist} - {cleanTitle}.%(ext)s");
                    
                    // Delete existing files with the same name (any extension) to avoid duplicates
                    DeleteExistingFile(cleanArtist, cleanTitle);
                }
                else
                {
                    filenameTemplate = Path.Combine(DownloadFolder, "%(uploader)s - %(title)s.%(ext)s");
                }

                List<string> argsList = new List<string>
        {
            $"\"{fullUrl}\"",
            "--extract-audio",
            "--audio-format", "mp3",
            "--audio-quality", "0",
            "--no-playlist"
        };

                // Only embed thumbnail if user wants thumbnails
                bool actuallySkipThumbnail = !SkipThumbnailDownload;
                if (!actuallySkipThumbnail)
                {
                    argsList.Add("--embed-thumbnail");
                }

                argsList.Add("--add-metadata");
                argsList.Add("-o");
                argsList.Add($"\"{filenameTemplate}\"");

                // Add FFmpeg location if available
                if (!string.IsNullOrEmpty(FfmpegPath) && File.Exists(FfmpegPath))
                {
                    string ffmpegDir = Path.GetDirectoryName(FfmpegPath) ?? "";
                    argsList.AddRange(new[] { "--ffmpeg-location", $"\"{ffmpegDir}\"" });
                }

                string args = string.Join(" ", argsList);

                StatusText = "Downloading converted Spotify track...";
                CliOutput += "Starting download of converted Spotify track...\n";
                DownloadProgress = 30;

                var result = await Cli.Wrap(YtDlpPath)
                    .WithArguments(args)
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(error))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();

                DownloadProgress = 90;

                var outputText = output.ToString();
                var errorText = error.ToString();

                if (result.ExitCode == 0)
                {
                    DownloadProgress = 95;
                    StatusText = "Applying metadata and finalizing...";

                    string actualFilePath = await FindDownloadedFile(outputText);

                    if (!string.IsNullOrEmpty(actualFilePath) && CurrentTrack != null)
                    {
                        CurrentTrack.FileName = Path.GetFileName(actualFilePath);
                        CurrentTrack.Comment = $"Converted from Spotify search: {searchTerm}";

                        await ApplyProperMetadata();
                    }

                    DownloadProgress = 100;
                    await VerifyAndReportDownloadSuccess(actualFilePath);

                    CliOutput += $"Successfully downloaded converted Spotify track: {CurrentTrack?.Title ?? "Unknown"}\n";
                }
                else
                {
                    throw new Exception($"Download failed with exit code {result.ExitCode}: {errorText}");
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Converted download failed: {ex.Message}";
                CliOutput += $"Converted download error: {ex.Message}\n";
                throw;
            }
        }

        private async Task<string> ConvertSpotifyUrlToSearchTerm(string spotifyUrl)
        {
            try
            {
                // Try to extract basic info from URL structure or page title
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                // Set a reasonable timeout
                client.Timeout = TimeSpan.FromSeconds(10);

                var response = await client.GetStringAsync(spotifyUrl);

                // Look for page title which often contains track info
                var titleMatch = System.Text.RegularExpressions.Regex.Match(response,
                    @"<title>([^<]+)</title>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (titleMatch.Success)
                {
                    string pageTitle = System.Net.WebUtility.HtmlDecode(titleMatch.Groups[1].Value);

                    // Remove Spotify branding
                    pageTitle = System.Text.RegularExpressions.Regex.Replace(pageTitle,
                        @"\s*[\|\-]\s*Spotify\s*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    // CLEAN UP SPOTIFY-SPECIFIC PHRASES
                    pageTitle = CleanSpotifyTitle(pageTitle);

                    // Clean up the title for search
                    pageTitle = pageTitle.Trim();

                    if (!string.IsNullOrEmpty(pageTitle) && !pageTitle.Equals("Spotify", StringComparison.OrdinalIgnoreCase))
                    {
                        CliOutput += $"Extracted title: {pageTitle}\n";
                        return pageTitle;
                    }
                }

                return "";
            }
            catch (Exception ex)
            {
                CliOutput += $"Title extraction failed: {ex.Message}\n";
                return "";
            }
        }

        private string CleanSpotifyTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return title;

            // List of Spotify-specific phrases to remove
            var spotifyPhrases = new[]
            {
        @"\s*-\s*song and lyrics by\s*",
        @"\s*\|\s*song and lyrics by\s*",
        @"\s*song and lyrics by\s*",
        @"\s*-\s*lyrics by\s*",
        @"\s*\|\s*lyrics by\s*",
        @"\s*lyrics by\s*",
        @"\s*-\s*song by\s*",
        @"\s*\|\s*song by\s*",
        @"\s*song by\s*",
        @"\s*on Spotify\s*",
        @"\s*\|\s*Spotify\s*",
        @"\s*-\s*Spotify\s*"
    };

            string cleaned = title;

            // Remove each Spotify-specific phrase
            foreach (var phrase in spotifyPhrases)
            {
                cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, phrase, "",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Additional cleanup for common Spotify title formats
            // Handle formats like "Song Title - song and lyrics by Artist Name"
            var songLyricsMatch = System.Text.RegularExpressions.Regex.Match(cleaned,
                @"^(.+?)\s*-\s*song and lyrics by\s*(.+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (songLyricsMatch.Success)
            {
                string songTitle = songLyricsMatch.Groups[1].Value.Trim();
                string artistName = songLyricsMatch.Groups[2].Value.Trim();
                cleaned = $"{artistName} - {songTitle}";
            }
            else
            {
                // Handle other formats like "Song Title | song and lyrics by Artist Name"
                var pipeMatch = System.Text.RegularExpressions.Regex.Match(cleaned,
                    @"^(.+?)\s*\|\s*song and lyrics by\s*(.+)$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (pipeMatch.Success)
                {
                    string songTitle = pipeMatch.Groups[1].Value.Trim();
                    string artistName = pipeMatch.Groups[2].Value.Trim();
                    cleaned = $"{artistName} - {songTitle}";
                }
            }

            // Handle concatenated text without spaces (e.g., "Remind Me to ForgetKygo, Miguel")
            cleaned = AddSpacesToConcatenatedText(cleaned);

            // Final cleanup - remove extra whitespace and normalize
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();

            return cleaned;
        }

        // NEW METHOD: Add spaces to concatenated artist/title text
        private string AddSpacesToConcatenatedText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Pattern to detect when a lowercase letter is immediately followed by an uppercase letter
            // This catches cases like "ForgetKygo" -> "Forget Kygo"
            string spaced = System.Text.RegularExpressions.Regex.Replace(text,
                @"([a-z])([A-Z])", "$1 $2");

            // Pattern to detect when a letter is immediately followed by a number
            // This catches cases like "Song1Artist" -> "Song1 Artist"
            spaced = System.Text.RegularExpressions.Regex.Replace(spaced,
                @"([a-zA-Z])(\d)", "$1 $2");

            // Pattern to detect when a number is immediately followed by a letter
            // This catches cases like "1Artist" -> "1 Artist"
            spaced = System.Text.RegularExpressions.Regex.Replace(spaced,
                @"(\d)([a-zA-Z])", "$1 $2");

            return spaced;
        }

        private async Task<TrackInfo?> ExtractMetadata(string url)
        {
            if (_downloadService == null)
            {
                InitializeDownloadService();
            }

            return await _downloadService!.ExtractMetadata(url, SelectedThumbnailSize, SkipThumbnailDownload);
        }

        private void UpdateArtworkSizeInfo()
        {
            if (CurrentTrack == null)
            {
                ArtworkSizeInfo = "";
                return;
            }

            var artwork = CurrentTrack.AlbumArt ?? CurrentTrack.Thumbnail;
            if (artwork != null)
            {
                int width = artwork.PixelSize.Width;
                int height = artwork.PixelSize.Height;
                string source = CurrentTrack.HasRealAlbumArt ? "Album Art" : "Thumbnail";
                ArtworkSizeInfo = $"{source}: {width}x{height}px";
            }
            else if (!SkipThumbnailDownload && !EnableAlbumArtDownload)
            {
                ArtworkSizeInfo = "No artwork (all disabled)";
            }
            else if (!SkipThumbnailDownload)
            {
                ArtworkSizeInfo = "Thumbnail skipped";
            }
            else
            {
                ArtworkSizeInfo = "No artwork found";
            }
        }

        private async Task ApplyProperMetadata()
        {
            if (_downloadService == null || CurrentTrack == null)
                return;

            if (!string.IsNullOrEmpty(CurrentTrack.FileName))
            {
                string audioFilePath = Path.Combine(DownloadFolder, CurrentTrack.FileName);
                await _downloadService.ApplyMetadata(CurrentTrack, audioFilePath);
            }
        }

        [RelayCommand]
        private async Task Download()
        {
            // Check if we're in resize mode
            if (IsResizeMode)
            {
                if (QueuedFiles.Count == 0)
                {
                    ShowToast("❌ Please drag and drop files to resize");
                    return;
                }

                // Reload settings to get latest AlbumArtSize
                LoadUserSettings();

                // Process the queued files
                IsDownloading = true;
                DownloadProgress = 0;
                ShowToast($"🎨 Resizing to {AlbumArtSize}x{AlbumArtSize}px...");

                int processed = 0;
                int failed = 0;
                int total = QueuedFiles.Count;

                for (int i = 0; i < QueuedFiles.Count; i++)
                {
                    try
                    {
                        await ResizeAlbumArtForFile(QueuedFiles[i]);
                        processed++;
                        DownloadProgress = (double)(i + 1) / total * 100;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MainWindowViewModel] Error processing {QueuedFiles[i]}: {ex.Message}");
                        failed++;
                    }
                }

                QueuedFiles.Clear();
                IsDownloading = false;
                DownloadProgress = 0;
                ShowToast($"✅ Resized: {processed}, Failed: {failed}");
                return;
            }

            if (string.IsNullOrWhiteSpace(DownloadUrl))
            {
                StatusText = "Please enter a URL or search term.";
                return;
            }

            if (string.IsNullOrEmpty(YtDlpPath))
            {
                StatusText = "yt-dlp not available. Please download it first.";
                return;
            }

            // Extract all URLs from the input - this is the key fix
            var urls = ExtractAllUrls(DownloadUrl);

            CliOutput += $"DEBUG: Found {urls.Count} URLs in input: {DownloadUrl}\n";
            foreach (var url in urls)
            {
                CliOutput += $"  - {url}\n";
            }

            // If multiple URLs detected, process as batch
            if (urls.Count > 1)
            {
                StatusText = $"Multiple URLs detected ({urls.Count})! Setting up batch processing...";
                CliOutput += $"Multiple URLs detected, starting batch processing...\n";
                IsDownloading = true;

                await Task.Delay(1000); // Brief pause for UI update
                await ProcessMultipleUrlsList(urls);
                return;
            }

            // Single URL processing
            string singleUrl = urls.Count == 1 ? urls[0] : DownloadUrl;

            if (IsSpotifyUrl(singleUrl))
            {
                await ProcessSpotifyDownload(singleUrl);
                return;
            }

            // Continue with existing single download logic
            IsDownloading = true;
            DownloadProgress = 0;
            ShowMetadata = false;
            CurrentTrack = new TrackInfo();

            StatusText = $"Starting download for: {singleUrl}";

            try
            {
                DownloadProgress = 5;
                var extractedTrack = await ExtractMetadata(singleUrl);

                if (extractedTrack != null)
                {
                    CurrentTrack = extractedTrack;
                    ShowMetadata = true;
                    StatusText = $"Found: {CurrentTrack.Title} by {CurrentTrack.Artist}";
                    DownloadProgress = 15;

                    // Show artwork size info
                    UpdateArtworkSizeInfo();

                    // Show artwork selection if enabled
                    if (EnableArtworkSelection && CurrentTrack.AvailableThumbnails != null && CurrentTrack.AvailableThumbnails.Count > 0)
                    {
                        await ShowArtworkSelectionDialog();
                    }
                }
                else
                {
                    StatusText = "Could not extract metadata, proceeding with download...";
                    DownloadProgress = 10;
                }

                await RealDownload();
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                ShowMetadata = false;
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
            }
        }

        private List<string> ExtractAllUrls(string input)
        {
            var urls = new List<string>();

            if (string.IsNullOrWhiteSpace(input))
                return urls;

            // Split by whitespace and newlines
            var parts = input.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var trimmedPart = part.Trim();

                // Check if it's a valid URL (HTTP/HTTPS or Spotify)
                if (trimmedPart.StartsWith("https://") || trimmedPart.StartsWith("http://"))
                {
                    urls.Add(trimmedPart);
                }
                else if (trimmedPart.StartsWith("spotify:"))
                {
                    urls.Add(trimmedPart);
                }
            }

            // Remove duplicates while preserving order
            return urls.Distinct().ToList();
        }

        private async Task ProcessMultipleUrlsList(List<string> urls)
        {
            try
            {
                StatusText = "Setting up batch processing...";
                CliOutput += "=== BATCH PROCESSING STARTED ===\n";
                CliOutput += $"Found {urls.Count} URLs to process:\n";

                int spotifyUrls = urls.Count(url => IsSpotifyUrl(url));
                int otherUrls = urls.Count - spotifyUrls;

                CliOutput += $"- Spotify URLs: {spotifyUrls}\n";
                CliOutput += $"- Other URLs: {otherUrls}\n\n";

                // List all URLs
                for (int i = 0; i < urls.Count; i++)
                {
                    string urlType = IsSpotifyUrl(urls[i]) ? "[SPOTIFY]" : "[OTHER]";
                    CliOutput += $"  {i + 1}. {urlType} {urls[i]}\n";
                }
                CliOutput += "\n";

                // Create batch file
                if (!Directory.Exists(DownloadFolder))
                {
                    Directory.CreateDirectory(DownloadFolder);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string batchFilePath = Path.Combine(DownloadFolder, $"batch_urls_{timestamp}.txt");
                await File.WriteAllLinesAsync(batchFilePath, urls);
                CurrentBatchFilePath = batchFilePath;

                CliOutput += $"Created batch file: {Path.GetFileName(batchFilePath)}\n\n";

                // Initialize batch processing state
                PendingUrls = new List<string>(urls);
                IsBatchProcessing = true;
                CurrentBatchIndex = 0;
                TotalBatchCount = urls.Count;
                IsDownloading = true;
                ActiveDownloads = 0;
                RunningTasks.Clear();

                // Determine processing strategy based on URL types
                if (spotifyUrls > 1 && spotifyUrls >= otherUrls)
                {
                    StatusText = $"Starting parallel Spotify processing: {spotifyUrls} Spotify URLs...";
                    CliOutput += $"Multiple Spotify URLs detected - using parallel processing (max {MaxConcurrentDownloads} concurrent)\n\n";
                    await ProcessUrlsInParallel(urls);
                }
                else
                {
                    StatusText = $"Starting sequential processing: {urls.Count} URLs...";
                    CliOutput += "Mixed URLs or few Spotify URLs - using sequential processing\n\n";
                    await ProcessUrlsSequentially(urls);
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error setting up batch processing: {ex.Message}";
                CliOutput += $"Batch setup error: {ex.Message}\n";
                await CleanupBatchProcessing();
            }
        }

        private async Task ProcessUrlsInParallel(List<string> urls)
        {
            try
            {
                var semaphore = new SemaphoreSlim(MaxConcurrentDownloads, MaxConcurrentDownloads);
                var tasks = new List<Task>();
                var completedCount = 0;
                var lockObject = new object();

                foreach (var (url, index) in urls.Select((url, i) => (url, i)))
                {
                    var task = ProcessUrlWithSemaphore(url, index, semaphore, () =>
                    {
                        lock (lockObject)
                        {
                            completedCount++;
                            StatusText = $"Completed {completedCount}/{urls.Count} downloads...";
                        }
                    });

                    tasks.Add(task);
                }

                // Wait for all downloads to complete
                await Task.WhenAll(tasks);

                // Final cleanup
                await CleanupBatchProcessing();
                StatusText = $"Batch download complete! Downloaded {urls.Count} songs in parallel.";
                CliOutput += $"\n=== PARALLEL BATCH PROCESSING COMPLETE ===\n";
                CliOutput += $"Successfully processed {urls.Count} URLs in parallel.\n";
            }
            catch (Exception ex)
            {
                CliOutput += $"Parallel processing error: {ex.Message}\n";
                await CleanupBatchProcessing();
            }
        }

        // New method for sequential processing (fallback)
        private async Task ProcessUrlsSequentially(List<string> urls)
        {
            try
            {
                for (int i = 0; i < urls.Count; i++)
                {
                    CurrentBatchIndex = i;
                    StatusText = $"Processing {i + 1}/{urls.Count}: {Path.GetFileName(urls[i])}";

                    try
                    {
                        await ProcessSingleUrlForBatch(urls[i], i);
                        CliOutput += $"✅ Completed {i + 1}/{urls.Count}\n";
                    }
                    catch (Exception urlEx)
                    {
                        CliOutput += $"❌ Failed {i + 1}/{urls.Count}: {urlEx.Message}\n";
                    }

                    // Brief pause between sequential downloads
                    if (i < urls.Count - 1)
                    {
                        await Task.Delay(1000);
                    }
                }

                await CleanupBatchProcessing();
                StatusText = $"Batch download complete! Downloaded {urls.Count} songs sequentially.";
            }
            catch (Exception ex)
            {
                CliOutput += $"Sequential processing error: {ex.Message}\n";
                await CleanupBatchProcessing();
            }
        }

        // Helper method for parallel processing with semaphore
        private async Task ProcessUrlWithSemaphore(string url, int index, SemaphoreSlim semaphore, Action onComplete)
        {
            await semaphore.WaitAsync();

            try
            {
                ActiveDownloads++;
                CliOutput += $"[{DateTime.Now:HH:mm:ss}] Starting download {index + 1}: {url}\n";

                await ProcessSingleUrlForBatch(url, index);

                CliOutput += $"[{DateTime.Now:HH:mm:ss}] ✅ Completed download {index + 1}\n";
                onComplete?.Invoke();
            }
            catch (Exception ex)
            {
                CliOutput += $"[{DateTime.Now:HH:mm:ss}] ❌ Failed download {index + 1}: {ex.Message}\n";
            }
            finally
            {
                ActiveDownloads--;
                semaphore.Release();
            }
        }

        private async Task ProcessSingleUrlForBatch(string url, int index)
        {
            try
            {
                // Create a new track info for this specific download
                var trackInfo = new TrackInfo();

                CliOutput += $"\n--- Processing {index + 1}/{TotalBatchCount} ---\n";
                CliOutput += $"URL: {url}\n";

                // Update UI to show current processing URL (thread-safe)
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    CurrentProcessingUrl = url;
                });

                if (IsSpotifyUrl(url))
                {
                    CliOutput += "Type: Spotify URL - Converting to search...\n";
                    await ProcessSpotifyDownloadForBatch(url, trackInfo, index);
                }
                else
                {
                    CliOutput += "Type: Direct URL - Processing...\n";
                    await ProcessDirectUrlForBatch(url, trackInfo, index);
                }

                // Update UI with the completed track info (thread-safe)
                if (!string.IsNullOrEmpty(trackInfo.Title))
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        LastProcessedTrack = trackInfo;
                        // Update main CurrentTrack for UI display of the latest completed download
                        CurrentTrack = trackInfo;
                        ShowMetadata = true;
                    });
                }
            }
            catch (Exception ex)
            {
                CliOutput += $"Error processing URL {url}: {ex.Message}\n";
                throw;
            }
        }

        private async Task ProcessSpotifyDownloadForBatch(string spotifyUrl, TrackInfo trackInfo, int index)
        {
            try
            {
                CliOutput += $"[Thread {index}] Spotify URL detected: {spotifyUrl}\n";
                CliOutput += $"[Thread {index}] Converting to YouTube search...\n";

                string searchTerm = await ConvertSpotifyUrlToSearchTerm(spotifyUrl);

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    CliOutput += $"[Thread {index}] Converted to search: {searchTerm}\n";

                    // Extract metadata for this specific track WITH ALBUM ART
                    var extractedTrack = await ExtractMetadataWithAlbumArt(searchTerm, index);

                    if (extractedTrack != null)
                    {
                        // Copy extracted data to our local track info
                        CopyTrackInfo(extractedTrack, trackInfo);
                        CliOutput += $"[Thread {index}] Match found: {trackInfo.Title} by {trackInfo.Artist}\n";

                        // Ensure album art is preserved
                        if (extractedTrack.AlbumArt != null)
                        {
                            trackInfo.AlbumArt = extractedTrack.AlbumArt;
                            trackInfo.HasRealAlbumArt = extractedTrack.HasRealAlbumArt;
                            CliOutput += $"[Thread {index}] Album art retrieved and preserved\n";
                        }

                        // Download with the converted search term
                        await RealDownloadForBatch(searchTerm, trackInfo, index);
                    }
                    else
                    {
                        CliOutput += $"[Thread {index}] No YouTube results found for converted Spotify term.\n";
                    }
                }
                else
                {
                    CliOutput += $"[Thread {index}] Conversion failed. Skipping this URL.\n";
                }
            }
            catch (Exception ex)
            {
                CliOutput += $"[Thread {index}] Spotify processing error: {ex.Message}\n";
                throw;
            }
        }

        // Direct URL processing method for batch operations
        private async Task ProcessDirectUrlForBatch(string url, TrackInfo trackInfo, int index)
        {
            try
            {
                CliOutput += $"[Thread {index}] Extracting metadata...\n";

                var extractedTrack = await ExtractMetadataWithAlbumArt(url, index);

                if (extractedTrack != null)
                {
                    CopyTrackInfo(extractedTrack, trackInfo);
                    CliOutput += $"[Thread {index}] Found: {trackInfo.Title} by {trackInfo.Artist}\n";

                    // Ensure album art is preserved
                    if (extractedTrack.AlbumArt != null)
                    {
                        trackInfo.AlbumArt = extractedTrack.AlbumArt;
                        trackInfo.HasRealAlbumArt = extractedTrack.HasRealAlbumArt;
                        CliOutput += $"[Thread {index}] Album art retrieved and preserved\n";
                    }
                }
                else
                {
                    CliOutput += $"[Thread {index}] Could not extract metadata, proceeding with download...\n";
                }

                await RealDownloadForBatch(url, trackInfo, index);
            }
            catch (Exception ex)
            {
                CliOutput += $"[Thread {index}] Direct URL processing error: {ex.Message}\n";
                throw;
            }
        }

        private async Task<TrackInfo?> ExtractMetadataWithAlbumArt(string url, int threadIndex)
        {
            if (_downloadService == null)
            {
                InitializeDownloadService();
            }

            return await _downloadService!.ExtractMetadataWithAlbumArt(url, threadIndex);
        }

        private Task RealDownloadForBatch(string url, TrackInfo trackInfo, int index)
        {
            if (_downloadService == null)
            {
                InitializeDownloadService();
            }

            return _downloadService!.DownloadForBatch(url, trackInfo, index);
        }

        // Helper method to copy track information
        private void CopyTrackInfo(TrackInfo source, TrackInfo destination)
        {
            destination.Title = source.Title;
            destination.Artist = source.Artist;
            destination.Album = source.Album;
            destination.Duration = source.Duration;
            destination.Url = source.Url;
            destination.Uploader = source.Uploader;
            destination.UploadDate = source.UploadDate;
            destination.ViewCount = source.ViewCount;
            destination.Thumbnail = source.Thumbnail;
            destination.AlbumArt = source.AlbumArt;
            destination.HasRealAlbumArt = source.HasRealAlbumArt;
            destination.Genre = source.Genre;
            destination.Year = source.Year;
            destination.TrackNumber = source.TrackNumber;
            destination.AlbumArtist = source.AlbumArtist;
            destination.Comment = source.Comment;
            destination.Encoder = source.Encoder;
        }

        // Metadata application for batch processing
        private async Task ApplyProperMetadataForBatch(TrackInfo trackInfo, string audioFilePath)
        {
            if (_downloadService == null)
                return;

            await _downloadService.ApplyMetadataForBatch(trackInfo, audioFilePath);
        }

        // Cleanup method for batch processing
        private async Task CleanupBatchProcessing()
        {
            try
            {
                // Delete batch file
                if (!string.IsNullOrEmpty(CurrentBatchFilePath) && File.Exists(CurrentBatchFilePath))
                {
                    try
                    {
                        File.Delete(CurrentBatchFilePath);
                        CliOutput += $"Batch file deleted: {Path.GetFileName(CurrentBatchFilePath)}\n";
                    }
                    catch (Exception)
                    {
                        CliOutput += "Could not delete batch file\n";
                    }
                }

                // Reset all batch-related state
                IsBatchProcessing = false;
                IsDownloading = false;
                CurrentBatchIndex = 0;
                TotalBatchCount = 0;
                CurrentBatchFilePath = "";
                ActiveDownloads = 0;
                PendingUrls?.Clear();
                RunningTasks.Clear();
                DownloadProgress = 0;
                ShowMetadata = false;
                CurrentTrack = new TrackInfo();

                CliOutput += $"Check your download folder: {DownloadFolder}\n\n";
            }
            catch (Exception ex)
            {
                CliOutput += $"Cleanup error: {ex.Message}\n";
            }
        }

        private async Task ProcessNextBatchUrl()
        {
            try
            {
                CliOutput += $"DEBUG: ProcessNextBatchUrl - Index: {CurrentBatchIndex}, Total: {PendingUrls?.Count ?? 0}\n";

                // Check if batch is complete
                if (PendingUrls == null || CurrentBatchIndex >= PendingUrls.Count)
                {
                    // DELETE BATCH FILE BEFORE COMPLETING
                    if (!string.IsNullOrEmpty(CurrentBatchFilePath) && File.Exists(CurrentBatchFilePath))
                    {
                        try
                        {
                            File.Delete(CurrentBatchFilePath);
                            CliOutput += $"Batch file deleted: {Path.GetFileName(CurrentBatchFilePath)}\n";
                        }
                        catch (Exception deleteEx)
                        {
                            CliOutput += $"Could not delete batch file: {deleteEx.Message}\n";
                        }
                    }

                    // Batch complete
                    StatusText = $"Batch download complete! Downloaded {TotalBatchCount} songs.";
                    CliOutput += $"\n=== BATCH PROCESSING COMPLETE ===\n";
                    CliOutput += $"Successfully processed {TotalBatchCount} URLs.\n";
                    CliOutput += $"Check your download folder: {DownloadFolder}\n\n";

                    // Reset batch state including file path
                    IsBatchProcessing = false;
                    IsDownloading = false;
                    CurrentBatchIndex = 0;
                    TotalBatchCount = 0;
                    CurrentBatchFilePath = ""; // Clear the file path
                    PendingUrls?.Clear();
                    DownloadProgress = 0;
                    ShowMetadata = false;
                    CurrentTrack = new TrackInfo();
                    return;
                }

                // Get current URL
                string currentUrl = PendingUrls[CurrentBatchIndex];

                // Update UI with progress
                StatusText = $"Processing {CurrentBatchIndex + 1}/{TotalBatchCount}: {Path.GetFileName(currentUrl)}";
                CliOutput += $"\n--- Processing {CurrentBatchIndex + 1}/{TotalBatchCount} ---\n";
                CliOutput += $"URL: {currentUrl}\n";

                // Reset progress for this item
                DownloadProgress = 0;
                ShowMetadata = false;

                try
                {
                    // Process this URL
                    if (IsSpotifyUrl(currentUrl))
                    {
                        CliOutput += "Type: Spotify URL - Converting to search...\n";
                        await ProcessSpotifyDownload(currentUrl);
                    }
                    else
                    {
                        CliOutput += "Type: Direct URL - Processing...\n";
                        await ProcessSingleBatchUrl(currentUrl);
                    }

                    CliOutput += $"✅ Completed {CurrentBatchIndex + 1}/{TotalBatchCount}\n";
                }
                catch (Exception urlEx)
                {
                    CliOutput += $"❌ Failed {CurrentBatchIndex + 1}/{TotalBatchCount}: {urlEx.Message}\n";
                    StatusText = $"Error with item {CurrentBatchIndex + 1}, continuing...";
                }

                // Move to next URL
                CurrentBatchIndex++;

                // Brief pause between downloads
                if (CurrentBatchIndex < PendingUrls.Count)
                {
                    StatusText = $"Completed {CurrentBatchIndex}/{TotalBatchCount}. Next in 2 seconds...";
                    await Task.Delay(2000);

                    // Continue to next URL
                    await ProcessNextBatchUrl();
                }
                else
                {
                    // This will trigger the completion logic above on next call
                    await ProcessNextBatchUrl();
                }
            }
            catch (Exception ex)
            {
                CliOutput += $"Critical batch error: {ex.Message}\n";
                StatusText = "Batch processing stopped due to error.";

                // ALSO DELETE BATCH FILE ON ERROR
                if (!string.IsNullOrEmpty(CurrentBatchFilePath) && File.Exists(CurrentBatchFilePath))
                {
                    try
                    {
                        File.Delete(CurrentBatchFilePath);
                        CliOutput += $"Batch file deleted after error: {Path.GetFileName(CurrentBatchFilePath)}\n";
                    }
                    catch (Exception deleteEx)
                    {
                        CliOutput += $"Could not delete batch file after error: {deleteEx.Message}\n";
                    }
                }

                IsBatchProcessing = false;
                IsDownloading = false;
                CurrentBatchFilePath = ""; // Clear the file path
            }
        }

        private async Task ProcessSingleBatchUrl(string url)
        {
            try
            {
                // Reset track info for this URL
                CurrentTrack = new TrackInfo();

                // Extract metadata first
                DownloadProgress = 5;
                var extractedTrack = await ExtractMetadata(url);

                if (extractedTrack != null)
                {
                    CurrentTrack = extractedTrack;
                    ShowMetadata = true;
                    CliOutput += $"Found: {CurrentTrack.Title} by {CurrentTrack.Artist}\n";
                    DownloadProgress = 15;
                }
                else
                {
                    CliOutput += "Could not extract metadata, proceeding with download...\n";
                    DownloadProgress = 10;
                }

                // Perform the actual download
                await RealDownload();

                CliOutput += $"Download completed for: {url}\n";
            }
            catch (Exception ex)
            {
                CliOutput += $"Error processing URL {url}: {ex.Message}\n";
                throw; // Re-throw so the batch processor can handle it
            }
        }

        private async Task RealDownload()
        {
            try
            {
                DownloadProgress = 20;

                if (_downloadService == null)
                {
                    InitializeDownloadService();
                }

                StatusText = DownloadUrl.StartsWith("http") ? "Downloading audio..." : "Searching and downloading audio...";
                DownloadProgress = 30;

                string actualFilePath = await _downloadService.PerformDownload(DownloadUrl, CurrentTrack);

                DownloadProgress = 90;

                if (!string.IsNullOrEmpty(actualFilePath) && CurrentTrack != null)
                {
                    CurrentTrack.FileName = Path.GetFileName(actualFilePath);
                    StatusText = "Applying metadata and finalizing...";
                    DownloadProgress = 95;

                    // Apply enhanced metadata
                    await ApplyProperMetadata();
                }

                DownloadProgress = 100;
                await VerifyAndReportDownloadSuccess(actualFilePath);
            }
            catch (Exception ex)
            {
                StatusText = $"Download failed: {ex.Message}";
                throw;
            }
        }

        private Task<string> FindDownloadedFile(string ytDlpOutput)
        {
            try
            {
                // Parse yt-dlp output to find the actual downloaded file
                var lines = ytDlpOutput.Split('\n');

                // Look for extraction destination line
                var destinationLine = lines.FirstOrDefault(l =>
                    l.Contains("[ExtractAudio] Destination:") ||
                    l.Contains("has already been downloaded"));

                if (destinationLine != null)
                {
                    if (destinationLine.Contains("Destination: "))
                    {
                        int destinationIndex = destinationLine.IndexOf("Destination: ");
                        return Task.FromResult(destinationLine.Substring(destinationIndex + "Destination: ".Length).Trim());
                    }
                }

                // Fallback: look for recently created MP3 files
                if (Directory.Exists(DownloadFolder))
                {
                    var recentFiles = Directory.GetFiles(DownloadFolder, "*.mp3")
                        .Where(f => File.GetCreationTime(f) > DateTime.Now.AddMinutes(-2))
                        .OrderByDescending(f => File.GetCreationTime(f))
                        .ToArray();

                    if (recentFiles.Length > 0)
                    {
                        return Task.FromResult(recentFiles[0]);
                    }
                }

                // Final fallback: construct expected filename
                if (CurrentTrack != null && !string.IsNullOrEmpty(CurrentTrack.Title) && !string.IsNullOrEmpty(CurrentTrack.Artist))
                {
                    string cleanTitle = SanitizeFilename(CurrentTrack.Title);
                    string cleanArtist = SanitizeFilename(CurrentTrack.Artist);
                    string expectedFilename = $"{cleanArtist} - {cleanTitle}.mp3";
                    return Task.FromResult(Path.Combine(DownloadFolder, expectedFilename));
                }

                return Task.FromResult("");
            }
            catch (Exception)
            {
                return Task.FromResult("");
            }
        }

        private async Task ShowSuccessToastNotification(string filename, string fileSize)
        {
            try
            {
                string songTitle = CurrentTrack?.Title ?? "Unknown Song";
                string artist = CurrentTrack?.Artist ?? "Unknown Artist";

                ToastMessage = $"Downloaded: {songTitle} by {artist} ({fileSize})";
                IsToastVisible = true;

                // Hide the toast after 5 seconds
                await Task.Delay(5000);
                IsToastVisible = false;
            }
            catch (Exception)
            {
                // Fallback to status text if toast fails
                StatusText = $"Successfully downloaded {filename} ({fileSize})";
            }
        }

        private async Task VerifyAndReportDownloadSuccess(string expectedFilePath)
        {
            try
            {
                // Wait a moment for file system to update
                await Task.Delay(500);

                string actualFilePath = "";
                long fileSize = 0;

                // Try to find the downloaded file
                if (!string.IsNullOrEmpty(expectedFilePath) && File.Exists(expectedFilePath))
                {
                    actualFilePath = expectedFilePath;
                    fileSize = new FileInfo(expectedFilePath).Length;
                }
                else
                {
                    // Fallback: search for recently created MP3 files in the download folder
                    var recentFiles = Directory.GetFiles(DownloadFolder, "*.mp3")
                        .Where(f => File.GetCreationTime(f) > DateTime.Now.AddMinutes(-5))
                        .OrderByDescending(f => File.GetCreationTime(f))
                        .ToArray();

                    if (recentFiles.Length > 0)
                    {
                        actualFilePath = recentFiles[0];
                        fileSize = new FileInfo(actualFilePath).Length;
                    }
                }

                if (!string.IsNullOrEmpty(actualFilePath) && File.Exists(actualFilePath))
                {
                    string filename = Path.GetFileName(actualFilePath);
                    string fileSizeText = FormatFileSize(fileSize);

                    // Update CurrentTrack with actual filename if not already set
                    if (CurrentTrack != null && string.IsNullOrEmpty(CurrentTrack.FileName))
                    {
                        CurrentTrack.FileName = filename;
                    }

                    // Show toast notification instead of popup
                    await ShowSuccessToastNotification(filename, fileSizeText);

                    // Also update status text as backup
                    if (CurrentTrack != null && !string.IsNullOrEmpty(CurrentTrack.Title))
                    {
                        StatusText = $"Successfully downloaded '{CurrentTrack.Title}' by {CurrentTrack.Artist} ({fileSizeText})";
                    }
                    else
                    {
                        StatusText = $"Successfully downloaded '{filename}' ({fileSizeText})";
                    }
                }
                else
                {
                    // File not found - this shouldn't happen if download was successful
                    StatusText = "Download completed but file not found. Check your download folder.";
                }
            }
            catch (Exception)
            {
                StatusText = "Download completed but couldn't verify file details.";
            }
        }

        // Add this helper method to format file sizes nicely
        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] suffixes = { "B", "KB", "MB", "GB" };
            double size = bytes;
            int suffixIndex = 0;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return $"{size:F1} {suffixes[suffixIndex]}";
        }

        private string SanitizeFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return "Unknown";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                filename = filename.Replace(c.ToString(), "");
            }

            filename = filename
                .Replace(":", "")
                .Replace("\"", "")
                .Replace("*", "")
                .Replace("?", "")
                .Replace("|", "")
                .Replace("<", "")
                .Replace(">", "")
                .Replace("_", " ")
                .Trim();

            while (filename.Contains("  "))
            {
                filename = filename.Replace("  ", " ");
            }

            return filename.Trim();
        }

        private void DeleteExistingFile(string artist, string title)
        {
            try
            {
                // Get the current format's file extension
                string extension = GetFileExtensionFromFormat(SelectedAudioFormat);
                
                // Look for files matching "Artist - Title.extension" pattern (same format only)
                string searchPattern = $"{artist} - {title}.{extension}";
                string[] existingFiles = Directory.GetFiles(DownloadFolder, searchPattern);
                
                foreach (string file in existingFiles)
                {
                    try
                    {
                        File.Delete(file);
                        Debug.WriteLine($"[DeleteExistingFile] Deleted duplicate: {Path.GetFileName(file)}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DeleteExistingFile] Failed to delete {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DeleteExistingFile] Error searching for duplicates: {ex.Message}");
            }
        }

        private string GetFileExtensionFromFormat(string formatOption)
        {
            return formatOption switch
            {
                "MP3" => "mp3",
                "FLAC (Lossless)" => "flac",
                "WAV (Uncompressed)" => "wav",
                "M4A (AAC)" => "m4a",
                "Opus" => "opus",
                "Vorbis (OGG)" => "ogg",
                _ => "mp3"
            };
        }

        [RelayCommand]
        private async Task DownloadYtDlp()
        {
            try
            {
                StatusText = $"⬇️ Downloading yt-dlp for {dependencyManager.GetCurrentOS()}...";
                IsDownloading = true;
                DownloadProgress = 0;

                DownloadProgress = 30;
                await dependencyManager.DownloadYtDlp();

                DownloadProgress = 100;
                StatusText = $"✅ yt-dlp downloaded successfully for {dependencyManager.GetCurrentOS()}!";
            }
            catch (Exception ex)
            {
                StatusText = $"❌ Failed to download yt-dlp: {ex.Message}";
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
            }
        }

        [RelayCommand]
        private async Task DownloadFfmpeg()
        {
            try
            {
                string os = dependencyManager.GetCurrentOS();
                StatusText = $"⬇️ Downloading FFmpeg for {os}...";
                IsDownloading = true;
                DownloadProgress = 0;

                DownloadProgress = 10;
                await dependencyManager.DownloadFfmpeg();

                DownloadProgress = 100;
                StatusText = $"✅ FFmpeg downloaded and extracted for {os}!";
            }
            catch (InvalidOperationException ex)
            {
                StatusText = $"ℹ️ {ex.Message}";
            }
            catch (Exception ex)
            {
                StatusText = $"❌ Failed to download FFmpeg: {ex.Message}";
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
            }
        }

        [RelayCommand]
        private async Task CopyOutput()
        {
            try
            {
                if (!string.IsNullOrEmpty(CliOutput))
                {
                    var topLevel = TopLevel.GetTopLevel(Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow
                        : null);

                    if (topLevel?.Clipboard != null)
                    {
                        await topLevel.Clipboard.SetTextAsync(CliOutput);
                    }
                    else
                    {
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        [RelayCommand]
        private void Clear()
        {
            if (IsResizeMode)
            {
                QueuedFiles.Clear();
                ShowToast("🗑️ Queue cleared");
                return;
            }

            DownloadUrl = "";
            StatusText = "🎵 Ready to download music...";
            DownloadProgress = 0;
            ShowMetadata = false;
            CurrentTrack = new TrackInfo();

            // Clear batch processing state
            if (IsBatchProcessing)
            {
                IsBatchProcessing = false;
                CurrentBatchIndex = 0;
                TotalBatchCount = 0;
                PendingUrls.Clear();
                StatusText = "Batch processing cancelled.";
            }
        }

        [RelayCommand]
        private void ClearLog()
        {
            CliOutput = "Symphex Music Downloader v1.2.3\n" +
                        "=============================\n" +
                        "Log cleared...\n\n";
        }

        [RelayCommand]
        private async Task OpenFolderAsync()
        {
            try
            {
                // Determine which folder to open based on resize mode
                var folderToOpen = IsResizeMode ? ResizedFolderPath : DownloadFolder;

                // Ensure the directory exists first
                if (!Directory.Exists(folderToOpen))
                {
                    Directory.CreateDirectory(folderToOpen);
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("explorer.exe", folderToOpen);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // Method 1: Try the simple approach first
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "open",
                            Arguments = $"\"{folderToOpen}\"", // Quote the path
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };

                        var process = Process.Start(psi);
                        if (process != null)
                        {
                            await process.WaitForExitAsync();

                            if (process.ExitCode != 0)
                            {
                                // If that failed, try method 2
                                var psi2 = new ProcessStartInfo
                                {
                                    FileName = "/usr/bin/open",
                                    Arguments = DownloadFolder,
                                    UseShellExecute = true
                                };
                                Process.Start(psi2);
                            }
                        }
                    }
                    catch
                    {
                        // Fallback method using shell
                        var psi = new ProcessStartInfo
                        {
                            FileName = "/bin/bash",
                            Arguments = $"-c \"open '{DownloadFolder.Replace("'", "\\'")}'\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        Process.Start(psi);
                    }
                }
                else // Linux
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        Arguments = DownloadFolder,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                // Show user-friendly message instead of just console logging
                Console.WriteLine($"Failed to open folder: {ex.Message}");

                // Optional: Show a toast notification or dialog
                // You could also copy the path to clipboard as fallback
                // Clipboard.SetTextAsync(DownloadFolder);

                // Show toast with the path so user can navigate manually
            }
        }

        // Playlist Management Methods
        private void ShowToast(string message)
        {
            ToastMessage = message;
            IsToastVisible = true;
            
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsToastVisible = false;
                });
            });
        }

        private void LoadSavedLinks()
        {
            try
            {
                var links = playlistService.LoadSavedLinks();
                SavedLinks.Clear();
                foreach (var link in links)
                {
                    SavedLinks.Add(link);
                }
                ApplyFilter();
                Debug.WriteLine($"[MainWindowViewModel] Loaded {links.Count} saved links");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] Error loading saved links: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ToggleSidePanel()
        {
            IsSidePanelOpen = !IsSidePanelOpen;
        }

        [RelayCommand]
        private async Task SaveCurrentLink()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(DownloadUrl))
                {
                    ShowToast("⚠️ No URL to save");
                    return;
                }

                string title = "Unknown";
                string artist = "Unknown";

                // If we don't have track info yet, try to extract it
                if (CurrentTrack == null || string.IsNullOrEmpty(CurrentTrack.Title) || CurrentTrack.Title == "Unknown")
                {
                    ShowToast("🔍 Fetching song info...");
                    
                    try
                    {
                        var trackInfo = await _downloadService?.ExtractMetadata(DownloadUrl.Trim(), SelectedThumbnailSize, !SkipThumbnailDownload);
                        if (trackInfo != null)
                        {
                            title = trackInfo.Title;
                            artist = trackInfo.Artist;
                        }
                    }
                    catch
                    {
                        // If extraction fails, use the URL as title
                        title = DownloadUrl.Trim();
                    }
                }
                else
                {
                    title = CurrentTrack.Title;
                    artist = CurrentTrack.Artist;
                }

                var newLink = new SavedLink
                {
                    Url = DownloadUrl.Trim(),
                    Title = title,
                    Artist = artist,
                    DateAdded = DateTime.Now
                };

                playlistService.AddLink(SavedLinks.ToList(), newLink);
                SavedLinks.Insert(0, newLink);
                ApplyFilter();
                
                ShowToast("✅ Link saved!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] Error saving link: {ex.Message}");
                ShowToast("❌ Failed to save link");
            }
        }

        [RelayCommand]
        private void LoadSavedLink(SavedLink link)
        {
            try
            {
                DownloadUrl = link.Url;
                ShowToast($"📋 Loaded: {link.Title}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] Error loading link: {ex.Message}");
            }
        }

        [RelayCommand]
        private void DeleteSavedLink(string id)
        {
            try
            {
                var link = SavedLinks.FirstOrDefault(l => l.Id == id);
                if (link != null)
                {
                    SavedLinks.Remove(link);
                    playlistService.RemoveLink(SavedLinks.ToList(), id);
                    ApplyFilter();
                    ShowToast("🗑️ Link deleted");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] Error deleting link: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ToggleFavorite(string id)
        {
            try
            {
                var link = SavedLinks.FirstOrDefault(l => l.Id == id);
                if (link != null)
                {
                    link.IsFavorite = !link.IsFavorite;
                    
                    // Save to file
                    var allLinks = SavedLinks.ToList();
                    playlistService.SaveLinks(allLinks);
                    
                    // Refresh the filtered view
                    ApplyFilter();
                    
                    Debug.WriteLine($"[MainWindowViewModel] Toggled favorite for: {link.Title}, IsFavorite: {link.IsFavorite}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] Error toggling favorite: {ex.Message}");
            }
        }

        [RelayCommand]
        private void FilterLinks(string filter)
        {
            SelectedFilter = filter;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            FilteredLinks.Clear();
            
            IEnumerable<SavedLink> filtered = SavedLinks;
            
            if (SelectedFilter == "Favorites")
            {
                filtered = SavedLinks.Where(l => l.IsFavorite);
            }
            else if (SelectedFilter == "Recent")
            {
                filtered = SavedLinks.OrderByDescending(l => l.DateAdded).Take(10);
            }
            
            foreach (var link in filtered)
            {
                FilteredLinks.Add(link);
            }
        }

        // Download History Methods
        private void LoadDownloadHistory()
        {
            try
            {
                var history = historyService.LoadHistory();
                DownloadHistory.Clear();
                foreach (var item in history)
                {
                    DownloadHistory.Add(item);
                }
                ApplyHistoryFilter();
                Debug.WriteLine($"[MainWindowViewModel] Loaded {history.Count} history items");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] Error loading history: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ToggleHistoryPanel()
        {
            IsHistoryPanelOpen = !IsHistoryPanelOpen;
            if (IsSidePanelOpen && IsHistoryPanelOpen)
            {
                IsSidePanelOpen = false; // Close saved links if history opens
            }
        }

        partial void OnHistorySearchTextChanged(string value)
        {
            ApplyHistoryFilter();
        }

        private void ApplyHistoryFilter()
        {
            FilteredHistory.Clear();
            
            var filtered = DownloadHistory.AsEnumerable();
            
            if (!string.IsNullOrWhiteSpace(HistorySearchText))
            {
                var search = HistorySearchText.ToLower();
                filtered = filtered.Where(h => 
                    h.Title.ToLower().Contains(search) || 
                    h.Artist.ToLower().Contains(search) ||
                    h.Album.ToLower().Contains(search));
            }
            
            foreach (var item in filtered)
            {
                FilteredHistory.Add(item);
            }
        }

        public void AddToDownloadHistory(TrackInfo track, string filePath)
        {
            try
            {
                if (track == null || string.IsNullOrEmpty(filePath))
                {
                    Debug.WriteLine("[MainWindowViewModel] Cannot add to history - track or filePath is null");
                    return;
                }

                long fileSize = 0;
                if (File.Exists(filePath))
                {
                    fileSize = new FileInfo(filePath).Length;
                }

                var historyItem = new DownloadHistoryItem
                {
                    Title = track.Title,
                    Artist = track.Artist,
                    Album = track.Album ?? "",
                    Url = track.Url,
                    FilePath = filePath,
                    DownloadDate = DateTime.Now,
                    FileSize = fileSize,
                    Duration = track.Duration
                };

                historyService.AddToHistory(DownloadHistory.ToList(), historyItem);
                DownloadHistory.Insert(0, historyItem);
                ApplyHistoryFilter();
                
                Debug.WriteLine($"[MainWindowViewModel] Added to history: {track.Title} by {track.Artist}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] Error adding to history: {ex.Message}");
            }
        }

        // Call this method after a successful download:
        // Example: OnDownloadComplete(CurrentTrack, downloadedFilePath);
        public void OnDownloadComplete(TrackInfo? track, string filePath)
        {
            if (track != null && !string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                AddToDownloadHistory(track, filePath);
                Debug.WriteLine($"[MainWindowViewModel] Download complete and added to history: {filePath}");
            }
        }

        [RelayCommand]
        private void DeleteHistoryItem(string id)
        {
            try
            {
                var item = DownloadHistory.FirstOrDefault(h => h.Id == id);
                if (item != null)
                {
                    DownloadHistory.Remove(item);
                    FilteredHistory.Remove(item);
                    historyService.SaveHistory(DownloadHistory.ToList());
                    ShowToast("🗑️ History item deleted");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] Error deleting history item: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ClearHistory()
        {
            try
            {
                DownloadHistory.Clear();
                FilteredHistory.Clear();
                historyService.SaveHistory(DownloadHistory.ToList());
                ShowToast("🗑️ History cleared");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] Error clearing history: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenHistoryFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    ShowToast("❌ File not found");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] Error opening file: {ex.Message}");
                ShowToast("❌ Could not open file");
            }
        }

        [RelayCommand]
        private void ToggleResizeMode()
        {
            IsResizeMode = !IsResizeMode;
            ShowToast(IsResizeMode ? "🎨 Resize Mode: Drag and drop files/folders" : "✅ Download Mode: Active");
        }

        private void SetupResizedFolder()
        {
            try
            {
                ResizedFolderPath = Path.Combine(DownloadFolder, "Resized");
                if (!Directory.Exists(ResizedFolderPath))
                {
                    Directory.CreateDirectory(ResizedFolderPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] Error setting up resized folder: {ex.Message}");
                ResizedFolderPath = DownloadFolder;
            }
        }

        public async Task ProcessDroppedFilesAsync(List<string> paths)
        {
            try
            {
                var musicFiles = new List<string>();

                foreach (var path in paths)
                {
                    if (Directory.Exists(path))
                    {
                        // It's a folder - get all music files
                        var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                            .Where(f => IsMusicFile(f))
                            .ToList();
                        musicFiles.AddRange(files);
                    }
                    else if (File.Exists(path) && IsMusicFile(path))
                    {
                        // It's a music file
                        musicFiles.Add(path);
                    }
                }

                if (musicFiles.Count == 0)
                {
                    ShowToast("❌ No music files found");
                    return;
                }

                // Add files to queue
                foreach (var file in musicFiles)
                {
                    if (!QueuedFiles.Contains(file))
                    {
                        QueuedFiles.Add(file);
                    }
                }

                ShowToast($"✅ Added {musicFiles.Count} file(s) to queue");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] Error in ProcessDroppedFilesAsync: {ex.Message}");
                ShowToast("❌ Error processing files");
            }
        }

        private bool IsMusicFile(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext == ".mp3" || ext == ".m4a" || ext == ".flac" || ext == ".wav" || ext == ".ogg" || ext == ".opus";
        }

        private async Task ResizeAlbumArtForFile(string filePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    var fileName = Path.GetFileName(filePath);
                    var sourceDir = Path.GetDirectoryName(filePath);
                    var outputPath = Path.Combine(ResizedFolderPath, fileName);

                    // Check if file is already in the Resized folder
                    if (string.Equals(sourceDir, ResizedFolderPath, StringComparison.OrdinalIgnoreCase))
                    {
                        // File is already in Resized folder, work on it directly
                        outputPath = filePath;
                    }
                    else
                    {
                        // Copy the file to the resized folder
                        try
                        {
                            File.Copy(filePath, outputPath, true);
                        }
                        catch (IOException ex)
                        {
                            Debug.WriteLine($"[MainWindowViewModel] File copy error: {ex.Message}");
                            // If copy fails, try working on original file
                            outputPath = filePath;
                        }
                    }

                    // Extract and resize album art using TagLib
                    var file = TagLib.File.Create(outputPath);
                    
                    // Calculate new dimensions based on AlbumArtSize from settings
                    int newSize = (int)AlbumArtSize;
                    
                    if (file.Tag.Pictures != null && file.Tag.Pictures.Length > 0)
                    {
                        var picture = file.Tag.Pictures[0];
                        
                        // Resize the image
                        using (var ms = new MemoryStream(picture.Data.Data))
                        {
                            var bitmap = new Bitmap(ms);
                            
                            // Create scaled bitmap
                            var resized = bitmap.CreateScaledBitmap(new Avalonia.PixelSize(newSize, newSize), Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality);
                            
                            // Save resized image back to file
                            using (var outputMs = new MemoryStream())
                            {
                                resized.Save(outputMs);
                                
                                var newPicture = new TagLib.Picture(new TagLib.ByteVector(outputMs.ToArray()))
                                {
                                    Type = picture.Type,
                                    MimeType = picture.MimeType,
                                    Description = picture.Description
                                };
                                
                                file.Tag.Pictures = new TagLib.IPicture[] { newPicture };
                                file.Save();
                            }
                        }
                    }

                    Debug.WriteLine($"[MainWindowViewModel] Resized album art to {newSize}x{newSize} for: {fileName}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MainWindowViewModel] Error processing file: {ex.Message}");
                    throw;
                }
            });
        }
        public void ReloadSettings()
        {
            LoadUserSettings();

            // Update download service with new settings
            if (_downloadService != null)
            {
                _downloadService.UpdateAlbumArtSetting(EnableAlbumArtDownload);
                _downloadService.UpdateThumbnailSettings(SkipThumbnailDownload);
                _downloadService.UpdateThumbnailSize(SelectedThumbnailSize);
                _downloadService.UpdateAudioFormat(SelectedAudioFormat);
                _downloadService.UpdateBitrate(SelectedBitrate);
            }

            Debug.WriteLine("[MainWindowViewModel] Settings reloaded from storage");
        }
    }
}
