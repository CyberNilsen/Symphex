using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace Symphex.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly HttpClient httpClient = new();
        private const string GITHUB_API_URL = "https://api.github.com/repos/CyberNilsen/symphex/releases/latest";
        private const string CURRENT_VERSION = "1.3.0";

        [ObservableProperty]
        private string currentVersionText = $"Current Version: {CURRENT_VERSION}";

        [ObservableProperty]
        private string updateStatusText = "Click 'Check for Updates' to see if a new version is available.";

        [ObservableProperty]
        private string latestVersionText = "";

        [ObservableProperty]
        private bool isCheckingForUpdates = false;

        [ObservableProperty]
        private bool isUpdateAvailable = false;

        [ObservableProperty]
        private bool isUpdating = false;

        [ObservableProperty]
        private string updateProgressText = "";

        [ObservableProperty]
        private double updateProgress = 0;

        [ObservableProperty]
        private bool enableAlbumArtDownload = true;

        [ObservableProperty]
        private bool skipThumbnailDownload = false;

        [ObservableProperty]
        private string selectedThumbnailSize = "Maximum Quality";

        [ObservableProperty]
        private bool enableArtworkSelection = true; // Default ON

        [ObservableProperty]
        private int artworkSelectionTimeout = 5; // 5 seconds gives users time to decide

        [ObservableProperty]
        private double albumArtSize = 600; // Default resize size

        public IRelayCommand? BackCommand { get; set; }
        
        private MainWindowViewModel? _mainWindowViewModel;
        public MainWindowViewModel? MainWindowViewModel
        {
            get => _mainWindowViewModel;
            set => _mainWindowViewModel = value;
        }

        [ObservableProperty]
        private List<string> thumbnailSizeOptions = new List<string>
        {
            "Maximum Quality",
            "High Quality (1200x1200)",
            "Medium Quality (600x600)",
            "Low Quality (300x300)"
        };

        [ObservableProperty]
        private string selectedAudioFormat = "MP3";

        [ObservableProperty]
        private List<string> audioFormatOptions = new List<string>
        {
            "MP3",
            "FLAC (Lossless)",
            "WAV (Uncompressed)",
            "M4A (AAC)",
            "Opus",
            "Vorbis (OGG)"
        };

        [ObservableProperty]
        private string selectedBitrate = "320";

        [ObservableProperty]
        private List<string> bitrateOptions = new List<string>();

        // Show bitrate selector only for lossy formats
        public bool ShowBitrateSelector => SelectedAudioFormat == "MP3" || 
                                           SelectedAudioFormat == "M4A (AAC)" || 
                                           SelectedAudioFormat == "Opus" || 
                                           SelectedAudioFormat == "Vorbis (OGG)";

        // Orange warning: Album art disabled but thumbnails enabled
        public bool ShowAlbumArtDisabledWarning => !EnableAlbumArtDownload && SkipThumbnailDownload;

        // Red warning: Both album art and thumbnails disabled
        public bool ShowNoArtworkWarning => !EnableAlbumArtDownload && !SkipThumbnailDownload;

        private string latestDownloadUrl = "";
        private string latestVersion = "";

        public SettingsViewModel()
        {
            CurrentVersionText = $"Current Version: v{CURRENT_VERSION}";
            UpdateBitrateOptions(); // Initialize bitrate options
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                var settings = Services.SettingsService.LoadSettings();
                EnableAlbumArtDownload = settings.EnableAlbumArtDownload;
                SkipThumbnailDownload = settings.SkipThumbnailDownload;
                SelectedThumbnailSize = settings.SelectedThumbnailSize;
                EnableArtworkSelection = settings.EnableArtworkSelection;
                ArtworkSelectionTimeout = settings.ArtworkSelectionTimeout;
                AlbumArtSize = settings.AlbumArtSize;
                
                // Migrate old format names to new ones
                SelectedAudioFormat = MigrateAudioFormat(settings.SelectedAudioFormat);
                SelectedBitrate = settings.SelectedBitrate;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsViewModel] Error loading settings: {ex.Message}");
            }
        }

        private string MigrateAudioFormat(string oldFormat)
        {
            // Migrate old format names to new simplified names
            return oldFormat switch
            {
                "MP3 (Best Quality)" => "MP3",
                "MP3 (320kbps)" => "MP3",
                "AAC (256kbps)" => "M4A (AAC)",
                "M4A (256kbps)" => "M4A (AAC)",
                "M4A (AAC - Best Quality)" => "M4A (AAC)",
                "Opus (192kbps)" => "Opus",
                "Opus (Best Quality)" => "Opus",
                "Vorbis (192kbps)" => "Vorbis (OGG)",
                "Vorbis (OGG - Best Quality)" => "Vorbis (OGG)",
                _ => AudioFormatOptions.Contains(oldFormat) ? oldFormat : "MP3" // Default to MP3 if unknown
            };
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new Services.UserSettings
                {
                    EnableAlbumArtDownload = EnableAlbumArtDownload,
                    SkipThumbnailDownload = SkipThumbnailDownload,
                    SelectedThumbnailSize = SelectedThumbnailSize,
                    EnableArtworkSelection = EnableArtworkSelection,
                    ArtworkSelectionTimeout = ArtworkSelectionTimeout,
                    AlbumArtSize = AlbumArtSize,
                    SelectedAudioFormat = SelectedAudioFormat,
                    SelectedBitrate = SelectedBitrate
                };
                Services.SettingsService.SaveSettings(settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsViewModel] Error saving settings: {ex.Message}");
            }
        }

        partial void OnEnableAlbumArtDownloadChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowNoArtworkWarning));
            OnPropertyChanged(nameof(ShowAlbumArtDisabledWarning));
            SaveSettings();
        }

        partial void OnSkipThumbnailDownloadChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowNoArtworkWarning));
            OnPropertyChanged(nameof(ShowAlbumArtDisabledWarning));
            SaveSettings();
        }

        partial void OnSelectedThumbnailSizeChanged(string value)
        {
            SaveSettings();
        }

        partial void OnEnableArtworkSelectionChanged(bool value)
        {
            SaveSettings();
        }

        partial void OnArtworkSelectionTimeoutChanged(int value)
        {
            SaveSettings();
        }

        partial void OnAlbumArtSizeChanged(double value)
        {
            SaveSettings();
        }

        partial void OnSelectedAudioFormatChanged(string value)
        {
            UpdateBitrateOptions();
            OnPropertyChanged(nameof(ShowBitrateSelector));
            SaveSettings();
            
            // Immediately update MainWindowViewModel if available
            if (_mainWindowViewModel != null)
            {
                _mainWindowViewModel.SelectedAudioFormat = value;
            }
        }

        partial void OnSelectedBitrateChanged(string value)
        {
            SaveSettings();
            
            // Immediately update MainWindowViewModel if available
            if (_mainWindowViewModel != null)
            {
                _mainWindowViewModel.SelectedBitrate = value;
            }
        }

        private void UpdateBitrateOptions()
        {
            BitrateOptions = SelectedAudioFormat switch
            {
                "MP3" => new List<string> { "128", "192", "256", "320" },
                "M4A (AAC)" => new List<string> { "128", "192", "256", "320" },
                "Opus" => new List<string> { "96", "128", "160", "192" },
                "Vorbis (OGG)" => new List<string> { "128", "192", "256", "320" },
                _ => new List<string>()
            };

            // Always default to highest quality (last item in list)
            if (BitrateOptions.Count > 0)
            {
                if (!BitrateOptions.Contains(SelectedBitrate))
                {
                    SelectedBitrate = BitrateOptions.Last(); // Default to highest quality
                }
            }
        }

        [RelayCommand]
        private async Task CheckForUpdates()
        {
            try
            {
                IsCheckingForUpdates = true;
                UpdateStatusText = "Checking GitHub for updates...";
                IsUpdateAvailable = false;

                using var request = new HttpRequestMessage(HttpMethod.Get, GITHUB_API_URL);
                request.Headers.Add("User-Agent", "Symphex-Updater/1.0");

                var response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    UpdateStatusText = "Failed to check for updates. Please try again later.";
                    return;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                if (!root.TryGetProperty("tag_name", out var tagName))
                {
                    UpdateStatusText = "Could not parse version information.";
                    return;
                }

                latestVersion = tagName.GetString()?.TrimStart('v') ?? "";

                if (string.IsNullOrEmpty(latestVersion))
                {
                    UpdateStatusText = "Invalid version information received.";
                    return;
                }

                // Compare versions
                if (IsNewerVersion(latestVersion, CURRENT_VERSION))
                {
                    // Find download URL
                    latestDownloadUrl = FindDownloadUrl(root);

                    if (!string.IsNullOrEmpty(latestDownloadUrl))
                    {
                        IsUpdateAvailable = true;
                        LatestVersionText = $"New version available: v{latestVersion}";
                        UpdateStatusText = "A new version is ready to download!";
                    }
                    else
                    {
                        UpdateStatusText = $"New version v{latestVersion} found, but no compatible download available.";
                    }
                }
                else
                {
                    UpdateStatusText = "You have the latest version!";
                    IsUpdateAvailable = false;
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText = $"Error checking for updates: {ex.Message}";
            }
            finally
            {
                IsCheckingForUpdates = false;
            }
        }

        private string FindDownloadUrl(JsonElement root)
        {
            if (!root.TryGetProperty("assets", out var assets))
            {
                return "";
            }

            string platform = GetPlatformIdentifier();

            // Look for platform-specific assets first - specifically look for "Windows.zip"
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var name) &&
                    asset.TryGetProperty("browser_download_url", out var url))
                {
                    string assetName = name.GetString()?.ToLowerInvariant() ?? "";
                    string downloadUrl = url.GetString() ?? "";

                    // Check for Windows.zip specifically
                    if (assetName == "windows.zip" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        return downloadUrl;
                    }

                    // Check for platform-specific zip
                    if (assetName.Contains(platform) && assetName.EndsWith(".zip"))
                    {
                        return downloadUrl;
                    }
                }
            }

            // Look for generic zip files
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var name) &&
                    asset.TryGetProperty("browser_download_url", out var url))
                {
                    string assetName = name.GetString()?.ToLowerInvariant() ?? "";
                    string downloadUrl = url.GetString() ?? "";

                    // Generic zip file
                    if (assetName.EndsWith(".zip"))
                    {
                        return downloadUrl;
                    }
                }
            }

            return "";
        }

        [RelayCommand]
        private async Task Update()
        {
            if (string.IsNullOrEmpty(latestDownloadUrl))
            {
                UpdateStatusText = "No download URL available.";
                return;
            }

            try
            {
                IsUpdating = true;
                UpdateProgress = 0;
                UpdateProgressText = "Preparing update...";

                // Get application info
                string currentExecutable = Assembly.GetExecutingAssembly().Location;
                string appDirectory = Path.GetDirectoryName(currentExecutable) ?? Environment.CurrentDirectory;
                string processName = Path.GetFileNameWithoutExtension(currentExecutable);

                if (string.IsNullOrEmpty(appDirectory) || !Directory.Exists(appDirectory))
                {
                    appDirectory = Environment.CurrentDirectory;
                }

                UpdateProgress = 20;
                UpdateProgressText = "Downloading update...";

                // Download update
                string tempUpdatePath = Path.Combine(Path.GetTempPath(), $"symphex_update_{Guid.NewGuid():N}.zip");
                await DownloadFileWithProgress(latestDownloadUrl, tempUpdatePath);

                UpdateProgress = 80;
                UpdateProgressText = "Installing update...";

                // Verify download
                if (!File.Exists(tempUpdatePath) || new FileInfo(tempUpdatePath).Length == 0)
                {
                    throw new Exception("Downloaded file is invalid");
                }

                // Test zip validity
                using (var archive = ZipFile.OpenRead(tempUpdatePath))
                {
                    if (!archive.Entries.Any())
                    {
                        throw new Exception("Downloaded archive is empty");
                    }
                }

                // Create updater script
                string updaterPath = CreateUpdaterScript(tempUpdatePath, appDirectory, processName, currentExecutable);

                UpdateProgress = 95;
                UpdateProgressText = "Finalizing...";

                await Task.Delay(500); // Brief pause for user to see progress

                // Launch updater and exit silently
                await LaunchUpdaterAndExit(updaterPath);

            }
            catch (Exception ex)
            {
                UpdateProgressText = $"Update failed: {ex.Message}";
                UpdateStatusText = $"Update failed: {ex.Message}";
                IsUpdating = false;
                UpdateProgress = 0;

                // Clean up on error
                try
                {
                    var tempFiles = Directory.GetFiles(Path.GetTempPath(), "symphex_update_*.zip");
                    foreach (var file in tempFiles)
                    {
                        File.Delete(file);
                    }
                }
                catch { }
            }
        }

        private async Task DownloadFileWithProgress(string url, string filePath)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Symphex-Updater/1.0");
                client.Timeout = TimeSpan.FromMinutes(10);

                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var contentStream = await response.Content.ReadAsStreamAsync();

                if (totalBytes == -1)
                {
                    // No content length, just copy
                    await contentStream.CopyToAsync(fileStream);
                    UpdateProgress = 50;
                }
                else
                {
                    var buffer = new byte[8192];
                    var totalBytesRead = 0L;
                    var bytesRead = 0;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                        var progressPercent = (double)totalBytesRead / totalBytes;
                        UpdateProgress = 10 + (progressPercent * 40); // 10-50% for download
                        UpdateProgressText = $"Downloading update: {progressPercent:P0}";
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Download failed: {ex.Message}", ex);
            }
        }

        private async Task LaunchUpdaterAndExit(string updaterPath)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Launch silently without showing command window
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = updaterPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    Process.Start(startInfo);
                }
                else
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "bash",
                        Arguments = $"\"{updaterPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    Process.Start(startInfo);
                }

                UpdateProgress = 100;
                UpdateProgressText = "Update will complete in background...";

                // Give the updater time to start
                await Task.Delay(1000);

                // Close the application silently
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to launch updater: {ex.Message}", ex);
            }
        }

        private string CreateUpdaterScript(string updateZipPath, string appDirectory, string processName, string currentExecutable)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return CreateWindowsUpdater(updateZipPath, appDirectory, processName, currentExecutable);
            }
            else
            {
                return CreateUnixUpdater(updateZipPath, appDirectory, processName, currentExecutable);
            }
        }

        private string CreateWindowsUpdater(string zipPath, string appDir, string processName, string currentExecutable)
        {
            string updaterPath = Path.Combine(Path.GetTempPath(), $"symphex_updater_{Guid.NewGuid():N}.bat");
            string backupDir = Path.Combine(Path.GetTempPath(), $"symphex_backup_{Guid.NewGuid():N}");
            string tempExtractDir = Path.Combine(Path.GetTempPath(), $"symphex_extract_{Guid.NewGuid():N}");
            string tempExtractDir2 = Path.Combine(Path.GetTempPath(), $"symphex_extract2_{Guid.NewGuid():N}");

            // Ensure we have a valid app directory
            if (string.IsNullOrEmpty(appDir) || !Directory.Exists(appDir))
            {
                appDir = Path.GetDirectoryName(currentExecutable) ?? Environment.CurrentDirectory;
            }

            // Escape paths properly
            zipPath = zipPath.Replace("/", "\\").Replace("\"", "\"\"");
            appDir = appDir.Replace("/", "\\").Replace("\"", "\"\"");
            backupDir = backupDir.Replace("/", "\\").Replace("\"", "\"\"");
            tempExtractDir = tempExtractDir.Replace("/", "\\").Replace("\"", "\"\"");
            tempExtractDir2 = tempExtractDir2.Replace("/", "\\").Replace("\"", "\"\"");

            string script = $@"@echo off
setlocal enabledelayedexpansion

REM Wait for main application to close
timeout /t 3 /nobreak >nul 2>&1

REM Close any remaining processes silently
taskkill /f /im {processName}.exe >nul 2>&1
taskkill /f /im Symphex.exe >nul 2>&1
timeout /t 2 /nobreak >nul 2>&1

REM Create backup silently
if not exist ""{backupDir}"" mkdir ""{backupDir}"" >nul 2>&1
if exist ""{appDir}"" (
    xcopy ""{appDir}\*"" ""{backupDir}\"" /E /H /C /I /Y /Q >nul 2>&1
) else (
    mkdir ""{appDir}"" >nul 2>&1
)

REM Extract first level (GitHub artifact zip)
if not exist ""{tempExtractDir}"" mkdir ""{tempExtractDir}"" >nul 2>&1

powershell.exe -WindowStyle Hidden -NoProfile -ExecutionPolicy Bypass -Command ""try {{ Expand-Archive -Path '{zipPath}' -DestinationPath '{tempExtractDir}' -Force }} catch {{ exit 1 }}"" >nul 2>&1

if !errorlevel! neq 0 (
    if exist ""{backupDir}"" (
        rmdir /s /q ""{appDir}"" >nul 2>&1
        xcopy ""{backupDir}\*"" ""{appDir}\"" /E /H /C /I /Y /Q >nul 2>&1
    )
    goto cleanup
)

REM Look for nested zip files (Windows.zip inside Windows folder)
set ""NESTED_ZIP=""
if exist ""{tempExtractDir}\Windows\Windows.zip"" (
    set ""NESTED_ZIP={tempExtractDir}\Windows\Windows.zip""
) else if exist ""{tempExtractDir}\Windows.zip"" (
    set ""NESTED_ZIP={tempExtractDir}\Windows.zip""
)

REM If we found a nested zip, extract it
if not ""!NESTED_ZIP!""=="""" (
    if not exist ""{tempExtractDir2}"" mkdir ""{tempExtractDir2}"" >nul 2>&1
    powershell.exe -WindowStyle Hidden -NoProfile -ExecutionPolicy Bypass -Command ""try {{ Expand-Archive -Path '!NESTED_ZIP!' -DestinationPath '{tempExtractDir2}' -Force }} catch {{ exit 1 }}"" >nul 2>&1
    if !errorlevel! equ 0 (
        set ""SEARCH_DIR={tempExtractDir2}""
    ) else (
        set ""SEARCH_DIR={tempExtractDir}""
    )
) else (
    set ""SEARCH_DIR={tempExtractDir}""
)

REM Find source directory with executable files
set ""SOURCE_DIR=""

REM First check if executables are directly in search dir
if exist ""!SEARCH_DIR!\*.exe"" (
    set ""SOURCE_DIR=!SEARCH_DIR!""
    goto found_source
)

REM Check Windows folder
if exist ""!SEARCH_DIR!\Windows\*.exe"" (
    set ""SOURCE_DIR=!SEARCH_DIR!\Windows""
    goto found_source
)

REM Check nested Windows folder
if exist ""!SEARCH_DIR!\Windows\Windows\*.exe"" (
    set ""SOURCE_DIR=!SEARCH_DIR!\Windows\Windows""
    goto found_source
)

REM Look in any subdirectory for exe files
for /d %%d in (""!SEARCH_DIR!\*"") do (
    if exist ""%%d\*.exe"" (
        set ""SOURCE_DIR=%%d""
        goto found_source
    )
    REM Check one level deeper
    for /d %%e in (""%%d\*"") do (
        if exist ""%%e\*.exe"" (
            set ""SOURCE_DIR=%%e""
            goto found_source
        )
    )
)

:found_source
if ""!SOURCE_DIR!""=="""" goto cleanup
if not exist ""!SOURCE_DIR!\*"" goto cleanup

REM Clear target directory silently (preserve settings)
pushd ""{appDir}"" >nul 2>&1
for /d %%D in (*) do (
    if /i not ""%%D""==""settings"" if /i not ""%%D""==""config"" if /i not ""%%D""==""data"" (
        rmdir /s /q ""%%D"" >nul 2>&1
    )
)
for %%F in (*) do (
    if /i not ""%%~nxF""==""settings.json"" if /i not ""%%~nxF""==""config.ini"" if /i not ""%%~nxF""==""user.config"" (
        del /q ""%%F"" >nul 2>&1
    )
)
popd >nul 2>&1

REM Copy new files silently
xcopy ""!SOURCE_DIR!\*"" ""{appDir}\"" /E /H /C /I /Y /Q >nul 2>&1

if !errorlevel! neq 0 (
    if exist ""{backupDir}"" (
        rmdir /s /q ""{appDir}"" >nul 2>&1
        xcopy ""{backupDir}\*"" ""{appDir}\"" /E /H /C /I /Y /Q >nul 2>&1
    )
    goto cleanup
)

REM Start updated application silently
cd /d ""{appDir}"" >nul 2>&1

if exist ""Symphex.exe"" (
    start """" ""Symphex.exe"" >nul 2>&1
) else (
    for %%f in (*.exe) do (
        if /i not ""%%f""==""updater.exe"" (
            start """" ""%%f"" >nul 2>&1
            goto cleanup
        )
    )
)

