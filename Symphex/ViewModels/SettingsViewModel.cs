using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
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
        private const string CURRENT_VERSION = "1.0.0"; // Update this with your actual version

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

        private string latestDownloadUrl = "";
        private string latestVersion = "";

        public SettingsViewModel()
        {
            CurrentVersionText = $"Current Version: v{CURRENT_VERSION}";
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
                UpdateProgressText = "Starting update...";

                // Get application info - FIXED: Use better method to get app directory
                string currentExecutable = Assembly.GetExecutingAssembly().Location;
                string appDirectory = Path.GetDirectoryName(currentExecutable) ?? Environment.CurrentDirectory;
                string processName = Path.GetFileNameWithoutExtension(currentExecutable);

                // Ensure we have the correct app directory
                if (string.IsNullOrEmpty(appDirectory) || !Directory.Exists(appDirectory))
                {
                    appDirectory = Environment.CurrentDirectory;
                }

                UpdateProgress = 10;
                UpdateProgressText = $"App Directory: {appDirectory}";
                await Task.Delay(1000); // Let user see the directory

                UpdateProgressText = "Downloading update...";

                // Download update with progress
                string tempUpdatePath = Path.Combine(Path.GetTempPath(), $"symphex_update_{Guid.NewGuid():N}.zip");
                await DownloadFileWithProgress(latestDownloadUrl, tempUpdatePath);

                UpdateProgress = 60;
                UpdateProgressText = "Verifying download...";

                // Verify the download
                if (!File.Exists(tempUpdatePath) || new FileInfo(tempUpdatePath).Length == 0)
                {
                    throw new Exception("Downloaded file is invalid or empty");
                }

                // Test if the zip file is valid
                try
                {
                    using var archive = ZipFile.OpenRead(tempUpdatePath);
                    if (!archive.Entries.Any())
                    {
                        throw new Exception("Downloaded archive is empty");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Downloaded archive is corrupted: {ex.Message}");
                }

                UpdateProgress = 70;
                UpdateProgressText = "Creating update script...";

                // Create and run updater with the CORRECT app directory
                string updaterPath = CreateUpdaterScript(tempUpdatePath, appDirectory, processName, currentExecutable);

                UpdateProgress = 90;
                UpdateProgressText = "Launching updater...";

                await LaunchUpdaterAndExit(updaterPath);

            }
            catch (Exception ex)
            {
                UpdateProgressText = $"Update failed: {ex.Message}";
                UpdateStatusText = $"Update failed: {ex.Message}";
                IsUpdating = false;
                UpdateProgress = 0;

                // Clean up temp file on error
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
                    // Use Process.Start with cmd.exe to ensure the window stays visible
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/k \"{updaterPath}\"", // /k keeps window open, /c would close it
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WindowStyle = ProcessWindowStyle.Normal
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
                        CreateNoWindow = true
                    };

                    Process.Start(startInfo);
                }

                UpdateProgress = 95;
                UpdateProgressText = "Updater launched successfully...";

                // Give the updater time to start
                await Task.Delay(1500);

                UpdateProgress = 100;
                UpdateProgressText = "Closing application...";

                // Close the application
                await Task.Delay(500);
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

            // Ensure we have a valid app directory - use the current executable's directory if appDir is empty
            if (string.IsNullOrEmpty(appDir) || !Directory.Exists(appDir))
            {
                appDir = Path.GetDirectoryName(currentExecutable) ?? Environment.CurrentDirectory;
            }

            // Ensure paths use proper Windows format and escape quotes
            zipPath = zipPath.Replace("/", "\\").Replace("\"", "\"\"");
            appDir = appDir.Replace("/", "\\").Replace("\"", "\"\"");
            backupDir = backupDir.Replace("/", "\\").Replace("\"", "\"\"");
            tempExtractDir = tempExtractDir.Replace("/", "\\").Replace("\"", "\"\"");

            string script = $@"@echo off
setlocal enabledelayedexpansion
title Symphex Updater
echo ============================================
echo Starting Symphex updater...
echo ============================================
echo.
echo Application Directory: ""{appDir}""
echo Backup Directory: ""{backupDir}""
echo Extract Directory: ""{tempExtractDir}""
echo Zip File: ""{zipPath}""
echo.

REM Wait for main application to close
echo Waiting for application to close...
timeout /t 5 /nobreak > nul

REM Forcefully close any remaining processes
echo Closing any remaining {processName} processes...
taskkill /f /im {processName}.exe 2>nul
taskkill /f /im Symphex.exe 2>nul
timeout /t 2 /nobreak > nul

