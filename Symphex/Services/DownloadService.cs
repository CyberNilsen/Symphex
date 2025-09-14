using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CliWrap;
using Symphex.Models;
using Symphex.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Symphex.Services
{
    public class DownloadService
    {
        private readonly string _downloadFolder;
        private readonly string _ytDlpPath;
        private readonly string _ffmpegPath;
        private readonly AlbumArtSearchService _albumArtSearchService;

        public DownloadService(string downloadFolder, string ytDlpPath, string ffmpegPath, AlbumArtSearchService albumArtSearchService)
        {
            _downloadFolder = downloadFolder;
            _ytDlpPath = ytDlpPath;
            _ffmpegPath = ffmpegPath;
            _albumArtSearchService = albumArtSearchService;
        }

        public async Task<TrackInfo?> ExtractMetadata(string url)
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

                var result = await Cli.Wrap(_ytDlpPath)
                    .WithArguments(string.Join(" ", args))
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(error))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();

                var outputText = output.ToString().Trim();
                var errorText = error.ToString();

                if (result.ExitCode != 0)
                {
                    return null;
                }

                if (string.IsNullOrEmpty(outputText))
                {
                    return null;
                }

                // Parse the JSON response
                var lines = outputText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var jsonLine = lines.FirstOrDefault(line => line.Trim().StartsWith("{"));

                if (string.IsNullOrEmpty(jsonLine))
                {
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
                await _albumArtSearchService.FindRealAlbumArt(trackInfo);

                return trackInfo;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<string> PerformDownload(string url, TrackInfo? trackInfo)
        {
            bool isUrl = url.StartsWith("http://") || url.StartsWith("https://");
            string fullUrl = isUrl ? url : $"ytsearch1:{url}";

            var output = new StringBuilder();
            var error = new StringBuilder();

            // Create filename based on cleaned metadata
            string filenameTemplate;
            if (trackInfo != null && !string.IsNullOrEmpty(trackInfo.Title) && !string.IsNullOrEmpty(trackInfo.Artist))
            {
                string cleanTitle = SanitizeFilename(trackInfo.Title);
                string cleanArtist = SanitizeFilename(trackInfo.Artist);
                filenameTemplate = Path.Combine(_downloadFolder, $"{cleanArtist} - {cleanTitle}.%(ext)s");
            }
            else
            {
                // Fallback to default naming
                filenameTemplate = Path.Combine(_downloadFolder, "%(uploader)s - %(title)s.%(ext)s");
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
            if (!string.IsNullOrEmpty(_ffmpegPath) && File.Exists(_ffmpegPath))
            {
                string ffmpegDir = Path.GetDirectoryName(_ffmpegPath) ?? "";
                argsList.AddRange(new[] { "--ffmpeg-location", $"\"{ffmpegDir}\"" });
            }

            string args = string.Join(" ", argsList);

            var result = await Cli.Wrap(_ytDlpPath)
                .WithArguments(args)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(error))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync();

            var outputText = output.ToString();
            var errorText = error.ToString();

            if (result.ExitCode != 0)
            {
                throw new Exception($"Download failed with exit code {result.ExitCode}: {errorText}");
            }

            return await FindDownloadedFile(outputText, trackInfo);
        }

        public async Task ApplyMetadata(TrackInfo trackInfo, string audioFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(_ffmpegPath) || !File.Exists(audioFilePath))
                {
                    return;
                }

                string tempOutput = Path.Combine(_downloadFolder, $"temp_{Guid.NewGuid():N}.mp3");
                var argsList = new List<string>();
                argsList.AddRange(new[] { "-i", audioFilePath });

                Bitmap? artworkToUse = trackInfo.AlbumArt ?? trackInfo.Thumbnail;

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
                if (!string.IsNullOrEmpty(trackInfo.Title) && trackInfo.Title != "Unknown")
                {
                    argsList.AddRange(new[] { "-metadata", $"title={trackInfo.Title}" });
                }

                if (!string.IsNullOrEmpty(trackInfo.Artist) && trackInfo.Artist != "Unknown")
                {
                    argsList.AddRange(new[] { "-metadata", $"artist={trackInfo.Artist}" });
                }

                if (!string.IsNullOrEmpty(trackInfo.Album))
                {
                    argsList.AddRange(new[] { "-metadata", $"album={trackInfo.Album}" });
                }

                if (!string.IsNullOrEmpty(trackInfo.AlbumArtist))
                {
                    argsList.AddRange(new[] { "-metadata", $"albumartist={trackInfo.AlbumArtist}" });
                    argsList.AddRange(new[] { "-metadata", $"album_artist={trackInfo.AlbumArtist}" });
                }

                if (!string.IsNullOrEmpty(trackInfo.Genre))
                {
                    argsList.AddRange(new[] { "-metadata", $"genre={trackInfo.Genre}" });
                }

                if (!string.IsNullOrEmpty(trackInfo.Year))
                {
                    argsList.AddRange(new[] { "-metadata", $"date={trackInfo.Year}" });
                    argsList.AddRange(new[] { "-metadata", $"year={trackInfo.Year}" });
                }

                if (trackInfo.TrackNumber > 0)
                {
                    argsList.AddRange(new[] { "-metadata", $"track={trackInfo.TrackNumber}" });
                }

                if (trackInfo.DiscNumber > 0)
                {
                    argsList.AddRange(new[] { "-metadata", $"disc={trackInfo.DiscNumber}" });
                }

                if (!string.IsNullOrEmpty(trackInfo.Comment))
                {
                    argsList.AddRange(new[] { "-metadata", $"comment={trackInfo.Comment}" });
                }

                if (!string.IsNullOrEmpty(trackInfo.Composer))
                {
                    argsList.AddRange(new[] { "-metadata", $"composer={trackInfo.Composer}" });
                }

                if (!string.IsNullOrEmpty(trackInfo.Encoder))
                {
                    argsList.AddRange(new[] { "-metadata", $"encoded_by={trackInfo.Encoder}" });
                }

                // Add URL as metadata for reference
                if (!string.IsNullOrEmpty(trackInfo.Url))
                {
                    argsList.AddRange(new[] { "-metadata", $"website={trackInfo.Url}" });
                }

                argsList.Add(tempOutput);

                var output = new StringBuilder();
                var error = new StringBuilder();

                var result = await Cli.Wrap(_ffmpegPath)
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

        public async Task<TrackInfo?> ExtractMetadataWithAlbumArt(string url, int threadIndex)
        {
            try
            {
                // Use the existing ExtractMetadata method
                var trackInfo = await ExtractMetadata(url);

                if (trackInfo == null) return null;

                // Now find real album art (this was the missing piece!)
                await _albumArtSearchService.FindRealAlbumArtForBatch(trackInfo, threadIndex);

                return trackInfo;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task ApplyMetadataForBatch(TrackInfo trackInfo, string audioFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(_ffmpegPath) || !File.Exists(audioFilePath))
                    return;

                string tempOutput = Path.Combine(_downloadFolder, $"temp_{Guid.NewGuid():N}.mp3");
                var argsList = new List<string>();
                argsList.AddRange(new[] { "-i", audioFilePath });

                // Handle album art embedding
                Bitmap? artworkToUse = trackInfo.AlbumArt ?? trackInfo.Thumbnail;
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
                    catch
                    {
                        tempArtwork = null;
                        argsList.AddRange(new[] { "-c", "copy" });
                    }
                }
                else
                {
                    argsList.AddRange(new[] { "-c", "copy" });
                }

                // Apply comprehensive metadata
                argsList.AddRange(new[] { "-id3v2_version", "3" });

                if (!string.IsNullOrEmpty(trackInfo.Title) && trackInfo.Title != "Unknown")
                {
                    argsList.AddRange(new[] { "-metadata", $"title={trackInfo.Title}" });
                }

                if (!string.IsNullOrEmpty(trackInfo.Artist) && trackInfo.Artist != "Unknown")
                {
                    argsList.AddRange(new[] { "-metadata", $"artist={trackInfo.Artist}" });
                }

                if (!string.IsNullOrEmpty(trackInfo.Album))
                {
                    argsList.AddRange(new[] { "-metadata", $"album={trackInfo.Album}" });
                }

                if (!string.IsNullOrEmpty(trackInfo.AlbumArtist))
                {
                    argsList.AddRange(new[] { "-metadata", $"albumartist={trackInfo.AlbumArtist}" });
                }

                if (!string.IsNullOrEmpty(trackInfo.Genre))
                {
                    argsList.AddRange(new[] { "-metadata", $"genre={trackInfo.Genre}" });
                }

                if (!string.IsNullOrEmpty(trackInfo.Year))
                {
                    argsList.AddRange(new[] { "-metadata", $"date={trackInfo.Year}" });
                }

                if (trackInfo.TrackNumber > 0)
                {
                    argsList.AddRange(new[] { "-metadata", $"track={trackInfo.TrackNumber}" });
                }

                argsList.Add(tempOutput);

                var result = await Cli.Wrap(_ffmpegPath)
                    .WithArguments(argsList)
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();

                try
                {
                    if (result.ExitCode == 0 && File.Exists(tempOutput))
                    {
                        File.Delete(audioFilePath);
                        File.Move(tempOutput, audioFilePath);
                    }
                    else if (File.Exists(tempOutput))
                    {
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
                // Silent fail for metadata application in batch mode
            }
        }

        public async Task<string> DownloadForBatch(string url, TrackInfo trackInfo, int index)
        {
            try
            {
                bool isDirectUrl = url.StartsWith("http://") || url.StartsWith("https://");
                string fullUrl = isDirectUrl ? url : $"ytsearch1:{url}";

                var output = new StringBuilder();
                var error = new StringBuilder();

                // Create filename based on track info
                string filenameTemplate;
                if (!string.IsNullOrEmpty(trackInfo.Title) && !string.IsNullOrEmpty(trackInfo.Artist))
                {
                    string cleanTitle = SanitizeFilename(trackInfo.Title);
                    string cleanArtist = SanitizeFilename(trackInfo.Artist);
                    filenameTemplate = Path.Combine(_downloadFolder, $"{cleanArtist} - {cleanTitle}.%(ext)s");
                }
                else
                {
                    filenameTemplate = Path.Combine(_downloadFolder, "%(uploader)s - %(title)s.%(ext)s");
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
                if (!string.IsNullOrEmpty(_ffmpegPath) && File.Exists(_ffmpegPath))
                {
                    string ffmpegDir = Path.GetDirectoryName(_ffmpegPath) ?? "";
                    argsList.AddRange(new[] { "--ffmpeg-location", $"\"{ffmpegDir}\"" });
                }

                string args = string.Join(" ", argsList);

                var result = await Cli.Wrap(_ytDlpPath)
                    .WithArguments(args)
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(error))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();

                if (result.ExitCode == 0)
                {
                    string actualFilePath = await FindDownloadedFile(output.ToString(), trackInfo);
                    if (!string.IsNullOrEmpty(actualFilePath))
                    {
                        trackInfo.FileName = Path.GetFileName(actualFilePath);
                        // Apply metadata if available
                        await ApplyMetadataForBatch(trackInfo, actualFilePath);
                        return actualFilePath;
                    }
                }
                else
                {
                    throw new Exception($"Download failed with exit code {result.ExitCode}");
                }

                return "";
            }
            catch (Exception ex)
            {
                throw new Exception($"Batch download failed: {ex.Message}");
            }
        }

        // Private helper methods
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

        private async Task<Bitmap?> LoadImageAsync(string url)
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
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

        private async Task<string> FindDownloadedFile(string ytDlpOutput, TrackInfo? trackInfo)
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
                if (Directory.Exists(_downloadFolder))
                {
                    var recentFiles = Directory.GetFiles(_downloadFolder, "*.mp3")
                        .Where(f => File.GetCreationTime(f) > DateTime.Now.AddMinutes(-2))
                        .OrderByDescending(f => File.GetCreationTime(f))
                        .ToArray();

                    if (recentFiles.Length > 0)
                    {
                        return recentFiles[0];
                    }
                }

                // Final fallback: construct expected filename
                if (trackInfo != null && !string.IsNullOrEmpty(trackInfo.Title) && !string.IsNullOrEmpty(trackInfo.Artist))
                {
                    string cleanTitle = SanitizeFilename(trackInfo.Title);
                    string cleanArtist = SanitizeFilename(trackInfo.Artist);
                    string expectedFilename = $"{cleanArtist} - {cleanTitle}.mp3";
                    return Path.Combine(_downloadFolder, expectedFilename);
                }

                return "";
            }
            catch (Exception ex)
            {
                return "";
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
    }
}