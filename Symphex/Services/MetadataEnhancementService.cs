using Avalonia.Media.Imaging;
using Symphex.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TagLib;

namespace Symphex.Services
{
    public class MetadataEnhancementService
    {
        private readonly AlbumArtSearchService _albumArtSearchService;
        private int _targetArtworkSize = 600; // Default size

        public MetadataEnhancementService(AlbumArtSearchService albumArtSearchService)
        {
            _albumArtSearchService = albumArtSearchService;
        }

        public void SetArtworkSize(int size)
        {
            _targetArtworkSize = size;
            Debug.WriteLine($"[MetadataEnhancement] Artwork size set to: {size}x{size}");
        }

        public async Task<(int success, int failed, List<string> errors)> EnhanceMetadataAsync(
            List<string> files,
            string outputFolder,
            bool downloadArtwork = true,
            bool forceRedownload = false,
            IProgress<(int current, int total, string fileName)>? progress = null,
            Action<string>? logCallback = null)
        {
            int successCount = 0;
            int failedCount = 0;
            var errors = new List<string>();

            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var fileName = Path.GetFileName(file);
                
                try
                {
                    progress?.Report((i + 1, files.Count, fileName));
                    logCallback?.Invoke($"[{i + 1}/{files.Count}] Processing: {fileName}");
                    Debug.WriteLine($"[MetadataEnhancement] Processing file {i + 1}/{files.Count}: {fileName}");

                    var metadata = ExtractMetadata(file);
                    if (metadata == null)
                    {
                        var error = $"{fileName}: Could not read file";
                        errors.Add(error);
                        logCallback?.Invoke($"  ❌ ERROR: {error}");
                        Debug.WriteLine($"[MetadataEnhancement] ERROR: {error}");
                        failedCount++;
                        continue;
                    }

                    logCallback?.Invoke($"  📋 Title: {metadata.Title ?? "None"}, Artist: {metadata.Artist ?? "None"}");
                    logCallback?.Invoke($"  🎨 Has Artwork: {metadata.HasArtwork}");
                    Debug.WriteLine($"[MetadataEnhancement] Metadata - Title: {metadata.Title}, Artist: {metadata.Artist}, HasArtwork: {metadata.HasArtwork}");

                    bool enhanced = false;

                    // Always search for artwork (force mode ignores existing artwork)
                    if (downloadArtwork)
                    {
                        logCallback?.Invoke($"  🔍 Searching for artwork...");
                        Debug.WriteLine($"[MetadataEnhancement] Searching for artwork...");
                        
                        // Parse filename to extract artist and title if metadata is missing
                        string searchTitle = metadata.Title;
                        string searchArtist = metadata.Artist;
                        
                        if (string.IsNullOrWhiteSpace(searchTitle) || string.IsNullOrWhiteSpace(searchArtist))
                        {
                            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                            
                            // Try to parse "Artist - Title" format
                            if (fileNameWithoutExt.Contains(" - "))
                            {
                                var parts = fileNameWithoutExt.Split(new[] { " - " }, 2, StringSplitOptions.None);
                                if (parts.Length == 2)
                                {
                                    if (string.IsNullOrWhiteSpace(searchArtist))
                                        searchArtist = parts[0].Trim();
                                    if (string.IsNullOrWhiteSpace(searchTitle))
                                        searchTitle = parts[1].Trim();
                                    
                                    logCallback?.Invoke($"  📝 Parsed filename: Artist='{searchArtist}', Title='{searchTitle}'");
                                }
                            }
                            
                            // Fallback
                            if (string.IsNullOrWhiteSpace(searchTitle))
                                searchTitle = fileNameWithoutExt;
                            if (string.IsNullOrWhiteSpace(searchArtist))
                                searchArtist = "Unknown Artist";
                        }
                        
                        // Create a TrackInfo object for the search
                        var trackInfo = new TrackInfo
                        {
                            Title = searchTitle,
                            Artist = searchArtist,
                            Album = metadata.Album ?? "",
                            FileName = fileName
                        };

                        logCallback?.Invoke($"  🔎 Query: {trackInfo.Artist} - {trackInfo.Title}");
                        Debug.WriteLine($"[MetadataEnhancement] Search query - Title: {trackInfo.Title}, Artist: {trackInfo.Artist}");

                        // Search for album art
                        await _albumArtSearchService.FindRealAlbumArt(trackInfo);
                        
                        logCallback?.Invoke($"  📊 Search Results:");
                        logCallback?.Invoke($"     - AlbumArt: {(trackInfo.AlbumArt != null ? "Found" : "Not found")}");
                        logCallback?.Invoke($"     - Title: {trackInfo.Title ?? "None"}");
                        logCallback?.Invoke($"     - Artist: {trackInfo.Artist ?? "None"}");
                        logCallback?.Invoke($"     - Album: {trackInfo.Album ?? "None"}");
                        logCallback?.Invoke($"     - Genre: {trackInfo.Genre ?? "None"}");
                        logCallback?.Invoke($"     - Year: {trackInfo.Year ?? "None"}");
                        
                        if (trackInfo.AlbumArt != null)
                        {
                            logCallback?.Invoke($"  ✅ Artwork found! Embedding...");
                            Debug.WriteLine($"[MetadataEnhancement] Artwork found! Embedding...");
                            
                            // Copy file to output folder
                            var outputPath = Path.Combine(outputFolder, fileName);
                            logCallback?.Invoke($"  📁 Copying to: {outputPath}");
                            Debug.WriteLine($"[MetadataEnhancement] Copying to: {outputPath}");
                            
                            if (System.IO.File.Exists(outputPath))
                            {
                                System.IO.File.Delete(outputPath);
                                logCallback?.Invoke($"  🗑️ Deleted existing file");
                            }
                            
                            System.IO.File.Copy(file, outputPath, overwrite: false);
                            logCallback?.Invoke($"  ✅ File copied");
                            
                            // Update ALL metadata from the search results
                            await UpdateAllMetadataAsync(outputPath, trackInfo);
                            logCallback?.Invoke($"  ✅ Metadata updated");
                            
                            enhanced = true;
                            logCallback?.Invoke($"  ✅ SUCCESS: Enhanced file saved");
                            Debug.WriteLine($"[MetadataEnhancement] SUCCESS: Added artwork and metadata to {fileName}");
                        }
                        else
                        {
                            logCallback?.Invoke($"  ❌ No artwork found");
                            Debug.WriteLine($"[MetadataEnhancement] No artwork found for {fileName}");
                            errors.Add($"{fileName}: No artwork found");
                        }
                    }

                    if (enhanced)
                        successCount++;
                    else
                        failedCount++;
                }
                catch (Exception ex)
                {
                    var error = $"{fileName}: {ex.Message}";
                    errors.Add(error);
                    failedCount++;
                    logCallback?.Invoke($"  ❌ EXCEPTION: {ex.Message}");
                    Debug.WriteLine($"[MetadataEnhancement] EXCEPTION processing {file}: {ex.Message}");
                    Debug.WriteLine($"[MetadataEnhancement] Stack trace: {ex.StackTrace}");
                }
            }