echo Creating backup...
if not exist ""{backupDir}"" mkdir ""{backupDir}""
if exist ""{appDir}"" (
    echo Backing up current installation...
    xcopy ""{appDir}\*"" ""{backupDir}\"" /E /H /C /I /Y /Q > nul
    echo Backup completed successfully.
) else (
    echo Warning: Application directory not found: ""{appDir}""
    echo Creating application directory...
    mkdir ""{appDir}""
)

echo.
echo Extracting update...
if not exist ""{tempExtractDir}"" mkdir ""{tempExtractDir}""

echo Extracting: ""{zipPath}""
echo To: ""{tempExtractDir}""
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ""try {{ Expand-Archive -Path '{zipPath}' -DestinationPath '{tempExtractDir}' -Force; Write-Host 'Extract SUCCESS' }} catch {{ Write-Host 'Extract ERROR:' $_.Exception.Message; exit 1 }}""

if !errorlevel! neq 0 (
    echo Failed to extract update
    echo Restoring backup...
    if exist ""{backupDir}"" (
        rmdir /s /q ""{appDir}"" 2>nul
        xcopy ""{backupDir}\*"" ""{appDir}\"" /E /H /C /I /Y /Q > nul
    )
    goto cleanup
)

echo.
echo Installing update...

REM Find the actual extracted folder (might be nested in Windows folder)
set ""SOURCE_DIR={tempExtractDir}""
if exist ""{tempExtractDir}\Windows"" (
    set ""SOURCE_DIR={tempExtractDir}\Windows""
    echo Found Windows subfolder, using as source.
) else (
    REM Look for any folder containing exe files
    for /d %%d in (""{tempExtractDir}\*"") do (
        if exist ""%%d\*.exe"" (
            set ""SOURCE_DIR=%%d""
            echo Found executable folder: %%d
            goto found_source
        )
    )
)
:found_source

echo Source directory: ""!SOURCE_DIR!""
echo Target directory: ""{appDir}""

REM Verify source directory has files
if not exist ""!SOURCE_DIR!\*"" (
    echo ERROR: No files found in source directory
    goto cleanup
)

echo.
echo Clearing target directory (keeping settings)...
REM Clear application directory but preserve settings
pushd ""{appDir}""
for /d %%D in (*) do (
    if /i not ""%%D""==""settings"" if /i not ""%%D""==""config"" if /i not ""%%D""==""data"" (
        echo Removing directory: %%D
        rmdir /s /q ""%%D"" 2>nul
    )
)
for %%F in (*) do (
    if /i not ""%%~nxF""==""settings.json"" if /i not ""%%~nxF""==""config.ini"" if /i not ""%%~nxF""==""user.config"" (
        echo Removing file: %%F
        del /q ""%%F"" 2>nul
    )
)
popd

echo.
echo Copying new files...
echo From: ""!SOURCE_DIR!""
echo To: ""{appDir}""
xcopy ""!SOURCE_DIR!\*"" ""{appDir}\"" /E /H /C /I /Y /F

if !errorlevel! neq 0 (
    echo.
    echo Failed to install update - restoring backup...
    if exist ""{backupDir}"" (
        rmdir /s /q ""{appDir}"" 2>nul
        xcopy ""{backupDir}\*"" ""{appDir}\"" /E /H /C /I /Y /Q > nul
        echo Backup restored successfully.
    )
    goto cleanup
)

echo.
echo Update installed successfully!
echo.

echo Starting updated application...
cd /d ""{appDir}""

REM Try to find and start the main executable
set ""STARTED=0""
if exist ""Symphex.exe"" (
    echo Starting Symphex.exe
    start """" ""Symphex.exe""
    set ""STARTED=1""
) else (
    for %%f in (*.exe) do (
        if /i not ""%%f""==""updater.exe"" (
            echo Starting %%f
            start """" ""%%f""
            set ""STARTED=1""
            goto cleanup
        )
    )
)

if ""!STARTED!""==""0"" (
    echo WARNING: No executable found to start
    echo Please manually start the application from: ""{appDir}""
)

:cleanup
echo.
echo Cleaning up temporary files...
if exist ""{backupDir}"" (
    echo Removing backup directory...
    rmdir /s /q ""{backupDir}"" 2>nul
)
if exist ""{tempExtractDir}"" (
    echo Removing extraction directory...
    rmdir /s /q ""{tempExtractDir}"" 2>nul
)
if exist ""{zipPath}"" (
    echo Removing download file...
    del /q ""{zipPath}"" 2>nul
)

echo.
echo ============================================
echo Update process completed successfully!
echo ============================================
echo.
echo Press any key to close this window...
pause >nul

REM Self-delete
del ""%~f0"" 2>nul
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
                var settingsWindow = desktop.Windows.FirstOrDefault(w => w is Symphex.Views.SettingsWindow);
                settingsWindow?.Close();
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