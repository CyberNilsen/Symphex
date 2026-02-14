using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Symphex.Models;

namespace Symphex.Services
{
    public class PlaylistService
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Symphex"
        );
        
        private static readonly string PlaylistFilePath = Path.Combine(AppDataFolder, "saved_links.json");

        public PlaylistService()
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }
        }

        public List<SavedLink> LoadSavedLinks()
        {
            try
            {
                if (!File.Exists(PlaylistFilePath))
                {
                    return new List<SavedLink>();
                }

                var json = File.ReadAllText(PlaylistFilePath);
                var links = JsonSerializer.Deserialize<List<SavedLink>>(json);
                return links ?? new List<SavedLink>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaylistService] Error loading saved links: {ex.Message}");
                return new List<SavedLink>();
            }
        }

        public void SaveLinks(List<SavedLink> links)
        {
            try
            {
                var json = JsonSerializer.Serialize(links, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(PlaylistFilePath, json);
                Debug.WriteLine($"[PlaylistService] Saved {links.Count} links");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaylistService] Error saving links: {ex.Message}");
            }
        }

        public void AddLink(List<SavedLink> links, SavedLink newLink)
        {
            // Check if URL already exists
            if (!links.Any(l => l.Url.Equals(newLink.Url, StringComparison.OrdinalIgnoreCase)))
            {
                links.Insert(0, newLink); // Add to top
                SaveLinks(links);
            }
        }

        public void RemoveLink(List<SavedLink> links, string id)
        {
            var link = links.FirstOrDefault(l => l.Id == id);
            if (link != null)
            {
                links.Remove(link);
                SaveLinks(links);
            }
        }

        public void ToggleFavorite(List<SavedLink> links, string id)
        {
            var link = links.FirstOrDefault(l => l.Id == id);
            if (link != null)
            {
                link.IsFavorite = !link.IsFavorite;
                SaveLinks(links);
            }
        }
    }
}
