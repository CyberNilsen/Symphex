using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Symphex.Models;

namespace Symphex.Services
{
    public class DownloadHistoryService
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Symphex"
        );
        
        private static readonly string HistoryFilePath = Path.Combine(AppDataFolder, "download_history.json");

        public DownloadHistoryService()
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }
        }

        public List<DownloadHistoryItem> LoadHistory()
        {
            try
            {
                if (!File.Exists(HistoryFilePath))
                {
                    return new List<DownloadHistoryItem>();
                }

                var json = File.ReadAllText(HistoryFilePath);
                var history = JsonSerializer.Deserialize<List<DownloadHistoryItem>>(json);
                return history ?? new List<DownloadHistoryItem>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DownloadHistoryService] Error loading history: {ex.Message}");
                return new List<DownloadHistoryItem>();
            }
        }

        public void SaveHistory(List<DownloadHistoryItem> history)
        {
            try
            {
                var json = JsonSerializer.Serialize(history, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(HistoryFilePath, json);
                Debug.WriteLine($"[DownloadHistoryService] Saved {history.Count} items");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DownloadHistoryService] Error saving history: {ex.Message}");
            }
        }

        public void AddToHistory(List<DownloadHistoryItem> history, DownloadHistoryItem item)
        {
            history.Insert(0, item); // Add to top
            
            // Keep only last 500 items
            if (history.Count > 500)
            {
                history.RemoveRange(500, history.Count - 500);
            }
            
            SaveHistory(history);
        }

        public void RemoveFromHistory(List<DownloadHistoryItem> history, string id)
        {
            var item = history.FirstOrDefault(h => h.Id == id);
            if (item != null)
            {
                history.Remove(item);
                SaveHistory(history);
            }
        }

        public void ClearHistory(List<DownloadHistoryItem> history)
        {
            history.Clear();
            SaveHistory(history);
        }
    }
}
