# 🎵 Symphex Music Downloader

A modern, cross-platform music downloader built with Avalonia UI that extracts audio from YouTube videos and searches, with intelligent metadata detection and album artwork embedding.

<img width="1111" height="839" alt="Symphex Program" src="https://github.com/user-attachments/assets/301e0b9c-1b93-478f-a3ad-b8347ccd6864" />
## ✨ Features

- **Smart Audio Extraction**: Download high-quality MP3 audio from YouTube URLs or search terms
- **Intelligent Metadata Detection**: Automatically parses artist and song titles from video titles
- **Album Artwork Search**: Finds and embeds real album artwork from iTunes and Deezer databases
- **Cross-Platform**: Works on Windows, macOS, and Linux
- **Portable Dependencies**: Automatically downloads and manages yt-dlp and FFmpeg
- **Clean Interface**: Modern UI with real-time download progress and metadata preview
- **Organized Downloads**: Saves to your Music folder with clean, sanitized filenames

## 🚀 Quick Start

1. **Download** the latest release for your platform
2. **Launch** Symphex
3. **Paste** a YouTube URL or enter a search term
4. **Click** Download and enjoy your music!

The app will automatically handle dependency setup on first run.

## 📋 Requirements

### Automatic (Handled by Symphex)
- **yt-dlp**: Downloads automatically via "Get yt-dlp" button
- **FFmpeg**: Downloads automatically via "Get FFmpeg" button

### Manual Installation (Optional)
If you prefer to install dependencies manually:

**Windows:**
- Download yt-dlp.exe and ffmpeg.exe to the app's `tools` folder

**macOS:**
```bash
brew install yt-dlp ffmpeg
```

**Linux:**
```bash
# Ubuntu/Debian
sudo apt install yt-dlp ffmpeg

# Fedora
sudo dnf install yt-dlp ffmpeg

# Arch
sudo pacman -S yt-dlp ffmpeg
```

## 🎯 Usage

### Basic Download
1. Paste a YouTube URL in the input field
2. Click "Download" 
3. Files are saved to `~/Music/Symphex Downloads/`

### Search and Download
1. Enter search terms (e.g., "Bohemian Rhapsody Queen")
2. Click "Download"
3. Symphex finds the best match and downloads it

### Features
- **Metadata Preview**: See song info before downloading
- **Album Art**: Automatically finds and embeds proper album artwork
- **Progress Tracking**: Real-time download progress with detailed logging
- **Smart Naming**: Clean, organized filenames (e.g., "Song Title - Artist Name.mp3")

## 🛠️ Advanced Features

### Dependency Management
- **Check Dependencies**: Verify yt-dlp and FFmpeg installation
- **Auto-Download**: One-click dependency installation
- **Portable Mode**: Dependencies stored in app folder

### Metadata Enhancement
- **Title Parsing**: Intelligently extracts artist/title from video titles
- **Album Art Search**: Searches iTunes and Deezer for authentic album artwork  
- **Fallback System**: Uses video thumbnail if no album art found
- **ID3v2.3 Tags**: Proper metadata embedding with FFmpeg

### Interface
- **Real-time Logging**: Detailed console output for troubleshooting
- **Progress Indicators**: Visual feedback during downloads
- **Folder Access**: Quick access to download directory
- **Copy/Clear**: Manage console output and inputs

## 📁 File Structure

```
Symphex/
├── tools/                 # Portable dependencies
│   ├── yt-dlp(.exe)       # YouTube downloader
│   └── ffmpeg(.exe)       # Media processor
└── ~/Music/Symphex Downloads/  # Download destination
```

## 🔧 Platform-Specific Notes

### Windows
- Fully automatic setup with .exe dependencies
- Downloads to `%USERPROFILE%\Music\Symphex Downloads\`

### macOS  
- Supports both Apple Silicon and Intel
- Downloads to `~/Music/Symphex Downloads/`
- Homebrew integration available

### Linux
- Package manager integration recommended
- Downloads to `~/Music/Symphex Downloads/`
- Automatic chmod +x for executables

## ❓ Troubleshooting

### Common Issues

**"yt-dlp not found"**
- Click "Get yt-dlp" button or install manually
- Ensure internet connection for download

**"No album artwork found"**  
- Normal for obscure tracks
- Video thumbnail used as fallback
- iTunes/Deezer searches are best-effort

**Download fails**
- Check URL validity
- Verify internet connection  
- Age-restricted videos may fail
- Try search terms instead of direct URLs

### Getting Help
- Check the console output for detailed error messages
- Use "Copy Output" to share logs when reporting issues
- Ensure both yt-dlp and FFmpeg are properly installed

## 🏗️ Building from Source

### Prerequisites
- .NET 8.0 SDK
- Git

### Build Steps
```bash
git clone https://github.com/CyberNilsen/Symphex.git
cd Symphex
dotnet restore
dotnet build
dotnet run
```

### Dependencies
- Avalonia UI 11.x
- CommunityToolkit.Mvvm
- CliWrap
- System.Text.Json

## 📜 License

This project is licensed under the [MIT License](LICENSE) - see the LICENSE file for details.

## 🙏 Acknowledgments

- **yt-dlp**: Powerful YouTube downloader
- **FFmpeg**: Media processing framework  
- **Avalonia UI**: Cross-platform UI framework
- **iTunes & Deezer**: Album artwork APIs

## 🛡️ Legal Notice

This tool is for personal use only. Users are responsible for complying with YouTube's Terms of Service and copyright laws. Only download content you have the right to download.