:cleanup
REM Clean up silently
if exist ""{backupDir}"" rmdir /s /q ""{backupDir}"" >nul 2>&1
if exist ""{tempExtractDir}"" rmdir /s /q ""{tempExtractDir}"" >nul 2>&1
if exist ""{tempExtractDir2}"" rmdir /s /q ""{tempExtractDir2}"" >nul 2>&1
if exist ""{zipPath}"" del /q ""{zipPath}"" >nul 2>&1

REM Wait a moment then self-delete
timeout /t 2 /nobreak >nul 2>&1
del ""%~f0"" >nul 2>&1
";

            File.WriteAllText(updaterPath, script);
            return updaterPath;
        }

        private string CreateUnixUpdater(string zipPath, string appDir, string processName, string currentExecutable)
        {
            string updaterPath = Path.Combine(Path.GetTempPath(), $"symphex_updater_{Guid.NewGuid():N}.sh");
            string backupDir = Path.Combine(Path.GetTempPath(), $"symphex_backup_{Guid.NewGuid():N}");
            string tempExtractDir = Path.Combine(Path.GetTempPath(), $"symphex_extract_{Guid.NewGuid():N}");

            // Use string.Format to avoid interpolation issues
            string script = string.Format(@"#!/bin/bash
set -e

echo ""Starting Symphex updater...""

# Wait for main application to close
echo ""Waiting for application to close...""
sleep 5

# Forcefully close any remaining processes
pkill -f {0} 2>/dev/null || true
sleep 2

echo ""Creating backup...""
mkdir -p ""{1}""
cp -rf ""{2}""/* ""{1}/"" 2>/dev/null || true

echo ""Extracting update...""
mkdir -p ""{3}""

if ! unzip -o ""{4}"" -d ""{3}""; then
    echo ""Failed to extract update""
    echo ""Restoring backup...""
    rm -rf ""{2}""/*
    cp -rf ""{1}""/* ""{2}/""
    exit 1
fi

echo ""Installing update...""
# Clear application directory
rm -rf ""{2}""/*

# Copy new files
if ! cp -rf ""{3}""/* ""{2}/""; then
    echo ""Failed to install update""
    echo ""Restoring backup...""
    rm -rf ""{2}""/*
    cp -rf ""{1}""/* ""{2}/""
    exit 1
fi

echo ""Setting permissions...""
cd ""{2}""
chmod +x *.exe 2>/dev/null || true
chmod +x *symphex* 2>/dev/null || true
chmod +x * 2>/dev/null || true

echo ""Starting updated application...""
# Find and start executable
for file in *.exe symphex Symphex; do
    if [ -f ""$file"" ] && [ -x ""$file"" ]; then
        echo ""Starting $file""
        nohup ./$file > /dev/null 2>&1 &
        break
    fi
done

echo ""Cleaning up...""
rm -rf ""{1}"" 2>/dev/null || true
rm -rf ""{3}"" 2>/dev/null || true
rm -f ""{4}"" 2>/dev/null || true

# Self-delete
sleep 3
rm -- ""$0"" 2>/dev/null || true
", processName, backupDir, appDir, tempExtractDir, zipPath);

            File.WriteAllText(updaterPath, script);

            // Make executable
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{updaterPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit();
            }
            catch { }

            return updaterPath;
        }

        [RelayCommand]
        private void Close()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Settings is now a UserControl, not a Window - no need to close
                // Just trigger navigation back in the main window
            }
        }

        private bool IsNewerVersion(string latest, string current)
        {
            try
            {
                string normalizedLatest = NormalizeVersion(latest);
                string normalizedCurrent = NormalizeVersion(current);

                Version latestVersion = new Version(normalizedLatest);
                Version currentVersion = new Version(normalizedCurrent);

                return latestVersion > currentVersion;
            }
            catch
            {
                return false;
            }
        }

        private string NormalizeVersion(string version)
        {
            version = version.TrimStart('v');
            string[] parts = version.Split('.');

            if (parts.Length == 1)
                return $"{parts[0]}.0.0";
            else if (parts.Length == 2)
                return $"{parts[0]}.{parts[1]}.0";

            return version;
        }

        private string GetPlatformIdentifier()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "windows";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "mac";
            else
                return "linux";
        }
    }
}