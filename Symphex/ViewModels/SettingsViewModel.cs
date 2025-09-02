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
                    // Find the appropriate download asset
                    if (root.TryGetProperty("assets", out var assets))
                    {
                        string platformIdentifier = GetPlatformIdentifier();

                        foreach (var asset in assets.EnumerateArray())
                        {
                            if (asset.TryGetProperty("name", out var name) &&
                                asset.TryGetProperty("browser_download_url", out var downloadUrl))
                            {
                                string assetName = name.GetString()?.ToLowerInvariant() ?? "";

                                if (assetName.Contains(platformIdentifier) &&
                                    (assetName.EndsWith(".zip") || assetName.EndsWith(".exe")))
                                {
                                    latestDownloadUrl = downloadUrl.GetString() ?? "";
                                    break;
                                }
                            }
                        }
                    }

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
                UpdateProgressText = "Downloading update...";

                // Get current application path
                string currentAppPath = Assembly.GetExecutingAssembly().Location;
                string appDirectory = Path.GetDirectoryName(currentAppPath) ?? "";
                string appExecutableName = Path.GetFileName(currentAppPath);

                // Download the update
                string tempUpdatePath = Path.Combine(Path.GetTempPath(), $"symphex_update_{Guid.NewGuid():N}.zip");

                UpdateProgress = 10;
                using var response = await httpClient.GetAsync(latestDownloadUrl);
                response.EnsureSuccessStatusCode();

                UpdateProgress = 40;
                var updateBytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(tempUpdatePath, updateBytes);

                UpdateProgress = 60;
                UpdateProgressText = "Preparing background installer...";

                // Create and launch the background updater
                string updaterScriptPath = await CreateBackgroundUpdater(tempUpdatePath, appDirectory, appExecutableName);

                UpdateProgress = 80;
                UpdateProgressText = "Starting background update...";

                // Launch the background updater
                var updaterProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? updaterScriptPath : "bash",
                        Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "" : updaterScriptPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    }
                };

                updaterProcess.Start();

                UpdateProgress = 90;
                UpdateProgressText = "Update started! Closing application...";

                await Task.Delay(1500); // Give user time to see the progress

                UpdateProgress = 100;
                UpdateProgressText = "Application will now close and restart with the new version.";

                await Task.Delay(1000);

                // Close the application - the updater will handle the rest
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
            catch (Exception ex)
            {
                UpdateProgressText = $"Update failed: {ex.Message}";
                UpdateStatusText = "Update failed. Please try again or download manually.";
                IsUpdating = false;
                UpdateProgress = 0;
            }
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
                var latestParts = latest.Split('.').Select(int.Parse).ToArray();
                var currentParts = current.Split('.').Select(int.Parse).ToArray();

                // Ensure both arrays have the same length
                int maxLength = Math.Max(latestParts.Length, currentParts.Length);
                Array.Resize(ref latestParts, maxLength);
                Array.Resize(ref currentParts, maxLength);

                for (int i = 0; i < maxLength; i++)
                {
                    if (latestParts[i] > currentParts[i]) return true;
                    if (latestParts[i] < currentParts[i]) return false;
                }

                return false; // Versions are equal
            }
            catch
            {
                return false; // Error parsing versions
            }
        }

        private string GetPlatformIdentifier()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "windows";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "macos";
            else
                return "linux";
        }

        private async Task<string> CreateBackgroundUpdater(string updateZipPath, string appDirectory, string appExecutableName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await CreateWindowsBackgroundUpdater(updateZipPath, appDirectory, appExecutableName);
            }
            else
            {
                return await CreateUnixBackgroundUpdater(updateZipPath, appDirectory, appExecutableName);
            }
        }

        private async Task<string> CreateWindowsBackgroundUpdater(string updateZipPath, string appDirectory, string appExecutableName)
        {
            string updaterPath = Path.Combine(Path.GetTempPath(), $"symphex_bg_updater_{Guid.NewGuid():N}.bat");
            string tempExtractPath = Path.Combine(Path.GetTempPath(), $"symphex_extract_{Guid.NewGuid():N}");

            string script = $@"@echo off
setlocal EnableDelayedExpansion

:: Hide console window
if ""%1"" neq ""ELEVATED"" (
    >nul 2>&1 ""%SYSTEMROOT%\system32\cacls.exe"" ""%SYSTEMROOT%\system32\config\system""
    if '!errorlevel!' NEQ '0' (
        echo Set UAC = CreateObject^(""Shell.Application""^) > ""%temp%\getadmin.vbs""
        echo UAC.ShellExecute ""%~s0"", ""ELEVATED"", """", ""runas"", 0 >> ""%temp%\getadmin.vbs""
        ""%temp%\getadmin.vbs""
        del ""%temp%\getadmin.vbs""
        exit /B
    )
)

:: Create log file for debugging
set LOGFILE=""%temp%\symphex_update.log""
echo Symphex Background Updater - %date% %time% > %LOGFILE%

echo Waiting for Symphex to close... >> %LOGFILE%
timeout /t 3 /nobreak > nul

:: Wait for the main application process to close
:WAIT_LOOP
tasklist /fi ""imagename eq {appExecutableName}"" 2>nul | find /i ""{appExecutableName}"" > nul
if not errorlevel 1 (
    echo Application still running, waiting... >> %LOGFILE%
    timeout /t 2 /nobreak > nul
    goto WAIT_LOOP
)

echo Application closed successfully >> %LOGFILE%

:: Create backup
echo Creating backup... >> %LOGFILE%
if exist ""{appDirectory}_backup"" (
    echo Removing old backup... >> %LOGFILE%
    rmdir /s /q ""{appDirectory}_backup"" > nul 2>&1
)
echo Copying current version to backup... >> %LOGFILE%
xcopy ""{appDirectory}"" ""{appDirectory}_backup\"" /e /i /h /y /q > nul 2>&1

:: Extract new version to temporary location
echo Extracting update to temporary location... >> %LOGFILE%
if exist ""{tempExtractPath}"" rmdir /s /q ""{tempExtractPath}"" > nul 2>&1
mkdir ""{tempExtractPath}"" > nul 2>&1

powershell -WindowStyle Hidden -ExecutionPolicy Bypass -Command ""& {{ try {{ Expand-Archive -Path '{updateZipPath}' -DestinationPath '{tempExtractPath}' -Force; Write-Host 'Extract successful' }} catch {{ Write-Host 'Extract failed:' $_.Exception.Message }} }}"" >> %LOGFILE% 2>&1

:: Verify extraction
if not exist ""{tempExtractPath}"" (
    echo ERROR: Extraction failed, aborting update >> %LOGFILE%
    goto CLEANUP
)

:: Remove all files from current application directory
echo Removing old application files... >> %LOGFILE%
pushd ""{appDirectory}""
for /d %%i in (*) do (
    echo Removing directory: %%i >> %LOGFILE%
    rmdir /s /q ""%%i"" > nul 2>&1
)
for %%i in (*) do (
    echo Removing file: %%i >> %LOGFILE%
    del /f /q ""%%i"" > nul 2>&1
)
popd

:: Copy new files to application directory
echo Installing new version... >> %LOGFILE%
xcopy ""{tempExtractPath}\*"" ""{appDirectory}\"" /e /i /h /y /q > nul 2>&1

:: Verify installation
if not exist ""{Path.Combine(appDirectory, appExecutableName)}"" (
    echo ERROR: Installation failed, restoring backup... >> %LOGFILE%
    rmdir /s /q ""{appDirectory}"" > nul 2>&1
    xcopy ""{appDirectory}_backup\*"" ""{appDirectory}\"" /e /i /h /y /q > nul 2>&1
    goto CLEANUP
)

echo Installation successful! >> %LOGFILE%

:: Wait a moment then start the updated application
timeout /t 2 /nobreak > nul
echo Starting updated application... >> %LOGFILE%
start """" ""{Path.Combine(appDirectory, appExecutableName)}""

:CLEANUP
echo Cleaning up temporary files... >> %LOGFILE%
if exist ""{tempExtractPath}"" rmdir /s /q ""{tempExtractPath}"" > nul 2>&1
if exist ""{updateZipPath}"" del /f /q ""{updateZipPath}"" > nul 2>&1

echo Update process completed - %date% %time% >> %LOGFILE%

:: Self-delete this updater script
timeout /t 1 /nobreak > nul
del /f /q ""%~f0"" > nul 2>&1";

            await File.WriteAllTextAsync(updaterPath, script);
            return updaterPath;
        }

        private async Task<string> CreateUnixBackgroundUpdater(string updateZipPath, string appDirectory, string appExecutableName)
        {
            string updaterPath = Path.Combine(Path.GetTempPath(), $"symphex_bg_updater_{Guid.NewGuid():N}.sh");
            string tempExtractPath = Path.Combine(Path.GetTempPath(), $"symphex_extract_{Guid.NewGuid():N}");

            string script = $@"#!/bin/bash

# Create log file for debugging
LOGFILE=""/tmp/symphex_update.log""
echo ""Symphex Background Updater - $(date)"" > ""$LOGFILE""

echo ""Waiting for Symphex to close..."" >> ""$LOGFILE""
sleep 3

# Wait for the main application process to close
while pgrep -f ""{appExecutableName}"" > /dev/null 2>&1; do
    echo ""Application still running, waiting..."" >> ""$LOGFILE""
    sleep 2
done

echo ""Application closed successfully"" >> ""$LOGFILE""

# Create backup
echo ""Creating backup..."" >> ""$LOGFILE""
if [ -d ""{appDirectory}_backup"" ]; then
    echo ""Removing old backup..."" >> ""$LOGFILE""
    rm -rf ""{appDirectory}_backup"" 2>/dev/null || true
fi
echo ""Copying current version to backup..."" >> ""$LOGFILE""
cp -r ""{appDirectory}"" ""{appDirectory}_backup"" 2>/dev/null || true

# Extract new version to temporary location
echo ""Extracting update to temporary location..."" >> ""$LOGFILE""
rm -rf ""{tempExtractPath}"" 2>/dev/null || true
mkdir -p ""{tempExtractPath}""

if command -v unzip >/dev/null 2>&1; then
    unzip -o ""{updateZipPath}"" -d ""{tempExtractPath}"" >> ""$LOGFILE"" 2>&1
else
    echo ""ERROR: unzip not available, cannot extract update"" >> ""$LOGFILE""
    exit 1
fi

# Verify extraction
if [ ! -d ""{tempExtractPath}"" ] || [ -z ""$(ls -A {tempExtractPath} 2>/dev/null)"" ]; then
    echo ""ERROR: Extraction failed, aborting update"" >> ""$LOGFILE""
    exit 1
fi

# Remove all files from current application directory
echo ""Removing old application files..."" >> ""$LOGFILE""
rm -rf ""{appDirectory}""/*

# Copy new files to application directory
echo ""Installing new version..."" >> ""$LOGFILE""
cp -rf ""{tempExtractPath}""/* ""{appDirectory}""/

# Make executable file executable
chmod +x ""{Path.Combine(appDirectory, appExecutableName)}"" 2>/dev/null || true

# Verify installation
if [ ! -f ""{Path.Combine(appDirectory, appExecutableName)}"" ]; then
    echo ""ERROR: Installation failed, restoring backup..."" >> ""$LOGFILE""
    rm -rf ""{appDirectory}""/*
    cp -rf ""{appDirectory}_backup""/* ""{appDirectory}""/
    chmod +x ""{Path.Combine(appDirectory, appExecutableName)}"" 2>/dev/null || true
    exit 1
fi

echo ""Installation successful!"" >> ""$LOGFILE""

# Wait a moment then start the updated application
sleep 2
echo ""Starting updated application..."" >> ""$LOGFILE""
nohup ""{Path.Combine(appDirectory, appExecutableName)}"" > /dev/null 2>&1 &

# Cleanup
echo ""Cleaning up temporary files..."" >> ""$LOGFILE""
rm -rf ""{tempExtractPath}"" 2>/dev/null || true
rm -f ""{updateZipPath}"" 2>/dev/null || true

echo ""Update process completed - $(date)"" >> ""$LOGFILE""

# Self-delete this updater script
sleep 1
rm -- ""$0""";

            await File.WriteAllTextAsync(updaterPath, script);

            // Make script executable
            var chmodProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{updaterPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            chmodProcess.Start();
            await chmodProcess.WaitForExitAsync();

            return updaterPath;
        }
    }
}