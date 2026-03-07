using System.Diagnostics;
using System.IO;
using System.Reflection;
using DeployTool.Models;

namespace DeployTool.Services;

public class ProcessService
{
    private readonly LogService _log = LogService.Instance;
    private readonly string _baseDir;
    private readonly string _labelDir;

    public ProcessService()
    {
        var systemService = SystemService.Instance;
        _baseDir = systemService.GetBaseDirectory();
        _labelDir = systemService.GetLabelDirectory();
    }

    public string GetInstallerArgs(string exePath)
    {
        if (!IsSafePath(exePath))
        {
            _log.Error($"不安全的路径: {exePath}", "安装引擎");
            return "/S";
        }

        var cfgPath = Path.ChangeExtension(exePath, ".cfg");
        if (File.Exists(cfgPath))
        {
            try
            {
                var args = File.ReadAllText(cfgPath).Trim();
                _log.Info($"从配置文件加载参数: {args}", "安装引擎");
                return args;
            }
            catch (Exception ex)
            {
                _log.Warning($"读取配置文件失败: {ex.Message}", "安装引擎");
            }
        }
        return "/S";
    }

    public bool ExecuteProcess(string filePath, string arguments, int timeoutSec = 300)
    {
        if (!IsSafePath(filePath))
        {
            _log.Error($"不安全的路径: {filePath}", "安装引擎");
            return false;
        }

        if (!File.Exists(filePath))
        {
            _log.Error($"文件不存在: {filePath}", "安装引擎");
            return false;
        }

        var ext = Path.GetExtension(filePath).ToLower();
        if (ext != ".exe" && ext != ".msi" && ext != ".msu")
        {
            _log.Error($"不支持的文件类型: {ext}", "安装引擎");
            return false;
        }

        try
        {
            ProcessStartInfo psi;
            if (ext == ".msi")
            {
                psi = new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = $"/i \"{filePath}\" {arguments} /quiet /norestart",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
            }
            else if (ext == ".msu")
            {
                psi = new ProcessStartInfo
                {
                    FileName = "wusa.exe",
                    Arguments = $"\"{filePath}\" /quiet /norestart",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName = filePath,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
            }

            using var process = Process.Start(psi);
            if (process == null)
            {
                _log.Error("无法启动进程", "安装引擎");
                return false;
            }

            if (timeoutSec > 0)
            {
                if (!process.WaitForExit(timeoutSec * 1000))
                {
                    process.Kill();
                    _log.Error($"进程超时 ({timeoutSec}秒)，已终止", "安装引擎");
                    return false;
                }
            }
            else
            {
                process.WaitForExit();
            }

            if (process.ExitCode == 0)
            {
                _log.Success($"安装成功: {Path.GetFileName(filePath)}", "安装引擎");
                return true;
            }
            else
            {
                _log.Error($"安装失败，退出码: {process.ExitCode}", "安装引擎");
                return false;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"执行进程失败: {ex.Message}", "安装引擎");
            return false;
        }
    }

    public bool InstallSoftware(string relativePath, string? customArgs = null)
    {
        var fullPath = Path.Combine(_baseDir, relativePath);
        if (!File.Exists(fullPath))
        {
            _log.Warning($"安装包不存在: {relativePath}", "安装引擎");
            return true;
        }

        var args = customArgs ?? GetInstallerArgs(fullPath);
        _log.Info($"正在安装: {Path.GetFileName(fullPath)}", "安装引擎");
        return ExecuteProcess(fullPath, args);
    }

    public int InstallAllFromDirectory(string relativeDir)
    {
        var dirPath = Path.Combine(_baseDir, relativeDir);
        if (!Directory.Exists(dirPath))
        {
            _log.Warning($"目录不存在: {relativeDir}", "安装引擎");
            return 0;
        }

        var exeFiles = Directory.GetFiles(dirPath, "*.exe");
        var successCount = 0;

        foreach (var file in exeFiles)
        {
            _log.Info($"开始安装: {Path.GetFileName(file)}", "安装引擎");
            var args = GetInstallerArgs(file);
            if (ExecuteProcess(file, args))
            {
                successCount++;
            }
            else
            {
                _log.Warning($"应用安装失败，已跳过: {Path.GetFileName(file)}", "安装引擎");
            }
        }

        _log.Success($"批量安装完成：{successCount} / {exeFiles.Length} 个成功", "安装引擎");
        return successCount;
    }

    public bool InstallGraphicsDriver()
    {
        var flagFile = Path.Combine(_labelDir, "DRIVER_INSTALLED.flag");
        if (File.Exists(flagFile))
        {
            _log.Warning("显卡驱动已安装，跳过。", "驱动安装");
            return true;
        }

        var systemService = SystemService.Instance;
        var cpuInfo = systemService.GetCpuInfo();
        var driveDir = Path.Combine(_baseDir, "Data", "Drive");

        string? driverFile = null;
        if (cpuInfo.Generation >= 7 && cpuInfo.Generation <= 10)
        {
            driverFile = Path.Combine(driveDir, "gfx_win_101.2137.exe");
            _log.Info($"检测到 {cpuInfo.Generation} 代 CPU，使用 gfx_win_101.2137.exe", "驱动安装");
        }
        else if (cpuInfo.Generation >= 11 && cpuInfo.Generation <= 14)
        {
            driverFile = Path.Combine(driveDir, "gfx_win_101.7076.exe");
            _log.Info($"检测到 {cpuInfo.Generation} 代 CPU，使用 gfx_win_101.7076.exe", "驱动安装");
        }
        else
        {
            driverFile = Path.Combine(driveDir, "gfx_win_101.2137.exe");
            _log.Warning($"无法识别 CPU 代数: {cpuInfo.Name}，使用默认驱动", "驱动安装");
        }

        if (!File.Exists(driverFile))
        {
            _log.Error($"未找到显卡驱动文件: {driverFile}", "驱动安装");
            return false;
        }

        var args = GetInstallerArgs(driverFile);
        _log.Info($"正在安装显卡驱动: {Path.GetFileName(driverFile)}", "驱动安装");

        if (ExecuteProcess(driverFile, args))
        {
            File.WriteAllText(flagFile, $"完成于 {DateTime.Now}");
            AppSettings.SetFlag("DRIVER_INSTALLED");
            _log.Success("显卡驱动安装完成", "驱动安装");
            return true;
        }

        _log.Error("显卡驱动安装失败", "驱动安装");
        return false;
    }

    public bool InstallDotNetSdk()
    {
        var flagFile = Path.Combine(_labelDir, "DOTNET_INSTALLED.flag");
        if (File.Exists(flagFile))
        {
            _log.Warning(".NET SDK 已安装，跳过。", "运行库");
            return true;
        }

        var appxDir = Path.Combine(_baseDir, "Data", "Appx");
        
        if (!Directory.Exists(appxDir))
        {
            _log.Error($"目录不存在: {appxDir}", "运行库");
            return false;
        }

        var dotnetFiles = Directory.GetFiles(appxDir, "dotnet*.exe");

        if (dotnetFiles.Length == 0)
        {
            _log.Error("未找到 .NET SDK 安装包", "运行库");
            return false;
        }

        var dotnetFile = dotnetFiles[0];
        var args = GetInstallerArgs(dotnetFile);
        _log.Info($"正在安装 .NET SDK: {Path.GetFileName(dotnetFile)}", "运行库");

        if (ExecuteProcess(dotnetFile, args))
        {
            File.WriteAllText(flagFile, $"完成于 {DateTime.Now}");
            AppSettings.SetFlag("DOTNET_INSTALLED");
            _log.Success(".NET SDK 安装完成", "运行库");
            return true;
        }

        _log.Error(".NET SDK 安装失败", "运行库");
        return false;
    }

    public bool ActivateWindows()
    {
        var appxDir = Path.Combine(_baseDir, "Data", "Appx");
        
        if (!Directory.Exists(appxDir))
        {
            _log.Error($"目录不存在: {appxDir}", "系统激活");
            return false;
        }

        var kmsFiles = Directory.GetFiles(appxDir, "*KMS*.exe");

        if (kmsFiles.Length == 0)
        {
            _log.Error("未找到 KMS 激活工具", "系统激活");
            return false;
        }

        var kmsFile = kmsFiles[0];
        var args = GetInstallerArgs(kmsFile);
        _log.Info($"正在使用 KMS 激活: {Path.GetFileName(kmsFile)}", "系统激活");

        if (ExecuteProcess(kmsFile, args))
        {
            AppSettings.SetFlag("WINDOWS_ACTIVATED");
            _log.Success("系统激活完成", "系统激活");
            return true;
        }

        _log.Warning("激活失败，已跳过", "系统激活");
        return false;
    }

    private bool IsSafePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var basePath = Path.GetFullPath(_baseDir).TrimEnd(Path.DirectorySeparatorChar);
            return fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
