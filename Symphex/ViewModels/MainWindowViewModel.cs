using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using CliWrap;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Input;
using System.Globalization; // If needed for formatting

namespace Symphex.ViewModels
{
    public partial class TrackInfo : ObservableObject
    {
        [ObservableProperty]
        private string title = "";

        [ObservableProperty]
        private string artist = "";

        [ObservableProperty]
        private string album = "";

        [ObservableProperty]
        private string duration = "";

        [ObservableProperty]
        private Bitmap? thumbnail;

        [ObservableProperty]
        private string fileName = "";

        [ObservableProperty]
        private string url = "";

        [ObservableProperty]
        private string uploader = "";

        [ObservableProperty]
        private string uploadDate = "";

        [ObservableProperty]
        private long viewCount = 0;

        [ObservableProperty]
        private Bitmap? albumArt;

        [ObservableProperty]
        private bool hasRealAlbumArt = false;
    }

    public partial class MainWindowViewModel : ViewModelBase
    {
        private ScrollViewer? _cliScrollViewer;
        private WaveOutEvent? _waveOut;
        private AudioFileReader? _audioFileReader;
        private readonly Timer _playbackTimer;
        private List<string> _playlist = new List<string>();
        private int _currentTrackIndex = -1;
        private bool _isSeeking;

        public ScrollViewer? CliScrollViewer
        {
            get => _cliScrollViewer;
            set => SetProperty(ref _cliScrollViewer, value);
        }

        [ObservableProperty]
        private string downloadUrl = "";

        [ObservableProperty]
        private string statusText = "🎵 Initializing Symphex...";

        [ObservableProperty]
        private string cliOutput = "Symphex Music Downloader v1.0\n" +
                                   "=============================\n" +
                                   "Starting automatic dependency check...\n\n";

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
        private bool isPlaying = false;

        [ObservableProperty]
        private double progressPercentage = 0;

        [ObservableProperty]
        private TimeSpan currentTime = TimeSpan.Zero;

        [ObservableProperty]
        private float volume = 0.75f;

        [ObservableProperty]
        private bool isPlayingTrack = false;

        private string YtDlpPath { get; set; } = "";
        private string FmpegPath { get; set; } = "";
        private string YtDlpExecutableName => GetYtDlpExecutableName();
        private string FmpegExecutableName => GetFmpegExecutableName();
        private readonly HttpClient httpClient = new();

        public MainWindowViewModel()
        {
            CurrentTrack = new TrackInfo();
            SetupDownloadFolder();
            LoadPlaylist();

            if (!Directory.Exists(DownloadFolder))
            {
                Directory.CreateDirectory(DownloadFolder);
            }

            _playbackTimer = new Timer(500);
            _playbackTimer.Elapsed += UpdatePlaybackProgress;
            _playbackTimer.AutoReset = true;

            _ = Task.Run(async () =>
            {
                await AutoSetupDependencies();
            });
        }

        private void LoadPlaylist()
        {
            try
            {
                _playlist = Directory.GetFiles(DownloadFolder, "*.mp3").ToList();
                if (_playlist.Any())
                {
                    LogToCli($"Loaded {_playlist.Count} tracks from download folder");
                }
                else
                {
                    LogToCli("No tracks found in download folder");
                }
            }
            catch (Exception ex)
            {
                LogToCli($"Error loading playlist: {ex.Message}");
            }
        }

