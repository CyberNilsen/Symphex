using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Symphex.Models;

namespace Symphex.Services
{
    public class AlbumArtSearchService
    {
        private readonly HttpClient httpClient;

        public AlbumArtSearchService(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task FindRealAlbumArt(TrackInfo trackInfo)
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

        public async Task FindRealAlbumArtForBatch(TrackInfo trackInfo, int threadIndex)
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

                // Generate search variations
                var searchVariations = GenerateComprehensiveSearchVariations(trackInfo.Title, trackInfo.Artist);

                // Try multiple APIs with timeout for batch processing
                var searchMethods = new[]
                {
                    () => SearchITunesComprehensiveWithTimeout(searchVariations, threadIndex),
                    () => SearchDeezerComprehensiveWithTimeout(searchVariations, threadIndex),
                    () => SearchMusicBrainzComprehensiveWithTimeout(searchVariations, threadIndex)
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

                // Fallback: use thumbnail
                trackInfo.AlbumArt = trackInfo.Thumbnail;
                trackInfo.HasRealAlbumArt = false;
            }
            catch (Exception)
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
                            var albumArt = await LoadImageWithRetryAndValidation(imageUrl!);
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

        public async Task<(Bitmap? albumArt, string album, string genre, string year, int trackNumber)?> SearchDeezerComprehensive(List<(string query, double weight)> searchVariations)
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
                            var albumArt = await LoadImageWithRetryAndValidation(imageUrl!);
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

                                    string mbid = releaseId.GetString() ?? "";
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

                        string imageUrl = coverImage.GetString() ?? "";
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

        private async Task<(Bitmap? albumArt, string album, string genre, string year, int trackNumber)?> SearchITunesComprehensiveWithTimeout(List<(string query, double weight)> searchVariations, int threadIndex)
        {
            foreach (var (query, weight) in searchVariations.Take(5)) // Reduced for batch processing
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // 10 second timeout

                    string searchUrl = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query)}&media=music&entity=song&limit=25";

                    using var response = await httpClient.GetAsync(searchUrl, cts.Token);
                    if (!response.IsSuccessStatusCode) continue;

                    var jsonResponse = await response.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(jsonResponse);

                    if (!doc.RootElement.TryGetProperty("results", out var results)) continue;

                    var matches = ScoreAndFilterMatches(results, query, weight * 0.15); // Slightly higher threshold for batch

