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
        private bool _enableAlbumArt = true;
        private bool _skipThumbnailDownload = false; // false = download thumbnails (inverted logic)
        private string _selectedThumbnailSize = "Medium Quality (600x600)";
        private string _selectedAudioFormat = "MP3 (320kbps)"; // Default audio format

        // Callback for download completion
        public Action<TrackInfo, string>? OnDownloadCompleted { get; set; }


        public DownloadService(string downloadFolder, string ytDlpPath, string ffmpegPath, AlbumArtSearchService albumArtSearchService, bool enableAlbumArt = true)
        {
            _downloadFolder = downloadFolder;
            _ytDlpPath = ytDlpPath;
            _ffmpegPath = ffmpegPath;
            _albumArtSearchService = albumArtSearchService;
            _enableAlbumArt = enableAlbumArt;
        }

        public void UpdateAlbumArtSetting(bool enabled)
        {
            _enableAlbumArt = enabled;
            Debug.WriteLine($"[DownloadService] Album art download {(enabled ? "enabled" : "disabled")}");
        }

        public void UpdateThumbnailSettings(bool skipThumbnail)
        {
            _skipThumbnailDownload = skipThumbnail;
            Debug.WriteLine($"[DownloadService] Thumbnail download {(skipThumbnail ? "disabled" : "enabled")}");
        }

        public void UpdateThumbnailSize(string size)
        {
            _selectedThumbnailSize = size;
            Debug.WriteLine($"[DownloadService] Thumbnail size set to: {size}");
        }

        public void UpdateAudioFormat(string format)
        {
            _selectedAudioFormat = format;
            Debug.WriteLine($"[DownloadService] Audio format set to: {format}");
        }

        public async Task<TrackInfo?> ExtractMetadata(string url, string thumbnailSizePreference = "Maximum Quality", bool skipThumbnail = false)
        {
            try
            {
                Debug.WriteLine($"[ExtractMetadata] Settings: EnableAlbumArt={_enableAlbumArt}, SkipThumbnail={skipThumbnail}, Size={thumbnailSizePreference}");
                
                // Validate yt-dlp exists before attempting to use it
                if (!File.Exists(_ytDlpPath))
                {
                    Debug.WriteLine($"[ExtractMetadata] ERROR: yt-dlp not found at: {_ytDlpPath}");
                    throw new FileNotFoundException($"yt-dlp not found at: {_ytDlpPath}");
                }

                // Better URL detection - check for any youtube/spotify/soundcloud domains
                bool isDirectUrl = IsValidUrl(url);
                string fullUrl = isDirectUrl ? url : $"ytsearch1:{url}";

                var output = new StringBuilder();
                var error = new StringBuilder();

                var args = new List<string>
        {
            fullUrl,
            "--dump-json",
            "--no-playlist",
            "--no-warnings",
            "--quiet"
        };

                Debug.WriteLine($"[ExtractMetadata] Executing: {_ytDlpPath}");
                Debug.WriteLine($"[ExtractMetadata] URL detected as: {(isDirectUrl ? "Direct URL" : "Search query")}");
                Debug.WriteLine($"[ExtractMetadata] Full URL: {fullUrl}");

                var result = await Cli.Wrap(_ytDlpPath)
                    .WithArguments(args)
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(error))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();

                if (result.ExitCode != 0)
                {
                    Debug.WriteLine($"[ExtractMetadata] yt-dlp exited with code {result.ExitCode}");
                    Debug.WriteLine($"[ExtractMetadata] Error output: {error}");
                    return null;
                }

                var outputText = output.ToString().Trim();
                if (string.IsNullOrEmpty(outputText))
                {
                    Debug.WriteLine("[ExtractMetadata] No output from yt-dlp");
                    return null;
                }

                var jsonLine = outputText.Split('\n').FirstOrDefault(line => line.Trim().StartsWith("{"));
                if (string.IsNullOrEmpty(jsonLine))
                {
                    Debug.WriteLine("[ExtractMetadata] No JSON line found in yt-dlp output");
                    return null;
                }

                using var doc = JsonDocument.Parse(jsonLine);
                var root = doc.RootElement;

                string rawTitle = root.TryGetProperty("title", out var title) ? title.GetString() ?? "Unknown" : "Unknown";
                string rawUploader = root.TryGetProperty("uploader", out var uploader) ? uploader.GetString() ?? "Unknown" : "Unknown";

                string finalArtist = "Unknown";
                string finalTitle = rawTitle;

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
                    finalArtist = CleanArtistName(rawUploader);
                    finalTitle = CleanSongTitle(rawTitle);
                }

                string albumInfo = root.TryGetProperty("album", out var album) ? album.GetString() ?? "" : "";

                string yearInfo = "";
                if (root.TryGetProperty("upload_date", out var uploadDate))
                {
                    string dateStr = uploadDate.GetString() ?? "";
                    if (dateStr.Length >= 4) yearInfo = dateStr.Substring(0, 4);
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

                // Handle artwork based on settings
                // NOTE: SkipThumbnailDownload is now INVERTED - checked = download, unchecked = skip
                bool actuallySkipThumbnail = !skipThumbnail;
                
                if (!_enableAlbumArt && actuallySkipThumbnail)
                {
                    // BOTH disabled - absolutely NO artwork
                    trackInfo.AlbumArt = null;
                    trackInfo.Thumbnail = null;
                    trackInfo.HasRealAlbumArt = false;
                    Debug.WriteLine("[ExtractMetadata] ALL artwork disabled - no pictures");
                    return trackInfo;
                }
                else if (!_enableAlbumArt && !actuallySkipThumbnail)
                {
                    // Album art disabled, thumbnail enabled - load thumbnail only
                    Bitmap? thumbnailImage = await LoadThumbnailBySize(root, thumbnailSizePreference);
                    if (thumbnailImage != null)
                    {
                        // Resize thumbnail
                        var (targetWidth, targetHeight) = GetTargetDimensionsFromSize(_selectedThumbnailSize);
                        var resized = ResizeImage(thumbnailImage, targetWidth, targetHeight);
                        if (resized != null)
                        {
                            trackInfo.Thumbnail = resized;
                            trackInfo.AlbumArt = resized; // Use thumbnail as album art
                            Debug.WriteLine($"[ExtractMetadata] Resized thumbnail to {resized.PixelSize.Width}x{resized.PixelSize.Height}");
                        }
                        else
                        {
                            trackInfo.Thumbnail = thumbnailImage;
                            trackInfo.AlbumArt = thumbnailImage;
                        }
                    }
                    trackInfo.HasRealAlbumArt = false;
                    Debug.WriteLine("[ExtractMetadata] Album art disabled - using thumbnail only");
                    return trackInfo;
                }
                else if (_enableAlbumArt && actuallySkipThumbnail)
                {
                    // Album art enabled, thumbnail skipped - search for album art only
                    trackInfo.Thumbnail = null;
                    trackInfo.HasRealAlbumArt = false;
                    Debug.WriteLine("[ExtractMetadata] Thumbnail skipped - will search for album art only");
                    
                    try
                    {
                        await _albumArtSearchService.FindRealAlbumArt(trackInfo);
                        
                        // Resize album art if found
                        if (trackInfo.AlbumArt != null)
                        {
                            var (targetWidth, targetHeight) = GetTargetDimensionsFromSize(_selectedThumbnailSize);
                            var resized = ResizeImage(trackInfo.AlbumArt, targetWidth, targetHeight);
                            if (resized != null)
                            {
                                trackInfo.AlbumArt = resized;
                                Debug.WriteLine($"[ExtractMetadata] Resized album art to {resized.PixelSize.Width}x{resized.PixelSize.Height}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ExtractMetadata] Album art search failed: {ex.Message}");
                    }
                    return trackInfo;
                }
                else
                {
                    // BOTH enabled - load thumbnail first, then search for album art
                    Bitmap? thumbnailImage = await LoadThumbnailBySize(root, thumbnailSizePreference);
                    if (thumbnailImage != null)
                    {
                        trackInfo.Thumbnail = thumbnailImage;
                    }
                    else
                    {
                        // Fallback to best thumbnail URL if size-based loading fails
                        string? thumbnailUrl = GetBestThumbnailUrl(root);
                        if (!string.IsNullOrEmpty(thumbnailUrl))
                        {
                            try
                            {
                                trackInfo.Thumbnail = await LoadImageAsync(thumbnailUrl);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[ExtractMetadata] Failed to load thumbnail: {ex.Message}");
                            }
                        }
                    }

                    // Store available thumbnails for user selection
                    trackInfo.AvailableThumbnails = ExtractAllThumbnails(root);

                    // Search for real album art
                    try
                    {
                        await _albumArtSearchService.FindRealAlbumArt(trackInfo);
                        
                        // Resize album art if found
                        if (trackInfo.AlbumArt != null)
                        {
                            var (targetWidth, targetHeight) = GetTargetDimensionsFromSize(_selectedThumbnailSize);
                            var resized = ResizeImage(trackInfo.AlbumArt, targetWidth, targetHeight);
                            if (resized != null)
                            {
                                trackInfo.AlbumArt = resized;
                                Debug.WriteLine($"[ExtractMetadata] Resized album art to {resized.PixelSize.Width}x{resized.PixelSize.Height}");
                            }
                        }
                        
                        // Also resize thumbnail if present
                        if (trackInfo.Thumbnail != null)
                        {
                            var (targetWidth, targetHeight) = GetTargetDimensionsFromSize(_selectedThumbnailSize);
                            var resized = ResizeImage(trackInfo.Thumbnail, targetWidth, targetHeight);
                            if (resized != null)
                            {
                                trackInfo.Thumbnail = resized;
                                Debug.WriteLine($"[ExtractMetadata] Resized thumbnail to {resized.PixelSize.Width}x{resized.PixelSize.Height}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ExtractMetadata] Album art search failed: {ex.Message}");
                    }
                    return trackInfo;
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Debug.WriteLine($"[ExtractMetadata] Win32Exception - Cannot execute yt-dlp: {ex.Message}");
                Debug.WriteLine($"[ExtractMetadata] yt-dlp path: {_ytDlpPath}");
                Debug.WriteLine($"[ExtractMetadata] File exists: {File.Exists(_ytDlpPath)}");
                throw new Exception($"Cannot execute yt-dlp. Please reinstall it. Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExtractMetadata] Exception: {ex.Message}");
                Debug.WriteLine($"[ExtractMetadata] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        public async Task<string> PerformDownload(string url, TrackInfo? trackInfo)
        {
            try
            {
                // Validate yt-dlp exists
                if (!File.Exists(_ytDlpPath))
                {
                    Debug.WriteLine($"[PerformDownload] ERROR: yt-dlp not found at: {_ytDlpPath}");
                    throw new FileNotFoundException($"yt-dlp not found at: {_ytDlpPath}");
                }

                // Better URL detection
                bool isUrl = IsValidUrl(url);
                string fullUrl = isUrl ? url : $"ytsearch1:{url}";

                Debug.WriteLine($"[PerformDownload] URL detected as: {(isUrl ? "Direct URL" : "Search query")}");
                Debug.WriteLine($"[PerformDownload] Original input: {url}");
                Debug.WriteLine($"[PerformDownload] Full URL: {fullUrl}");

                string filenameTemplate;
                if (trackInfo != null && !string.IsNullOrEmpty(trackInfo.Title) && !string.IsNullOrEmpty(trackInfo.Artist))
                {
                    string cleanTitle = SanitizeFilename(trackInfo.Title);
                    string cleanArtist = SanitizeFilename(trackInfo.Artist);
                    filenameTemplate = Path.Combine(_downloadFolder, $"{cleanArtist} - {cleanTitle}.%(ext)s");
                }
                else filenameTemplate = Path.Combine(_downloadFolder, "%(uploader)s - %(title)s.%(ext)s");

                // Parse audio format selection
                var (audioFormat, audioQuality) = ParseAudioFormat(_selectedAudioFormat);

                var argsList = new List<string>
        {
            fullUrl,
            "--extract-audio",
            "--audio-format", audioFormat,
            "--audio-quality", audioQuality,
            "--no-playlist"
        };

                // Only embed thumbnail if user wants thumbnails (inverted logic: _skipThumbnailDownload=true means download)
                bool actuallySkipThumbnail = !_skipThumbnailDownload;
                if (!actuallySkipThumbnail)
                {
                    argsList.Add("--embed-thumbnail");
                }

                argsList.Add("--add-metadata");
                argsList.Add("-o");
                argsList.Add(filenameTemplate);

                // Only add ffmpeg location if it actually exists
                if (!string.IsNullOrEmpty(_ffmpegPath) && File.Exists(_ffmpegPath))
                {
                    string ffmpegDir = Path.GetDirectoryName(_ffmpegPath) ?? "";
                    if (Directory.Exists(ffmpegDir))
                    {
                        argsList.AddRange(new[] { "--ffmpeg-location", ffmpegDir });
                        Debug.WriteLine($"[PerformDownload] Using FFmpeg from: {ffmpegDir}");
                    }
                }
                else
                {
                    Debug.WriteLine("[PerformDownload] FFmpeg path not set or file doesn't exist - using system FFmpeg");
                }

                Debug.WriteLine($"[PerformDownload] Executing: {_ytDlpPath}");
                Debug.WriteLine($"[PerformDownload] Arguments: {string.Join(" ", argsList)}");

                var outputLines = new List<string>();
                var errorLines = new List<string>();

                var result = await Cli.Wrap(_ytDlpPath)
                    .WithArguments(argsList)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
                    {
                        Debug.WriteLine($"[yt-dlp stdout] {line}");
                        outputLines.Add(line);
                    }))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
                    {
                        Debug.WriteLine($"[yt-dlp stderr] {line}");
                        errorLines.Add(line);
                    }))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();

                if (result.ExitCode != 0)
                {
                    Debug.WriteLine($"[PerformDownload] yt-dlp failed with code {result.ExitCode}");
                    Debug.WriteLine($"[PerformDownload] Error output:\n{string.Join("\n", errorLines)}");
                    throw new Exception($"yt-dlp exited with code {result.ExitCode}. Check debug output for details.");
                }

                string actualFilePath = await FindDownloadedFile(string.Join("\n", outputLines), trackInfo);

                if (string.IsNullOrEmpty(actualFilePath))
                {
                    Debug.WriteLine("[PerformDownload] ERROR: Could not find downloaded file");
                    Debug.WriteLine($"[PerformDownload] yt-dlp output:\n{string.Join("\n", outputLines)}");
                    throw new Exception("Download completed but file not found");
                }

                Debug.WriteLine($"[PerformDownload] Completed: {actualFilePath}");
                
                // Notify download completion for history tracking
                if (trackInfo != null)
                {
                    OnDownloadCompleted?.Invoke(trackInfo, actualFilePath);
                }
                
                return actualFilePath;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Debug.WriteLine($"[PerformDownload] Win32Exception - Cannot execute yt-dlp: {ex.Message}");
                Debug.WriteLine($"[PerformDownload] yt-dlp path: {_ytDlpPath}");
                Debug.WriteLine($"[PerformDownload] File exists: {File.Exists(_ytDlpPath)}");
                throw new Exception($"Cannot execute yt-dlp at '{_ytDlpPath}'. Please check if the file exists and is not corrupted. Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PerformDownload] Exception: {ex.Message}");
                Debug.WriteLine($"[PerformDownload] Stack trace: {ex.StackTrace}");
                throw;
            }
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

                // Determine which artwork to use: album art (if enabled) or thumbnail (as fallback)
                Bitmap? artworkToUse = null;
                if (_enableAlbumArt)
                {
                    artworkToUse = trackInfo.AlbumArt ?? trackInfo.Thumbnail;
                }
                else
                {
                    // Album art disabled, but still use thumbnail if available
                    artworkToUse = trackInfo.Thumbnail;
                }

                string? tempArtwork = null;
                if (artworkToUse != null)
                {
                    tempArtwork = Path.Combine(Path.GetTempPath(), $"temp_artwork_{Guid.NewGuid():N}.jpg");

                    try
                    {
                        // Resize artwork based on selected size
                        var (targetWidth, targetHeight) = GetTargetDimensionsFromSize(_selectedThumbnailSize);
                        var resizedArtwork = ResizeImage(artworkToUse, targetWidth, targetHeight);
                        
                        if (resizedArtwork != null)
                        {
                            using (var fileStream = new FileStream(tempArtwork, FileMode.Create))
                            {
                                resizedArtwork.Save(fileStream);
                            }
                            Debug.WriteLine($"[ApplyMetadata] Saved resized artwork: {resizedArtwork.PixelSize.Width}x{resizedArtwork.PixelSize.Height}");
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
                        Debug.WriteLine($"[ApplyMetadata] Failed to process artwork: {ex}");
                    }
                }
                else
                {
                    argsList.AddRange(new[] { "-c", "copy" });
                    Debug.WriteLine("[ApplyMetadata] No artwork to embed");
                }

                // Rest of metadata application continues as normal...
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
                Debug.WriteLine($"[ApplyMetadata] Exception: {ex}");
            }
        }

        public async Task<TrackInfo?> ExtractMetadataWithAlbumArt(string url, int threadIndex)
        {
            try
            {
                var trackInfo = await ExtractMetadata(url);

                if (trackInfo == null) return null;

                // Only find album art if enabled (ExtractMetadata already handles this, but double-check for batch)
                if (_enableAlbumArt)
                {
                    await _albumArtSearchService.FindRealAlbumArtForBatch(trackInfo, threadIndex);
                    
                    // Resize album art if found
                    if (trackInfo.AlbumArt != null)
                    {
                        var (targetWidth, targetHeight) = GetTargetDimensionsFromSize(_selectedThumbnailSize);
                        var resized = ResizeImage(trackInfo.AlbumArt, targetWidth, targetHeight);
                        if (resized != null)
                        {
                            trackInfo.AlbumArt = resized;
                            Debug.WriteLine($"[ExtractMetadataWithAlbumArt #{threadIndex}] Resized album art to {resized.PixelSize.Width}x{resized.PixelSize.Height}");
                        }
                    }
                    
                    // Also resize thumbnail if present
                    if (trackInfo.Thumbnail != null)
                    {
                        var (targetWidth, targetHeight) = GetTargetDimensionsFromSize(_selectedThumbnailSize);
                        var resized = ResizeImage(trackInfo.Thumbnail, targetWidth, targetHeight);
                        if (resized != null)
                        {
                            trackInfo.Thumbnail = resized;
                            Debug.WriteLine($"[ExtractMetadataWithAlbumArt #{threadIndex}] Resized thumbnail to {resized.PixelSize.Width}x{resized.PixelSize.Height}");
                        }
                    }
                }
                else
                {
                    trackInfo.AlbumArt = null;
                    trackInfo.Thumbnail = null;
                    trackInfo.HasRealAlbumArt = false;
                    Debug.WriteLine($"[ExtractMetadataWithAlbumArt #{threadIndex}] Album art disabled - no artwork loaded");
                }

                return trackInfo;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExtractMetadataWithAlbumArt] Exception: {ex}");
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
                        // Resize artwork based on selected size
                        var (targetWidth, targetHeight) = GetTargetDimensionsFromSize(_selectedThumbnailSize);
                        var resizedArtwork = ResizeImage(artworkToUse, targetWidth, targetHeight);
                        
                        if (resizedArtwork != null)
                        {
                            using (var fileStream = new FileStream(tempArtwork, FileMode.Create))
                            {
                                resizedArtwork.Save(fileStream);
                            }
                            Debug.WriteLine($"[ApplyMetadataForBatch] Saved resized artwork: {resizedArtwork.PixelSize.Width}x{resizedArtwork.PixelSize.Height}");
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
                Debug.WriteLine($"[ApplyMetadataForBatch] Exception: {ex}");
            }
        }

        public async Task<string> DownloadForBatch(string url, TrackInfo trackInfo, int index)
        {
            try
            {
                // Validate yt-dlp exists
                if (!File.Exists(_ytDlpPath))
                {
                    Debug.WriteLine($"[DownloadForBatch] ERROR: yt-dlp not found at: {_ytDlpPath}");
                    return "";
                }

                // Better URL detection
                bool isDirectUrl = IsValidUrl(url);
                string fullUrl = isDirectUrl ? url : $"ytsearch1:{url}";

                var output = new StringBuilder();
                var error = new StringBuilder();

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

                // Parse audio format selection
                var (audioFormat, audioQuality) = ParseAudioFormat(_selectedAudioFormat);

                List<string> argsList = new List<string>
        {
            fullUrl,
            "--extract-audio",
            "--audio-format", audioFormat,
            "--audio-quality", audioQuality,
            "--no-playlist"
        };

                // Only embed thumbnail if user wants thumbnails (inverted logic: _skipThumbnailDownload=true means download)
                bool actuallySkipThumbnail = !_skipThumbnailDownload;
                if (!actuallySkipThumbnail)
                {
                    argsList.Add("--embed-thumbnail");
                }

                argsList.Add("--add-metadata");
                argsList.Add("-o");
                argsList.Add(filenameTemplate);

                // Add FFmpeg location if available and valid
                if (!string.IsNullOrEmpty(_ffmpegPath) && File.Exists(_ffmpegPath))
                {
                    string ffmpegDir = Path.GetDirectoryName(_ffmpegPath) ?? "";
                    if (Directory.Exists(ffmpegDir))
                    {
                        argsList.AddRange(new[] { "--ffmpeg-location", ffmpegDir });
                    }
                }

                Debug.WriteLine($"[DownloadForBatch #{index}] Downloading: {fullUrl}");

                var result = await Cli.Wrap(_ytDlpPath)
                    .WithArguments(argsList)
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
                        await ApplyMetadataForBatch(trackInfo, actualFilePath);
                        Debug.WriteLine($"[DownloadForBatch #{index}] Success: {actualFilePath}");
                        
                        // Notify download completion
                        OnDownloadCompleted?.Invoke(trackInfo, actualFilePath);
                        
                        return actualFilePath;
                    }
                    else
                    {
                        Debug.WriteLine($"[DownloadForBatch #{index}] Download succeeded but file not found");
                    }
                }
                else
                {
                    Debug.WriteLine($"[DownloadForBatch #{index}] yt-dlp failed with code {result.ExitCode}");
                    Debug.WriteLine($"[DownloadForBatch #{index}] Error: {error}");
                }

                return "";
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Debug.WriteLine($"[DownloadForBatch #{index}] Win32Exception: {ex.Message}");
                Debug.WriteLine($"[DownloadForBatch #{index}] yt-dlp path: {_ytDlpPath}");
                return "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DownloadForBatch #{index}] Exception: {ex.Message}");
                return "";
            }
        }

        private bool IsValidUrl(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Direct protocol check
            if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return true;

            // Check for common music platforms without protocol
            var domains = new[]
            {
        "youtube.com", "youtu.be", "music.youtube.com",
        "spotify.com", "open.spotify.com",
        "soundcloud.com",
        "bandcamp.com",
        "vimeo.com",
        "dailymotion.com"
    };

            string lowerInput = input.ToLower();
            foreach (var domain in domains)
            {
                if (lowerInput.Contains(domain))
                {
                    // It's a URL without protocol - add https://
                    return true;
                }
            }

            return false;
        }

        // Private helper methods
        private string CleanSongTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return "Unknown";

            string cleaned = title;

            // Remove ALL parentheses and their contents
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s*\([^)]*\)", "", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Remove ALL square brackets and their contents
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s*\[[^\]]*\]", "", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Remove common suffixes and patterns that might not be in brackets
            var patterns = new[]
            {
                @"\s*Official\s*Video",
                @"\s*Official\s*Audio",
                @"\s*Official\s*Music\s*Video",
                @"\s*Music\s*Video",
                @"\s*Lyric\s*Video",
                @"\s*Lyrics?",
                @"\s*Visualizer",
                @"\s*Live\s*Performance",
                @"\s*Live",
                @"\s*HD",
                @"\s*4K",
                @"\s*HQ",
                @"\s*Audio\s*Only",
                @"\s*Full\s*Song",
                @"\s*Full\s*Video",
                @"\s*Remaster(ed)?",
                @"\s*\d{4}\s*Remaster",
                @"\s*Extended\s*Version",
                @"\s*Radio\s*Edit",
                @"\s*Explicit",
                @"\s*Clean\s*Version",
                @"\s+ft\.?\s+.*$",  // Remove featuring artists at the end (word boundary)
                @"\s+feat\.?\s+.*$",
                @"\s+featuring\s+.*$"
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
                .Replace("|", " ")
                .Replace("–", " ") // En dash
                .Replace("—", " ") // Em dash
                .Trim();

            // Remove multiple spaces
            while (cleaned.Contains("  "))
            {
                cleaned = cleaned.Replace("  ", " ");
            }

            Debug.WriteLine($"[CleanSongTitle] Original: '{title}' -> Cleaned: '{cleaned}'");
            return string.IsNullOrEmpty(cleaned) ? title : cleaned;
        }

        private string CleanArtistName(string artist)
        {
            if (string.IsNullOrEmpty(artist))
                return "Unknown";

            string cleaned = artist;

            // Remove ALL parentheses and their contents
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s*\([^)]*\)", "", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Remove ALL square brackets and their contents
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s*\[[^\]]*\]", "", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Clean common channel suffixes and patterns
            var patterns = new[]
            {
                @"\s*-\s*Topic",
                @"\s*VEVO",
                @"\s*Records",
                @"\s*Music",
                @"\s*Official",
                @"\s*Channel",
                @"\s*TV",
                @"\s*Entertainment",
                @"\s*Productions?",
                @"\s*Studios?",
                @"\s*Label"
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
                .Replace("-", " ")
                .Replace("|", " ")
                .Replace("–", " ")
                .Replace("—", " ")
                .Trim();

            // Remove multiple spaces
            while (cleaned.Contains("  "))
            {
                cleaned = cleaned.Replace("  ", " ");
            }

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

        public List<ThumbnailOption> ExtractAllThumbnails(JsonElement root)
        {
            var thumbnailOptions = new List<ThumbnailOption>();

            try
            {
                if (root.TryGetProperty("thumbnails", out var thumbnailsProp))
                {
                    foreach (var thumb in thumbnailsProp.EnumerateArray())
                    {
                        if (thumb.TryGetProperty("url", out var urlProp))
                        {
                            string url = urlProp.GetString() ?? "";
                            int width = thumb.TryGetProperty("width", out var widthProp) ? widthProp.GetInt32() : 0;
                            int height = thumb.TryGetProperty("height", out var heightProp) ? heightProp.GetInt32() : 0;

                            if (!string.IsNullOrEmpty(url) && width > 0 && height > 0)
                            {
                                string quality = DetermineQuality(width, height);
                                thumbnailOptions.Add(new ThumbnailOption
                                {
                                    Url = url,
                                    Width = width,
                                    Height = height,
                                    Quality = quality
                                });
                            }
                        }
                    }
                }

                // Sort by resolution (highest first)
                thumbnailOptions = thumbnailOptions
                    .OrderByDescending(t => t.Width * t.Height)
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExtractAllThumbnails] Error: {ex.Message}");
            }

            return thumbnailOptions;
        }

        private string DetermineQuality(int width, int height)
        {
            int pixels = width * height;

            if (pixels >= 1440000) // 1200x1200 or higher
                return "Maximum Quality";
            else if (pixels >= 360000) // 600x600 or higher
                return "High Quality";
            else if (pixels >= 90000) // 300x300 or higher
                return "Medium Quality";
            else
                return "Low Quality";
        }

        public async Task<Bitmap?> LoadThumbnailBySize(JsonElement root, string sizePreference)
        {
            try
            {
                var thumbnails = ExtractAllThumbnails(root);
                if (thumbnails.Count == 0)
                    return null;

                ThumbnailOption? selectedThumb = null;
                int targetWidth = 0;
                int targetHeight = 0;

                switch (sizePreference)
                {
                    case "Maximum Quality":
                        // Get the absolute largest, no resizing
                        selectedThumb = thumbnails.OrderByDescending(t => t.Width * t.Height).FirstOrDefault();
                        break;
                        
                    case "High Quality (1200x1200)":
                        // Target 1200x1200
                        targetWidth = 1200;
                        targetHeight = 1200;
                        selectedThumb = thumbnails.OrderByDescending(t => t.Width * t.Height).FirstOrDefault();
                        break;
                        
                    case "Medium Quality (600x600)":
                        // Target 600x600
                        targetWidth = 600;
                        targetHeight = 600;
                        selectedThumb = thumbnails.OrderByDescending(t => t.Width * t.Height).FirstOrDefault();
                        break;
                        
                    case "Low Quality (300x300)":
                        // Target 300x300
                        targetWidth = 300;
                        targetHeight = 300;
                        selectedThumb = thumbnails.OrderByDescending(t => t.Width * t.Height).FirstOrDefault();
                        break;
                        
                    default:
                        selectedThumb = thumbnails.FirstOrDefault();
                        break;
                }

                if (selectedThumb != null && !string.IsNullOrEmpty(selectedThumb.Url))
                {
                    Debug.WriteLine($"[LoadThumbnailBySize] Loading {sizePreference}: {selectedThumb.Width}x{selectedThumb.Height}");
                    
                    var image = await LoadImageAsync(selectedThumb.Url);
                    if (image == null)
                        return null;

                    // Resize if needed (not Maximum Quality)
                    if (targetWidth > 0 && targetHeight > 0)
                    {
                        var resized = ResizeImage(image, targetWidth, targetHeight);
                        Debug.WriteLine($"[LoadThumbnailBySize] Final size: {resized?.PixelSize.Width}x{resized?.PixelSize.Height}");
                        return resized;
                    }

                    Debug.WriteLine($"[LoadThumbnailBySize] No resize, final size: {image.PixelSize.Width}x{image.PixelSize.Height}");
                    return image;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoadThumbnailBySize] Error: {ex.Message}");
            }

            return null;
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
            catch (Exception)
            {
                return null;
            }
        }

        private Bitmap? ResizeImage(Bitmap source, int targetWidth, int targetHeight)
        {
            try
            {
                // If already smaller or equal, don't resize
                if (source.PixelSize.Width <= targetWidth && source.PixelSize.Height <= targetHeight)
                    return source;

                // Calculate aspect ratio
                double aspectRatio = (double)source.PixelSize.Width / source.PixelSize.Height;
                
                int newWidth, newHeight;
                
                // Fit within target dimensions while maintaining aspect ratio
                if (aspectRatio > 1) // Wider than tall
                {
                    newWidth = Math.Min(targetWidth, source.PixelSize.Width);
                    newHeight = (int)(newWidth / aspectRatio);
                }
                else // Taller than wide or square
                {
                    newHeight = Math.Min(targetHeight, source.PixelSize.Height);
                    newWidth = (int)(newHeight * aspectRatio);
                }

                Debug.WriteLine($"[ResizeImage] Resizing from {source.PixelSize.Width}x{source.PixelSize.Height} to {newWidth}x{newHeight}");

                // Create a render target bitmap
                var renderTarget = new Avalonia.Media.Imaging.RenderTargetBitmap(
                    new Avalonia.PixelSize(newWidth, newHeight),
                    new Avalonia.Vector(96, 96));

                using (var context = renderTarget.CreateDrawingContext())
                {
                    var sourceRect = new Avalonia.Rect(0, 0, source.PixelSize.Width, source.PixelSize.Height);
                    var destRect = new Avalonia.Rect(0, 0, newWidth, newHeight);
                    context.DrawImage(source, sourceRect, destRect);
                }

                return renderTarget;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResizeImage] Error: {ex.Message}");
                return source; // Return original if resize fails
            }
        }

        private (int width, int height) GetTargetDimensionsFromSize(string sizeOption)
        {
            return sizeOption switch
            {
                "Low Quality (300x300)" => (300, 300),
                "Medium Quality (600x600)" => (600, 600),
                "High Quality (1200x1200)" => (1200, 1200),
                "Maximum Quality" => (int.MaxValue, int.MaxValue), // No resize
                _ => (600, 600) // Default to medium
            };
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

        private Task<string> FindDownloadedFile(string ytDlpOutput, TrackInfo? trackInfo)
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
                if (Directory.Exists(_downloadFolder))
                {
                    var recentFiles = Directory.GetFiles(_downloadFolder, "*.mp3")
                        .Where(f => File.GetCreationTime(f) > DateTime.Now.AddMinutes(-2))
                        .OrderByDescending(f => File.GetCreationTime(f))
                        .ToArray();

                    if (recentFiles.Length > 0)
                    {
                        return Task.FromResult(recentFiles[0]);
                    }
                }

                // Final fallback: construct expected filename
                if (trackInfo != null && !string.IsNullOrEmpty(trackInfo.Title) && !string.IsNullOrEmpty(trackInfo.Artist))
                {
                    string cleanTitle = SanitizeFilename(trackInfo.Title);
                    string cleanArtist = SanitizeFilename(trackInfo.Artist);
                    string expectedFilename = $"{cleanArtist} - {cleanTitle}.mp3";
                    return Task.FromResult(Path.Combine(_downloadFolder, expectedFilename));
                }

                return Task.FromResult("");
            }
            catch (Exception)
            {
                return Task.FromResult("");
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

        private (string format, string quality) ParseAudioFormat(string formatOption)
        {
            // Parse format selection and return appropriate yt-dlp parameters
            return formatOption switch
            {
                "MP3 (320kbps)" => ("mp3", "0"), // 0 = best quality for mp3
                "FLAC (Lossless)" => ("flac", "0"),
                "WAV (Uncompressed)" => ("wav", "0"),
                "AAC (256kbps)" => ("aac", "256k"),
                "M4A (256kbps)" => ("m4a", "256k"),
                "Opus (192kbps)" => ("opus", "192k"),
                "Vorbis (192kbps)" => ("vorbis", "192k"),
                _ => ("mp3", "0") // Default to MP3 320kbps
            };
        }
    }
}