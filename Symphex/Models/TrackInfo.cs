using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

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
    }
}