                    foreach (var match in matches.Take(3)) // Reduced for performance
                    {
                        if (!match.TryGetProperty("artworkUrl100", out var artworkUrl)) continue;

                        // Try high-res version first
                        var imageUrls = new[]
                        {
                            artworkUrl.GetString()?.Replace("100x100", "600x600"),
                            artworkUrl.GetString()?.Replace("100x100", "400x400")
                        };

                        foreach (var imageUrl in imageUrls.Where(url => !string.IsNullOrEmpty(url)))
                        {
                            var albumArt = await LoadImageWithRetryAndValidation(imageUrl!, 2); // Reduced retries for batch
                            if (albumArt != null && IsHighQualityAlbumArt(albumArt))
                            {
                                var metadata = ExtractITunesMetadata(match);
                                return (albumArt, metadata.album, metadata.genre, metadata.year, metadata.trackNumber);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch
                {
                    continue;
                }

                await Task.Delay(100, CancellationToken.None); // Reduced delay for batch processing
            }

            return null;
        }

        private async Task<(Bitmap? albumArt, string album, string genre, string year, int trackNumber)?> SearchDeezerComprehensiveWithTimeout(List<(string query, double weight)> searchVariations, int threadIndex)
        {
            foreach (var (query, weight) in searchVariations.Take(5))
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                    string searchUrl = $"https://api.deezer.com/search/track?q={Uri.EscapeDataString(query)}&limit=25";

                    using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                    request.Headers.Add("User-Agent", "Symphex/1.0");

                    var response = await httpClient.SendAsync(request, cts.Token);
                    if (!response.IsSuccessStatusCode) continue;

                    var jsonResponse = await response.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(jsonResponse);

                    if (!doc.RootElement.TryGetProperty("data", out var data)) continue;

                    var matches = ScoreDeezerMatches(data, query, weight * 0.15);

                    foreach (var match in matches.Take(3))
                    {
                        if (!match.TryGetProperty("album", out var album)) continue;

                        var imageUrls = new[]
                        {
                            album.TryGetProperty("cover_big", out var big) ? big.GetString() : null,
                            album.TryGetProperty("cover_medium", out var med) ? med.GetString() : null
                        };

                        foreach (var imageUrl in imageUrls.Where(url => !string.IsNullOrEmpty(url)))
                        {
                            var albumArt = await LoadImageWithRetryAndValidation(imageUrl!, 2);
                            if (albumArt != null && IsHighQualityAlbumArt(albumArt))
                            {
                                var metadata = ExtractDeezerMetadata(match);
                                return (albumArt, metadata.album, metadata.genre, metadata.year, 0);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch
                {
                    continue;
                }

                await Task.Delay(150, CancellationToken.None);
            }

            return null;
        }

        private async Task<(Bitmap? albumArt, string album, string genre, string year, int trackNumber)?> SearchMusicBrainzComprehensiveWithTimeout(List<(string query, double weight)> searchVariations, int threadIndex)
        {
            foreach (var (query, weight) in searchVariations.Take(3)) // Even fewer for MusicBrainz due to stricter rate limits
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                    string searchUrl = $"https://musicbrainz.org/ws/2/recording/?query={Uri.EscapeDataString($"recording:{query}")}&fmt=json&limit=15";

                    using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                    request.Headers.Add("User-Agent", "Symphex/1.0");

                    var response = await httpClient.SendAsync(request, cts.Token);
                    if (!response.IsSuccessStatusCode) continue;

                    var jsonResponse = await response.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(jsonResponse);

                    if (!doc.RootElement.TryGetProperty("recordings", out var recordings)) continue;

                    foreach (var recording in recordings.EnumerateArray().Take(5))
                    {
                        if (!recording.TryGetProperty("releases", out var releases)) continue;

                        foreach (var release in releases.EnumerateArray().Take(3))
                        {
                            if (!release.TryGetProperty("id", out var releaseId)) continue;

                            string mbid = releaseId.GetString() ?? "";
                            if (string.IsNullOrEmpty(mbid)) continue;

                            var coverResult = await GetCoverArtFromArchiveRobustWithTimeout(mbid, cts.Token);
                            if (coverResult.HasValue && coverResult.Value.albumArt != null && IsHighQualityAlbumArt(coverResult.Value.albumArt))
                            {
                                return (coverResult.Value.albumArt, coverResult.Value.album, "", coverResult.Value.year, 0);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch
                {
                    continue;
                }

                await Task.Delay(1500, CancellationToken.None); // Longer delay for MusicBrainz rate limiting
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

                    string artUrl = imageUrl.GetString() ?? "";
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

        private async Task<(Bitmap? albumArt, string album, string year)?> GetCoverArtFromArchiveRobustWithTimeout(string mbid, CancellationToken cancellationToken)
        {
            try
            {
                string coverArtUrl = $"https://coverartarchive.org/release/{mbid}";

                using var request = new HttpRequestMessage(HttpMethod.Get, coverArtUrl);
                request.Headers.Add("User-Agent", "Symphex/1.0");

                var response = await httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode) return null;

                var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(jsonResponse);

                if (!doc.RootElement.TryGetProperty("images", out var images)) return null;

                var imagesToTry = images.EnumerateArray()
                    .OrderByDescending(img => img.TryGetProperty("front", out var front) && front.GetBoolean())
                    .Take(2) // Only try top 2 for batch processing
                    .ToList();

                foreach (var image in imagesToTry)
                {
                    if (!image.TryGetProperty("image", out var imageUrl)) continue;

                    string artUrl = imageUrl.GetString() ?? "";
                    if (string.IsNullOrEmpty(artUrl)) continue;

                    var albumArt = await LoadImageWithRetryAndValidation(artUrl, 1);
                    if (albumArt != null && IsHighQualityAlbumArt(albumArt))
                    {
                        var releaseInfo = await GetMusicBrainzReleaseInfoRobust(mbid);
                        return (albumArt, releaseInfo.album, releaseInfo.year);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation
            }
            catch
            {
                // Silent failure
            }

            return null;
        }
    }
}