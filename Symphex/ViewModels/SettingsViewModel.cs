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

            // Look for platform-specific assets first
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var name) &&
                    asset.TryGetProperty("browser_download_url", out var url))
                {
                    string assetName = name.GetString()?.ToLowerInvariant() ?? "";
                    string downloadUrl = url.GetString() ?? "";

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

            // Fallback to source code zip
            if (root.TryGetProperty("zipball_url", out var zipballUrl))
            {
                return zipballUrl.GetString() ?? "";
            }

            return "";
        }

        private string FindBestDownloadAsset(JsonElement assets)
        {
            string platformIdentifier = GetPlatformIdentifier();
            string downloadUrl = "";

            // First: Look for platform-specific zip files
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var name) &&
                    asset.TryGetProperty("browser_download_url", out var url))
                {
                    string assetName = name.GetString()?.ToLowerInvariant() ?? "";
                    string assetUrl = url.GetString() ?? "";

                    // Platform-specific match
                    if (assetName.Contains(platformIdentifier) && assetName.EndsWith(".zip"))
                    {
                        return assetUrl;
                    }
                }
            }

            // Second: Look for generic zip files
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var name) &&
                    asset.TryGetProperty("browser_download_url", out var url))
                {
                    string assetName = name.GetString()?.ToLowerInvariant() ?? "";
                    string assetUrl = url.GetString() ?? "";

                    if (assetName.EndsWith(".zip") && !ContainsPlatformName(assetName))
                    {
                        return assetUrl;
                    }
                }
            }

            return downloadUrl;
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

                // Get application info
                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string processName = Process.GetCurrentProcess().ProcessName;

                UpdateProgress = 10;
                UpdateProgressText = "Downloading update...";

                // Download update
                string tempUpdatePath = Path.Combine(Path.GetTempPath(), $"symphex_update_{Guid.NewGuid():N}.zip");
                await DownloadFile(latestDownloadUrl, tempUpdatePath);

                UpdateProgress = 60;
                UpdateProgressText = "Creating update script...";

                // Create and run updater
                string updaterPath = CreateSimpleUpdater(tempUpdatePath, appDirectory, processName);

                UpdateProgress = 90;
                UpdateProgressText = "Launching updater...";

                await LaunchUpdater(updaterPath);

                UpdateProgress = 100;
                UpdateProgressText = "Update starting...";

                // Close application
                await Task.Delay(2000);
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
            catch (Exception ex)
            {
                UpdateProgressText = $"Update failed: {ex.Message}";
                UpdateStatusText = $"Update failed: {ex.Message}";
                IsUpdating = false;
                UpdateProgress = 0;
            }
        }

        private async Task LaunchUpdater(string updaterPath)
        {
            var process = new Process();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                process.StartInfo.FileName = updaterPath;
            }
            else
            {
                process.StartInfo.FileName = "bash";
                process.StartInfo.Arguments = updaterPath;
            }

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            await Task.Delay(1000); // Give it time to start
        }

        private async Task DownloadFile(string url, string filePath)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Symphex-Updater/1.0");

            using var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using var fileStream = File.Create(filePath);
            await response.Content.CopyToAsync(fileStream);
        }

        private string CreateSimpleUpdater(string zipPath, string appDir, string processName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return CreateWindowsUpdater(zipPath, appDir, processName);
            }
            else
            {
                return CreateUnixUpdater(zipPath, appDir, processName);
            }
        }



        private void ResetUpdateState()
        {
            IsUpdating = false;
            UpdateProgress = 0;
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

        private async Task DownloadUpdateWithProgress(string downloadUrl, string destinationPath)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Symphex-Updater/1.0");
                client.Timeout = TimeSpan.FromSeconds(60);

                var progress = new Progress<double>(percent =>
                {
                    UpdateProgress = 10 + (percent * 40); // 10-50% for download
                    UpdateProgressText = $"Downloading update: {percent:P0}";
                });

                using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var contentStream = await response.Content.ReadAsStreamAsync();

                if (totalBytes == -1)
                {
                    await contentStream.CopyToAsync(fileStream);
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
                        ((IProgress<double>)progress).Report((double)totalBytesRead / totalBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Download failed: {ex.Message}", ex);
            }
        }

        private string GetMainExecutableName(string appDirectory, string processName)
        {
            // Try to find the main executable
            string[] possibleNames = {
                $"{processName}.exe",
                "Symphex.exe",
                "symphex.exe"
            };

            foreach (string name in possibleNames)
            {
                string fullPath = Path.Combine(appDirectory, name);
                if (File.Exists(fullPath))
                {
                    return name;
                }
            }

            // If nothing found, look for any .exe file
            var exeFiles = Directory.GetFiles(appDirectory, "*.exe");
            if (exeFiles.Length > 0)
            {
                return Path.GetFileName(exeFiles[0]);
            }

            // Fallback
            return $"{processName}.exe";
        }

        private async Task LaunchUpdaterAndClose(string updaterPath)
        {
            var updaterProcess = new Process();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                updaterProcess.StartInfo.FileName = "powershell.exe";
                updaterProcess.StartInfo.Arguments = $"-WindowStyle Hidden -ExecutionPolicy Bypass -File \"{updaterPath}\"";
            }
            else
            {
                updaterProcess.StartInfo.FileName = "bash";
                updaterProcess.StartInfo.Arguments = $"\"{updaterPath}\"";
            }

            updaterProcess.StartInfo.UseShellExecute = false;
            updaterProcess.StartInfo.CreateNoWindow = true;
            updaterProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            updaterProcess.Start();

            UpdateProgress = 90;
            UpdateProgressText = "Update in progress...";

            // Give the updater a moment to start
            await Task.Delay(3000);

            UpdateProgress = 100;
            UpdateProgressText = "Closing application...";

            // Close the application
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
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

        private string[] GetAlternativePlatformNames(string platformIdentifier)
        {
            return platformIdentifier.ToLowerInvariant() switch
            {
                "windows" => new[] { "win", "win64", "win32", "windows" },
                "mac" => new[] { "macos", "osx", "darwin", "mac" },
                "linux" => new[] { "linux", "unix" },
                _ => new[] { platformIdentifier }
            };
        }

        private bool ContainsPlatformName(string fileName)
        {
            string[] allPlatformNames = { "windows", "win", "win64", "win32", "mac", "macos", "osx", "darwin", "linux", "unix" };
            return allPlatformNames.Any(platform => fileName.Contains(platform));
        }

        private string GetPlatformIdentifier()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "windows";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "mac"; // Changed from "macos" to match common naming
            else
                return "linux";
        }

        private string CreateUpdaterScript(string updateZipPath, string appDirectory, string appExecutableName, string processName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return CreateWindowsUpdater(updateZipPath, appDirectory, processName);
            }
            else
            {
                return CreateUnixUpdater(updateZipPath, appDirectory, processName);
            }
        }

        private string CreateWindowsUpdater(string zipPath, string appDir, string processName)
        {
            string updaterPath = Path.Combine(Path.GetTempPath(), $"symphex_updater.bat");

            string script = $@"@echo off
timeout /t 5 /nobreak
taskkill /f /im {processName}.exe 2>nul
timeout /t 2 /nobreak

powershell -Command ""
Add-Type -AssemblyName System.IO.Compression.FileSystem;
$tempDir = '{Path.GetTempPath()}symphex_temp';
if (Test-Path $tempDir) {{ Remove-Item $tempDir -Recurse -Force }};
[System.IO.Compression.ZipFile]::ExtractToDirectory('{zipPath}', $tempDir);
Remove-Item '{appDir}\*' -Recurse -Force -Exclude 'symphex_updater.bat';
Copy-Item '$tempDir\*' '{appDir}' -Recurse -Force;
Remove-Item $tempDir -Recurse -Force;
""

cd /d ""{appDir}""
for %%f in (*.exe) do (
    start """" ""%%f""
    goto :end
)
:end
del ""%~f0""
";

            File.WriteAllText(updaterPath, script);
            return updaterPath;
        }

        private string CreateUnixUpdater(string zipPath, string appDir, string processName)
        {
            string updaterPath = Path.Combine(Path.GetTempPath(), "symphex_updater.sh");

            string script = $@"#!/bin/bash
sleep 5
pkill -f {processName} 2>/dev/null
sleep 2

TEMP_DIR=""/tmp/symphex_temp""
rm -rf ""$TEMP_DIR""
mkdir -p ""$TEMP_DIR""

unzip -o ""{zipPath}"" -d ""$TEMP_DIR""
rm -rf ""{appDir}""/*
cp -rf ""$TEMP_DIR""/* ""{appDir}/""
rm -rf ""$TEMP_DIR""

cd ""{appDir}""
chmod +x *.exe 2>/dev/null
chmod +x *symphex* 2>/dev/null

# Find and start executable
for file in *.exe symphex Symphex; do
    if [ -f ""$file"" ] && [ -x ""$file"" ]; then
        nohup ./$file &
        break
    fi
done

rm -- ""$0""
";

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
    }
}