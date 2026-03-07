using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32;
using DeployTool.Models;

namespace DeployTool.Services;

public class OptimizationService
{
    private readonly LogService _log = LogService.Instance;
    private readonly SystemService _systemService = SystemService.Instance;

    public bool SyncTime()
    {
        try
        {
            var ntpServer = AppSettings.NtpServer;
            _log.Info($"正在同步时间到 {ntpServer}...", "时间同步");

            var psi = new ProcessStartInfo
            {
                FileName = "w32tm.exe",
                Arguments = $"/config /manualpeerlist:\"{ntpServer}\" /syncfromflags:MANUAL /reliable:yes /updatenow",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(30000);

            _log.Success("时间同步完成", "时间同步");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"时间同步失败: {ex.Message}", "时间同步");
            return false;
        }
    }

    public bool SetNtpServer(string ntpServer)
    {
        try
        {
            _log.Info($"正在设置NTP服务器: {ntpServer}...", "时间同步");

            var psi = new ProcessStartInfo
            {
                FileName = "w32tm.exe",
                Arguments = $"/config /manualpeerlist:\"{ntpServer}\" /syncfromflags:MANUAL /reliable:yes /update",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(30000);

            var psi2 = new ProcessStartInfo
            {
                FileName = "net.exe",
                Arguments = "stop w32time",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi2)?.WaitForExit(10000);

            var psi3 = new ProcessStartInfo
            {
                FileName = "net.exe",
                Arguments = "start w32time",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi3)?.WaitForExit(10000);

            _log.Success($"NTP服务器已设置为: {ntpServer}", "时间同步");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置NTP服务器失败: {ex.Message}", "时间同步");
            return false;
        }
    }

    public bool HideControlPanelIcon()
    {
        try
        {
            var keyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
            if (key != null)
            {
                key.SetValue("{{5399E694-6CE7-4D70-B33D-3354F7E0519}}", 1, RegistryValueKind.DWord);
                _log.Info("已隐藏控制面板桌面图标", "系统优化");
            }
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"隐藏控制面板图标失败: {ex.Message}", "系统优化");
            return false;
        }
    }

