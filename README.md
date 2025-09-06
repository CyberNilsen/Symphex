# 🎵 Symphex - Free YouTube Music Downloader | Cross-Platform MP3 Converter

**Open-source YouTube music downloader** - Extract high-quality MP3 audio from YouTube videos with automatic metadata detection, album artwork embedding, and cross-platform support for Windows, macOS, and Linux.

![Symphex Program](https://github.com/user-attachments/assets/301e0b9c-1b93-478f-a3ad-b8347ccd6864)

[![GitHub release](https://img.shields.io/github/v/release/CyberNilsen/Symphex?style=for-the-badge)](https://github.com/CyberNilsen/Symphex/releases)
[![GitHub stars](https://img.shields.io/github/stars/CyberNilsen/Symphex?style=for-the-badge)](https://github.com/CyberNilsen/Symphex/stargazers)
[![License](https://img.shields.io/github/license/CyberNilsen/Symphex?style=for-the-badge)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-blue?style=for-the-badge)](https://github.com/CyberNilsen/Symphex/releases)

## 🎯 Why Choose Symphex?

✅ **100% Free & Open Source** - No ads, no premium features, no hidden costs  
✅ **Privacy First** - No data collection, works completely offline  
✅ **High Quality Audio** - Download in best available quality (up to 320kbps)  
✅ **Smart Metadata** - Automatically detects artist, title, and album artwork  
✅ **Cross-Platform** - Native apps for Windows, macOS, and Linux  
✅ **No Installation Hassles** - Portable dependencies, works out of the box  

## ✨ Features

### 🎵 Audio Extraction
- **Smart Audio Extraction**: Download high-quality MP3 audio from YouTube URLs or search terms
- **Format**: MP3
- **Batch Downloads**: Queue multiple songs for bulk downloading
- **Search Integration**: Find music by artist, song title, or keywords

### 🎨 Metadata & Artwork
- **Intelligent Metadata Detection**: Automatically parses artist and song titles from video titles
- **Album Artwork Search**: Finds and embeds authentic album artwork from iTunes and Deezer databases
- **ID3 Tag Support**: Proper metadata embedding with FFmpeg for universal compatibility
- **Smart Fallbacks**: Uses video thumbnails when album art isn't found

### 🖥️ User Experience  
- **Modern Interface**: Clean, intuitive UI built with Avalonia framework
- **Real-time Progress**: Live download status with detailed logging
- **Organized Downloads**: Automatic file organization in your Music folder
- **Portable Mode**: All dependencies bundled, no system-wide installations required

### 🔧 Technical Features
- **Cross-Platform**: Works on Windows 10/11, macOS (Intel/Apple Silicon), and Linux
- **Automatic Dependencies**: One-click download of yt-dlp and FFmpeg
- **Smart Naming**: Clean, organized filenames (e.g., "Song Title - Artist Name.mp3")
- **Error Handling**: Comprehensive error reporting and troubleshooting guides

## 🚀 Quick Start Guide

### Installation
1. **Download** the latest release for your platform from [Releases](https://github.com/CyberNilsen/Symphex/releases)
2. **Extract** the archive to your preferred location
3. **Launch** Symphex executable
4. **First Run**: App will offer to download required dependencies (yt-dlp, FFmpeg)

### Basic Usage
1. **Paste YouTube URL** or enter search terms (e.g., "Bohemian Rhapsody Queen")
2. **Preview metadata** - See detected artist, title, and album art
3. **Click Download** - Files save to `~/Music/Symphex Downloads/`
4. **Enjoy** - Your music is ready with proper metadata and artwork!

## 📋 System Requirements

### Minimum Requirements
- **OS**: Windows 10, macOS 10.15, or Linux (Ubuntu 18.04+)
- **RAM**: 512MB available memory
- **Storage**: 100MB for app + space for downloads
- **Internet**: Required for downloading and metadata lookup

### Automatic Dependencies (Handled by Symphex)
- **yt-dlp**: Latest version downloaded automatically
- **FFmpeg**: Static builds downloaded for your platform
- **Portable**: All tools stored in app folder, no system pollution

### Manual Installation (Advanced Users)
If you prefer system-wide installations:

**Windows:**
```powershell
# Using Chocolatey
choco install yt-dlp ffmpeg

# Or download manually to Symphex/tools/ folder
```

**macOS:**
```bash
# Using Homebrew
brew install yt-dlp ffmpeg
```

**Linux:**
```bash
# Ubuntu/Debian
sudo apt install yt-dlp ffmpeg

# Fedora/CentOS
sudo dnf install yt-dlp ffmpeg

# Arch Linux
sudo pacman -S yt-dlp ffmpeg
```

## 🎯 Use Cases

### For Music Lovers
- Build your personal music library from YouTube
- Get high-quality audio with proper metadata
- Organize music with automatic album artwork
- Create offline playlists from your favorite videos

### For Content Creators  
- Extract audio for video editing projects
- Download royalty-free music and audio
- Archive audio content for offline editing
- Convert video content to podcast format

### For Developers
- Reference implementation for Avalonia UI applications
- Example of cross-platform .NET desktop development
- Integration patterns for yt-dlp and FFmpeg
- Modern MVVM architecture with CommunityToolkit

## 📁 File Structure & Organization

```
Symphex/
├── Symphex.exe (or app equivalent)    # Main application
├── tools/                             # Portable dependencies
│   ├── yt-dlp(.exe)                  # YouTube downloader
│   └── ffmpeg(.exe)                  # Media processor
├── logs/                             # Application logs
└── config/                           # User preferences

Downloads saved to:
~/Music/Symphex Downloads/            # Clean, organized music files
├── Artist Name - Song Title.mp3
├── Another Artist - Another Song.mp3
└── ...
```

## 🔧 Platform-Specific Features

### Windows
- **Native Integration**: Right-click context menus (planned)
- **File Association**: Set as default for YouTube links (planned)
- **Automatic Updates**: Built-in updater functionality
- **System Tray**: Minimize to tray for background downloads

### macOS
- **Apple Silicon**: Native ARM64 support for M1/M2 Macs
- **Finder Integration**: Quick access to downloads folder
- **Notification Center**: Download completion notifications
- **Homebrew**: Optional system dependency management

### Linux
- **Package Managers**: Integration with apt, dnf, pacman
- **Desktop Files**: Proper .desktop file installation
- **Themes**: Respects system dark/light theme preferences
- **Wayland/X11**: Compatible with both display servers

## ❓ Troubleshooting & FAQ

### Common Issues

**"yt-dlp not found" Error**
- Solution: Click "Get yt-dlp" button in the app
- Alternative: Install manually using your package manager
- Check: Ensure internet connection for automatic download

**"FFmpeg not found" Error**  
- Solution: Click "Get FFmpeg" button in the app
- Alternative: Install system-wide via package manager
- Verify: Check tools/ folder for ffmpeg executable

**Downloads Fail or Time Out**
- Check URL validity - ensure the video exists and isn't private
- Verify internet connection stability
- Try search terms instead of direct URLs for better results
- Age-restricted content may require additional authentication

**No Album Artwork Found**
- Normal behavior for obscure or independent tracks
- Symphex searches iTunes and Deezer databases
- Video thumbnails used as intelligent fallbacks
- Manual artwork can be added after download

**Poor Audio Quality**
- Check source video quality - Symphex can't enhance beyond original
- Verify yt-dlp is latest version (auto-updated by app)
- Some videos only available in lower quality formats

### Getting Help
- **Console Output**: Check detailed logs in the app's console
- **Copy Logs**: Use "Copy Output" button when reporting issues  
- **GitHub Issues**: Report bugs with full error messages
- **Discussions**: Ask questions in GitHub Discussions tab

## 🏗️ Building from Source

### Prerequisites
- **.NET 8.0 SDK** - Download from Microsoft
- **Git** - For cloning the repository
- **IDE** (Optional) - Visual Studio, VS Code, or JetBrains Rider

### Build Steps
```bash
# Clone repository
git clone https://github.com/CyberNilsen/Symphex.git
cd Symphex

# Restore NuGet packages
dotnet restore

# Build the application
dotnet build --configuration Release

# Run locally
dotnet run

# Create platform-specific builds
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r osx-x64 --self-contained  
dotnet publish -c Release -r linux-x64 --self-contained
```

### Development Dependencies
- **Avalonia UI 11.x** - Cross-platform UI framework
- **CommunityToolkit.Mvvm** - MVVM helpers and source generators
- **CliWrap** - Command-line interface wrapper
- **System.Text.Json** - JSON serialization for metadata APIs

### Contributing
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 🏷️ Keywords & Tags

**Primary**: YouTube downloader, MP3 converter, music downloader, free audio extractor, yt-dlp GUI, cross-platform music app

**Secondary**: FFmpeg frontend, open source downloader, portable music downloader, YouTube to MP3, album art downloader, metadata extraction, Avalonia UI, .NET desktop app

**Long-tail**: best free YouTube music downloader 2025, open source music downloader Windows Mac Linux, YouTube MP3 downloader with album art, portable music downloader no installation required

## 📜 License & Legal

### MIT License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for complete details.

### Legal Notice & Disclaimer
**Important**: This tool is designed for personal use only. Users are solely responsible for:
- Complying with YouTube's Terms of Service
- Respecting copyright laws and intellectual property rights
- Only downloading content they have legal rights to access
- Understanding local laws regarding media downloading

Symphex developers assume no responsibility for misuse of this software.

### Privacy & Data Collection
- **No Telemetry**: Symphex collects no usage data or personal information
- **No Analytics**: No tracking, no user behavior monitoring  
- **Offline Capable**: Works completely offline after dependency setup
- **Local Storage**: All data stays on your device

## 🙏 Acknowledgments & Credits

### Core Technologies
- **[yt-dlp](https://github.com/yt-dlp/yt-dlp)** - Powerful, feature-rich YouTube downloader
- **[FFmpeg](https://ffmpeg.org/)** - Complete multimedia processing framework
- **[Avalonia UI](https://avaloniaui.net/)** - Cross-platform .NET UI framework
- **[.NET](https://dotnet.microsoft.com/)** - Microsoft's cross-platform development framework

### APIs & Services  
- **iTunes Search API** - Album artwork and metadata lookup
- **Deezer API** - Additional album artwork sources
- **YouTube** - Video platform (please respect their ToS)

### Community
- Special thanks to all contributors and users who report issues
- Open source community for inspiration and code examples
- Beta testers who helped identify and fix bugs

## 🔗 Links & Resources

- **🏠 Homepage**: [Coming Soon]
- **📱 Download**: [Latest Releases](https://github.com/CyberNilsen/Symphex/releases)
- **🐛 Bug Reports**: [GitHub Issues](https://github.com/CyberNilsen/Symphex/issues)
- **💬 Discussions**: [GitHub Discussions](https://github.com/CyberNilsen/Symphex/discussions)  
- **📚 Documentation**: [Wiki](https://github.com/CyberNilsen/Symphex/wiki)
- **👥 Contributors**: [Contributors Graph](https://github.com/CyberNilsen/Symphex/graphs/contributors)

---

**Made with ❤️ by the open source community** | **Star ⭐ if you find this useful!**
