using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading.Tasks;
using CliWrap;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Symphex.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string downloadUrl = "";

        [ObservableProperty]
        private string statusText = "🎵 Ready to download music...";

        [ObservableProperty]
        private string cliOutput = "Symphex Music Downloader v1.0\n" +
                                   "=============================\n" +
                                   "Ready to process downloads...\n\n";

        [ObservableProperty]
        private double downloadProgress = 0;

        [ObservableProperty]
        private bool isDownloading = false;

        [ObservableProperty]
        private string downloadFolder;

        private string YtDlpPath { get; set; } = "";
        private string YtDlpExecutableName => GetYtDlpExecutableName();

        public MainWindowViewModel()
        {
            SetupDownloadFolder();

            if (!Directory.Exists(DownloadFolder))
            {
                Directory.CreateDirectory(DownloadFolder);
            }

            SetupPortableYtDlp();
        }

        private void SetupDownloadFolder()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                DownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Symphex Downloads");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                DownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music", "Symphex Downloads");
            }
            else
            {
                DownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music", "Symphex Downloads");
            }
        }

        private string GetYtDlpExecutableName()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "yt-dlp.exe" : "yt-dlp";
        }

        private string GetYtDlpDownloadUrl()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_macos";
            else
                return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp";
        }

        private void LogToCli(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            CliOutput += $"[{timestamp}] {message}\n";
        }

        private void SetupPortableYtDlp()
        {
            try
            {
                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

                string[] possiblePaths = {
                    Path.Combine(appDirectory, "tools", YtDlpExecutableName),
                    Path.Combine(appDirectory, YtDlpExecutableName),
                    YtDlpExecutableName
                };

                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path) || path == YtDlpExecutableName)
                    {
                        YtDlpPath = path;

                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(path))
                        {
                            MakeExecutable(path);
                        }

                        string location = path == YtDlpExecutableName ? "System PATH" : path;
                        StatusText = $"✅ yt-dlp found at: {location}";
                        LogToCli($"yt-dlp found at: {location}");
                        return;
                    }
                }

                string os = GetCurrentOS();
                StatusText = $"❌ yt-dlp not found! Click 'Get yt-dlp' to download for {os}.";
                LogToCli($"yt-dlp not found for {os}");
                YtDlpPath = "";
            }
            catch (Exception ex)
            {
                StatusText = $"Error setting up yt-dlp: {ex.Message}";
                LogToCli($"ERROR: {ex.Message}");
                YtDlpPath = "";
            }
        }

        private string GetCurrentOS()
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

        [RelayCommand]
        private async Task Download()
        {
            if (string.IsNullOrWhiteSpace(DownloadUrl))
            {
                StatusText = "⚠️ Please enter a URL or search term.";
                LogToCli("WARNING: No URL or search term provided");
                return;
            }

            if (string.IsNullOrEmpty(YtDlpPath))
            {
                StatusText = "❌ yt-dlp not available. Please download it first.";
                LogToCli("ERROR: yt-dlp not available");
                return;
            }

            IsDownloading = true;
            DownloadProgress = 0;
            StatusText = $"🚀 Starting download for: {DownloadUrl}";
            LogToCli($"Starting download: {DownloadUrl}");

            try
            {
                await RealDownload();
                StatusText = $"✅ Download completed! Check your music folder.";
                LogToCli("Download completed successfully");
            }
            catch (Exception ex)
            {
                StatusText = $"❌ Error: {ex.Message}";
                LogToCli($"ERROR: {ex.Message}");
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
            }
        }

        private async Task RealDownload()
        {
            try
            {
                DownloadProgress = 10;
                LogToCli($"Using yt-dlp at: {YtDlpPath}");

                bool isUrl = DownloadUrl.StartsWith("http://") || DownloadUrl.StartsWith("https://");
                string searchPrefix = isUrl ? "" : "ytsearch1:";

                LogToCli(isUrl ? "Direct URL detected" : "Search term detected - will search YouTube");

                var output = new StringBuilder();
                var error = new StringBuilder();

                string args;
                if (isUrl)
                {
                    args = $"\"{DownloadUrl}\" --extract-audio --audio-format mp3 --audio-quality 0 -o \"{Path.Combine(DownloadFolder, "%(title)s.%(ext)s")}\" --no-playlist";
                }
                else
                {
                    args = $"\"{searchPrefix}{DownloadUrl}\" --extract-audio --audio-format mp3 --audio-quality 0 -o \"{Path.Combine(DownloadFolder, "%(title)s.%(ext)s")}\"";
                }

                LogToCli($"Command: yt-dlp {args}");
                StatusText = isUrl ? "🎵 Downloading from URL..." : "🔍 Searching and downloading...";
                DownloadProgress = 25;

                var result = await Cli.Wrap(YtDlpPath)
                    .WithArguments(args)
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(error))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();

                DownloadProgress = 90;

                if (!string.IsNullOrEmpty(output.ToString()))
                {
                    LogToCli($"yt-dlp output:\n{output}");
                }

                if (result.ExitCode == 0)
                {
                    LogToCli("SUCCESS: Download completed");
                    DownloadProgress = 100;
                }
                else
                {
                    LogToCli($"FAILED: Exit code {result.ExitCode}");
                    if (!string.IsNullOrEmpty(error.ToString()))
                    {
                        LogToCli($"Error details:\n{error}");
                    }
                    throw new Exception($"Download failed with exit code {result.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                LogToCli($"EXCEPTION: {ex.Message}");
                throw;
            }
        }

        [RelayCommand]
        private async Task DownloadYtDlp()
        {
            try
            {
                StatusText = $"⬇️ Downloading yt-dlp for {GetCurrentOS()}...";
                LogToCli($"Downloading yt-dlp for {GetCurrentOS()}");
                IsDownloading = true;
                DownloadProgress = 0;

                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string toolsDir = Path.Combine(appDirectory, "tools");

                if (!Directory.Exists(toolsDir))
                {
                    Directory.CreateDirectory(toolsDir);
                }

                string ytDlpPath = Path.Combine(toolsDir, YtDlpExecutableName);
                string downloadUrl = GetYtDlpDownloadUrl();

                LogToCli($"Download URL: {downloadUrl}");
                LogToCli($"Saving to: {ytDlpPath}");

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    DownloadProgress = 30;
                    var response = await httpClient.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();

                    DownloadProgress = 60;
                    await File.WriteAllBytesAsync(ytDlpPath, await response.Content.ReadAsByteArrayAsync());

                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        MakeExecutable(ytDlpPath);
                        LogToCli("Made executable for Unix system");
                    }

                    DownloadProgress = 100;
                    StatusText = $"✅ yt-dlp downloaded successfully for {GetCurrentOS()}!";
                    LogToCli("yt-dlp download completed successfully");

                    YtDlpPath = ytDlpPath;
                }
            }
            catch (Exception ex)
            {
                StatusText = $"❌ Failed to download yt-dlp: {ex.Message}";
                LogToCli($"ERROR downloading yt-dlp: {ex.Message}");
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
            }
        }

        [RelayCommand]
        private void Clear()
        {
            DownloadUrl = "";
            StatusText = "🎵 Ready to download music...";
            DownloadProgress = 0;
            LogToCli("Input cleared");
        }

        [RelayCommand]
        private void ClearLog()
        {
            CliOutput = "Symphex Music Downloader v1.0\n" +
                        "=============================\n" +
                        "Log cleared...\n\n";
        }

        [RelayCommand]
        private void OpenFolder()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("explorer.exe", DownloadFolder);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", DownloadFolder);
                }
                else
                {
                    Process.Start("xdg-open", DownloadFolder);
                }
                LogToCli("Opened download folder");
            }
            catch (Exception ex)
            {
                LogToCli($"ERROR opening folder: {ex.Message}");
            }
        }
    }
}