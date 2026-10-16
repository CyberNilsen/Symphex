using CliWrap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Symphex.Services
{
    public class PlaylistExtractionService
    {
        private readonly string _ytDlpPath;

        public PlaylistExtractionService(string ytDlpPath)
        {
            _ytDlpPath = ytDlpPath;
        }

        /// <summary>
        /// Checks if a URL is a YouTube playlist
        /// </summary>
        public bool IsPlaylistUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            url = url.ToLower();
            return (url.Contains("youtube.com/playlist") || 
                    url.Contains("youtube.com/@") ||
                    url.Contains("youtu.be/") && url.Contains("list=")) ||
                    url.Contains("youtube.com/watch") && url.Contains("list=");
        }

        /// <summary>
        /// Extracts all video URLs from a YouTube playlist
        /// </summary>
        public async Task<List<string>> ExtractPlaylistUrls(string playlistUrl)
        {
            var urls = new List<string>();

            try
            {
                if (string.IsNullOrWhiteSpace(playlistUrl))
                {
                    Debug.WriteLine("[PlaylistExtractionService] Playlist URL is empty");
                    return urls;
                }

                Debug.WriteLine($"[PlaylistExtractionService] Extracting URLs from playlist: {playlistUrl}");

                var output = new StringBuilder();
                var error = new StringBuilder();

                // Use yt-dlp to extract all video URLs from the playlist
                var args = new List<string>
                {
                    playlistUrl,
                    "--flat-playlist",  // Get flat list without downloading metadata
                    "--dump-json",      // Output as JSON
                    "--no-warnings",
                    "--quiet"
                };

                var result = await Cli.Wrap(_ytDlpPath)
                    .WithArguments(args)
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(error))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();

                if (result.ExitCode != 0)
                {
                    Debug.WriteLine($"[PlaylistExtractionService] yt-dlp exited with code {result.ExitCode}");
                    Debug.WriteLine($"[PlaylistExtractionService] Error: {error}");
                    return urls;
                }

                var outputText = output.ToString().Trim();
                if (string.IsNullOrEmpty(outputText))
                {
                    Debug.WriteLine("[PlaylistExtractionService] No output from yt-dlp");
                    return urls;
                }

                // Parse JSON output - each line is a video
                var lines = outputText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                Debug.WriteLine($"[PlaylistExtractionService] Found {lines.Length} videos in playlist");

                foreach (var line in lines)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;

                        // Extract the video ID
                        if (root.TryGetProperty("id", out var idProp))
                        {
                            string? videoId = idProp.GetString();
                            if (!string.IsNullOrEmpty(videoId))
                            {
                                string videoUrl = $"https://www.youtube.com/watch?v={videoId}";
                                urls.Add(videoUrl);
                                Debug.WriteLine($"[PlaylistExtractionService] Extracted: {videoUrl}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PlaylistExtractionService] Error parsing line: {ex.Message}");
                    }
                }

                Debug.WriteLine($"[PlaylistExtractionService] Successfully extracted {urls.Count} URLs from playlist");
                return urls;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaylistExtractionService] Exception: {ex.Message}");
                Debug.WriteLine($"[PlaylistExtractionService] Stack trace: {ex.StackTrace}");
                return urls;
            }
        }
    }
}
