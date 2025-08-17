using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading.Tasks;
using CliWrap;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media.Imaging;
using System.Net.Http;

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
    }

    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string downloadUrl = "";

        [ObservableProperty]
        private string statusText = "🎵 Ready to download music...";

        [ObservableProperty]
        private string cliOutput = "Symphex Music Downloader v1.0\n" +
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

        private string YtDlpPath { get; set; } = "";
        private string YtDlpExecutableName => GetYtDlpExecutableName();
        private readonly HttpClient httpClient = new();

        public MainWindowViewModel()
        {
            SetupDownloadFolder();

            if (!Directory.Exists(DownloadFolder))
            {
                Directory.CreateDirectory(DownloadFolder);
            }

            SetupPortableYtDlp();
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

        private string GetYtDlpDownloadUrl()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_macos";
            else
                return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp";
        }

        private void LogToCli(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            CliOutput += $"[{timestamp}] {message}\n";
        }

        private void SetupPortableYtDlp()
        {
            try
            {
                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

                string[] possiblePaths = {
                    Path.Combine(appDirectory, "tools", YtDlpExecutableName),
                    Path.Combine(appDirectory, YtDlpExecutableName),
                    YtDlpExecutableName
                };

                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path) || path == YtDlpExecutableName)
                    {
                        YtDlpPath = path;

                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(path))
                        {
                            MakeExecutable(path);
                        }

                        string location = path == YtDlpExecutableName ? "System PATH" : path;
                        StatusText = $"✅ yt-dlp found at: {location}";
                        LogToCli($"yt-dlp found at: {location}");
                        return;
                    }
                }

                string os = GetCurrentOS();
                StatusText = $"❌ yt-dlp not found! Click 'Get yt-dlp' to download for {os}.";
                LogToCli($"yt-dlp not found for {os}");
                YtDlpPath = "";
            }
            catch (Exception ex)
            {
                StatusText = $"Error setting up yt-dlp: {ex.Message}";
                LogToCli($"ERROR: {ex.Message}");
                YtDlpPath = "";
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

                var trackInfo = new TrackInfo
                {
                    Title = root.TryGetProperty("title", out var title) ? title.GetString() ?? "Unknown" : "Unknown",
                    Artist = root.TryGetProperty("uploader", out var uploader) ? uploader.GetString() ?? "Unknown" : "Unknown",
                    Album = root.TryGetProperty("album", out var album) ? album.GetString() ?? "" : "",
                    Duration = FormatDuration(root.TryGetProperty("duration", out var duration) ? duration.GetDouble() : 0),
                    Url = root.TryGetProperty("webpage_url", out var webUrl) ? webUrl.GetString() ?? url : url
                };

                string? thumbnailUrl = null;

                if (root.TryGetProperty("thumbnail", out var thumbnailProp))
                {
                    if (thumbnailProp.ValueKind == JsonValueKind.String)
                    {
                        thumbnailUrl = thumbnailProp.GetString();
                    }
                }
                else if (root.TryGetProperty("thumbnails", out var thumbnailsProp))
                {
                    if (thumbnailsProp.ValueKind == JsonValueKind.Array)
                    {
                        var thumbnails = thumbnailsProp.EnumerateArray().ToList();
                        if (thumbnails.Count > 0)
                        {
                            var bestThumbnail = thumbnails.LastOrDefault();
                            if (bestThumbnail.TryGetProperty("url", out var urlProp))
                            {
                                thumbnailUrl = urlProp.GetString();
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(thumbnailUrl))
                {
                    trackInfo.Thumbnail = await LoadThumbnailAsync(thumbnailUrl);
                }

                LogToCli($"Metadata extracted: {trackInfo.Title} by {trackInfo.Artist}");
                return trackInfo;
            }
            catch (Exception ex)
            {
                LogToCli($"Error extracting metadata: {ex.Message}");
                return null;
            }
        }

        private async Task<Bitmap?> LoadThumbnailAsync(string url)
        {
            try
            {
                LogToCli("Loading thumbnail...");
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                using var stream = new MemoryStream(imageBytes);

                return new Bitmap(stream);
            }
            catch (Exception ex)
            {
                LogToCli($"Failed to load thumbnail: {ex.Message}");
                return null;
            }
        }

        private string FormatDuration(double seconds)
        {
            if (seconds <= 0) return "Unknown";

            var timeSpan = TimeSpan.FromSeconds(seconds);
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}:{timeSpan:mm\\:ss}";
            }
            return $"{timeSpan:mm\\:ss}";
        }

        [RelayCommand]
        private async Task Download()
        {
            if (string.IsNullOrWhiteSpace(DownloadUrl))
            {
                StatusText = "⚠️ Please enter a URL or search term.";
                LogToCli("WARNING: No URL or search term provided");
                return;
            }

            if (string.IsNullOrEmpty(YtDlpPath))
            {
                StatusText = "❌ yt-dlp not available. Please download it first.";
                LogToCli("ERROR: yt-dlp not available");
                return;
            }

            IsDownloading = true;
            DownloadProgress = 0;
            ShowMetadata = false;
            CurrentTrack = null;

            StatusText = $"🚀 Starting download for: {DownloadUrl}";
            LogToCli($"Starting download: {DownloadUrl}");

            try
            {
                DownloadProgress = 5;
                CurrentTrack = await ExtractMetadata(DownloadUrl);

                if (CurrentTrack != null)
                {
                    ShowMetadata = true;
                    StatusText = $"📝 Found: {CurrentTrack.Title} by {CurrentTrack.Artist}";
                }

                DownloadProgress = 15;
                await RealDownload();

                StatusText = $"✅ Download completed! Check your music folder.";
                LogToCli("Download completed successfully");
            }
            catch (Exception ex)
            {
                StatusText = $"❌ Error: {ex.Message}";
                LogToCli($"ERROR: {ex.Message}");
                ShowMetadata = false;
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
            }
        }

        private async Task RealDownload()
        {
            try
            {
                DownloadProgress = 20;
                LogToCli($"Using yt-dlp at: {YtDlpPath}");

                bool isUrl = DownloadUrl.StartsWith("http://") || DownloadUrl.StartsWith("https://");
                string searchPrefix = isUrl ? "" : "ytsearch1:";

                LogToCli(isUrl ? "Direct URL detected" : "Search term detected - will search YouTube");

                var output = new StringBuilder();
                var error = new StringBuilder();

                string args;
                if (isUrl)
                {
                    args = $"\"{DownloadUrl}\" --extract-audio --audio-format mp3 --audio-quality 0 -o \"{Path.Combine(DownloadFolder, "%(title)s.%(ext)s")}\" --no-playlist --embed-metadata --add-metadata";
                }
                else
                {
                    args = $"\"{searchPrefix}{DownloadUrl}\" --extract-audio --audio-format mp3 --audio-quality 0 -o \"{Path.Combine(DownloadFolder, "%(title)s.%(ext)s")}\" --embed-metadata --add-metadata";
                }

                LogToCli($"Command: yt-dlp {args}");
                StatusText = isUrl ? "🎵 Downloading from URL..." : "🔍 Searching and downloading...";
                DownloadProgress = 30;

                var result = await Cli.Wrap(YtDlpPath)
                    .WithArguments(args)
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(error))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();

                DownloadProgress = 90;

                if (!string.IsNullOrEmpty(output.ToString()))
                {
                    LogToCli($"yt-dlp output:\n{output}");
                }

                if (result.ExitCode == 0)
                {
                    LogToCli("SUCCESS: Download completed with metadata");
                    DownloadProgress = 100;

                    if (CurrentTrack != null)
                    {
                        var outputText = output.ToString();
                        var lines = outputText.Split('\n');
                        var destinationLine = lines.FirstOrDefault(l => l.Contains("[ExtractAudio] Destination:"));
                        if (destinationLine != null)
                        {
                            var filename = Path.GetFileName(destinationLine.Split(':').LastOrDefault()?.Trim());
                            CurrentTrack.FileName = filename ?? "";
                        }
                    }
                }
                else
                {
                    LogToCli($"FAILED: Exit code {result.ExitCode}");
                    if (!string.IsNullOrEmpty(error.ToString()))
                    {
                        LogToCli($"Error details:\n{error}");
                    }
                    throw new Exception($"Download failed with exit code {result.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                LogToCli($"EXCEPTION: {ex.Message}");
                throw;
            }
        }

        [RelayCommand]
        private async Task DownloadYtDlp()
        {
            try
            {
                StatusText = $"⬇️ Downloading yt-dlp for {GetCurrentOS()}...";
                LogToCli($"Downloading yt-dlp for {GetCurrentOS()}");
                IsDownloading = true;
                DownloadProgress = 0;

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
                    DownloadProgress = 30;
                    var response = await localHttpClient.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();

                    DownloadProgress = 60;
                    await File.WriteAllBytesAsync(ytDlpPath, await response.Content.ReadAsByteArrayAsync());

                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        MakeExecutable(ytDlpPath);
                        LogToCli("Made executable for Unix system");
                    }

                    DownloadProgress = 100;
                    StatusText = $"✅ yt-dlp downloaded successfully for {GetCurrentOS()}!";
                    LogToCli("yt-dlp download completed successfully");

                    YtDlpPath = ytDlpPath;
                }
            }
            catch (Exception ex)
            {
                StatusText = $"❌ Failed to download yt-dlp: {ex.Message}";
                LogToCli($"ERROR downloading yt-dlp: {ex.Message}");
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
            StatusText = "🎵 Ready to download music...";
            DownloadProgress = 0;
            ShowMetadata = false;
            CurrentTrack = null;
            LogToCli("Input cleared");
        }

        [RelayCommand]
        private void ClearLog()
        {
            CliOutput = "Symphex Music Downloader v1.0\n" +
                        "=============================\n" +
                        "Log cleared...\n\n";
        }

        [RelayCommand]
        private void OpenFolder()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("explorer.exe", DownloadFolder);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", DownloadFolder);
                }
                else
                {
                    Process.Start("xdg-open", DownloadFolder);
                }
                LogToCli("Opened download folder");
            }
            catch (Exception ex)
            {
                LogToCli($"ERROR opening folder: {ex.Message}");
            }
        }
    }
}