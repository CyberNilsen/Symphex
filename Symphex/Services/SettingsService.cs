using System;
using System.IO;
using System.Text.Json;
using System.Diagnostics;

namespace Symphex.Services
{
    public class UserSettings
    {
        public bool EnableAlbumArtDownload { get; set; } = true;
        public bool SkipThumbnailDownload { get; set; } = true; // true = download thumbnails (inverted logic)
        public string SelectedThumbnailSize { get; set; } = "Medium Quality (600x600)";
        public bool EnableArtworkSelection { get; set; } = true; 
        public int ArtworkSelectionTimeout { get; set; } = 5;
        public double AlbumArtSize { get; set; } = 600; // Resize mode album art size
        public string SelectedAudioFormat { get; set; } = "MP3"; // Audio format selection
        public string SelectedBitrate { get; set; } = "320"; // Bitrate for lossy formats (kbps)
    }

    public class SettingsService
    {
        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Symphex"
        );
        private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

        public static UserSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    var settings = JsonSerializer.Deserialize<UserSettings>(json);
                    Debug.WriteLine($"[SettingsService] Loaded settings from {SettingsFile}");
                    return settings ?? new UserSettings();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsService] Error loading settings: {ex.Message}");
            }

            Debug.WriteLine("[SettingsService] Using default settings");
            return new UserSettings();
        }

        public static void SaveSettings(UserSettings settings)
        {
            try
            {
                // Ensure directory exists
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                    Debug.WriteLine($"[SettingsService] Created settings folder: {SettingsFolder}");
                }

                // Serialize and save
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsFile, json);
                Debug.WriteLine($"[SettingsService] Saved settings to {SettingsFile}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsService] Error saving settings: {ex.Message}");
            }
        }
    }
}
