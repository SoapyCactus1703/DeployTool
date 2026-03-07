using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace DeployTool.Services;

public class UpdateService
{
    private static readonly Lazy<UpdateService> _instance = new(() => new UpdateService());
    public static UpdateService Instance => _instance.Value;

    private readonly HttpClient _httpClient;
    private string _githubRepo = "SoapyCactus1703/DeployTool";
    
    public string CurrentVersion { get; }
    public string DownloadUrl { get; private set; } = "";
    public string UpdateInfoUrl { get; private set; } = "";
    
    public event EventHandler<DownloadProgressEventArgs>? DownloadProgressChanged;
    public event EventHandler<string>? UpdateError;

    private UpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "DeployTool-Updater");
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
        
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        CurrentVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
    }

    public void SetGitHubRepo(string repo)
    {
        _githubRepo = repo;
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{_githubRepo}/releases/latest";
            var response = await _httpClient.GetStringAsync(url);
            var release = JsonSerializer.Deserialize<GitHubRelease>(response);
            
            if (release == null || release.Assets == null)
                return null;

            var latestVersion = release.TagName?.TrimStart('v') ?? "0.0.0";
            
            if (IsNewerVersion(latestVersion, CurrentVersion))
            {
                var asset = release.Assets.FirstOrDefault(a => 
                    a.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true ||
                    a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);
                
                if (asset?.BrowserDownloadUrl != null)
                {
                    DownloadUrl = asset.BrowserDownloadUrl;
                    UpdateInfoUrl = release.HtmlUrl ?? "";
                    
                    return new UpdateInfo
                    {
                        Version = latestVersion,
                        ReleaseNotes = release.Body ?? "暂无更新说明",
                        DownloadUrl = asset.BrowserDownloadUrl,
                        ReleaseDate = release.PublishedAt,
                        ReleaseName = release.Name ?? $"v{latestVersion}"
                    };
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            UpdateError?.Invoke(this, $"检查更新失败: {ex.Message}");
            return null;
        }
    }

    private bool IsNewerVersion(string latest, string current)
    {
        try
        {
            var latestParts = latest.Split('.').Select(int.Parse).ToArray();
            var currentParts = current.Split('.').Select(int.Parse).ToArray();
            
            for (int i = 0; i < Math.Max(latestParts.Length, currentParts.Length); i++)
            {
                var latestPart = i < latestParts.Length ? latestParts[i] : 0;
                var currentPart = i < currentParts.Length ? currentParts[i] : 0;
                
                if (latestPart > currentPart) return true;
                if (latestPart < currentPart) return false;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DownloadUpdateAsync(string downloadUrl, string savePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1;

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            var totalBytesRead = 0L;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;

                if (canReportProgress)
                {
                    var progress = (double)totalBytesRead / totalBytes * 100;
                    DownloadProgressChanged?.Invoke(this, new DownloadProgressEventArgs
                    {
                        BytesReceived = totalBytesRead,
                        TotalBytes = totalBytes,
                        ProgressPercentage = progress
                    });
                }
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            UpdateError?.Invoke(this, $"下载更新失败: {ex.Message}");
            return false;
        }
    }

    public bool InstallUpdate(string zipPath, string targetDirectory)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"DeployTool_Update_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            if (zipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(zipPath, tempDir);
            }
            else
            {
                File.Copy(zipPath, Path.Combine(tempDir, Path.GetFileName(zipPath)));
            }

            var currentExePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
            var currentDir = Path.GetDirectoryName(currentExePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            
            var updaterPath = CreateUpdaterScript(tempDir, currentDir);
            
            var psi = new ProcessStartInfo
            {
                FileName = updaterPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };
            
            Process.Start(psi);
            
            Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
            
            return true;
        }
        catch (Exception ex)
        {
            UpdateError?.Invoke(this, $"安装更新失败: {ex.Message}");
            return false;
        }
    }

    private string CreateUpdaterScript(string sourceDir, string targetDir)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "update_deploy_tool.bat");
        var currentProcessId = Environment.ProcessId;
        var exeName = "西林民高部署工具.exe";
        
        var script = $@"@echo off
chcp 65001 >nul
echo 正在更新部署工具，请稍候...
echo.

:wait_process
tasklist /FI ""PID eq {currentProcessId}"" 2>nul | find ""{currentProcessId}"" >nul
if %errorlevel% equ 0 (
    timeout /t 1 /nobreak >nul
    goto wait_process
)

timeout /t 2 /nobreak >nul

echo 正在复制文件...

if exist ""{sourceDir}\{exeName}"" (
    copy /Y ""{sourceDir}\{exeName}"" ""{targetDir}\""
)

if exist ""{sourceDir}\Data"" (
    xcopy /E /Y /I ""{sourceDir}\Data"" ""{targetDir}\Data""
)

echo.
echo 更新完成！正在启动程序...
start """" ""{targetDir}\{exeName}""

rd /s /q ""{sourceDir}"" 2>nul
del ""%~f0""
exit
";

        File.WriteAllText(scriptPath, script);
        return scriptPath;
    }

    public void OpenReleasePage()
    {
        if (!string.IsNullOrEmpty(UpdateInfoUrl))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = UpdateInfoUrl,
                UseShellExecute = true
            });
        }
    }
}

public class UpdateInfo
{
    public string Version { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public DateTime? ReleaseDate { get; set; }
    public string ReleaseName { get; set; } = "";
}

public class DownloadProgressEventArgs : EventArgs
{
    public long BytesReceived { get; set; }
    public long TotalBytes { get; set; }
    public double ProgressPercentage { get; set; }
}

internal class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("body")]
    public string? Body { get; set; }
    
    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }
    
    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }
    
    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

internal class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }
    
    [JsonPropertyName("size")]
    public long Size { get; set; }
}
