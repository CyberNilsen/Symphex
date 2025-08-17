using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
using Avalonia.Threading;

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

            SetupPortableYtDlp();
            SetupPortableFfmpeg();
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

        private void LogToCli(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");

            string cleanMessage = message.Trim();

            string logEntry = $"[{timestamp}] {cleanMessage}\n";

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
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
                        LogToCli($"yt-dlp found at: {location}");
                        return;
                    }
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

        private void SetupPortableFfmpeg()
        {
            try
            {
                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

                string[] possiblePaths = {
                    Path.Combine(appDirectory, "tools", FfmpegExecutableName),
                    Path.Combine(appDirectory, "tools", "ffmpeg", "bin", FfmpegExecutableName),
                    Path.Combine(appDirectory, "tools", "bin", FfmpegExecutableName),
                    Path.Combine(appDirectory, FfmpegExecutableName),
                    FfmpegExecutableName
                };

                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path) || path == FfmpegExecutableName)
                    {
                        FfmpegPath = path;

                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(path))
                        {
                            MakeExecutable(path);
                        }

                        string location = path == FfmpegExecutableName ? "System PATH" : path;
                        LogToCli($"FFmpeg found at: {location}");
                        return;
                    }
                }

                string os = GetCurrentOS();
                LogToCli($"FFmpeg not found for {os}. Metadata embedding may not work properly.");
                FfmpegPath = "";
            }
            catch (Exception ex)
            {
                LogToCli($"ERROR setting up FFmpeg: {ex.Message}");
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
            try
            {
                if (CurrentTrack == null || string.IsNullOrEmpty(CurrentTrack.FileName) || string.IsNullOrEmpty(FfmpegPath))
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

                string tempOutput = Path.Combine(DownloadFolder, $"temp_{Guid.NewGuid():N}.mp3");

                var argsList = new List<string>();

                argsList.AddRange(new[] { "-i", audioFilePath });

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

                        argsList.AddRange(new[] { "-i", tempArtwork });

                        argsList.AddRange(new[] { "-map", "0:a", "-map", "1:0" });

                        argsList.AddRange(new[] { "-c:a", "copy", "-c:v", "mjpeg" });

                        argsList.AddRange(new[] { "-disposition:v", "attached_pic" });
                    }
                    catch (Exception ex)
                    {
                        LogToCli($"Error preparing artwork: {ex.Message}");
                        tempArtwork = null;
                        argsList.AddRange(new[] { "-c", "copy" });
                    }
                }
                else
                {
                    LogToCli("No artwork available to embed");
                    argsList.AddRange(new[] { "-c", "copy" });
                }

                argsList.AddRange(new[] { "-id3v2_version", "3" });

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

                if (!string.IsNullOrEmpty(CurrentTrack.UploadDate))
                {
                    argsList.AddRange(new[] { "-metadata", $"date={CurrentTrack.UploadDate}" });
                }

                argsList.Add(tempOutput);

                LogToCli($"Applying metadata: '{CurrentTrack.Title}' by '{CurrentTrack.Artist}' with {artworkSource}");

                var output = new StringBuilder();
                var error = new StringBuilder();

                var result = await Cli.Wrap(FfmpegPath)
                    .WithArguments(argsList)
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(error))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();

                var outputText = output.ToString();
                var errorText = error.ToString();

                if (!string.IsNullOrEmpty(outputText))
                {
                    LogToCli($"FFmpeg output: {outputText}");
                }

                if (!string.IsNullOrEmpty(errorText))
                {
                    LogToCli($"FFmpeg error: {errorText}");
                }

                try
                {
                    if (result.ExitCode == 0 && File.Exists(tempOutput))
                    {
                        if (File.Exists(audioFilePath))
                            File.Delete(audioFilePath);
                        File.Move(tempOutput, audioFilePath);
                        LogToCli($"✅ Metadata and {artworkSource} applied successfully");
                    }
                    else
                    {
                        LogToCli($"⚠️ Failed to apply metadata (exit code: {result.ExitCode})");
                        if (File.Exists(tempOutput))
                            File.Delete(tempOutput);
                    }
                }
                finally
                {
                    if (tempArtwork != null && File.Exists(tempArtwork))
                    {
                        File.Delete(tempArtwork);
                        LogToCli("Cleaned up temporary artwork file");
                    }
                }
            }
            catch (Exception ex)
            {
                LogToCli($"Error applying metadata: {ex.Message}");
            }
        }

        private async Task<Bitmap?> SearchITunesAlbumArt(string title, string artist)
        {
            try
            {
                var searchStrategies = new List<string>();

                if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(title))
                {
                    searchStrategies.Add($"{CleanForSearch(artist)} {CleanForSearch(title)}");
                    searchStrategies.Add($"{CleanForSearch(title)} {CleanForSearch(artist)}");

                    string cleanTitle = RemoveCommonWords(CleanForSearch(title));
                    string cleanArtist = RemoveCommonWords(CleanForSearch(artist));
                    if (!string.IsNullOrEmpty(cleanTitle) && !string.IsNullOrEmpty(cleanArtist))
                    {
                        searchStrategies.Add($"{cleanArtist} {cleanTitle}");
                    }
                }
                else if (!string.IsNullOrEmpty(artist))
                {
                    searchStrategies.Add(CleanForSearch(artist));
                }
                else if (!string.IsNullOrEmpty(title))
                {
                    searchStrategies.Add(CleanForSearch(title));
                }

                foreach (var searchTerm in searchStrategies)
                {
                    if (string.IsNullOrEmpty(searchTerm.Trim()))
                        continue;

                    LogToCli($"iTunes search strategy: {searchTerm}");

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
                                    result.TryGetProperty("artistName", out var artistName) &&
                                    result.TryGetProperty("artworkUrl100", out var artworkUrl))
                                {
                                    string resultTitle = trackName.GetString() ?? "";
                                    string resultArtist = artistName.GetString() ?? "";

                                    double titleScore = string.IsNullOrEmpty(title) ? 1.0 : CalculateSimilarity(CleanForSearch(title), CleanForSearch(resultTitle));
                                    double artistScore = string.IsNullOrEmpty(artist) ? 1.0 : CalculateSimilarity(CleanForSearch(artist), CleanForSearch(resultArtist));
                                    double totalScore = (titleScore * 0.7) + (artistScore * 0.3);

                                    if (totalScore > 0.4) 
                                    {
                                        scoredResults.Add((result, totalScore));
                                    }
                                }
                            }

                            scoredResults.Sort((a, b) => b.score.CompareTo(a.score));

                            foreach (var (result, score) in scoredResults.Take(3))
                            {
                                if (result.TryGetProperty("artworkUrl100", out var artworkUrl))
                                {
                                    string imageUrl = artworkUrl.GetString();
                                    if (!string.IsNullOrEmpty(imageUrl))
                                    {
                                        imageUrl = imageUrl.Replace("100x100", "600x600");
                                        var albumArt = await LoadImageAsync(imageUrl);
                                        if (albumArt != null)
                                        {
                                            LogToCli($"Found artwork via iTunes (score: {score:F2})");
                                            return albumArt;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToCli($"iTunes search failed for '{searchTerm}': {ex.Message}");
                        continue; 
                    }
                }
            }
            catch (Exception ex)
            {
                LogToCli($"iTunes search failed: {ex.Message}");
            }

            return null;
        }

        private string RemoveCommonWords(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var commonWords = new[] { "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by", "official", "video", "audio", "lyrics", "hd", "4k", "1080", "1080p", "720", "720p" };

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var filteredWords = words.Where(word => !commonWords.Contains(word.ToLowerInvariant())).ToArray();

            return string.Join(" ", filteredWords);
        }

        private async Task<Bitmap?> SearchDeezerAlbumArt(string title, string artist)
        {
            try
            {
                string cleanTitle = CleanForSearch(title);
                string cleanArtist = CleanForSearch(artist);

                string searchTerm = $"{cleanArtist} {cleanTitle}".Trim();
                string searchUrl = $"https://api.deezer.com/search/track?q={Uri.EscapeDataString(searchTerm)}&limit=10";

                LogToCli($"Deezer search: {searchTerm}");

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
                                    LogToCli($"Found artwork via Deezer (score: {score:F2})");
                                    return albumArt;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogToCli($"Deezer search failed: {ex.Message}");
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
                LogToCli($"Failed to load image: {ex.Message}");
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
            CurrentTrack = new TrackInfo();

            StatusText = $"🚀 Starting download for: {DownloadUrl}";
            LogToCli($"Starting download: {DownloadUrl}");

            try
            {
                DownloadProgress = 5;
                var extractedTrack = await ExtractMetadata(DownloadUrl);

                if (extractedTrack != null)
                {
                    CurrentTrack = extractedTrack;
                    ShowMetadata = true;
                    StatusText = $"📝 Found: {CurrentTrack.Title} by {CurrentTrack.Artist}";
                    LogToCli($"Metadata: {CurrentTrack.Title} by {CurrentTrack.Artist}");
                }
                else
                {
                    LogToCli("Failed to extract metadata, continuing with download...");
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

                if (!string.IsNullOrEmpty(FfmpegPath))
                {
                    LogToCli($"Using FFmpeg at: {FfmpegPath}");
                }

                bool isUrl = DownloadUrl.StartsWith("http://") || DownloadUrl.StartsWith("https://");
                string searchPrefix = isUrl ? "" : "ytsearch1:";
                string fullUrl = $"{searchPrefix}{DownloadUrl}";

                LogToCli(isUrl ? "Direct URL detected" : "Search term detected - will search YouTube");

                var output = new StringBuilder();
                var error = new StringBuilder();

                string filenameTemplate;
                if (CurrentTrack != null && !string.IsNullOrEmpty(CurrentTrack.Title) && !string.IsNullOrEmpty(CurrentTrack.Artist))
                {
                    string cleanTitle = SanitizeFilename(CurrentTrack.Title);
                    string cleanArtist = SanitizeFilename(CurrentTrack.Artist);
                    filenameTemplate = Path.Combine(DownloadFolder, $"{cleanTitle} - {cleanArtist}.%(ext)s");
                    LogToCli($"Using custom filename: {cleanTitle} - {cleanArtist}.mp3");
                }
                else
                {
                    filenameTemplate = Path.Combine(DownloadFolder, "%(title)s.%(ext)s");
                }

                List<string> argsList = new List<string>
        {
            $"\"{fullUrl}\"",
            "--extract-audio",
            "--audio-format", "mp3",
            "--audio-quality", "0",
            "--no-playlist",
            "-o", $"\"{filenameTemplate}\""
        };

                if (!string.IsNullOrEmpty(FfmpegPath) && File.Exists(FfmpegPath))
                {
                    string ffmpegDir = Path.GetDirectoryName(FfmpegPath) ?? "";
                    argsList.AddRange(new[] { "--ffmpeg-location", $"\"{ffmpegDir}\"" });
                }

                string args = string.Join(" ", argsList);

                LogToCli($"Command: yt-dlp {args}");
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

                if (!string.IsNullOrEmpty(outputText))
                {
                    LogToCli($"yt-dlp output:\n{outputText}");
                }

                if (!string.IsNullOrEmpty(errorText))
                {
                    LogToCli($"yt-dlp stderr:\n{errorText}");
                }

                if (result.ExitCode == 0)
                {
                    LogToCli("SUCCESS: Audio download completed");
                    DownloadProgress = 100;

                    if (CurrentTrack != null)
                    {
                        var lines = outputText.Split('\n');
                        var destinationLine = lines.FirstOrDefault(l =>
                            l.Contains("[ExtractAudio] Destination:") ||
                            l.Contains("has already been downloaded"));

                        if (destinationLine != null)
                        {
                            int destinationIndex = destinationLine.IndexOf("Destination: ");
                            if (destinationIndex >= 0)
                            {
                                string fullPath = destinationLine.Substring(destinationIndex + "Destination: ".Length).Trim();
                                string filename = Path.GetFileName(fullPath);
                                CurrentTrack.FileName = filename;
                                LogToCli($"Downloaded file: {filename}");

                                await ApplyProperMetadata();
                            }
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(CurrentTrack.Title) && !string.IsNullOrEmpty(CurrentTrack.Artist))
                            {
                                string cleanTitle = SanitizeFilename(CurrentTrack.Title);
                                string cleanArtist = SanitizeFilename(CurrentTrack.Artist);
                                CurrentTrack.FileName = $"{cleanTitle} - {cleanArtist}.mp3";
                                LogToCli($"Using constructed filename: {CurrentTrack.FileName}");
                                await ApplyProperMetadata();
                            }
                        }
                    }
                }
                else
                {
                    throw new Exception($"Download failed with exit code {result.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                LogToCli($"Download error: {ex.Message}");
                throw;
            }
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
        private async Task DownloadFfmpeg()
        {
            try
            {
                string os = GetCurrentOS();
                string downloadUrl = GetFfmpegDownloadUrl();

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    StatusText = "ℹ️ Linux users should install FFmpeg via package manager (apt install ffmpeg)";
                    LogToCli("Linux detected. Please install FFmpeg using your package manager:");
                    LogToCli("Ubuntu/Debian: sudo apt install ffmpeg");
                    LogToCli("Fedora: sudo dnf install ffmpeg");
                    LogToCli("Arch: sudo pacman -S ffmpeg");
                    return;
                }

                StatusText = $"⬇️ Downloading FFmpeg for {os}...";
                LogToCli($"Downloading FFmpeg for {os}");
                IsDownloading = true;
                DownloadProgress = 0;

                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string toolsDir = Path.Combine(appDirectory, "tools");

                if (!Directory.Exists(toolsDir))
                {
                    Directory.CreateDirectory(toolsDir);
                }

                LogToCli($"Download URL: {downloadUrl}");
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
                    LogToCli("Extracting FFmpeg...");

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
                    LogToCli("FFmpeg download and extraction completed successfully");
                }
            }
            catch (Exception ex)
            {
                StatusText = $"❌ Failed to download FFmpeg: {ex.Message}";
                LogToCli($"ERROR downloading FFmpeg: {ex.Message}");
                LogToCli("You can manually download FFmpeg from https://ffmpeg.org/download.html");
                LogToCli("Extract and place the executable in the 'tools' folder");
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
                    LogToCli($"Extracted ffmpeg.exe to: {extractPath}");
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
                        LogToCli($"Copied ffmpeg.exe to: {destPath}");
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
                    LogToCli($"Extracted ffmpeg to: {extractPath}");
                }
            }
            return Task.CompletedTask;
        }

        [RelayCommand]
        private void CheckDependencies()
        {
            try
            {
                LogToCli("Checking dependencies...");

                bool ytDlpFound = !string.IsNullOrEmpty(YtDlpPath) && (File.Exists(YtDlpPath) || YtDlpPath == YtDlpExecutableName);
                bool ffmpegFound = !string.IsNullOrEmpty(FfmpegPath) && (File.Exists(FfmpegPath) || FfmpegPath == FfmpegExecutableName);

                LogToCli($"yt-dlp: {(ytDlpFound ? "✅ Found" : "❌ Not found")}");
                LogToCli($"FFmpeg: {(ffmpegFound ? "✅ Found" : "❌ Not found")}");

                if (ytDlpFound && ffmpegFound)
                {
                    StatusText = "✅ All dependencies found! Ready to download with full metadata support.";
                    LogToCli("All dependencies are available. You can download music with full metadata and album art.");
                }
                else if (ytDlpFound && !ffmpegFound)
                {
                    StatusText = "⚠️ yt-dlp found, but FFmpeg missing. Downloads will work but metadata may be limited.";
                    LogToCli("yt-dlp is available but FFmpeg is missing.");
                    LogToCli("Click 'Get FFmpeg' to download it for full metadata support.");
                }
                else if (!ytDlpFound && ffmpegFound)
                {
                    StatusText = "⚠️ FFmpeg found, but yt-dlp missing. Cannot download without yt-dlp.";
                    LogToCli("FFmpeg is available but yt-dlp is missing.");
                    LogToCli("Click 'Get yt-dlp' to download it.");
                }
                else
                {
                    StatusText = "❌ Both yt-dlp and FFmpeg are missing. Please download them first.";
                    LogToCli("Both yt-dlp and FFmpeg are missing.");
                    LogToCli("Use the 'Get yt-dlp' and 'Get FFmpeg' buttons to download them.");
                }
            }
            catch (Exception ex)
            {
                LogToCli($"Error checking dependencies: {ex.Message}");
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
                        LogToCli("Console output copied to clipboard");
                    }
                    else
                    {
                        LogToCli("Clipboard not available");
                    }
                }
            }
            catch (Exception ex)
            {
                LogToCli($"Error copying to clipboard: {ex.Message}");
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