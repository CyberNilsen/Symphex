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
            catch (Exception ex)
            {
                // Silent fail - dependency setup is optional
            }
        }

        private async Task SetupOrDownloadYtDlp()
        {
            try
            {
                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

                string[] possiblePaths = {
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
            catch (Exception ex)
            {
                YtDlpPath = "";
            }
        }

        private async Task SetupOrDownloadFfmpeg()
        {
            try
            {
                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

                string[] possiblePaths = {
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
            catch (Exception ex)
            {
                FfmpegPath = "";
            }
        }

        public async Task DownloadYtDlp()
        {
            string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            string toolsDir = Path.Combine(appDirectory, "tools");

            if (!Directory.Exists(toolsDir))
            {
                Directory.CreateDirectory(toolsDir);
            }

            string ytDlpPath = Path.Combine(toolsDir, YtDlpExecutableName);
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

            string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            string toolsDir = Path.Combine(appDirectory, "tools");

            if (!Directory.Exists(toolsDir))
            {
                Directory.CreateDirectory(toolsDir);
            }

            using (var localHttpClient = new HttpClient())
            {
                localHttpClient.Timeout = TimeSpan.FromMinutes(10);

                var response = await localHttpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();

                var zipBytes = await response.Content.ReadAsByteArrayAsync();

                string zipPath = Path.Combine(toolsDir, "ffmpeg.zip");
                await File.WriteAllBytesAsync(zipPath, zipBytes);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    await ExtractFfmpegWindows(zipPath, toolsDir);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    await ExtractFfmpegMacOS(zipPath, toolsDir);
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
            catch (Exception ex)
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
            catch (Exception ex)
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