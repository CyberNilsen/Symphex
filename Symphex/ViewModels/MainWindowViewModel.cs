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
                // Skip if we don't have proper title/artist info
                if (string.IsNullOrEmpty(trackInfo.Title) || string.IsNullOrEmpty(trackInfo.Artist) ||
                    trackInfo.Title == "Unknown" || trackInfo.Artist == "Unknown")
                {
                    trackInfo.AlbumArt = trackInfo.Thumbnail;
                    trackInfo.HasRealAlbumArt = false;
                    return;
                }

                // Try iTunes first for better metadata
                var iTunesResult = await SearchITunesForFullMetadata(trackInfo.Title, trackInfo.Artist);
                if (iTunesResult.HasValue)
                {
                    var (artworkBitmap, albumName, genreName, releaseYear, trackNum) = iTunesResult.Value;

                    trackInfo.AlbumArt = artworkBitmap;
                    trackInfo.HasRealAlbumArt = artworkBitmap != null;

                    // Update metadata from iTunes if available and better
                    if (!string.IsNullOrEmpty(albumName))
                    {
                        trackInfo.Album = albumName;
                    }
                    if (!string.IsNullOrEmpty(genreName))
                    {
                        trackInfo.Genre = genreName;
                    }
                    if (!string.IsNullOrEmpty(releaseYear))
                    {
                        trackInfo.Year = releaseYear;
                    }
                    if (trackNum > 0)
                    {
                        trackInfo.TrackNumber = trackNum;
                    }

                    trackInfo.AlbumArtist = trackInfo.Artist;
                    return;
                }

                // Fallback to Deezer
                var albumArt = await SearchDeezerAlbumArt(trackInfo.Title, trackInfo.Artist);
                if (albumArt != null)
                {
                    trackInfo.AlbumArt = albumArt;
                    trackInfo.HasRealAlbumArt = true;
                }
                else
                {
                    trackInfo.AlbumArt = trackInfo.Thumbnail;
                    trackInfo.HasRealAlbumArt = false;
                }
            }
            catch (Exception ex)
            {
                trackInfo.AlbumArt = trackInfo.Thumbnail;
                trackInfo.HasRealAlbumArt = false;
            }
        }

        private async Task<(Bitmap? albumArt, string album, string genre, string year, int trackNumber)?> SearchITunesForFullMetadata(string title, string artist)
        {
            try
            {
                var searchStrategies = new List<string>();

                if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(title))
                {
                    searchStrategies.Add($"{CleanForSearch(artist)} {CleanForSearch(title)}");
                    searchStrategies.Add($"{CleanForSearch(title)} {CleanForSearch(artist)}");
                }

                foreach (var searchTerm in searchStrategies)
                {
                    if (string.IsNullOrEmpty(searchTerm.Trim()))
                        continue;

                    string searchUrl = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(searchTerm)}&media=music&entity=song&limit=15";

                    try
                    {
                        var response = await httpClient.GetStringAsync(searchUrl);
                        using var doc = JsonDocument.Parse(response);

                        if (doc.RootElement.TryGetProperty("results", out var results))
                        {
                            var scoredResults = new List<(JsonElement result, double score)>();

                            foreach (var result in results.EnumerateArray())
                            {
                                if (result.TryGetProperty("trackName", out var trackName) &&
                                    result.TryGetProperty("artistName", out var artistName))
                                {
                                    string resultTitle = trackName.GetString() ?? "";
                                    string resultArtist = artistName.GetString() ?? "";

                                    double titleScore = CalculateSimilarity(CleanForSearch(title), CleanForSearch(resultTitle));
                                    double artistScore = CalculateSimilarity(CleanForSearch(artist), CleanForSearch(resultArtist));
                                    double totalScore = (titleScore * 0.7) + (artistScore * 0.3);

                                    if (totalScore > 0.6)
                                    {
                                        scoredResults.Add((result, totalScore));
                                    }
                                }
                            }

                            scoredResults.Sort((a, b) => b.score.CompareTo(a.score));

                            var bestResult = scoredResults.FirstOrDefault().result;
                            if (bestResult.ValueKind != JsonValueKind.Undefined)
                            {
                                Bitmap? albumArt = null;
                                if (bestResult.TryGetProperty("artworkUrl100", out var artworkUrl))
                                {
                                    string imageUrl = artworkUrl.GetString()?.Replace("100x100", "600x600");
                                    if (!string.IsNullOrEmpty(imageUrl))
                                    {
                                        albumArt = await LoadImageAsync(imageUrl);
                                    }
                                }

                                string album = bestResult.TryGetProperty("collectionName", out var albumProp) ? albumProp.GetString() ?? "" : "";
                                string genre = bestResult.TryGetProperty("primaryGenreName", out var genreProp) ? genreProp.GetString() ?? "" : "";
                                string year = "";
                                if (bestResult.TryGetProperty("releaseDate", out var releaseProp))
                                {
                                    string releaseDate = releaseProp.GetString() ?? "";
                                    if (releaseDate.Length >= 4)
                                    {
                                        year = releaseDate.Substring(0, 4);
                                    }
                                }
                                int trackNumber = bestResult.TryGetProperty("trackNumber", out var trackProp) ? trackProp.GetInt32() : 0;

                                return (albumArt, album, genre, year, trackNumber);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exception
            }

            return null;
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

        private async Task<Bitmap?> SearchDeezerAlbumArt(string title, string artist)
        {
            try
            {
                string cleanTitle = CleanForSearch(title);
                string cleanArtist = CleanForSearch(artist);

                string searchTerm = $"{cleanArtist} {cleanTitle}".Trim();
                string searchUrl = $"https://api.deezer.com/search/track?q={Uri.EscapeDataString(searchTerm)}&limit=10";


                var response = await httpClient.GetStringAsync(searchUrl);
                using var doc = JsonDocument.Parse(response);

                if (doc.RootElement.TryGetProperty("data", out var data))
                {
                    var scoredResults = new List<(JsonElement track, double score)>();

                    foreach (var track in data.EnumerateArray())
                    {
                        if (track.TryGetProperty("title", out var trackTitle) &&
                            track.TryGetProperty("artist", out var artistObj) &&
                            artistObj.TryGetProperty("name", out var artistName) &&
                            track.TryGetProperty("album", out var album) &&
                            album.TryGetProperty("cover_xl", out var coverUrl))
                        {
                            string resultTitle = trackTitle.GetString() ?? "";
                            string resultArtist = artistName.GetString() ?? "";

                            double titleScore = CalculateSimilarity(cleanTitle, CleanForSearch(resultTitle));
                            double artistScore = CalculateSimilarity(cleanArtist, CleanForSearch(resultArtist));
                            double totalScore = (titleScore * 0.7) + (artistScore * 0.3);

                            if (totalScore > 0.6)
                            {
                                scoredResults.Add((track, totalScore));
                            }
                        }
                    }

                    scoredResults.Sort((a, b) => b.score.CompareTo(a.score));

                    foreach (var (track, score) in scoredResults.Take(3))
                    {
                        if (track.TryGetProperty("album", out var album) &&
                            album.TryGetProperty("cover_xl", out var coverUrl))
                        {
                            string imageUrl = coverUrl.GetString();
                            if (!string.IsNullOrEmpty(imageUrl))
                            {
                                var albumArt = await LoadImageAsync(imageUrl);
                                if (albumArt != null)
                                {
                                    return albumArt;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }

            return null;
        }

        private string CleanForSearch(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            return text
                .ToLowerInvariant()
                .Replace("_", " ")
                .Replace("-", " ")
                .Replace("feat.", "")
                .Replace("ft.", "")
                .Replace("featuring", "")
                .Trim();
        }

        private double CalculateSimilarity(string text1, string text2)
        {
            if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2))
                return 0;

            text1 = text1.ToLowerInvariant().Trim();
            text2 = text2.ToLowerInvariant().Trim();

            if (text1 == text2)
                return 1.0;

            if (text1.Contains(text2) || text2.Contains(text1))
                return 0.9;

            var words1 = text1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var words2 = text2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            int commonWords = words1.Intersect(words2).Count();
            int totalWords = Math.Max(words1.Length, words2.Length);

            double wordSimilarity = totalWords > 0 ? (double)commonWords / totalWords : 0;

            int maxLength = Math.Max(text1.Length, text2.Length);
            int minLength = Math.Min(text1.Length, text2.Length);
            double lengthSimilarity = (double)minLength / maxLength;

            return Math.Max(wordSimilarity, lengthSimilarity * 0.6);
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
                StatusText = "⚠️ Please enter a URL or search term.";
                return;
            }

            if (string.IsNullOrEmpty(YtDlpPath))
            {
                StatusText = "❌ yt-dlp not available. Please download it first.";
                return;
            }

            IsDownloading = true;
            DownloadProgress = 0;
            ShowMetadata = false;
            CurrentTrack = new TrackInfo();

            StatusText = $"🚀 Starting download for: {DownloadUrl}";

            try
            {
                DownloadProgress = 5;

                // ALWAYS extract metadata first, regardless of URL type
                var extractedTrack = await ExtractMetadata(DownloadUrl);

                if (extractedTrack != null)
                {
                    CurrentTrack = extractedTrack;
                    ShowMetadata = true;
                    StatusText = $"📝 Found: {CurrentTrack.Title} by {CurrentTrack.Artist}";
                    DownloadProgress = 15;
                }
                else
                {
                    StatusText = "⚠️ Could not extract metadata, proceeding with download...";
                    DownloadProgress = 10;
                }

                await RealDownload();
            }
            catch (Exception ex)
            {
                StatusText = $"❌ Error: {ex.Message}";
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