        private void InitializeAudio(string filePath)
        {
            try
            {
                StopAudio();
                _waveOut = new WaveOutEvent();
                _audioFileReader = new AudioFileReader(filePath);
                _waveOut.Init(_audioFileReader);
                _waveOut.Volume = Volume;
                _playbackTimer.Start();
                LogToCli($"Initialized audio: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                LogToCli($"Error initializing audio: {ex.Message}");
                StopAudio();
            }
        }

        private void StopAudio()
        {
            _playbackTimer.Stop();
            if (_audioFileReader != null)
            {
                _audioFileReader.Dispose();
                _audioFileReader = null;
            }
            if (_waveOut != null)
            {
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }
            IsPlaying = false;
            ProgressPercentage = 0;
            CurrentTime = TimeSpan.Zero;
        }

        private void UpdatePlaybackProgress(object? sender, ElapsedEventArgs e)
        {
            if (_audioFileReader != null && !_isSeeking)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    CurrentTime = _audioFileReader.CurrentTime;
                    ProgressPercentage = (_audioFileReader.CurrentTime.TotalSeconds / _audioFileReader.TotalTime.TotalSeconds) * 100;
                    if (_audioFileReader.CurrentTime >= _audioFileReader.TotalTime)
                    {
                        SkipForward();
                    }
                });
            }
        }

        [RelayCommand]
        private void PlayPause()
        {
            if (_currentTrackIndex < 0 || !_playlist.Any())
            {
                if (_playlist.Any())
                {
                    _currentTrackIndex = 0;
                    LoadTrack(_playlist[_currentTrackIndex]);
                    PlayTrack();
                }
                else
                {
                    LogToCli("No tracks available to play");
                    StatusText = "⚠️ No tracks available";
                }
                return;
            }

            if (IsPlaying)
            {
                _waveOut?.Pause();
                IsPlaying = false;
                _playbackTimer.Stop();
                LogToCli("Paused playback");
            }
            else
            {
                PlayTrack();
            }
        }

        private void PlayTrack()
        {
            try
            {
                if (_waveOut == null && _currentTrackIndex >= 0)
                {
                    InitializeAudio(_playlist[_currentTrackIndex]);
                }
                _waveOut?.Play();
                IsPlaying = true;
                IsPlayingTrack = true;
                _playbackTimer.Start();
                LogToCli($"Playing: {Path.GetFileName(_playlist[_currentTrackIndex])}");
                UpdateCurrentTrackInfo();
            }
            catch (Exception ex)
            {
                LogToCli($"Error playing track: {ex.Message}");
                StopAudio();
                StatusText = "❌ Error playing track";
            }
        }

        [RelayCommand]
        private void SkipForward()
        {
            if (_playlist.Any())
            {
                StopAudio();
                _currentTrackIndex = (_currentTrackIndex + 1) % _playlist.Count;
                LoadTrack(_playlist[_currentTrackIndex]);
                PlayTrack();
            }
        }

        [RelayCommand]
        private void SkipBack()
        {
            if (_playlist.Any())
            {
                StopAudio();
                _currentTrackIndex = (_currentTrackIndex - 1) < 0 ? _playlist.Count - 1 : _currentTrackIndex - 1;
                LoadTrack(_playlist[_currentTrackIndex]);
                PlayTrack();
            }
        }

        private void LoadTrack(string filePath)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                CurrentTrack = new TrackInfo
                {
                    FileName = fileName,
                    Title = Path.GetFileNameWithoutExtension(fileName),
                    Artist = "Unknown",
                    Duration = GetTrackDuration(filePath)
                };
                LogToCli($"Loaded track: {fileName}");
            }
            catch (Exception ex)
            {
                LogToCli($"Error loading track: {ex.Message}");
            }
        }

        private string GetTrackDuration(string filePath)
        {
            try
            {
                using var reader = new AudioFileReader(filePath);
                return reader.TotalTime.ToString(@"mm\:ss");
            }
            catch
            {
                return "Unknown";
            }
        }

        private void UpdateCurrentTrackInfo()
        {
            if (_currentTrackIndex >= 0 && _currentTrackIndex < _playlist.Count)
            {
                var filePath = _playlist[_currentTrackIndex];
                try
                {
                    using var reader = new AudioFileReader(filePath);
                    CurrentTrack.Duration = reader.TotalTime.ToString(@"mm\:ss");
                    var tags = TagLib.File.Create(filePath);
                    CurrentTrack.Title = tags.Tag.Title ?? Path.GetFileNameWithoutExtension(filePath);
                    CurrentTrack.Artist = tags.Tag.FirstPerformer ?? "Unknown";
                    CurrentTrack.Album = tags.Tag.Album ?? "";
                    CurrentTrack.AlbumArt = LoadAlbumArt(filePath);
                }
                catch
                {
                    CurrentTrack.Title = Path.GetFileNameWithoutExtension(filePath);
                    CurrentTrack.Artist = "Unknown";
                    CurrentTrack.Album = "";
                    CurrentTrack.AlbumArt = null;
                }
            }
        }

        private Bitmap? LoadAlbumArt(string filePath)
        {
            try
            {
                var tags = TagLib.File.Create(filePath);
                var picture = tags.Tag.Pictures.FirstOrDefault();
                if (picture != null)
                {
                    using var stream = new MemoryStream(picture.Data.Data);
                    return new Bitmap(stream);
                }
            }
            catch
            {
                return null;
            }
            return null;
        }

        private async Task AutoSetupDependencies()
        {
            try
            {
                LogToCli("Starting automatic dependency check...");

                await SetupPortableYtDlp();
                bool ytDlpAvailable = !string.IsNullOrEmpty(YtDlpPath) &&
                    (File.Exists(YtDlpPath) || await IsExecutableInPath(YtDlpExecutableName));

                if (!ytDlpAvailable)
                {
                    LogToCli("yt-dlp not found. Downloading automatically...");
                    await AutoDownloadYtDlp();
                }

                await SetupPortableFmpeg();
                bool ffmpegAvailable = !string.IsNullOrEmpty(FmpegPath) &&
                    (File.Exists(FmpegPath) || await IsExecutableInPath(FmpegExecutableName));

                if (!ffmpegAvailable)
                {
                    LogToCli("FFmpeg not found. Downloading automatically...");
                    await AutoDownloadFmpeg();
                }

                await UpdateFinalStatus();
            }
            catch (Exception ex)
            {
                LogToCli($"Error during automatic dependency setup: {ex.Message}");
            }
        }

        private async Task AutoDownloadYtDlp()
        {
            try
            {
                string os = GetCurrentOS();
                LogToCli($"Auto-downloading yt-dlp for {os}...");

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    StatusText = $"⬇️ Auto-downloading yt-dlp for {os}...";
                    IsDownloading = true;
                    DownloadProgress = 0;
                });

                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string toolsDir = Path.Combine(appDirectory, "tools");

                if (!Directory.Exists(toolsDir))
                {
                    Directory.CreateDirectory(toolsDir);
                }

                string ytDlpPath = Path.Combine(toolsDir, YtDlpExecutableName);
                string downloadUrl = GetYtDlpDownloadUrl();

                LogToCli($"Download URL: {downloadUrl}");
                LogToCli($"Saving to: {ytDlpPath}");

                using (var localHttpClient = new HttpClient())
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => DownloadProgress = 30);
                    var response = await localHttpClient.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => DownloadProgress = 60);
                    await File.WriteAllBytesAsync(ytDlpPath, await response.Content.ReadAsByteArrayAsync());

                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        MakeExecutable(ytDlpPath);
                        LogToCli("Made executable for Unix system");
                    }

                    Avalonia.Threading.Dispatcher.UIThread.Post(() => DownloadProgress = 100);
                    LogToCli("✅ yt-dlp downloaded successfully");
                    YtDlpPath = ytDlpPath;
                }
            }
            catch (Exception ex)
            {
                LogToCli($"ERROR auto-downloading yt-dlp: {ex.Message}");
            }
            finally
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    IsDownloading = false;
                    DownloadProgress = 0;
                });
            }
        }

        private async Task AutoDownloadFmpeg()
        {
            try
            {
                string os = GetCurrentOS();
                string downloadUrl = GetFmpegDownloadUrl();

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    LogToCli($"FFmpeg auto-download not available for {os}");
                    LogToCli("Linux users should install FFmpeg via package manager (apt install ffmpeg)");
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = "ℹ️ Linux detected - please install FFmpeg via package manager";
                    });
                    return;
                }

                LogToCli($"Auto-downloading FFmpeg for {os}...");
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    StatusText = $"⬇️ Auto-downloading FFmpeg for {os}...";
                    IsDownloading = true;
                    DownloadProgress = 0;
                });

                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string toolsDir = Path.Combine(appDirectory, "tools");

                if (!Directory.Exists(toolsDir))
                {
                    Directory.CreateDirectory(toolsDir);
                }

                LogToCli($"Download URL: {downloadUrl}");
                Avalonia.Threading.Dispatcher.UIThread.Post(() => DownloadProgress = 10);

                using (var localHttpClient = new HttpClient())
                {
                    localHttpClient.Timeout = TimeSpan.FromMinutes(10);
                    var response = await localHttpClient.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => DownloadProgress = 40);
                    var zipBytes = await response.Content.ReadAsByteArrayAsync();
                    string zipPath = Path.Combine(toolsDir, "ffmpeg.zip");
                    await File.WriteAllBytesAsync(zipPath, zipBytes);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => DownloadProgress = 60);

                    LogToCli("Extracting FFmpeg...");
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        await ExtractFmpegWindows(zipPath, toolsDir);
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        await ExtractFmpegMacOS(zipPath, toolsDir);
                    }

                    File.Delete(zipPath);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => DownloadProgress = 90);
                    await SetupPortableFmpeg();
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => DownloadProgress = 100);
                    LogToCli("✅ FFmpeg downloaded and extracted successfully");
                }
            }
            catch (Exception ex)
            {
                LogToCli($"ERROR auto-downloading FFmpeg: {ex.Message}");
                LogToCli("You can manually download FFmpeg from https://ffmpeg.org/download.html");
                LogToCli("Extract and place the executable in the 'tools' folder");
            }
            finally
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    IsDownloading = false;
                    DownloadProgress = 0;
                });
            }
        }

        private async Task UpdateFinalStatus()
        {
            await SetupPortableYtDlp();
            await SetupPortableFmpeg();

            bool ytDlpFound = !string.IsNullOrEmpty(YtDlpPath) &&
                (File.Exists(YtDlpPath) || await IsExecutableInPath(YtDlpExecutableName));
            bool ffmpegFound = !string.IsNullOrEmpty(FmpegPath) &&
                (File.Exists(FmpegPath) || await IsExecutableInPath(FmpegExecutableName));

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (ytDlpFound && ffmpegFound)
                {
                    StatusText = "✅ All dependencies ready! You can now download music with full metadata support.";
                    LogToCli("🎉 Setup complete! All dependencies are available.");
                }
                else if (ytDlpFound && !ffmpegFound)
                {
                    StatusText = "⚠️ yt-dlp ready, FFmpeg missing. Downloads will work but metadata may be limited.";
                    LogToCli("yt-dlp is ready but FFmpeg is missing. Metadata features will be limited.");
                }
                else if (!ytDlpFound && ffmpegFound)
                {
                    StatusText = "⚠️ FFmpeg ready, yt-dlp missing. Cannot download without yt-dlp.";
                    LogToCli("FFmpeg is ready but yt-dlp is missing. Downloads are not possible.");
                }
                else
                {
                    StatusText = "❌ Setup incomplete. Please check your internet connection and restart the app.";
                    LogToCli("❌ Automatic setup failed. You may need to manually install dependencies.");
                }
            });
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

        private string GetYtDlpExecutableName()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "yt-dlp.exe" : "yt-dlp";
        }

        private string GetFmpegExecutableName()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
        }

        private string GetYtDlpDownloadUrl()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_macos";
            else
                return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux";
        }

        private string GetFmpegDownloadUrl()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "https://evermeet.cx/ffmpeg/getrelease/zip";
            else
                return "";
        }

        private void LogToCli(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            string cleanMessage = message.Trim();
            string logEntry = $"[{timestamp}] {cleanMessage}\n";

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    CliOutput += logEntry;
                    ScrollToBottom();
                });
            }
            else
            {
                CliOutput += logEntry;
                ScrollToBottom();
            }
        }

        private void ScrollToBottom()
        {
            if (CliScrollViewer != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    CliScrollViewer.ScrollToEnd();
                }, Avalonia.Threading.DispatcherPriority.Background);
            }
        }

        private async Task SetupPortableYtDlp()
        {
            try
            {
                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string[] possiblePaths = {
                    Path.Combine(appDirectory, "tools", YtDlpExecutableName),
                    Path.Combine(appDirectory, YtDlpExecutableName)
                };

                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        YtDlpPath = path;
                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            MakeExecutable(path);
                        }
                        LogToCli($"yt-dlp found at: {path}");
                        return;
                    }
                }

                if (await IsExecutableInPath(YtDlpExecutableName))
                {
                    YtDlpPath = YtDlpExecutableName;
                    LogToCli($"yt-dlp found in system PATH");
                    return;
                }

                string os = GetCurrentOS();
                LogToCli($"yt-dlp not found for {os}");
                YtDlpPath = "";
            }
            catch (Exception ex)
            {
                LogToCli($"ERROR setting up yt-dlp: {ex.Message}");
                YtDlpPath = "";
            }
        }

        private async Task<bool> IsExecutableInPath(string executableName)
        {
            try
            {
                var result = await Cli.Wrap(executableName)
                    .WithArguments("--version")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();
                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task SetupPortableFmpeg()
        {
            try
            {
                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string[] possiblePaths = {
                    Path.Combine(appDirectory, "tools", FmpegExecutableName),
                    Path.Combine(appDirectory, "tools", "ffmpeg", "bin", FmpegExecutableName),
                    Path.Combine(appDirectory, "tools", "bin", FmpegExecutableName),
                    Path.Combine(appDirectory, FmpegExecutableName)
                };

                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        FmpegPath = path;
                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            MakeExecutable(path);
                        }
                        LogToCli($"FFmpeg found at: {path}");
                        return;
                    }
                }

                if (await IsExecutableInPath(FmpegExecutableName))
                {
                    FmpegPath = FmpegExecutableName;
                    LogToCli($"FFmpeg found in system PATH");
                    return;
                }

                string os = GetCurrentOS();
                LogToCli($"FFmpeg not found for {os}. Metadata embedding may not work properly.");
                FmpegPath = "";
            }
            catch (Exception ex)
            {
                LogToCli($"ERROR setting up FFmpeg: {ex.Message}");
                FmpegPath = "";
            }
        }

        private string GetCurrentOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Windows";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "macOS";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "Linux";
            else
                return "Unknown OS";
        }

        private void MakeExecutable(string filePath)
        {
            try
            {
                var result = Process.Start(new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{filePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                result?.WaitForExit();
            }
            catch { }
        }

        private async Task<TrackInfo?> ExtractMetadata(string url)
        {
            try
            {
                LogToCli("Extracting metadata...");
                bool isUrl = url.StartsWith("http://") || url.StartsWith("https://");
                string searchPrefix = isUrl ? "" : "ytsearch1:";
                string fullUrl = $"{searchPrefix}{url}";

                var output = new StringBuilder();
                var args = $"\"{fullUrl}\" --dump-json --no-playlist";

                var result = await Cli.Wrap(YtDlpPath)
                    .WithArguments(args)
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();

                if (result.ExitCode != 0)
                {
                    LogToCli("Failed to extract metadata");
                    return null;
                }

                var jsonOutput = output.ToString().Trim();
                var lines = jsonOutput.Split('\n');
                var jsonLine = lines.FirstOrDefault(line => line.StartsWith("{"));

                if (string.IsNullOrEmpty(jsonLine))
                {
                    LogToCli("No valid JSON found in metadata output");
                    return null;
                }

                using var doc = JsonDocument.Parse(jsonLine);
                var root = doc.RootElement;

                string rawTitle = root.TryGetProperty("title", out var title) ? title.GetString() ?? "Unknown" : "Unknown";
                string rawUploader = root.TryGetProperty("uploader", out var uploader) ? uploader.GetString() ?? "Unknown" : "Unknown";

                LogToCli($"Raw title: {rawTitle}");
                LogToCli($"Raw uploader: {rawUploader}");

                string finalArtist = "Unknown";
                string finalTitle = rawTitle;

                if (rawTitle.Contains(" - "))
                {
                    var parts = rawTitle.Split(new[] { " - " }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        string potentialArtist = parts[0].Trim();
                        string potentialSong = parts[1].Trim();
                        finalArtist = CleanArtistName(potentialArtist);
                        finalTitle = CleanSongTitle(potentialSong);
                        LogToCli($"Parsed from title: Song='{finalTitle}' by Artist='{finalArtist}'");
                    }
                }
                else
                {
                    finalArtist = CleanArtistName(rawUploader);
                    finalTitle = CleanSongTitle(rawTitle);
                }

                string albumInfo = "";
                if (root.TryGetProperty("album", out var album))
                {
                    albumInfo = album.GetString() ?? "";
                }

                var trackInfo = new TrackInfo
                {
                    Title = finalTitle,
                    Artist = finalArtist,
                    Album = albumInfo,
                    Duration = FormatDuration(root.TryGetProperty("duration", out var duration) ? duration.GetDouble() : 0),
                    Url = root.TryGetProperty("webpage_url", out var webUrl) ? webUrl.GetString() ?? url : url,
                    Uploader = rawUploader,
                    UploadDate = root.TryGetProperty("upload_date", out var uploadDate) ? FormatUploadDate(uploadDate.GetString()) : "",
                    ViewCount = root.TryGetProperty("view_count", out var viewCount) ? viewCount.GetInt64() : 0
                };

                string? thumbnailUrl = GetBestThumbnailUrl(root);
                if (!string.IsNullOrEmpty(thumbnailUrl))
                {
                    trackInfo.Thumbnail = await LoadImageAsync(thumbnailUrl);
                }

                LogToCli($"Final metadata: '{trackInfo.Title}' by '{trackInfo.Artist}'");
                await FindRealAlbumArt(trackInfo);
                return trackInfo;
            }
            catch (Exception ex)
            {
                LogToCli($"Error extracting metadata: {ex.Message}");
                return null;
            }
        }

        private string CleanSongTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return "Unknown";

            string cleaned = title;
            var patterns = new[]
            {
                @"\s*\(Official Video\)",
                @"\s*\(Official Audio\)",
                @"\s*\(Official Music Video\)",
                @"\s*\(Official\)",
                @"\s*\(Lyrics\)",
                @"\s*\(Lyric Video\)",
                @"\s*\(HD\)",
                @"\s*\(4K\)",
                @"\s*\[Official Video\]",
                @"\s*\[Official Audio\]",
                @"\s*\[Official Music Video\]",
                @"\s*\[Lyrics\]",
                @"\s*\[HD\]"
            };

            foreach (var pattern in patterns)
            {
                cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, pattern, "",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            cleaned = cleaned
                .Replace("\"", "")
                .Replace("'", "")
                .Replace("\u201C", "")
                .Replace("\u201D", "")
                .Replace("\u2018", "")
                .Replace("\u2019", "")
                .Replace("_", " ")
                .Replace("  ", " ")
                .Trim();

            return cleaned;
        }

        private string CleanArtistName(string artist)
        {
            if (string.IsNullOrEmpty(artist))
                return "Unknown";

            string cleaned = artist;
            cleaned = cleaned
                .Replace(" - Topic", "", StringComparison.OrdinalIgnoreCase)
                .Replace("VEVO", "", StringComparison.OrdinalIgnoreCase)
                .Replace(" Records", "", StringComparison.OrdinalIgnoreCase)
                .Replace(" Music", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Official", "", StringComparison.OrdinalIgnoreCase)
                .Replace("\"", "")
                .Replace("'", "")
                .Replace("\u201C", "")
                .Replace("\u201D", "")
                .Replace("\u2018", "")
                .Replace("\u2019", "")
                .Replace("_", " ")
                .Replace("  ", " ")
                .Trim();

            return cleaned;
        }

        private string GetBestThumbnailUrl(JsonElement root)
        {
            if (root.TryGetProperty("thumbnail", out var thumbnailProp))
            {
                return thumbnailProp.GetString() ?? "";
            }

            if (root.TryGetProperty("thumbnails", out var thumbnailsProp))
            {
                var thumbnails = thumbnailsProp.EnumerateArray().ToList();
                if (thumbnails.Count > 0)
                {
                    var bestThumbnail = thumbnails.LastOrDefault();
                    if (bestThumbnail.TryGetProperty("url", out var urlProp))
                    {
                        return urlProp.GetString() ?? "";
                    }
                }
            }
            return "";
        }

        private async Task FindRealAlbumArt(TrackInfo trackInfo)
        {
            try
            {
                LogToCli("Searching for album artwork...");
                var albumArt = await SearchITunesAlbumArt(trackInfo.Title, trackInfo.Artist);

                if (albumArt == null)
                {
                    LogToCli("iTunes search failed, trying Deezer...");
                    albumArt = await SearchDeezerAlbumArt(trackInfo.Title, trackInfo.Artist);
                }

                if (albumArt == null && trackInfo.Title.Contains(" - "))
                {
                    LogToCli("Trying alternative search without separators...");
                    string altTitle = trackInfo.Title.Replace(" - ", " ");
                    albumArt = await SearchITunesAlbumArt(altTitle, trackInfo.Artist);

                    if (albumArt == null)
                    {
                        albumArt = await SearchDeezerAlbumArt(altTitle, trackInfo.Artist);
                    }
                }

                if (albumArt == null)
                {
                    LogToCli("Trying artist-focused search...");
                    albumArt = await SearchITunesAlbumArt("", trackInfo.Artist);
                }

                if (albumArt != null)
                {
                    trackInfo.AlbumArt = albumArt;
                    trackInfo.HasRealAlbumArt = true;
                    LogToCli("✅ Real album artwork found!");
                }
                else
                {
                    LogToCli("⚠️ No album artwork found in databases, using video thumbnail");
                    trackInfo.AlbumArt = trackInfo.Thumbnail;
                    trackInfo.HasRealAlbumArt = false;
                }
            }
            catch (Exception ex)
            {
                LogToCli($"Error finding album art: {ex.Message}");
                trackInfo.AlbumArt = trackInfo.Thumbnail;
                trackInfo.HasRealAlbumArt = false;
            }
        }

        private async Task ApplyProperMetadata()
        {
            string? tempOutput = null;
            try
            {
                if (CurrentTrack == null || string.IsNullOrEmpty(CurrentTrack.FileName) || string.IsNullOrEmpty(FmpegPath))
                {
                    LogToCli("Skipping metadata application - missing requirements");
                    return;
                }

                string audioFilePath = Path.Combine(DownloadFolder, CurrentTrack.FileName);
                if (!File.Exists(audioFilePath))
                {
                    LogToCli($"Audio file not found: {audioFilePath}");
                    return;
                }

                LogToCli("Applying proper metadata and artwork...");
                tempOutput = Path.Combine(DownloadFolder, $"temp_{Guid.NewGuid():N}.mp3");
                var argsList = new List<string>
                {
                    "-i", audioFilePath
                };

                Bitmap? artworkToUse = CurrentTrack.AlbumArt ?? CurrentTrack.Thumbnail;
                string artworkSource = CurrentTrack.HasRealAlbumArt ? "album art" : "thumbnail";
                LogToCli($"Using {artworkSource} for metadata");

                string? tempArtwork = null;
                if (artworkToUse != null)
                {
                    tempArtwork = Path.Combine(Path.GetTempPath(), $"temp_artwork_{Guid.NewGuid():N}.jpg");
                    try
                    {
                        using (var fileStream = new FileStream(tempArtwork, FileMode.Create))
                        {
                            artworkToUse.Save(fileStream);
                        }
                        LogToCli($"Saved {artworkSource} to: {tempArtwork}");
                        argsList.AddRange(new[] { "-i", tempArtwork, "-map", "0:a", "-map", "1:0", "-c:a", "copy", "-c:v", "mjpeg" });
                    }
                    catch (Exception ex)
                    {
                        LogToCli($"Error saving artwork: {ex.Message}");
                    }
                }

                argsList.AddRange(new[]
                {
                    "-metadata", $"title={CurrentTrack.Title}",
                    "-metadata", $"artist={CurrentTrack.Artist}",
                    "-metadata", $"album={CurrentTrack.Album}",
                    "-y", tempOutput
                });

                var args = string.Join(" ", argsList.Select(arg => arg.Contains(" ") ? $"\"{arg}\"" : arg));
                var output = new StringBuilder();
                var errorOutput = new StringBuilder();

                var result = await Cli.Wrap(FmpegPath)
                    .WithArguments(args)
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(errorOutput))
                    .ExecuteAsync();

                if (result.ExitCode == 0)
                {
                    LogToCli("Metadata applied successfully");
                    File.Delete(audioFilePath);
                    File.Move(tempOutput, audioFilePath);
                    LogToCli($"Updated file: {audioFilePath}");
                    LoadPlaylist();
                }
                else
                {
                    LogToCli($"FFmpeg error: {errorOutput}");
                    if (File.Exists(tempOutput))
                    {
                        File.Delete(tempOutput);
                    }
                }

                if (tempArtwork != null && File.Exists(tempArtwork))
                {
                    File.Delete(tempArtwork);
                }
            }
            catch (Exception ex)
            {
                LogToCli($"Error applying metadata: {ex.Message}");
                if (tempOutput != null && File.Exists(tempOutput))
                {
                    File.Delete(tempOutput);
                }
            }
        }

        private async Task<Bitmap?> SearchITunesAlbumArt(string title, string artist)
        {
            try
            {
                string query = $"{artist} {title}".Trim().Replace(" ", "+");
                string url = $"https://itunes.apple.com/search?term={query}&entity=song&limit=1";
                var response = await httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
                var results = doc.RootElement.GetProperty("results").EnumerateArray();

                if (results.Any())
                {
                    var result = results.First();
                    if (result.TryGetProperty("artworkUrl100", out var artworkUrl))
                    {
                        string highResUrl = artworkUrl.GetString()?.Replace("100x100bb", "600x600bb") ?? "";
                        using var stream = await httpClient.GetStreamAsync(highResUrl);
                        return new Bitmap(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToCli($"iTunes search error: {ex.Message}");
            }
            return null;
        }

        private async Task<Bitmap?> SearchDeezerAlbumArt(string title, string artist)
        {
            try
            {
                string query = $"{artist} {title}".Trim().Replace(" ", "+");
                string url = $"https://api.deezer.com/search?q={query}&limit=1";
                var response = await httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
                var data = doc.RootElement.GetProperty("data").EnumerateArray();

                if (data.Any())
                {
                    var result = data.First();
                    if (result.TryGetProperty("album", out var album) && album.TryGetProperty("cover_big", out var coverUrl))
                    {
                        using var stream = await httpClient.GetStreamAsync(coverUrl.GetString());
                        return new Bitmap(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToCli($"Deezer search error: {ex.Message}");
            }
            return null;
        }

        private async Task ExtractFmpegWindows(string zipPath, string toolsDir)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var ffmpegEntry = archive.Entries.FirstOrDefault(e => e.Name == FmpegExecutableName);
                if (ffmpegEntry != null)
                {
                    string ffmpegPath = Path.Combine(toolsDir, FmpegExecutableName);
                    ffmpegEntry.ExtractToFile(ffmpegPath, true);
                    LogToCli($"Extracted FFmpeg to: {ffmpegPath}");
                }
                else
                {
                    LogToCli("FFmpeg executable not found in archive");
                }
            }
            catch (Exception ex)
            {
                LogToCli($"Error extracting FFmpeg for Windows: {ex.Message}");
            }
        }

        private async Task ExtractFmpegMacOS(string zipPath, string toolsDir)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var ffmpegEntry = archive.Entries.FirstOrDefault(e => e.Name == FmpegExecutableName);
                if (ffmpegEntry != null)
                {
                    string ffmpegPath = Path.Combine(toolsDir, FmpegExecutableName);
                    ffmpegEntry.ExtractToFile(ffmpegPath, true);
                    MakeExecutable(ffmpegPath);
                    LogToCli($"Extracted FFmpeg to: {ffmpegPath}");
                }
                else
                {
                    LogToCli("FFmpeg executable not found in archive");
                }
            }
            catch (Exception ex)
            {
                LogToCli($"Error extracting FFmpeg for macOS: {ex.Message}");
            }
        }

        private string FormatDuration(double seconds)
        {
            return TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
        }

        private string FormatUploadDate(string? date)
        {
            if (string.IsNullOrEmpty(date) || date.Length != 8)
                return "";
            try
            {
                return $"{date.Substring(0, 4)}-{date.Substring(4, 2)}-{date.Substring(6, 2)}";
            }
            catch
            {
                return "";
            }
        }

        private async Task<Bitmap?> LoadImageAsync(string url)
        {
            try
            {
                using var stream = await httpClient.GetStreamAsync(url);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }

        [RelayCommand]
        private async Task Download()
        {
            if (string.IsNullOrEmpty(DownloadUrl))
            {
                LogToCli("Please enter a URL or search query");
                StatusText = "⚠️ No URL provided";
                return;
            }

            if (string.IsNullOrEmpty(YtDlpPath))
            {
                LogToCli("yt-dlp is not available. Please ensure dependencies are set up.");
                StatusText = "❌ yt-dlp not found";
                return;
            }

            try
            {
                IsDownloading = true;
                DownloadProgress = 0;
                StatusText = "⬇️ Extracting metadata...";

                CurrentTrack = await ExtractMetadata(DownloadUrl);
                if (CurrentTrack == null)
                {
                    LogToCli("Failed to extract metadata");
                    StatusText = "❌ Failed to extract metadata";
                    IsDownloading = false;
                    return;
                }

                StatusText = "⬇️ Downloading audio...";
                string safeFileName = $"{CurrentTrack.Artist} - {CurrentTrack.Title}".Replace("/", "_").Replace("\\", "_");
                string outputPath = Path.Combine(DownloadFolder, $"{safeFileName}.mp3");

                var args = $"\"{CurrentTrack.Url}\" -x --audio-format mp3 --audio-quality 0 -o \"{outputPath}\"";
                if (!string.IsNullOrEmpty(FmpegPath))
                {
                    args += $" --ffmpeg-location \"{FmpegPath}\"";
                }

                var output = new StringBuilder();
                var errorOutput = new StringBuilder();

                var result = await Cli.Wrap(YtDlpPath)
                    .WithArguments(args)
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(errorOutput))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();

                DownloadProgress = 50;

                if (result.ExitCode == 0)
                {
                    LogToCli($"Downloaded: {safeFileName}.mp3");
                    CurrentTrack.FileName = $"{safeFileName}.mp3";
                    await ApplyProperMetadata();
                    LoadPlaylist();
                    DownloadProgress = 100;
                    StatusText = "✅ Download complete!";
                }
                else
                {
                    LogToCli($"Download failed: {errorOutput}");
                    StatusText = "❌ Download failed";
                }
            }
            catch (Exception ex)
            {
                LogToCli($"Error during download: {ex.Message}");
                StatusText = "❌ Download error";
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
            }
        }

        [RelayCommand]
        private void Clear()
        {
            DownloadUrl = "";
            CurrentTrack = new TrackInfo();
            StatusText = "🎵 Ready to download...";
            LogToCli("Cleared input and preview");
        }

        [RelayCommand]
        private void OpenFolder()
        {
            try
            {
                if (Directory.Exists(DownloadFolder))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = DownloadFolder,
                        UseShellExecute = true
                    });
                    LogToCli("Opened download folder");
                }
                else
                {
                    LogToCli("Download folder does not exist");
                    StatusText = "⚠️ Download folder not found";
                }
            }
            catch (Exception ex)
            {
                LogToCli($"Error opening folder: {ex.Message}");
                StatusText = "❌ Error opening folder";
            }
        }

        [RelayCommand]
        private void ProgressSliderChanged(double value)
        {
            if (_audioFileReader == null || !IsPlayingTrack) return;

            _isSeeking = true;
            ProgressPercentage = value;
            var newPosition = TimeSpan.FromSeconds(value * _audioFileReader.TotalTime.TotalSeconds / 100);
            _audioFileReader.CurrentTime = newPosition;
            CurrentTime = newPosition;
            LogToCli($"Seeked to {CurrentTime:mm\\:ss}");
            _isSeeking = false;
        }

        partial void OnVolumeChanged(float value)
        {
            if (_waveOut != null)
            {
                _waveOut.Volume = value / 100f;
                LogToCli($"Volume set to {value:0}%");
            }
        }
    }
}