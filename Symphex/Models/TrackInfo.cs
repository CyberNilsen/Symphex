using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace Symphex.Models
{
    public partial class TrackInfo : ObservableObject
    {
        [ObservableProperty]
        private string title = "";

        [ObservableProperty]
        private bool isDownloading = false;

        [ObservableProperty]
        private bool isDownloadComplete = false;

        [ObservableProperty]
        private string downloadFolder = "";

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

        [ObservableProperty]
        private string genre = "";

        [ObservableProperty]
        private string year = "";

        [ObservableProperty]
        private int trackNumber = 0;

        [ObservableProperty]
        private int discNumber = 0;

        [ObservableProperty]
        private string composer = "";

        [ObservableProperty]
        private string albumArtist = "";

        [ObservableProperty]
        private string comment = "";

        [ObservableProperty]
        private int bitrate = 0;

        [ObservableProperty]
        private string encoder = "";

        [ObservableProperty]
        private List<ThumbnailOption> availableThumbnails = new List<ThumbnailOption>();

        [ObservableProperty]
        private ThumbnailOption? selectedThumbnail;
    }

    public class ThumbnailOption
    {
        public string Url { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public string Quality { get; set; } = "";
        public Bitmap? PreviewImage { get; set; }
        
        public string DisplayText => $"{Quality} ({Width}x{Height})";
    }
}