    public bool CleanContextMenu()
    {
        try
        {
            var cleaned = 0;
            
            string[] menuItems = {
                "Software\\Classes\\Directory\\Background\\shell\\Git GUI Here",
                "Software\\Classes\\Directory\\Background\\shell\\Git Bash Here",
                "Software\\Classes\\Directory\\shell\\Git GUI Here",
                "Software\\Classes\\Directory\\shell\\Git Bash Here"
            };

            foreach (var item in menuItems)
            {
                try
                {
                    using var key = Registry.ClassesRoot.OpenSubKey(item, true);
                    if (key != null)
                    {
                        Registry.ClassesRoot.DeleteSubKeyTree(item);
                        cleaned++;
                    }
                }
                catch { }
            }

            if (cleaned > 0)
            {
                _log.Info($"已清理 {cleaned} 个右键菜单项", "系统优化");
            }
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"清理右键菜单失败: {ex.Message}", "系统优化");
            return false;
        }
    }

    public bool OptimizeForSSD()
    {
        try
        {
            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrEmpty(systemDrive))
                return false;

            var drivePath = systemDrive.TrimEnd('\\');
            var isSSD = IsSSD(drivePath);

            if (isSSD)
            {
                _log.Info("检测到 SSD，正在优化...", "系统优化");
                
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "fsutil.exe",
                        Arguments = "behavior set DisableDeleteNotify 1",
                        Verb = "runas",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(psi)?.WaitForExit(5000);
                }
                catch { }

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "fsutil.exe",
                        Arguments = "behavior set DisableLastAccess 1",
                        Verb = "runas",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(psi)?.WaitForExit(5000);
                }
                catch { }

                _log.Success("SSD 优化完成", "系统优化");
            }
            else
            {
                _log.Info("检测到 HDD，跳过 SSD 优化", "系统优化");
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"SSD 优化失败: {ex.Message}", "系统优化");
            return false;
        }
    }

    private bool IsSSD(string drivePath)
    {
        try
        {
            var query = new ManagementObjectSearcher(
                "SELECT MediaType FROM Win32_LogicalDisk WHERE DeviceID = '" + drivePath.TrimEnd('\\') + "'");

            foreach (ManagementObject obj in query.Get())
            {
                var mediaType = Convert.ToInt32(obj["MediaType"]);
                return mediaType == 4;
            }
        }
        catch { }
        return false;
    }

    public bool RunAllOptimizations()
    {
        _log.Info("开始执行系统优化...", "系统优化");

        if (AppSettings.AutoSyncTime)
        {
            SyncTime();
        }

        HideControlPanelIcon();
        CleanContextMenu();
        OptimizeForSSD();
        DiskCleanup();

        _log.Success("系统优化完成", "系统优化");
        return true;
    }

    public bool DiskCleanup()
    {
        try
        {
            _log.Info("正在清理磁盘临时文件...", "磁盘清理");

            var cleanupPaths = new[]
            {
                Path.Combine(Path.GetTempPath(), "*"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp", "*"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "*")
            };

            var cleanedCount = 0;
            foreach (var path in cleanupPaths)
            {
                try
                {
                    var dir = Path.GetDirectoryName(path);
                    if (dir != null && Directory.Exists(dir))
                    {
                        foreach (var file in Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly))
                        {
                            try
                            {
                                File.Delete(file);
                                cleanedCount++;
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }

            _log.Info($"已清理 {cleanedCount} 个临时文件", "磁盘清理");
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"磁盘清理失败: {ex.Message}", "磁盘清理");
            return false;
        }
    }

    public bool SystemOptimize()
    {
        _log.Info("开始执行系统优化...", "系统优化");
        
        SyncTime();
        HideControlPanelIcon();
        CleanContextMenu();
        OptimizeForSSD();
        DiskCleanup();
        
        _log.Success("系统优化完成", "系统优化");
        return true;
    }

    public bool DiskOptimize()
    {
        try
        {
            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrEmpty(systemDrive))
                return false;

            var drivePath = systemDrive.TrimEnd('\\');
            var isSSD = IsSSD(drivePath);

            if (isSSD)
            {
                _log.Info("检测到 SSD，执行 TRIM 优化...", "磁盘优化");
                var psi = new ProcessStartInfo
                {
                    FileName = "defrag.exe",
                    Arguments = $"{systemDrive} /L /O",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi)?.WaitForExit(120000);
                _log.Success("TRIM 优化完成", "磁盘优化");
            }
            else
            {
                _log.Info("检测到 HDD，执行碎片整理...", "磁盘优化");
                var psi = new ProcessStartInfo
                {
                    FileName = "defrag.exe",
                    Arguments = $"{systemDrive} /D",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi)?.WaitForExit(300000);
                _log.Success("碎片整理完成", "磁盘优化");
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"磁盘优化失败: {ex.Message}", "磁盘优化");
            return false;
        }
    }

    public bool SetHighPerformanceMode()
    {
        try
        {
            _log.Info("正在切换到高性能电源计划...", "电源管理");
            
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            
            using var process = Process.Start(psi);
            process?.WaitForExit(10000);
            
            _log.Success("已切换到高性能电源计划", "电源管理");
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"切换电源计划失败: {ex.Message}", "电源管理");
            return false;
        }
    }

    public bool OptimizePerformance()
    {
        try
        {
            _log.Info("正在执行性能优化...", "性能优化");
            
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = "/hibernate off",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi)?.WaitForExit(5000);
                _log.Info("已禁用休眠", "性能优化");
            }
            catch { }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "config \"SysMain\" start= disabled",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi)?.WaitForExit(5000);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "stop \"SysMain\"",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                })?.WaitForExit(5000);
                _log.Info("已禁用 SuperFetch/SysMain", "性能优化");
            }
            catch { }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "config \"WSearch\" start= disabled",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi)?.WaitForExit(5000);
                _log.Info("已禁用 Windows Search 服务", "性能优化");
            }
            catch { }

            _log.Success("性能优化完成", "性能优化");
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"性能优化失败: {ex.Message}", "性能优化");
            return false;
        }
    }

    public bool OptimizeSystemServices()
    {
        try
        {
            _log.Info("正在优化系统服务...", "服务优化");
            
            string[] servicesToDisable = { "Fax", "lfsvc", "MapsBroker", "TrkWks", "TabletInputService" };
            
            foreach (var serviceName in servicesToDisable)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = $"config \"{serviceName}\" start= disabled",
                        Verb = "runas",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(psi)?.WaitForExit(5000);
                }
                catch { }
            }
            
            _log.Success("系统服务优化完成", "服务优化");
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"系统服务优化失败: {ex.Message}", "服务优化");
            return false;
        }
    }

    public bool RemoveBloatware()
    {
        try
        {
            _log.Info("正在移除预装应用...", "应用清理");
            
            string[] appsToRemove = {
                "Microsoft.BingNews",
                "Microsoft.XboxApp",
                "Microsoft.XboxGameOverlay",
                "Microsoft.XboxGamingOverlay",
                "Microsoft.XboxIdentityProvider",
                "Microsoft.XboxSpeechToTextOverlay",
                "Microsoft.SkypeApp",
                "Microsoft.GetHelp",
                "Microsoft.WindowsFeedbackHub"
            };
            
            foreach (var appId in appsToRemove)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"Get-AppxPackage -Name '{appId}' -AllUsers | Remove-AppxPackage -AllUsers\"",
                        Verb = "runas",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(psi)?.WaitForExit(30000);
                }
                catch { }
            }
            
            _log.Success("预装应用清理完成", "应用清理");
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"预装应用清理失败: {ex.Message}", "应用清理");
            return false;
        }
    }

    public bool OptimizeNetwork()
    {
        try
        {
            _log.Info("正在优化网络设置...", "网络优化");
            
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netsh.exe",
                    Arguments = "int tcp set global autotuninglevel=normal",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi)?.WaitForExit(5000);
            }
            catch { }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ipconfig.exe",
                    Arguments = "/flushdns",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi)?.WaitForExit(5000);
                _log.Info("已清除 DNS 缓存", "网络优化");
            }
            catch { }
            
            _log.Success("网络优化完成", "网络优化");
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"网络优化失败: {ex.Message}", "网络优化");
            return false;
        }
    }

    public bool EnhancePrivacy()
    {
        try
        {
            _log.Info("正在调整隐私设置...", "隐私设置");
            
            try
            {
                var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true);
                if (key == null)
                {
                    key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection");
                }
                key?.SetValue("AllowTelemetry", 0, RegistryValueKind.DWord);
                _log.Info("已禁用 Windows 遥测", "隐私设置");
            }
            catch { }

            try
            {
                var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy", true);
                if (key == null)
                {
                    key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy");
                }
                key?.SetValue("TailoredExperiencesWithDiagnosticDataEnabled", 0, RegistryValueKind.DWord);
                _log.Info("已禁用活动历史记录", "隐私设置");
            }
            catch { }
            
            _log.Success("隐私设置调整完成", "隐私设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"隐私设置调整失败: {ex.Message}", "隐私设置");
            return false;
        }
    }

    public bool EnhanceSecurity()
    {
        try
        {
            _log.Info("正在执行安全加固...", "安全加固");
            
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-Command \"Disable-WindowsOptionalFeature -Online -FeatureName SMB1Protocol -NoRestart\"",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi)?.WaitForExit(60000);
                _log.Info("已禁用 SMBv1 协议", "安全加固");
            }
            catch { }

            try
            {
                var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient", true);
                if (key == null)
                {
                    key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient");
                }
                key?.SetValue("EnableMulticast", 0, RegistryValueKind.DWord);
                _log.Info("已禁用 LLMNR", "安全加固");
            }
            catch { }
            
            _log.Success("安全加固完成", "安全加固");
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"安全加固失败: {ex.Message}", "安全加固");
            return false;
        }
    }

    public bool QuickOptimize()
    {
        _log.Info("开始执行快速优化套餐...", "快速优化");
        
        DiskCleanup();
        DiskOptimize();
        SetHighPerformanceMode();
        OptimizePerformance();
        OptimizeNetwork();
        
        _log.Success("快速优化套餐完成", "快速优化");
        return true;
    }

    public bool FullOptimize()
    {
        _log.Info("开始执行完整优化套餐...", "完整优化");
        
        DiskCleanup();
        DiskOptimize();
        SetHighPerformanceMode();
        OptimizePerformance();
        OptimizeSystemServices();
        RemoveBloatware();
        OptimizeNetwork();
        EnhancePrivacy();
        EnhanceSecurity();
        
        _log.Success("完整优化套餐完成", "完整优化");
        return true;
    }

    public bool SetDhcpIp()
    {
        try
        {
            _log.Info("正在设置 DHCP 自动获取 IP...", "网络设置");
            
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var adapter in adapters)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "netsh.exe",
                        Arguments = $"interface ip set address \"{adapter.Name}\" dhcp",
                        Verb = "runas",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(psi)?.WaitForExit(10000);
                }
                catch { }
            }
            
            _log.Success("已设置所有网卡为 DHCP 自动获取 IP", "网络设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"设置 DHCP IP 失败: {ex.Message}", "网络设置");
            return false;
        }
    }

    public bool SetDhcpDns()
    {
        try
        {
            _log.Info("正在设置 DNS 为自动获取...", "网络设置");
            
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var adapter in adapters)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "netsh.exe",
                        Arguments = $"interface ip set dns \"{adapter.Name}\" dhcp",
                        Verb = "runas",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(psi)?.WaitForExit(10000);
                }
                catch { }
            }
            
            _log.Success("已设置所有网卡 DNS 为自动获取", "网络设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"设置 DHCP DNS 失败: {ex.Message}", "网络设置");
            return false;
        }
    }

    public bool ApplyNetworkPreset()
    {
        try
        {
            var preset = AppSettings.NetworkPreset;
            if (preset == null || string.IsNullOrEmpty(preset.IpAddress))
            {
                _log.Warning("未配置网络预设，跳过", "网络设置");
                return false;
            }

            _log.Info($"正在应用网络预设: {preset.Name}...", "网络设置");
            
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var adapter in adapters)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "netsh.exe",
                        Arguments = $"interface ip set address \"{adapter.Name}\" static {preset.IpAddress} {preset.SubnetMask} {preset.Gateway}",
                        Verb = "runas",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(psi)?.WaitForExit(10000);

                    if (!string.IsNullOrEmpty(preset.PrimaryDns))
                    {
                        var dnsArgs = string.IsNullOrEmpty(preset.SecondaryDns)
                            ? $"interface ip set dns \"{adapter.Name}\" static {preset.PrimaryDns}"
                            : $"interface ip set dns \"{adapter.Name}\" static {preset.PrimaryDns} primary && netsh interface ip add dns \"{adapter.Name}\" {preset.SecondaryDns} index=2";
                        
                        var dnsPsi = new ProcessStartInfo
                        {
                            FileName = "netsh.exe",
                            Arguments = dnsArgs,
                            Verb = "runas",
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        Process.Start(dnsPsi)?.WaitForExit(10000);
                    }
                }
                catch { }
            }
            
            _log.Success($"网络预设 {preset.Name} 已应用", "网络设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"应用网络预设失败: {ex.Message}", "网络设置");
            return false;
        }
    }
}
