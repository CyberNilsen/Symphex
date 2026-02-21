using CliWrap;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Symphex.Services
{
    public class DependencyManager
    {
        private readonly HttpClient httpClient = new();

        public string YtDlpPath { get; private set; } = "";
        public string FfmpegPath { get; private set; } = "";

        private string YtDlpExecutableName => GetYtDlpExecutableName();
        private string FfmpegExecutableName => GetFfmpegExecutableName();

        public async Task AutoInstallDependencies()
        {
            try
            {
                // Check and auto-install yt-dlp
                await SetupOrDownloadYtDlp();

                // Check and auto-install FFmpeg
                await SetupOrDownloadFfmpeg();
            }
            catch (Exception)
            {
                // Silent fail - dependency setup is optional
            }
        }

        private async Task SetupOrDownloadYtDlp()
        {
            try
            {
                // Use appropriate data directory based on OS
                string appDataDir = GetAppDataDirectory();
                
                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

                string[] possiblePaths = {
                    Path.Combine(appDataDir, YtDlpExecutableName),
                    Path.Combine(appDirectory, "tools", YtDlpExecutableName),
                    Path.Combine(appDirectory, YtDlpExecutableName)
                };

                // First check local paths
                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        YtDlpPath = path;

                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            MakeExecutable(path);
                        }

                        return;
                    }
                }

                // Then check system PATH
                if (await IsExecutableInPath(YtDlpExecutableName))
                {
                    YtDlpPath = YtDlpExecutableName;
                    return;
                }

                // If not found, auto-download
                await AutoDownloadYtDlp();
            }
            catch (Exception)
            {
                YtDlpPath = "";
            }
        }

        private async Task SetupOrDownloadFfmpeg()
        {
            try
            {
                // Use appropriate data directory based on OS
                string appDataDir = GetAppDataDirectory();
                
                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

                string[] possiblePaths = {
                    Path.Combine(appDataDir, FfmpegExecutableName),
                    Path.Combine(appDataDir, "ffmpeg", "bin", FfmpegExecutableName),
                    Path.Combine(appDataDir, "bin", FfmpegExecutableName),
                    Path.Combine(appDirectory, "tools", FfmpegExecutableName),
                    Path.Combine(appDirectory, "tools", "ffmpeg", "bin", FfmpegExecutableName),
                    Path.Combine(appDirectory, "tools", "bin", FfmpegExecutableName),
                    Path.Combine(appDirectory, FfmpegExecutableName)
                };

                // First check local paths
                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        FfmpegPath = path;

                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            MakeExecutable(path);
                        }

                        return;
                    }
                }

                // Check system PATH
                if (await IsExecutableInPath(FfmpegExecutableName))
                {
                    FfmpegPath = FfmpegExecutableName;
                    return;
                }

                // If not found, auto-download (except for Linux)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    FfmpegPath = "";
                }
                else
                {
                    await AutoDownloadFfmpeg();
                }
            }
            catch (Exception)
            {
                FfmpegPath = "";
            }
        }

        public async Task DownloadYtDlp()
        {
            // Use appropriate data directory based on OS
            string appDataDir = GetAppDataDirectory();

            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }

            string ytDlpPath = Path.Combine(appDataDir, YtDlpExecutableName);
            string downloadUrl = GetYtDlpDownloadUrl();

            using (var localHttpClient = new HttpClient())
            {
                localHttpClient.Timeout = TimeSpan.FromMinutes(5);
                var response = await localHttpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();

                await File.WriteAllBytesAsync(ytDlpPath, await response.Content.ReadAsByteArrayAsync());

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    MakeExecutable(ytDlpPath);
                }

                YtDlpPath = ytDlpPath;
            }
        }

        public async Task DownloadFfmpeg()
        {
            string downloadUrl = GetFfmpegDownloadUrl();

            if (string.IsNullOrEmpty(downloadUrl))
            {
                throw new InvalidOperationException("Linux users should install FFmpeg via package manager (apt install ffmpeg)");
            }

            // Use appropriate data directory based on OS
            string appDataDir = GetAppDataDirectory();

            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }

            using (var localHttpClient = new HttpClient())
            {
                localHttpClient.Timeout = TimeSpan.FromMinutes(10);

                var response = await localHttpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();

                var zipBytes = await response.Content.ReadAsByteArrayAsync();

                string zipPath = Path.Combine(appDataDir, "ffmpeg.zip");
                await File.WriteAllBytesAsync(zipPath, zipBytes);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    await ExtractFfmpegWindows(zipPath, appDataDir);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    await ExtractFfmpegMacOS(zipPath, appDataDir);
                }

                File.Delete(zipPath);

                // Re-run setup to find the extracted executable
                await SetupOrDownloadFfmpeg();
            }
        }

        private async Task AutoDownloadYtDlp()
        {
            try
            {
                await DownloadYtDlp();
            }
            catch (Exception)
            {
                YtDlpPath = "";
            }
        }

        private async Task AutoDownloadFfmpeg()
        {
            try
            {
                await DownloadFfmpeg();
            }
            catch (Exception)
            {
                FfmpegPath = "";
            }
        }

        private string GetYtDlpExecutableName()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "yt-dlp.exe" : "yt-dlp";
        }

        private string GetFfmpegExecutableName()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
        }

        private string GetYtDlpDownloadUrl()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_macos";
            else
                return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux";
        }

        private string GetFfmpegDownloadUrl()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "https://evermeet.cx/ffmpeg/getrelease/zip";
            else
                return "";
        }

        private async Task<bool> IsExecutableInPath(string executableName)
        {
            try
            {
                var result = await Cli.Wrap(executableName)
                    .WithArguments("--version")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();

                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private void MakeExecutable(string filePath)
        {
            try
            {
                var result = Process.Start(new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{filePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                result?.WaitForExit();
            }
            catch { }
        }

        public string GetCurrentOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Windows";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "macOS";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "Linux";
            else
                return "Unknown OS";
        }

        private string GetAppDataDirectory()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Symphex",
                    "tools"
                );
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library",
                    "Application Support",
                    "Symphex",
                    "tools"
                );
            }
            else // Linux
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                
                // Priority 1: ~/.config/Symphex/tools (XDG_CONFIG_HOME or fallback)
                var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                var configDir = !string.IsNullOrEmpty(xdgConfigHome) 
                    ? Path.Combine(xdgConfigHome, "Symphex", "tools")
                    : Path.Combine(userProfile, ".config", "Symphex", "tools");
                
                // Try to create config directory
                try
                {
                    if (!Directory.Exists(configDir))
                    {
                        Directory.CreateDirectory(configDir);
                    }
                    return configDir;
                }
                catch
                {
                    // Config directory creation failed, try fallback
                }
                
                // Priority 2: ~/Documents/Symphex/tools
                var documentsDir = Path.Combine(userProfile, "Documents", "Symphex", "tools");
                try
                {
                    if (!Directory.Exists(documentsDir))
                    {
                        Directory.CreateDirectory(documentsDir);
                    }
                    return documentsDir;
                }
                catch
                {
                    // Documents directory creation failed, try final fallback
                }
                
                // Priority 3: ~/Desktop/Symphex/tools (last resort)
                var desktopDir = Path.Combine(userProfile, "Desktop", "Symphex", "tools");
                try
                {
                    if (!Directory.Exists(desktopDir))
                    {
                        Directory.CreateDirectory(desktopDir);
                    }
                    return desktopDir;
                }
                catch
                {
                    // If all else fails, use home directory
                    return Path.Combine(userProfile, "Symphex", "tools");
                }
            }
        }

        private Task ExtractFfmpegWindows(string zipPath, string toolsDir)
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var ffmpegEntry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));

                if (ffmpegEntry != null)
                {
                    string extractPath = Path.Combine(toolsDir, "ffmpeg.exe");
                    ffmpegEntry.ExtractToFile(extractPath, true);
                }
                else
                {
                    string extractDir = Path.Combine(toolsDir, "ffmpeg_temp");

                    if (Directory.Exists(extractDir))
                    {
                        Directory.Delete(extractDir, true);
                    }

                    archive.ExtractToDirectory(extractDir);

                    var ffmpegFiles = Directory.GetFiles(extractDir, "ffmpeg.exe", SearchOption.AllDirectories);
                    if (ffmpegFiles.Length > 0)
                    {
                        string sourcePath = ffmpegFiles[0];
                        string destPath = Path.Combine(toolsDir, "ffmpeg.exe");
                        File.Copy(sourcePath, destPath, true);
                    }

                    Directory.Delete(extractDir, true);
                }
            }
            return Task.CompletedTask;
        }

        private Task ExtractFfmpegMacOS(string zipPath, string toolsDir)
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var ffmpegEntry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith("ffmpeg", StringComparison.OrdinalIgnoreCase) && !e.FullName.Contains("/"));

                if (ffmpegEntry != null)
                {
                    string extractPath = Path.Combine(toolsDir, "ffmpeg");
                    ffmpegEntry.ExtractToFile(extractPath, true);
                    MakeExecutable(extractPath);
                }
            }
            return Task.CompletedTask;
        }
    }
}