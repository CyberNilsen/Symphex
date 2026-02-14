using System;

namespace Symphex.Models
{
    public class DownloadHistoryItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Album { get; set; } = "";
        public string Url { get; set; } = "";
        public string FilePath { get; set; } = "";
        public DateTime DownloadDate { get; set; } = DateTime.Now;
        public long FileSize { get; set; } = 0;
        public string Duration { get; set; } = "";
    }
}
