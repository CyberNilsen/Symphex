# Changelog

All notable changes to Symphex will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [v1.2.0] - 2025-09-06

### 🎉 Added
- **Batch Processing**: Multiple Spotify links now download 8 songs simultaneously for faster processing
- **Concurrent Downloads**: Process multiple songs at the same time for improved efficiency

### 🚀 Improved
- **Performance**: Significantly reduced download times for playlists and albums
- **Download Speed**: Optimized processing pipeline for faster batch operations

### 🐛 Fixed
- **macOS Download Folder Bug**: Fixed issue where opening the download folder would fail on macOS
- **Folder Access**: Improved cross-platform folder opening reliability

### 🔧 Technical
- Enhanced batch processing architecture
- Improved error handling for concurrent operations
- Better resource management during multiple downloads

---

## [v1.1.0] - 2025-09-02

### 🎉 Added
- **Spotify Support**: Now accepts Spotify track, album, and playlist URLs
- **Settings Page**: New settings panel for updating the program
- **Smart Matching**: Automatically finds YouTube matches for Spotify content
- **Multi-Platform Support**: Extended beyond YouTube to include Spotify and other platforms

### 🚀 Improved
- **User Interface**: Enhanced GUI with new settings panel
- **Content Discovery**: Intelligent matching system for cross-platform content
- **User Experience**: Streamlined workflow for different music platforms

### 🔧 Technical
- Integrated Spotify API functionality
- Enhanced URL parsing and platform detection
- Improved matching algorithms for cross-platform content

---

## [v1.0.0] - 2025-08-16

### 🎉 Initial Release
- **Core Functionality**: Download music from YouTube and convert to high-quality MP3
- **Smart Song Detection**: Automatically extracts song title, artist, and album information
- **Album Art Integration**: Searches iTunes and Deezer APIs for proper album artwork
- **Automatic Metadata**: Embeds song information directly into MP3 files
- **Cross-Platform Support**: Works on Windows, macOS, and Linux
- **Auto-Installation**: Automatically downloads required dependencies (yt-dlp and FFmpeg)
- **Search Support**: Enter URLs directly or search by song title and artist
- **Progress Tracking**: Real-time download progress with visual feedback

### 🎨 User Interface
- Clean, modern dark-themed GUI with intuitive controls
- Split-panel design with download controls and live preview
- Real-time song information and album art preview
- Progress indicators and visual feedback

### 🔧 Technical Features
- Built with Avalonia UI and .NET 6+
- MVVM architecture with CommunityToolkit.Mvvm
- Automatic dependency management (yt-dlp, FFmpeg)
- Smart filename sanitization and metadata cleaning
- Cross-platform file system integration

### 📁 File Management
- Organized downloads in `~/Music/Symphex Downloads/` folder
- Clean filename format: `Song Title - Artist Name.mp3`
- Automatic folder creation and management

---

## [Pre-release] - 2025-08-06

### ⚠️ Known Issues (Pre-release)
- GUI stability issues - interface may break during certain operations
- **Linux/macOS**: Limited testing on these platforms, stability not guaranteed
- **Windows**: Album art may not work if FFmpeg and yt-dlp are in system PATH instead of app-managed installation
- **GUI Freezing**: Interface freezes when downloading FFmpeg dependencies
- **Linux**: False positive detection of FFmpeg and yt-dlp in system PATH when they're not actually available

### 🧪 Experimental Features
- Basic YouTube downloading functionality
- Initial metadata detection
- Prototype album art integration
- Cross-platform build testing

---

## Development Notes

### Version Numbering
- **Major.Minor.Patch** format (Semantic Versioning)
- Major: Breaking changes or significant new features
- Minor: New features, backward compatible
- Patch: Bug fixes, small improvements

### Platform Support
- **Windows**: Primary development and testing platform
- **macOS**: Tested on Apple Silicon
- **Linux**: Community testing, various distributions supported

### Dependencies
- **yt-dlp**: Automatically managed, latest stable version
- **FFmpeg**: Platform-specific static builds
- **.NET**: Runtime version 6.0 or higher required

### Contributing
See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on submitting changes and reporting issues.

### Support
- **Issues**: [GitHub Issues](https://github.com/CyberNilsen/Symphex/issues)
- **Discussions**: [GitHub Discussions](https://github.com/CyberNilsen/Symphex/discussions)
- **Documentation**: [Wiki](https://github.com/CyberNilsen/Symphex/wiki)