            Debug.WriteLine($"[MetadataEnhancement] Complete - Success: {successCount}, Failed: {failedCount}");
            return (successCount, failedCount, errors);
        }

        private async Task UpdateAllMetadataAsync(string filePath, TrackInfo trackInfo)
        {
            await Task.Run(() =>
            {
                try
                {
                    Debug.WriteLine($"[MetadataEnhancement] Opening file: {filePath}");
                    
                    // Verify file exists
                    if (!System.IO.File.Exists(filePath))
                    {
                        throw new FileNotFoundException($"File not found: {filePath}");
                    }
                    
                    using var file = TagLib.File.Create(filePath);
                    
                    Debug.WriteLine($"[MetadataEnhancement] File opened successfully, current pictures: {file.Tag.Pictures?.Length ?? 0}");
                    
                    // Update title, artist, album from search results
                    if (!string.IsNullOrWhiteSpace(trackInfo.Title))
                    {
                        file.Tag.Title = trackInfo.Title;
                        Debug.WriteLine($"[MetadataEnhancement] Set Title: {trackInfo.Title}");
                    }
                    
                    if (!string.IsNullOrWhiteSpace(trackInfo.Artist))
                    {
                        file.Tag.Performers = new[] { trackInfo.Artist };
                        file.Tag.AlbumArtists = new[] { trackInfo.Artist };
                        Debug.WriteLine($"[MetadataEnhancement] Set Artist: {trackInfo.Artist}");
                    }
                    
                    if (!string.IsNullOrWhiteSpace(trackInfo.Album))
                    {
                        file.Tag.Album = trackInfo.Album;
                        Debug.WriteLine($"[MetadataEnhancement] Set Album: {trackInfo.Album}");
                    }
                    
                    if (!string.IsNullOrWhiteSpace(trackInfo.Genre))
                    {
                        file.Tag.Genres = new[] { trackInfo.Genre };
                        Debug.WriteLine($"[MetadataEnhancement] Set Genre: {trackInfo.Genre}");
                    }
                    
                    if (!string.IsNullOrWhiteSpace(trackInfo.Year))
                    {
                        if (uint.TryParse(trackInfo.Year, out uint year))
                        {
                            file.Tag.Year = year;
                            Debug.WriteLine($"[MetadataEnhancement] Set Year: {year}");
                        }
                    }
                    
                    // Embed artwork
                    if (trackInfo.AlbumArt != null)
                    {
                        Debug.WriteLine($"[MetadataEnhancement] Converting bitmap to bytes...");
                        
                        // Resize artwork if needed
                        var artworkToEmbed = trackInfo.AlbumArt;
                        if (_targetArtworkSize > 0 && (trackInfo.AlbumArt.PixelSize.Width > _targetArtworkSize || trackInfo.AlbumArt.PixelSize.Height > _targetArtworkSize))
                        {
                            Debug.WriteLine($"[MetadataEnhancement] Resizing artwork from {trackInfo.AlbumArt.PixelSize.Width}x{trackInfo.AlbumArt.PixelSize.Height} to {_targetArtworkSize}x{_targetArtworkSize}");
                            artworkToEmbed = ResizeImage(trackInfo.AlbumArt, _targetArtworkSize, _targetArtworkSize);
                        }
                        
                        using var memoryStream = new MemoryStream();
                        artworkToEmbed.Save(memoryStream);
                        var imageData = memoryStream.ToArray();
                        Debug.WriteLine($"[MetadataEnhancement] Bitmap converted: {imageData.Length} bytes");

                        var picture = new TagLib.Picture(new TagLib.ByteVector(imageData))
                        {
                            Type = TagLib.PictureType.FrontCover,
                            MimeType = "image/jpeg",
                            Description = "Cover"
                        };

                        file.Tag.Pictures = new TagLib.IPicture[] { picture };
                        Debug.WriteLine($"[MetadataEnhancement] Picture assigned to tag, count: {file.Tag.Pictures.Length}");
                    }

                    Debug.WriteLine($"[MetadataEnhancement] Calling file.Save()...");
                    file.Save();
                    Debug.WriteLine($"[MetadataEnhancement] file.Save() completed");
                    
                    // Verify the save worked by re-reading
                    using var verifyFile = TagLib.File.Create(filePath);
                    Debug.WriteLine($"[MetadataEnhancement] VERIFICATION - Pictures in saved file: {verifyFile.Tag.Pictures?.Length ?? 0}");
                    if (verifyFile.Tag.Pictures != null && verifyFile.Tag.Pictures.Length > 0)
                    {
                        Debug.WriteLine($"[MetadataEnhancement] VERIFICATION - Picture size: {verifyFile.Tag.Pictures[0].Data.Count} bytes");
                    }
                    else
                    {
                        Debug.WriteLine($"[MetadataEnhancement] ERROR: VERIFICATION FAILED - No pictures found after save!");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MetadataEnhancement] ERROR updating metadata: {ex.Message}");
                    Debug.WriteLine($"[MetadataEnhancement] Stack trace: {ex.StackTrace}");
                    throw;
                }
            });
        }

