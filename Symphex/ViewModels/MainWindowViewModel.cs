using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CliWrap;
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
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Symphex.ViewModels
{
    public partial class TrackInfo : ObservableObject
    {
        [ObservableProperty]
        private string title = "";

        [ObservableProperty]
        private bool isDownloading = false;

        [ObservableProperty]
        private bool isDownloadComplete = false;

        [ObservableProperty]
        private string downloadFolder = "";

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

        [ObservableProperty]
        private string genre = "";

        [ObservableProperty]
        private string year = "";

        [ObservableProperty]
        private int trackNumber = 0;

        [ObservableProperty]
        private int discNumber = 0;

        [ObservableProperty]
        private string composer = "";

        [ObservableProperty]
        private string albumArtist = "";

        [ObservableProperty]
        private string comment = "";

        [ObservableProperty]
        private int bitrate = 0;

        [ObservableProperty]
        private string encoder = "";

    }

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

        [ObservableProperty]
        private bool isToastVisible = false;

        [ObservableProperty]
        private string toastMessage = "";

        private string YtDlpPath { get; set; } = "";
        private string FfmpegPath { get; set; } = "";
        private string YtDlpExecutableName => GetYtDlpExecutableName();
        private string FfmpegExecutableName => GetFfmpegExecutableName();
        private readonly HttpClient httpClient = new();



        public MainWindowViewModel()
        {
            CurrentTrack = new TrackInfo();

            SetupDownloadFolder();

            if (!Directory.Exists(DownloadFolder))
            {
                Directory.CreateDirectory(DownloadFolder);
            }

            // Start auto-installation in background
            _ = Task.Run(async () =>
            {
                await AutoInstallDependencies();
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

        private async Task AutoInstallDependencies()
        {
            try
            {

                // Check and auto-install yt-dlp
                await SetupOrDownloadYtDlp();

                // Check and auto-install FFmpeg
                await SetupOrDownloadFfmpeg();

            }
            catch (Exception ex)
            {
            }
        }

        private async Task SetupOrDownloadYtDlp()
        {
            try
            {
                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

                string[] possiblePaths = {
            Path.Combine(appDirectory, "tools", YtDlpExecutableName),
            Path.Combine(appDirectory, YtDlpExecutableName)
        };

                // First check local paths
                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        YtDlpPath = path;

                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            MakeExecutable(path);
                        }

                        return;
                    }
                }

                // Then check system PATH
                if (await IsExecutableInPath(YtDlpExecutableName))
                {
                    YtDlpPath = YtDlpExecutableName;
                    return;
                }

                // If not found, auto-download
                string os = GetCurrentOS();

                await AutoDownloadYtDlp();
            }
            catch (Exception ex)
            {
                YtDlpPath = "";
            }
        }

        private async Task SetupOrDownloadFfmpeg()
        {
            try
            {
                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

                string[] possiblePaths = {
            Path.Combine(appDirectory, "tools", FfmpegExecutableName),
            Path.Combine(appDirectory, "tools", "ffmpeg", "bin", FfmpegExecutableName),
            Path.Combine(appDirectory, "tools", "bin", FfmpegExecutableName),
            Path.Combine(appDirectory, FfmpegExecutableName)
        };

                // First check local paths
                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        FfmpegPath = path;

                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            MakeExecutable(path);
                        }

                        return;
                    }
                }

                // Check system PATH
                if (await IsExecutableInPath(FfmpegExecutableName))
                {
                    FfmpegPath = FfmpegExecutableName;
                    return;
                }

                // If not found, auto-download (except for Linux)
                string os = GetCurrentOS();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                 
                    FfmpegPath = "";
                }
                else
                {
                    await AutoDownloadFfmpeg();
                }
            }
            catch (Exception ex)
            {
                FfmpegPath = "";
            }
        }

        private async Task AutoDownloadYtDlp()
        {
            try
            {

                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string toolsDir = Path.Combine(appDirectory, "tools");

                if (!Directory.Exists(toolsDir))
                {
                    Directory.CreateDirectory(toolsDir);
                }

                string ytDlpPath = Path.Combine(toolsDir, YtDlpExecutableName);
                string downloadUrl = GetYtDlpDownloadUrl();


                using (var localHttpClient = new HttpClient())
                {
                    localHttpClient.Timeout = TimeSpan.FromMinutes(5);

                    var response = await localHttpClient.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();

                    await File.WriteAllBytesAsync(ytDlpPath, await response.Content.ReadAsByteArrayAsync());

                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        MakeExecutable(ytDlpPath);
                    }

                    YtDlpPath = ytDlpPath;
                }
            }
            catch (Exception ex)
            {
             
                YtDlpPath = "";
            }
        }

        private async Task AutoDownloadFfmpeg()
        {
            try
            {
                string os = GetCurrentOS();
                string downloadUrl = GetFfmpegDownloadUrl();

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    return;
                }


                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string toolsDir = Path.Combine(appDirectory, "tools");

                if (!Directory.Exists(toolsDir))
                {
                    Directory.CreateDirectory(toolsDir);
                }


                using (var localHttpClient = new HttpClient())
                {
                    localHttpClient.Timeout = TimeSpan.FromMinutes(10); // FFmpeg is larger

                    var response = await localHttpClient.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();

                    var zipBytes = await response.Content.ReadAsByteArrayAsync();

                    string zipPath = Path.Combine(toolsDir, "ffmpeg.zip");
                    await File.WriteAllBytesAsync(zipPath, zipBytes);


                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        await ExtractFfmpegWindows(zipPath, toolsDir);
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        await ExtractFfmpegMacOS(zipPath, toolsDir);
                    }

                    File.Delete(zipPath);

                    // Re-run setup to find the extracted executable
                    await SetupOrDownloadFfmpeg();

                }
            }
            catch (Exception ex)
            {
              
                FfmpegPath = "";
            }
        }

        private string GetYtDlpExecutableName()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "yt-dlp.exe" : "yt-dlp";
        }

        private string GetFfmpegExecutableName()
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

        private string GetFfmpegDownloadUrl()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "https://evermeet.cx/ffmpeg/getrelease/zip";
            else
                return "";
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

        private async void SetupPortableFfmpeg()
        {
            try
            {
                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

                string[] possiblePaths = {
            Path.Combine(appDirectory, "tools", FfmpegExecutableName),
            Path.Combine(appDirectory, "tools", "ffmpeg", "bin", FfmpegExecutableName),
            Path.Combine(appDirectory, "tools", "bin", FfmpegExecutableName),
            Path.Combine(appDirectory, FfmpegExecutableName)
        };

                // First check local paths
                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        FfmpegPath = path;

                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            MakeExecutable(path);
                        }

                        return;
                    }
                }

                if (await IsExecutableInPath(FfmpegExecutableName))
                {
                    FfmpegPath = FfmpegExecutableName;
                    return;
                }

                string os = GetCurrentOS();
                FfmpegPath = "";
            }
            catch (Exception ex)
            {
                FfmpegPath = "";
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

        private bool IsSpotifyUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            // More comprehensive Spotify URL detection
            return url.Contains("open.spotify.com/") ||
                   url.Contains("spotify.com/") ||
                   url.StartsWith("spotify:") ||
                   url.Contains("spotify://");
        }

        public class SpotifyTrack
        {
            public string Title { get; set; } = "";
            public string Artist { get; set; } = "";
            public string Album { get; set; } = "";
            public string Duration { get; set; } = "";
            public string SpotifyUrl { get; set; } = "";
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

                // Since web scraping is unreliable, let's try a different approach
                // Convert the Spotify URL to a search term and let user confirm

                string searchTerm = await ConvertSpotifyUrlToSearchTerm(spotifyUrl);

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    CliOutput += $"Converted to search: {searchTerm}\n";
                    StatusText = $"Searching YouTube for: {searchTerm}";

                    // Update the download URL to the search term and process it
                    DownloadUrl = searchTerm;

                    // Process as a regular YouTube search
                    await ProcessAsYouTubeSearch(searchTerm);
                }
                else
                {
                    StatusText = "Could not convert Spotify URL. Please copy track name manually.";
                    CliOutput += "Conversion failed. Manual input required.\n";

                    // Show instructions to user
                    await ShowSpotifyInstructions();
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error processing Spotify URL: {ex.Message}";
                CliOutput += $"Spotify processing error: {ex.Message}\n";
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

        // ADD: Process the search term as a YouTube search
        private async Task ProcessAsYouTubeSearch(string searchTerm)
        {
            try
            {
                IsDownloading = true;
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

                    await RealDownload();
                }
                else
                {
                    StatusText = "No results found. Try refining your search.";
                    CliOutput += "No YouTube results found for search term.\n";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Search failed: {ex.Message}";
                CliOutput += $"YouTube search error: {ex.Message}\n";
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
            }
        }

        // ADD: Show instructions to user when auto-extraction fails
        private async Task ShowSpotifyInstructions()
        {
            CliOutput += "\n=== SPOTIFY HELP ===\n";
            CliOutput += "Since Spotify blocks automatic extraction, please:\n";
            CliOutput += "1. Go to your Spotify link\n";
            CliOutput += "2. Copy the track name and artist\n";
            CliOutput += "3. Clear the URL field\n";
            CliOutput += "4. Paste the track name (e.g., 'Artist Name - Song Title')\n";
            CliOutput += "5. Click Download\n";
            CliOutput += "===================\n\n";

            StatusText = "See instructions in log. Copy track name manually from Spotify.";
        }

        private async Task<TrackInfo?> ExtractMetadata(string url)
        {
            try
            {
                // Always treat direct URLs as URLs, don't add search prefix
                bool isDirectUrl = url.StartsWith("http://") || url.StartsWith("https://");
                string fullUrl = isDirectUrl ? url : $"ytsearch1:{url}";

                var output = new StringBuilder();
                var error = new StringBuilder();

                // Use simpler arguments for better compatibility
                var args = new List<string>
        {
            $"\"{fullUrl}\"",
            "--dump-json",
            "--no-playlist",
            "--no-warnings",
            "--quiet"
        };

                var result = await Cli.Wrap(YtDlpPath)
                    .WithArguments(string.Join(" ", args))
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(error))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();

                var outputText = output.ToString().Trim();
                var errorText = error.ToString();

                if (result.ExitCode != 0)
                {
                    StatusText = $"Failed to extract metadata. Exit code: {result.ExitCode}";
                    if (!string.IsNullOrEmpty(errorText))
                    {
                        StatusText += $" Error: {errorText.Split('\n').FirstOrDefault()}";
                    }
                    return null;
                }

                if (string.IsNullOrEmpty(outputText))
                {
                    StatusText = "No metadata output received from yt-dlp";
                    return null;
                }

                // Parse the JSON response
                var lines = outputText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var jsonLine = lines.FirstOrDefault(line => line.Trim().StartsWith("{"));

                if (string.IsNullOrEmpty(jsonLine))
                {
                    StatusText = "No valid JSON found in yt-dlp output";
                    return null;
                }

                using var doc = JsonDocument.Parse(jsonLine);
                var root = doc.RootElement;

                // Extract basic info
                string rawTitle = root.TryGetProperty("title", out var title) ? title.GetString() ?? "Unknown" : "Unknown";
                string rawUploader = root.TryGetProperty("uploader", out var uploader) ? uploader.GetString() ?? "Unknown" : "Unknown";

                // Clean up title and extract artist
                string finalArtist = "Unknown";
                string finalTitle = rawTitle;

                // Try to parse artist - title format
                if (rawTitle.Contains(" - "))
                {
                    var parts = rawTitle.Split(new[] { " - " }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        finalArtist = CleanArtistName(parts[0]);
                        finalTitle = CleanSongTitle(parts[1]);
                    }
                }
                else
                {
                    // Fallback to using uploader as artist
                    finalArtist = CleanArtistName(rawUploader);
                    finalTitle = CleanSongTitle(rawTitle);
                }

                // Extract additional metadata
                string albumInfo = root.TryGetProperty("album", out var album) ? album.GetString() ?? "" : "";

                string yearInfo = "";
                if (root.TryGetProperty("upload_date", out var uploadDate))
                {
                    string dateStr = uploadDate.GetString() ?? "";
                    if (dateStr.Length >= 4)
                    {
                        yearInfo = dateStr.Substring(0, 4);
                    }
                }

                var trackInfo = new TrackInfo
                {
                    Title = finalTitle,
                    Artist = finalArtist,
                    Album = albumInfo,
                    Duration = FormatDuration(root.TryGetProperty("duration", out var duration) ? duration.GetDouble() : 0),
                    Url = root.TryGetProperty("webpage_url", out var webUrl) ? webUrl.GetString() ?? url : url,
                    Uploader = rawUploader,
                    UploadDate = root.TryGetProperty("upload_date", out var uploadDateProp) ? FormatUploadDate(uploadDateProp.GetString()) : "",
                    ViewCount = root.TryGetProperty("view_count", out var viewCount) ? viewCount.GetInt64() : 0,
                    AlbumArtist = finalArtist,
                    Comment = $"Downloaded from {rawUploader}",
                    Encoder = "Symphex",
                    Year = yearInfo
                };

                // Load thumbnail
                string? thumbnailUrl = GetBestThumbnailUrl(root);
                if (!string.IsNullOrEmpty(thumbnailUrl))
                {
                    trackInfo.Thumbnail = await LoadImageAsync(thumbnailUrl);
                }

                // Find real album art and additional metadata
                await FindRealAlbumArt(trackInfo);

                return trackInfo;
            }
            catch (Exception ex)
            {
                StatusText = $"Error extracting metadata: {ex.Message}";
                return null;
            }
        }


        private string CleanSongTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return "Unknown";

            string cleaned = title;

            // More comprehensive cleaning patterns
            var patterns = new[]
            {
        @"\s*\(Official\s*Video\)",
        @"\s*\(Official\s*Audio\)",
        @"\s*\(Official\s*Music\s*Video\)",
        @"\s*\(Official\)",
        @"\s*\(Lyrics?\)",
        @"\s*\(Lyric\s*Video\)",
        @"\s*\(HD\)",
        @"\s*\(4K\)",
        @"\s*\[Official\s*Video\]",
        @"\s*\[Official\s*Audio\]",
        @"\s*\[Official\s*Music\s*Video\]",
        @"\s*\[Lyrics?\]",
        @"\s*\[HD\]",
        @"\s*\[4K\]",
        @"\s*\(Music\s*Video\)",
        @"\s*\[Music\s*Video\]",
        @"\s*\(Visualizer\)",
        @"\s*\[Visualizer\]",
        @"\s*\(Live\)",
        @"\s*\[Live\]"
    };

            foreach (var pattern in patterns)
            {
                cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, pattern, "",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Clean up quotes and special characters
            cleaned = cleaned
                .Replace("\"", "")
                .Replace("'", "")
                .Replace("\u201C", "") // Left double quote
                .Replace("\u201D", "") // Right double quote
                .Replace("\u2018", "") // Left single quote
                .Replace("\u2019", "") // Right single quote
                .Replace("_", " ")
                .Replace("  ", " ")
                .Trim();

            return string.IsNullOrEmpty(cleaned) ? title : cleaned;
        }

        private string CleanArtistName(string artist)
        {
            if (string.IsNullOrEmpty(artist))
                return "Unknown";

            string cleaned = artist;

            // Clean common channel suffixes and patterns
            var patterns = new[]
            {
        @"\s*-\s*Topic",
        @"\s*VEVO",
        @"\s*Records",
        @"\s*Music",
        @"\s*Official",
        @"\s*Channel",
        @"\s*TV"
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

            return string.IsNullOrEmpty(cleaned) ? artist : cleaned;
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
                // Skip if we don't have basic info
                if (string.IsNullOrEmpty(trackInfo.Title) || string.IsNullOrEmpty(trackInfo.Artist) ||
                    trackInfo.Title == "Unknown" || trackInfo.Artist == "Unknown")
                {
                    trackInfo.AlbumArt = trackInfo.Thumbnail;
                    trackInfo.HasRealAlbumArt = false;
                    return;
                }

                // Generate comprehensive search variations
                var searchVariations = GenerateComprehensiveSearchVariations(trackInfo.Title, trackInfo.Artist);

                // Try multiple APIs with comprehensive fallback strategy
                var searchMethods = new[]
                {
            () => SearchITunesComprehensive(searchVariations),
            () => SearchDeezerComprehensive(searchVariations),
            () => SearchMusicBrainzComprehensive(searchVariations),
            () => SearchDiscogsComprehensive(searchVariations),
            () => SearchLastFmComprehensive(searchVariations)
        };

                foreach (var searchMethod in searchMethods)
                {
                    try
                    {
                        var result = await searchMethod();
                        if (result.HasValue && result.Value.albumArt != null && IsHighQualityAlbumArt(result.Value.albumArt))
                        {
                            trackInfo.AlbumArt = result.Value.albumArt;
                            trackInfo.HasRealAlbumArt = true;
                            UpdateMetadataFromSource(trackInfo, result.Value.album, result.Value.genre, result.Value.year, result.Value.trackNumber);
                            return;
                        }
                    }
                    catch
                    {
                        continue; // Silently continue to next API
                    }
                }

                // Final fallback: use thumbnail
                trackInfo.AlbumArt = trackInfo.Thumbnail;
                trackInfo.HasRealAlbumArt = false;
            }
            catch
            {
                trackInfo.AlbumArt = trackInfo.Thumbnail;
                trackInfo.HasRealAlbumArt = false;
            }
        }

        private List<(string query, double weight)> GenerateComprehensiveSearchVariations(string title, string artist)
        {
            var variations = new List<(string query, double weight)>();

            // Normalize inputs
            string cleanTitle = SmartCleanText(title);
            string cleanArtist = SmartCleanText(artist);

            // High priority - exact matches
            variations.Add(($"{cleanArtist} {cleanTitle}", 1.0));
            variations.Add(($"\"{cleanArtist}\" \"{cleanTitle}\"", 0.95));
            variations.Add(($"{cleanArtist} - {cleanTitle}", 0.9));

            // Medium priority - reordered
            variations.Add(($"{cleanTitle} {cleanArtist}", 0.8));
            variations.Add(($"{cleanTitle} - {cleanArtist}", 0.75));

            // Lower priority - partial matches
            variations.Add((cleanTitle, 0.6));
            variations.Add((cleanArtist, 0.5));

            // Alternative formats
            variations.Add(($"{cleanArtist}: {cleanTitle}", 0.7));
            variations.Add(($"{cleanArtist} {cleanTitle} song", 0.65));

            // Remove duplicates and sort by weight
            return variations
                .Where(v => !string.IsNullOrWhiteSpace(v.query))
                .GroupBy(v => v.query.ToLowerInvariant())
                .Select(g => g.OrderByDescending(v => v.weight).First())
                .OrderByDescending(v => v.weight)
                .ToList();
        }

        private string SmartCleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Remove common patterns that interfere with search
            var patterns = new[]
            {
        @"\s*\([^)]*official[^)]*\)",
        @"\s*\[[^\]]*official[^\]]*\]",
        @"\s*\([^)]*video[^)]*\)",
        @"\s*\[[^\]]*video[^\]]*\]",
        @"\s*\([^)]*audio[^)]*\)",
        @"\s*\[[^\]]*audio[^\]]*\]",
        @"\s*\([^)]*lyric[^)]*\)",
        @"\s*\[[^\]]*lyric[^\]]*\]",
        @"\s*\([^)]*hd[^)]*\)",
        @"\s*\[[^\]]*hd[^\]]*\]",
        @"\s*\([^)]*4k[^)]*\)",
        @"\s*\[[^\]]*4k[^\]]*\]",
        @"\s*\bfeat\.?\s+.*$",
        @"\s*\bft\.?\s+.*$",
        @"\s*\bfeaturing\s+.*$",
        @"\s*\bremix\b.*$",
        @"\s*\bremaster\b.*$",
        @"\s*\bdeluxe\b.*$",
        @"\s*\bextended\b.*$",
        @"\s*\bradio\s+edit\b.*$",
        @"\s*\bclean\b.*$",
        @"\s*\bexplicit\b.*$"
    };

            string cleaned = text;
            foreach (var pattern in patterns)
            {
                cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, pattern, "",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Clean up special characters and normalize spacing
            cleaned = cleaned
                .Replace("\"", "")
                .Replace("'", "")
                .Replace("\u201C", "") // Smart quotes
                .Replace("\u201D", "")
                .Replace("\u2018", "")
                .Replace("\u2019", "")
                .Replace("_", " ")
                .Replace("  ", " ")
                .Trim();

            return string.IsNullOrEmpty(cleaned) ? text : cleaned;
        }

        private async Task<(Bitmap? albumArt, string album, string genre, string year, int trackNumber)?> SearchITunesComprehensive(List<(string query, double weight)> searchVariations)
        {
            foreach (var (query, weight) in searchVariations.Take(8)) // Try top 8 variations
            {
                try
                {
                    string searchUrl = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query)}&media=music&entity=song&limit=50";

                    using var response = await httpClient.GetAsync(searchUrl);
                    if (!response.IsSuccessStatusCode) continue;

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);

                    if (!doc.RootElement.TryGetProperty("results", out var results)) continue;

                    var matches = ScoreAndFilterMatches(results, query, weight * 0.1); // Lower threshold based on weight

                    foreach (var match in matches.Take(5))
                    {
                        if (!match.TryGetProperty("artworkUrl100", out var artworkUrl)) continue;

                        // Try multiple resolutions
                        var imageUrls = new[]
                        {
                    artworkUrl.GetString()?.Replace("100x100", "1200x1200"),
                    artworkUrl.GetString()?.Replace("100x100", "600x600"),
                    artworkUrl.GetString()?.Replace("100x100", "400x400"),
                    artworkUrl.GetString()
                };

                        foreach (var imageUrl in imageUrls.Where(url => !string.IsNullOrEmpty(url)))
                        {
                            var albumArt = await LoadImageWithRetryAndValidation(imageUrl);
                            if (albumArt != null && IsHighQualityAlbumArt(albumArt))
                            {
                                var metadata = ExtractITunesMetadata(match);
                                return (albumArt, metadata.album, metadata.genre, metadata.year, metadata.trackNumber);
                            }
                        }
                    }
                }
                catch
                {
                    continue;
                }

                await Task.Delay(200); // Rate limiting
            }

            return null;
        }

        private async Task<(Bitmap? albumArt, string album, string genre, string year, int trackNumber)?> SearchDeezerComprehensive(List<(string query, double weight)> searchVariations)
        {
            foreach (var (query, weight) in searchVariations.Take(8))
            {
                try
                {
                    string searchUrl = $"https://api.deezer.com/search/track?q={Uri.EscapeDataString(query)}&limit=50";

                    using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                    request.Headers.Add("User-Agent", "Symphex/1.0");

                    var response = await httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode) continue;

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);

                    if (!doc.RootElement.TryGetProperty("data", out var data)) continue;

                    var matches = ScoreDeezerMatches(data, query, weight * 0.1);

                    foreach (var match in matches.Take(5))
                    {
                        if (!match.TryGetProperty("album", out var album)) continue;

                        var imageUrls = new[]
                        {
                    album.TryGetProperty("cover_xl", out var xl) ? xl.GetString() : null,
                    album.TryGetProperty("cover_big", out var big) ? big.GetString() : null,
                    album.TryGetProperty("cover_medium", out var med) ? med.GetString() : null
                };

                        foreach (var imageUrl in imageUrls.Where(url => !string.IsNullOrEmpty(url)))
                        {
                            var albumArt = await LoadImageWithRetryAndValidation(imageUrl);
                            if (albumArt != null && IsHighQualityAlbumArt(albumArt))
                            {
                                var metadata = ExtractDeezerMetadata(match);
                                return (albumArt, metadata.album, metadata.genre, metadata.year, 0);
                            }
                        }
                    }
                }
                catch
                {
                    continue;
                }

                await Task.Delay(300); // Rate limiting
            }

            return null;
        }

        private async Task<(Bitmap? albumArt, string album, string genre, string year, int trackNumber)?> SearchMusicBrainzComprehensive(List<(string query, double weight)> searchVariations)
        {
            foreach (var (query, weight) in searchVariations.Take(6)) // Fewer for MusicBrainz due to rate limits
            {
                try
                {
                    // Try different MusicBrainz query formats
                    var mbQueries = new[]
                    {
                $"recording:\"{query}\"",
                $"recording:{query.Replace("\"", "")}",
                query
            };

                    foreach (var mbQuery in mbQueries)
                    {
                        try
                        {
                            string searchUrl = $"https://musicbrainz.org/ws/2/recording/?query={Uri.EscapeDataString(mbQuery)}&fmt=json&limit=25";

                            using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                            request.Headers.Add("User-Agent", "Symphex/1.0 (+https://github.com/symphex)");

                            var response = await httpClient.SendAsync(request);
                            if (!response.IsSuccessStatusCode) continue;

                            var jsonResponse = await response.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(jsonResponse);

                            if (!doc.RootElement.TryGetProperty("recordings", out var recordings)) continue;

                            foreach (var recording in recordings.EnumerateArray().Take(10))
                            {
                                if (!recording.TryGetProperty("releases", out var releases)) continue;

                                foreach (var release in releases.EnumerateArray().Take(5))
                                {
                                    if (!release.TryGetProperty("id", out var releaseId)) continue;

                                    string mbid = releaseId.GetString();
                                    if (string.IsNullOrEmpty(mbid)) continue;

                                    var coverResult = await GetCoverArtFromArchiveRobust(mbid);
                                    if (coverResult.HasValue && coverResult.Value.albumArt != null && IsHighQualityAlbumArt(coverResult.Value.albumArt))
                                    {
                                        return (coverResult.Value.albumArt, coverResult.Value.album, "", coverResult.Value.year, 0);
                                    }
                                }
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
                catch
                {
                    continue;
                }

                await Task.Delay(1000); // MusicBrainz rate limiting
            }

            return null;
        }

        private async Task<(Bitmap? albumArt, string album, string year)?> GetCoverArtFromArchiveRobust(string mbid)
        {
            try
            {
                string coverArtUrl = $"https://coverartarchive.org/release/{mbid}";

                using var request = new HttpRequestMessage(HttpMethod.Get, coverArtUrl);
                request.Headers.Add("User-Agent", "Symphex/1.0");

                var response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);

                if (!doc.RootElement.TryGetProperty("images", out var images)) return null;

                // Try front cover first, then any cover
                var imagesToTry = images.EnumerateArray()
                    .OrderByDescending(img => img.TryGetProperty("front", out var front) && front.GetBoolean())
                    .ThenByDescending(img => img.TryGetProperty("types", out var types) &&
                        types.EnumerateArray().Any(t => t.GetString() == "Front"))
                    .ToList();

                foreach (var image in imagesToTry.Take(3))
                {
                    if (!image.TryGetProperty("image", out var imageUrl)) continue;

                    string artUrl = imageUrl.GetString();
                    if (string.IsNullOrEmpty(artUrl)) continue;

                    var albumArt = await LoadImageWithRetryAndValidation(artUrl);
                    if (albumArt != null && IsHighQualityAlbumArt(albumArt))
                    {
                        var releaseInfo = await GetMusicBrainzReleaseInfoRobust(mbid);
                        return (albumArt, releaseInfo.album, releaseInfo.year);
                    }
                }
            }
            catch
            {
                // Silent failure
            }

            return null;
        }

        private async Task<(string album, string year)> GetMusicBrainzReleaseInfoRobust(string mbid)
        {
            try
            {
                string releaseUrl = $"https://musicbrainz.org/ws/2/release/{mbid}?fmt=json";

                using var request = new HttpRequestMessage(HttpMethod.Get, releaseUrl);
                request.Headers.Add("User-Agent", "Symphex/1.0");

                var response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return ("", "");

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);

                string album = doc.RootElement.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "";
                string year = "";

                if (doc.RootElement.TryGetProperty("date", out var date))
                {
                    string dateStr = date.GetString() ?? "";
                    if (dateStr.Length >= 4 && int.TryParse(dateStr.Substring(0, 4), out _))
                    {
                        year = dateStr.Substring(0, 4);
                    }
                }

                return (album, year);
            }
            catch
            {
                return ("", "");
            }
        }



        private async Task<(Bitmap? albumArt, string album, string genre, string year, int trackNumber)?> SearchDiscogsComprehensive(List<(string query, double weight)> searchVariations)
        {
            foreach (var (query, weight) in searchVariations.Take(6))
            {
                try
                {
                    string searchUrl = $"https://api.discogs.com/database/search?q={Uri.EscapeDataString(query)}&type=release&per_page=25";

                    using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                    request.Headers.Add("User-Agent", "Symphex/1.0 +https://github.com/symphex");

                    var response = await httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode) continue;

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);

                    if (!doc.RootElement.TryGetProperty("results", out var results)) continue;

                    foreach (var release in results.EnumerateArray().Take(10))
                    {
                        if (!release.TryGetProperty("cover_image", out var coverImage)) continue;

                        string imageUrl = coverImage.GetString();
                        if (string.IsNullOrEmpty(imageUrl)) continue;

                        var albumArt = await LoadImageWithRetryAndValidation(imageUrl);
                        if (albumArt != null && IsHighQualityAlbumArt(albumArt))
                        {
                            var metadata = ExtractDiscogsMetadata(release);
                            return (albumArt, metadata.album, metadata.genre, metadata.year, 0);
                        }
                    }
                }
                catch
                {
                    continue;
                }

                await Task.Delay(1000); // Discogs rate limiting
            }

            return null;
        }

        private async Task<(Bitmap? albumArt, string album, string genre, string year, int trackNumber)?> SearchLastFmComprehensive(List<(string query, double weight)> searchVariations)
        {
            // Note: Last.fm requires API key for most functionality, but we can try some public endpoints
            foreach (var (query, weight) in searchVariations.Take(5))
            {
                try
                {
                    // Try to use Last.fm's search without auth (limited functionality)
                    string searchUrl = $"https://ws.audioscrobbler.com/2.0/?method=track.search&track={Uri.EscapeDataString(query)}&format=json&limit=30";

                    using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                    request.Headers.Add("User-Agent", "Symphex/1.0");

                    var response = await httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode) continue;

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    if (jsonResponse.Contains("error")) continue; // Skip if API error

                    using var doc = JsonDocument.Parse(jsonResponse);

                    // Basic parsing - Last.fm free tier is very limited
                    // This is mainly for fallback cases

                }
                catch
                {
                    continue;
                }

                await Task.Delay(500);
            }

            return null;
        }

        private List<JsonElement> ScoreAndFilterMatches(JsonElement results, string originalQuery, double minThreshold)
        {
            var scoredMatches = new List<(JsonElement element, double score)>();

            foreach (var result in results.EnumerateArray())
            {
                try
                {
                    if (!result.TryGetProperty("trackName", out var trackName) ||
                        !result.TryGetProperty("artistName", out var artistName))
                        continue;

                    string resultTitle = SmartCleanText(trackName.GetString() ?? "");
                    string resultArtist = SmartCleanText(artistName.GetString() ?? "");
                    string resultCombined = $"{resultArtist} {resultTitle}".ToLowerInvariant();
                    string queryCombined = originalQuery.ToLowerInvariant();

                    double score = CalculateFlexibleSimilarity(queryCombined, resultCombined);

                    if (score >= minThreshold)
                    {
                        scoredMatches.Add((result, score));
                    }
                }
                catch
                {
                    continue;
                }
            }

            return scoredMatches
                .OrderByDescending(x => x.score)
                .Select(x => x.element)
                .ToList();
        }

        private List<JsonElement> ScoreDeezerMatches(JsonElement data, string originalQuery, double minThreshold)
        {
            var scoredMatches = new List<(JsonElement element, double score)>();

            foreach (var track in data.EnumerateArray())
            {
                try
                {
                    if (!track.TryGetProperty("title", out var trackTitle) ||
                        !track.TryGetProperty("artist", out var artistObj) ||
                        !artistObj.TryGetProperty("name", out var artistName))
                        continue;

                    string resultTitle = SmartCleanText(trackTitle.GetString() ?? "");
                    string resultArtist = SmartCleanText(artistName.GetString() ?? "");
                    string resultCombined = $"{resultArtist} {resultTitle}".ToLowerInvariant();
                    string queryCombined = originalQuery.ToLowerInvariant();

                    double score = CalculateFlexibleSimilarity(queryCombined, resultCombined);

                    if (score >= minThreshold)
                    {
                        scoredMatches.Add((track, score));
                    }
                }
                catch
                {
                    continue;
                }
            }

            return scoredMatches
                .OrderByDescending(x => x.score)
                .Select(x => x.element)
                .ToList();
        }

        private double CalculateFlexibleSimilarity(string query, string result)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(result))
                return 0;

            query = query.ToLowerInvariant().Trim();
            result = result.ToLowerInvariant().Trim();

            // Exact match
            if (query == result) return 1.0;

            // Contains check (very common for music)
            if (result.Contains(query)) return 0.9;
            if (query.Contains(result)) return 0.85;

            // Word-based matching (most important for music)
            var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2) // Ignore very short words
                .ToArray();
            var resultWords = result.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2)
                .ToArray();

            if (queryWords.Length == 0 || resultWords.Length == 0) return 0;

            // Count exact word matches
            int exactMatches = queryWords.Intersect(resultWords).Count();
            double exactWordRatio = (double)exactMatches / queryWords.Length;

            // Count partial word matches (for slight variations)
            int partialMatches = 0;
            foreach (var qWord in queryWords)
            {
                foreach (var rWord in resultWords)
                {
                    if (qWord.Length > 3 && rWord.Length > 3)
                    {
                        if (qWord.Contains(rWord) || rWord.Contains(qWord) ||
                            CalculateLevenshteinDistance(qWord, rWord) <= 1)
                        {
                            partialMatches++;
                            break;
                        }
                    }
                }
            }

            double partialWordRatio = (double)partialMatches / queryWords.Length;

            // Combine scores with preference for exact matches
            double combinedScore = (exactWordRatio * 0.8) + (partialWordRatio * 0.2);

            // Bonus for having most words match
            if (exactWordRatio > 0.7) combinedScore += 0.1;
            if (exactWordRatio > 0.5 && partialWordRatio > 0.8) combinedScore += 0.05;

            return Math.Min(combinedScore, 1.0);
        }

        private async Task<Bitmap?> LoadImageWithRetryAndValidation(string url, int maxRetries = 3)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    using var response = await httpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode) continue;

                    var imageBytes = await response.Content.ReadAsByteArrayAsync();

                    // Enhanced validation
                    if (imageBytes.Length < 5000) continue; // Too small (likely placeholder)
                    if (imageBytes.Length > 10_000_000) continue; // Too large (likely not album art)

                    if (!IsValidImageFormat(imageBytes)) continue;

                    using var stream = new MemoryStream(imageBytes);
                    var bitmap = new Bitmap(stream);

                    // Additional quality validation
                    if (IsValidAlbumArt(bitmap))
                    {
                        return bitmap;
                    }
                }
                catch
                {
                    if (attempt == maxRetries - 1) break;
                    await Task.Delay(500 * (attempt + 1)); // Progressive backoff
                }
            }

            return null;
        }

        private bool IsValidImageFormat(byte[] imageBytes)
        {
            if (imageBytes.Length < 12) return false;

            // JPEG
            if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8 && imageBytes[imageBytes.Length - 2] == 0xFF && imageBytes[imageBytes.Length - 1] == 0xD9)
                return true;

            // PNG
            if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47 &&
                imageBytes[4] == 0x0D && imageBytes[5] == 0x0A && imageBytes[6] == 0x1A && imageBytes[7] == 0x0A)
                return true;

            // WebP
            if (imageBytes.Length >= 12 &&
                imageBytes[0] == 0x52 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46 && imageBytes[3] == 0x46 &&
                imageBytes[8] == 0x57 && imageBytes[9] == 0x45 && imageBytes[10] == 0x42 && imageBytes[11] == 0x50)
                return true;

            return false;
        }

        private (string album, string genre, string year, int trackNumber) ExtractITunesMetadata(JsonElement match)
        {
            string album = "";
            string genre = "";
            string year = "";
            int trackNumber = 0;

            try
            {
                album = match.TryGetProperty("collectionName", out var albumProp) ?
                    (albumProp.GetString() ?? "").Trim() : "";

                genre = match.TryGetProperty("primaryGenreName", out var genreProp) ?
                    (genreProp.GetString() ?? "").Trim() : "";

                if (match.TryGetProperty("releaseDate", out var releaseProp))
                {
                    string releaseDate = releaseProp.GetString() ?? "";
                    if (releaseDate.Length >= 4 && int.TryParse(releaseDate.Substring(0, 4), out _))
                    {
                        year = releaseDate.Substring(0, 4);
                    }
                }

                trackNumber = match.TryGetProperty("trackNumber", out var trackProp) ?
                    trackProp.GetInt32() : 0;
            }
            catch
            {
                // Return defaults if extraction fails
            }

            return (album, genre, year, trackNumber);
        }

        private (string album, string genre, string year) ExtractDeezerMetadata(JsonElement match)
        {
            string album = "";
            string genre = "";
            string year = "";

            try
            {
                if (match.TryGetProperty("album", out var albumObj))
                {
                    album = albumObj.TryGetProperty("title", out var titleProp) ?
                        (titleProp.GetString() ?? "").Trim() : "";

                    if (albumObj.TryGetProperty("release_date", out var releaseProp))
                    {
                        string releaseDate = releaseProp.GetString() ?? "";
                        if (releaseDate.Length >= 4 && int.TryParse(releaseDate.Substring(0, 4), out _))
                        {
                            year = releaseDate.Substring(0, 4);
                        }
                    }
                }

                // Try to get genre from track or artist
                if (match.TryGetProperty("artist", out var artistObj) &&
                    artistObj.TryGetProperty("name", out var artistName))
                {
                    // Genre mapping is limited in Deezer public API
                    // Could be enhanced with genre ID mapping if available
                }
            }
            catch
            {
                // Return defaults if extraction fails
            }

            return (album, genre, year);
        }

        private (string album, string genre, string year) ExtractDiscogsMetadata(JsonElement release)
        {
            string album = "";
            string genre = "";
            string year = "";

            try
            {
                if (release.TryGetProperty("title", out var titleProp))
                {
                    string fullTitle = titleProp.GetString() ?? "";
                    // Discogs format is usually "Artist - Album"
                    if (fullTitle.Contains(" - "))
                    {
                        var parts = fullTitle.Split(new[] { " - " }, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            album = parts[1].Trim();
                        }
                    }
                    else
                    {
                        album = fullTitle.Trim();
                    }
                }

                if (release.TryGetProperty("genre", out var genreArray) && genreArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var genreItem in genreArray.EnumerateArray())
                    {
                        var genreStr = genreItem.GetString();
                        if (!string.IsNullOrEmpty(genreStr))
                        {
                            genre = genreStr.Trim();
                            break;
                        }
                    }
                }

                if (release.TryGetProperty("year", out var yearProp))
                {
                    var yearStr = yearProp.GetString() ?? "";
                    if (int.TryParse(yearStr, out _))
                    {
                        year = yearStr;
                    }
                }
            }
            catch
            {
                // Return defaults if extraction fails
            }

            return (album, genre, year);
        }

        private bool IsHighQualityAlbumArt(Bitmap bitmap)
        {
            try
            {
                // Flexible size requirements
                if (bitmap.PixelSize.Width < 250 || bitmap.PixelSize.Height < 250)
                    return false;

                // Very large images might be posters or other non-album art
                if (bitmap.PixelSize.Width > 2000 || bitmap.PixelSize.Height > 2000)
                    return false;

                // Flexible aspect ratio for album covers
                double aspectRatio = (double)bitmap.PixelSize.Width / bitmap.PixelSize.Height;
                if (aspectRatio < 0.75 || aspectRatio > 1.35)
                    return false;

                // Check minimum pixel count for quality
                int totalPixels = bitmap.PixelSize.Width * bitmap.PixelSize.Height;
                if (totalPixels < 62500) // 250x250 minimum
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
        private bool IsValidAlbumArt(Bitmap bitmap)
        {
            try
            {
                // Size validation - real album art should be reasonably sized
                if (bitmap.PixelSize.Width < 200 || bitmap.PixelSize.Height < 200)
                    return false;

                if (bitmap.PixelSize.Width > 3000 || bitmap.PixelSize.Height > 3000)
                    return false;

                // Aspect ratio validation - album covers are typically square or close to square
                double aspectRatio = (double)bitmap.PixelSize.Width / bitmap.PixelSize.Height;
                if (aspectRatio < 0.7 || aspectRatio > 1.43) // Allow some flexibility
                    return false;

                // Check for minimum resolution quality
                int totalPixels = bitmap.PixelSize.Width * bitmap.PixelSize.Height;
                if (totalPixels < 40000) // Less than ~200x200
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }


        private void UpdateMetadataFromSource(TrackInfo trackInfo, string albumName, string genreName, string releaseYear, int trackNum)
        {
            if (!string.IsNullOrEmpty(albumName))
                trackInfo.Album = albumName;

            if (!string.IsNullOrEmpty(genreName))
                trackInfo.Genre = genreName;

            if (!string.IsNullOrEmpty(releaseYear))
                trackInfo.Year = releaseYear;

            if (trackNum > 0)
                trackInfo.TrackNumber = trackNum;

            trackInfo.AlbumArtist = trackInfo.Artist;
        }

        private async Task ApplyProperMetadata()
        {
            try
            {
                if (CurrentTrack == null || string.IsNullOrEmpty(CurrentTrack.FileName) || string.IsNullOrEmpty(FfmpegPath))
                {
                    return;
                }

                string audioFilePath = Path.Combine(DownloadFolder, CurrentTrack.FileName);
                if (!File.Exists(audioFilePath))
                {
                    return;
                }

                string tempOutput = Path.Combine(DownloadFolder, $"temp_{Guid.NewGuid():N}.mp3");

                var argsList = new List<string>();
                argsList.AddRange(new[] { "-i", audioFilePath });

                Bitmap? artworkToUse = CurrentTrack.AlbumArt ?? CurrentTrack.Thumbnail;

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

                        argsList.AddRange(new[] { "-i", tempArtwork });
                        argsList.AddRange(new[] { "-map", "0:a", "-map", "1:0" });
                        argsList.AddRange(new[] { "-c:a", "copy", "-c:v", "mjpeg" });
                        argsList.AddRange(new[] { "-disposition:v", "attached_pic" });
                    }
                    catch (Exception ex)
                    {
                        tempArtwork = null;
                        argsList.AddRange(new[] { "-c", "copy" });
                    }
                }
                else
                {
                    argsList.AddRange(new[] { "-c", "copy" });
                }

                argsList.AddRange(new[] { "-id3v2_version", "3" });

                // Apply comprehensive metadata
                if (!string.IsNullOrEmpty(CurrentTrack.Title) && CurrentTrack.Title != "Unknown")
                {
                    argsList.AddRange(new[] { "-metadata", $"title={CurrentTrack.Title}" });
                }

                if (!string.IsNullOrEmpty(CurrentTrack.Artist) && CurrentTrack.Artist != "Unknown")
                {
                    argsList.AddRange(new[] { "-metadata", $"artist={CurrentTrack.Artist}" });
                }

                if (!string.IsNullOrEmpty(CurrentTrack.Album))
                {
                    argsList.AddRange(new[] { "-metadata", $"album={CurrentTrack.Album}" });
                }

                if (!string.IsNullOrEmpty(CurrentTrack.AlbumArtist))
                {
                    argsList.AddRange(new[] { "-metadata", $"albumartist={CurrentTrack.AlbumArtist}" });
                    argsList.AddRange(new[] { "-metadata", $"album_artist={CurrentTrack.AlbumArtist}" });
                }

                if (!string.IsNullOrEmpty(CurrentTrack.Genre))
                {
                    argsList.AddRange(new[] { "-metadata", $"genre={CurrentTrack.Genre}" });
                }

                if (!string.IsNullOrEmpty(CurrentTrack.Year))
                {
                    argsList.AddRange(new[] { "-metadata", $"date={CurrentTrack.Year}" });
                    argsList.AddRange(new[] { "-metadata", $"year={CurrentTrack.Year}" });
                }

                if (CurrentTrack.TrackNumber > 0)
                {
                    argsList.AddRange(new[] { "-metadata", $"track={CurrentTrack.TrackNumber}" });
                }

                if (CurrentTrack.DiscNumber > 0)
                {
                    argsList.AddRange(new[] { "-metadata", $"disc={CurrentTrack.DiscNumber}" });
                }

                if (!string.IsNullOrEmpty(CurrentTrack.Comment))
                {
                    argsList.AddRange(new[] { "-metadata", $"comment={CurrentTrack.Comment}" });
                }

                if (!string.IsNullOrEmpty(CurrentTrack.Composer))
                {
                    argsList.AddRange(new[] { "-metadata", $"composer={CurrentTrack.Composer}" });
                }

                if (!string.IsNullOrEmpty(CurrentTrack.Encoder))
                {
                    argsList.AddRange(new[] { "-metadata", $"encoded_by={CurrentTrack.Encoder}" });
                }

                // Add URL as metadata for reference
                if (!string.IsNullOrEmpty(CurrentTrack.Url))
                {
                    argsList.AddRange(new[] { "-metadata", $"website={CurrentTrack.Url}" });
                }

                argsList.Add(tempOutput);

                var output = new StringBuilder();
                var error = new StringBuilder();

                var result = await Cli.Wrap(FfmpegPath)
                    .WithArguments(argsList)
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(error))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();

                try
                {
                    if (result.ExitCode == 0 && File.Exists(tempOutput))
                    {
                        if (File.Exists(audioFilePath))
                            File.Delete(audioFilePath);
                        File.Move(tempOutput, audioFilePath);
                    }
                    else
                    {
                        if (File.Exists(tempOutput))
                            File.Delete(tempOutput);
                    }
                }
                finally
                {
                    if (tempArtwork != null && File.Exists(tempArtwork))
                    {
                        File.Delete(tempArtwork);
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exception
            }
        }

        // Levenshtein distance for better string matching
        private int CalculateLevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
            if (string.IsNullOrEmpty(target)) return source.Length;

            int sourceLength = source.Length;
            int targetLength = target.Length;
            var distance = new int[sourceLength + 1, targetLength + 1];

            for (int i = 0; i <= sourceLength; distance[i, 0] = i++) { }
            for (int j = 0; j <= targetLength; distance[0, j] = j++) { }

            for (int i = 1; i <= sourceLength; i++)
            {
                for (int j = 1; j <= targetLength; j++)
                {
                    int cost = target[j - 1] == source[i - 1] ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            return distance[sourceLength, targetLength];
        }

        private async Task<Bitmap?> LoadImageAsync(string url)
        {
            try
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                if (imageBytes.Length < 100)
                    return null;

                using var stream = new MemoryStream(imageBytes);
                return new Bitmap(stream);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private string FormatUploadDate(string? uploadDate)
        {
            if (string.IsNullOrEmpty(uploadDate) || uploadDate.Length != 8)
                return "";

            try
            {
                var year = uploadDate.Substring(0, 4);
                var month = uploadDate.Substring(4, 2);
                var day = uploadDate.Substring(6, 2);
                return $"{year}-{month}-{day}";
            }
            catch
            {
                return uploadDate;
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
                StatusText = "Please enter a URL or search term.";
                return;
            }

            if (string.IsNullOrEmpty(YtDlpPath))
            {
                StatusText = "yt-dlp not available. Please download it first.";
                return;
            }

            // Check if it's a Spotify URL
            if (IsSpotifyUrl(DownloadUrl))
            {
                await ProcessSpotifyDownload(DownloadUrl);
                return;
            }

            // Continue with existing download logic for non-Spotify URLs
            IsDownloading = true;
            DownloadProgress = 0;
            ShowMetadata = false;
            CurrentTrack = new TrackInfo();

            StatusText = $"Starting download for: {DownloadUrl}";

            try
            {
                DownloadProgress = 5;

                var extractedTrack = await ExtractMetadata(DownloadUrl);

                if (extractedTrack != null)
                {
                    CurrentTrack = extractedTrack;
                    ShowMetadata = true;
                    StatusText = $"Found: {CurrentTrack.Title} by {CurrentTrack.Artist}";
                    DownloadProgress = 15;
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
        private async Task RealDownload()
        {
            try
            {
                DownloadProgress = 20;

                bool isUrl = DownloadUrl.StartsWith("http://") || DownloadUrl.StartsWith("https://");
                string fullUrl = isUrl ? DownloadUrl : $"ytsearch1:{DownloadUrl}";

                var output = new StringBuilder();
                var error = new StringBuilder();

                // Create filename based on cleaned metadata
                string filenameTemplate;
                if (CurrentTrack != null && !string.IsNullOrEmpty(CurrentTrack.Title) && !string.IsNullOrEmpty(CurrentTrack.Artist))
                {
                    string cleanTitle = SanitizeFilename(CurrentTrack.Title);
                    string cleanArtist = SanitizeFilename(CurrentTrack.Artist);
                    filenameTemplate = Path.Combine(DownloadFolder, $"{cleanArtist} - {cleanTitle}.%(ext)s");
                }
                else
                {
                    // Fallback to default naming
                    filenameTemplate = Path.Combine(DownloadFolder, "%(uploader)s - %(title)s.%(ext)s");
                }

                List<string> argsList = new List<string>
        {
            $"\"{fullUrl}\"",
            "--extract-audio",
            "--audio-format", "mp3",
            "--audio-quality", "0",
            "--no-playlist",
            "--embed-thumbnail",
            "--add-metadata",
            "-o", $"\"{filenameTemplate}\""
        };

                // Add FFmpeg location if available
                if (!string.IsNullOrEmpty(FfmpegPath) && File.Exists(FfmpegPath))
                {
                    string ffmpegDir = Path.GetDirectoryName(FfmpegPath) ?? "";
                    argsList.AddRange(new[] { "--ffmpeg-location", $"\"{ffmpegDir}\"" });
                }

                string args = string.Join(" ", argsList);

                StatusText = isUrl ? "🎵 Downloading audio..." : "🔍 Searching and downloading audio...";
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
                    StatusText = "🔧 Applying metadata and finalizing...";

                    // Find the actual downloaded file
                    string actualFilePath = await FindDownloadedFile(outputText);

                    if (!string.IsNullOrEmpty(actualFilePath) && CurrentTrack != null)
                    {
                        CurrentTrack.FileName = Path.GetFileName(actualFilePath);

                        // Apply enhanced metadata
                        await ApplyProperMetadata();
                    }

                    DownloadProgress = 100;
                    await VerifyAndReportDownloadSuccess(actualFilePath);
                }
                else
                {
                    throw new Exception($"Download failed with exit code {result.ExitCode}: {errorText}");
                }
            }
            catch (Exception ex)
            {
                StatusText = $"❌ Download failed: {ex.Message}";
                throw;
            }
        }

        private async Task<string> FindDownloadedFile(string ytDlpOutput)
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
                        return destinationLine.Substring(destinationIndex + "Destination: ".Length).Trim();
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
                        return recentFiles[0];
                    }
                }

                // Final fallback: construct expected filename
                if (CurrentTrack != null && !string.IsNullOrEmpty(CurrentTrack.Title) && !string.IsNullOrEmpty(CurrentTrack.Artist))
                {
                    string cleanTitle = SanitizeFilename(CurrentTrack.Title);
                    string cleanArtist = SanitizeFilename(CurrentTrack.Artist);
                    string expectedFilename = $"{cleanArtist} - {cleanTitle}.mp3";
                    return Path.Combine(DownloadFolder, expectedFilename);
                }

                return "";
            }
            catch (Exception ex)
            {
                return "";
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
            catch (Exception ex)
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
            catch (Exception ex)
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

        [RelayCommand]
        private async Task DownloadYtDlp()
        {
            try
            {
                StatusText = $"⬇️ Downloading yt-dlp for {GetCurrentOS()}...";
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
                    }

                    DownloadProgress = 100;
                    StatusText = $"✅ yt-dlp downloaded successfully for {GetCurrentOS()}!";

                    YtDlpPath = ytDlpPath;
                }
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
                string os = GetCurrentOS();
                string downloadUrl = GetFfmpegDownloadUrl();

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    StatusText = "ℹ️ Linux users should install FFmpeg via package manager (apt install ffmpeg)";
                   
                    return;
                }

                StatusText = $"⬇️ Downloading FFmpeg for {os}...";
                IsDownloading = true;
                DownloadProgress = 0;

                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string toolsDir = Path.Combine(appDirectory, "tools");

                if (!Directory.Exists(toolsDir))
                {
                    Directory.CreateDirectory(toolsDir);
                }

                DownloadProgress = 10;

                using (var localHttpClient = new HttpClient())
                {
                    localHttpClient.Timeout = TimeSpan.FromMinutes(10);

                    var response = await localHttpClient.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();

                    DownloadProgress = 40;
                    var zipBytes = await response.Content.ReadAsByteArrayAsync();

                    string zipPath = Path.Combine(toolsDir, "ffmpeg.zip");
                    await File.WriteAllBytesAsync(zipPath, zipBytes);

                    DownloadProgress = 60;

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        await ExtractFfmpegWindows(zipPath, toolsDir);
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        await ExtractFfmpegMacOS(zipPath, toolsDir);
                    }

                    File.Delete(zipPath);

                    DownloadProgress = 90;

                    SetupPortableFfmpeg();

                    DownloadProgress = 100;
                    StatusText = $"✅ FFmpeg downloaded and extracted for {os}!";
                }
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

        private Task ExtractFfmpegWindows(string zipPath, string toolsDir)
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var ffmpegEntry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));

                if (ffmpegEntry != null)
                {
                    string extractPath = Path.Combine(toolsDir, "ffmpeg.exe");
                    ffmpegEntry.ExtractToFile(extractPath, true);
                }
                else
                {
                    string extractDir = Path.Combine(toolsDir, "ffmpeg_temp");

                    if (Directory.Exists(extractDir))
                    {
                        Directory.Delete(extractDir, true);
                    }

                    archive.ExtractToDirectory(extractDir);

                    var ffmpegFiles = Directory.GetFiles(extractDir, "ffmpeg.exe", SearchOption.AllDirectories);
                    if (ffmpegFiles.Length > 0)
                    {
                        string sourcePath = ffmpegFiles[0];
                        string destPath = Path.Combine(toolsDir, "ffmpeg.exe");
                        File.Copy(sourcePath, destPath, true);
                    }

                    Directory.Delete(extractDir, true);
                }
            }
            return Task.CompletedTask;
        }

        private Task ExtractFfmpegMacOS(string zipPath, string toolsDir)
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var ffmpegEntry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith("ffmpeg", StringComparison.OrdinalIgnoreCase) && !e.FullName.Contains("/"));

                if (ffmpegEntry != null)
                {
                    string extractPath = Path.Combine(toolsDir, "ffmpeg");
                    ffmpegEntry.ExtractToFile(extractPath, true);
                    MakeExecutable(extractPath);
                }
            }
            return Task.CompletedTask;
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
            catch (Exception ex)
            {
            }
        }

        [RelayCommand]
        private void Clear()
        {
            DownloadUrl = "";
            StatusText = "🎵 Ready to download music...";
            DownloadProgress = 0;
            ShowMetadata = false;
            CurrentTrack = new TrackInfo();
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
            }
            catch (Exception ex)
            {
            }
        }
    }
}