        private async Task EmbedArtworkAsync(string filePath, Bitmap artwork)
        {
            await Task.Run(() =>
            {
                try
                {
                    using var file = TagLib.File.Create(filePath);
                    
                    // Convert Bitmap to byte array
                    using var memoryStream = new MemoryStream();
                    artwork.Save(memoryStream);
                    var imageData = memoryStream.ToArray();

                    // Create picture
                    var picture = new TagLib.Picture(new TagLib.ByteVector(imageData))
                    {
                        Type = TagLib.PictureType.FrontCover,
                        MimeType = "image/jpeg"
                    };

                    file.Tag.Pictures = new TagLib.IPicture[] { picture };
                    file.Save();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MetadataEnhancement] Error embedding artwork: {ex.Message}");
                    throw;
                }
            });
        }

        public MetadataInfo? ExtractMetadata(string filePath)
        {
            try
            {
                using var file = TagLib.File.Create(filePath);
                
                return new MetadataInfo
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    Title = file.Tag.Title,
                    Artist = file.Tag.FirstPerformer,
                    Album = file.Tag.Album,
                    Year = file.Tag.Year,
                    HasArtwork = file.Tag.Pictures != null && file.Tag.Pictures.Length > 0,
                    ArtworkSize = file.Tag.Pictures?.FirstOrDefault()?.Data.Count ?? 0
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MetadataEnhancement] Error extracting metadata from {filePath}: {ex.Message}");
                return null;
            }
        }

        private Bitmap ResizeImage(Bitmap source, int maxWidth, int maxHeight)
        {
            var sourceWidth = source.PixelSize.Width;
            var sourceHeight = source.PixelSize.Height;

            // Calculate new dimensions maintaining aspect ratio
            double scale = Math.Min((double)maxWidth / sourceWidth, (double)maxHeight / sourceHeight);
            int newWidth = (int)(sourceWidth * scale);
            int newHeight = (int)(sourceHeight * scale);

            return source.CreateScaledBitmap(new Avalonia.PixelSize(newWidth, newHeight));
        }
    }

    public class MetadataInfo
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public uint Year { get; set; }
        public bool HasArtwork { get; set; }
        public int ArtworkSize { get; set; }
    }
}
