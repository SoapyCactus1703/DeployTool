using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32;

namespace DeployTool.Services;

public class WindowsSettingsService
{
    private readonly LogService _log = LogService.Instance;

    public class SettingStatus
    {
        public string Name { get; set; } = "";
        public bool IsEnabled { get; set; }
        public string StatusText { get; set; } = "";
        public string Description { get; set; } = "";
        public bool RequiresRestart { get; set; }
        public bool RequiresExplorerRestart { get; set; }
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool QueryServiceStatus(IntPtr hService, ref SERVICE_STATUS lpServiceStatus);

    [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr OpenSCManager(string? lpMachineName, string? lpDatabaseName, uint dwDesiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr hSCObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
    }

    private const uint SC_MANAGER_CONNECT = 0x0001;
    private const uint SERVICE_QUERY_STATUS = 0x0004;

    public SettingStatus GetSystemRestoreStatus()
    {
        var status = new SettingStatus { Name = "系统还原", Description = "管理系统还原功能" };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore");
            if (key == null)
            {
                status.IsEnabled = true;
                status.StatusText = "已启用";
                return status;
            }
            var disableSR = key.GetValue("DisableSR");
            status.IsEnabled = disableSR == null || (int)disableSR == 0;
            status.StatusText = status.IsEnabled ? "已启用" : "已禁用";
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetSystemRestore(bool enable)
    {
        try
        {
            var keyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
            if (key != null)
            {
                key.SetValue("DisableSR", enable ? 0 : 1, RegistryValueKind.DWord);
            }
            _log.Info($"系统还原已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置系统还原失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetSearchHighlightsStatus()
    {
        var status = new SettingStatus { Name = "显示搜索热点", Description = "控制搜索框中的热点资讯显示" };
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SearchSettings");
            var isDynamicSearchBoxEnabled = key?.GetValue("IsDynamicSearchBoxEnabled");
            status.IsEnabled = isDynamicSearchBoxEnabled != null && (int)isDynamicSearchBoxEnabled == 1;
            status.StatusText = status.IsEnabled ? "已启用" : "已禁用";
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetSearchHighlights(bool enable)
    {
        try
        {
            var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\SearchSettings";
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true) ?? 
                           Registry.CurrentUser.CreateSubKey(keyPath);
            key?.SetValue("IsDynamicSearchBoxEnabled", enable ? 1 : 0, RegistryValueKind.DWord);
            _log.Info($"搜索热点已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置搜索热点失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetWidgetsStatus()
    {
        var status = new SettingStatus { Name = "小组件", Description = "控制任务栏小组件功能" };
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            var taskbarDa = key?.GetValue("TaskbarDa");
            status.IsEnabled = taskbarDa == null || (int)taskbarDa == 1;
            status.StatusText = status.IsEnabled ? "已启用" : "已禁用";
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetWidgets(bool enable)
    {
        try
        {
            var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
            key?.SetValue("TaskbarDa", enable ? 1 : 0, RegistryValueKind.DWord);
            _log.Info($"小组件已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置小组件失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetAutoUpdateDriverStatus()
    {
        var status = new SettingStatus { Name = "自动更新硬件驱动", Description = "控制Windows自动更新硬件驱动程序" };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching");
            var searchOrderConfig = key?.GetValue("SearchOrderConfig");
            status.IsEnabled = searchOrderConfig != null && (int)searchOrderConfig == 1;
            status.StatusText = status.IsEnabled ? "已启用" : "已禁用";
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetAutoUpdateDriver(bool enable)
    {
        try
        {
            var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
            key?.SetValue("SearchOrderConfig", enable ? 1 : 0, RegistryValueKind.DWord);
            _log.Info($"自动更新硬件驱动已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置自动更新硬件驱动失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetCeipStatus()
    {
        var status = new SettingStatus { Name = "微软客户体验改善计划", Description = "控制是否参与微软客户体验改善计划" };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\SQMClient\Windows");
            var ceipEnable = key?.GetValue("CEIPEnable");
            status.IsEnabled = ceipEnable != null && (int)ceipEnable == 1;
            status.StatusText = status.IsEnabled ? "已启用" : "已禁用";
        }
        catch
        {
            status.IsEnabled = false;
            status.StatusText = "已禁用";
        }
        return status;
    }

    public bool SetCeip(bool enable)
    {
        try
        {
            var keyPath = @"SOFTWARE\Microsoft\SQMClient\Windows";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
            key?.SetValue("CEIPEnable", enable ? 1 : 0, RegistryValueKind.DWord);
            _log.Info($"微软客户体验改善计划已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置微软客户体验改善计划失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetWindowsUpdateStatus()
    {
        var status = new SettingStatus { Name = "Windows更新", Description = "控制Windows自动更新服务" };
        try
        {
            var scManager = OpenSCManager(null, null, SC_MANAGER_CONNECT);
            if (scManager == IntPtr.Zero)
            {
                status.IsEnabled = true;
                status.StatusText = "已启用";
                return status;
            }

            var serviceHandle = OpenService(scManager, "wuauserv", SERVICE_QUERY_STATUS);
            if (serviceHandle == IntPtr.Zero)
            {
                CloseServiceHandle(scManager);
                status.IsEnabled = true;
                status.StatusText = "已启用";
                return status;
            }

            var serviceStatus = new SERVICE_STATUS();
            if (QueryServiceStatus(serviceHandle, ref serviceStatus))
            {
                var isRunning = serviceStatus.dwCurrentState == 0x04;
                status.IsEnabled = isRunning;
                status.StatusText = isRunning ? "正在运行" : "已停止";
            }

            CloseServiceHandle(serviceHandle);
            CloseServiceHandle(scManager);
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetWindowsUpdate(bool enable)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = enable 
                    ? "config wuauserv start= auto && net start wuauserv" 
                    : "config wuauserv start= disabled && net stop wuauserv",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(10000);
            _log.Info($"Windows更新已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置Windows更新失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetPcaStatus()
    {
        var status = new SettingStatus { Name = "程序兼容性助手", Description = "控制程序兼容性助手服务" };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
            var enablePca = key?.GetValue("EnablePca");
            status.IsEnabled = enablePca == null || (int)enablePca == 1;
            status.StatusText = status.IsEnabled ? "已启用" : "已禁用";
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetPca(bool enable)
    {
        try
        {
            var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, true) ?? 
                           Registry.LocalMachine.CreateSubKey(keyPath);
            key?.SetValue("EnablePca", enable ? 1 : 0, RegistryValueKind.DWord);
            _log.Info($"程序兼容性助手已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置程序兼容性助手失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetDpsStatus()
    {
        var status = new SettingStatus { Name = "诊断策略服务", Description = "控制诊断策略服务" };
        try
        {
            var scManager = OpenSCManager(null, null, SC_MANAGER_CONNECT);
            if (scManager == IntPtr.Zero)
            {
                status.IsEnabled = true;
                status.StatusText = "已启用";
                return status;
            }

            var serviceHandle = OpenService(scManager, "DPS", SERVICE_QUERY_STATUS);
            if (serviceHandle == IntPtr.Zero)
            {
                CloseServiceHandle(scManager);
                status.IsEnabled = true;
                status.StatusText = "已启用";
                return status;
            }

            var serviceStatus = new SERVICE_STATUS();
            if (QueryServiceStatus(serviceHandle, ref serviceStatus))
            {
                var isRunning = serviceStatus.dwCurrentState == 0x04;
                status.IsEnabled = isRunning;
                status.StatusText = isRunning ? "正在运行" : "已停止";
            }

            CloseServiceHandle(serviceHandle);
            CloseServiceHandle(scManager);
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetDps(bool enable)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = enable 
                    ? "config DPS start= auto && net start DPS" 
                    : "config DPS start= disabled && net stop DPS",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(10000);
            _log.Info($"诊断策略服务已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置诊断策略服务失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetMeltdownSpectreStatus()
    {
        var status = new SettingStatus { Name = "Meltdown与Spectre保护", Description = "控制CPU漏洞缓解措施" };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management");
            var featureSettings = key?.GetValue("FeatureSettings");
            var featureSettingsOverride = key?.GetValue("FeatureSettingsOverride");
            var featureSettingsOverrideMask = key?.GetValue("FeatureSettingsOverrideMask");
            
            if (featureSettings != null && (int)featureSettings == 1 &&
                featureSettingsOverride != null && (int)featureSettingsOverride == 3 &&
                featureSettingsOverrideMask != null && (int)featureSettingsOverrideMask == 3)
            {
                status.IsEnabled = false;
                status.StatusText = "已禁用";
            }
            else
            {
                status.IsEnabled = true;
                status.StatusText = "已启用";
            }
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetMeltdownSpectre(bool enable)
    {
        try
        {
            var keyPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
            if (enable)
            {
                key?.SetValue("FeatureSettings", 0, RegistryValueKind.DWord);
                key?.SetValue("FeatureSettingsOverride", 0, RegistryValueKind.DWord);
                key?.SetValue("FeatureSettingsOverrideMask", 0, RegistryValueKind.DWord);
            }
            else
            {
                key?.SetValue("FeatureSettings", 1, RegistryValueKind.DWord);
                key?.SetValue("FeatureSettingsOverride", 3, RegistryValueKind.DWord);
                key?.SetValue("FeatureSettingsOverrideMask", 3, RegistryValueKind.DWord);
            }
            _log.Info($"Meltdown与Spectre保护已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置Meltdown与Spectre保护失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetMemoryIntegrityStatus()
    {
        var status = new SettingStatus { Name = "内存完整性", Description = "核心隔离内存完整性保护" };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity");
            var enabled = key?.GetValue("Enabled");
            status.IsEnabled = enabled != null && (int)enabled == 1;
            status.StatusText = status.IsEnabled ? "已启用" : "已禁用";
            status.RequiresRestart = true;
        }
        catch
        {
            status.IsEnabled = false;
            status.StatusText = "已禁用";
        }
        return status;
    }

    public bool SetMemoryIntegrity(bool enable)
    {
        try
        {
            var keyPath = @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, true) ?? 
                           Registry.LocalMachine.CreateSubKey(keyPath);
            key?.SetValue("Enabled", enable ? 1 : 0, RegistryValueKind.DWord);
            _log.Info($"内存完整性已{(enable ? "启用" : "禁用")}，需重启生效", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置内存完整性失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetWdacStatus()
    {
        var status = new SettingStatus { Name = "WDAC", Description = "Windows Defender应用程序控制" };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\CI\Config");
            var vulnerableDriverBlocklistEnable = key?.GetValue("VulnerableDriverBlocklistEnable");
            status.IsEnabled = vulnerableDriverBlocklistEnable != null && (int)vulnerableDriverBlocklistEnable == 1;
            status.StatusText = status.IsEnabled ? "已启用" : "已禁用";
        }
        catch
        {
            status.IsEnabled = false;
            status.StatusText = "已禁用";
        }
        return status;
    }

    public bool SetWdac(bool enable)
    {
        try
        {
            var keyPath = @"SYSTEM\CurrentControlSet\Control\CI\Config";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, true) ?? 
                           Registry.LocalMachine.CreateSubKey(keyPath);
            key?.SetValue("VulnerableDriverBlocklistEnable", enable ? 1 : 0, RegistryValueKind.DWord);
            _log.Info($"WDAC已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置WDAC失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetVbsStatus()
    {
        var status = new SettingStatus { Name = "应用基于虚拟化的安全", Description = "启用虚拟化安全功能" };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard");
            var enableVirtualizationBasedSecurity = key?.GetValue("EnableVirtualizationBasedSecurity");
            status.IsEnabled = enableVirtualizationBasedSecurity != null && (int)enableVirtualizationBasedSecurity == 1;
            status.StatusText = status.IsEnabled ? "已启用" : "已禁用";
            status.RequiresRestart = true;
        }
        catch
        {
            status.IsEnabled = false;
            status.StatusText = "已禁用";
        }
        return status;
    }

    public bool SetVbs(bool enable)
    {
        try
        {
            var keyPath = @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, true) ?? 
                           Registry.LocalMachine.CreateSubKey(keyPath);
            key?.SetValue("EnableVirtualizationBasedSecurity", enable ? 1 : 0, RegistryValueKind.DWord);
            key?.SetValue("RequirePlatformSecurityFeatures", 1, RegistryValueKind.DWord);
            _log.Info($"应用基于虚拟化的安全已{(enable ? "启用" : "禁用")}，需重启生效", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置应用基于虚拟化的安全失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetTcpBbr2Status()
    {
        var status = new SettingStatus { Name = "TCP BBR2", Description = "TCP拥塞控制算法优化" };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters");
            var tcpCongestionProvider = key?.GetValue("TcpCongestionProvider")?.ToString();
            status.IsEnabled = tcpCongestionProvider != null && tcpCongestionProvider.Equals("bbr2", StringComparison.OrdinalIgnoreCase);
            status.StatusText = status.IsEnabled ? "已启用 (BBR2)" : "系统默认";
        }
        catch
        {
            status.IsEnabled = false;
            status.StatusText = "系统默认";
        }
        return status;
    }

    public bool SetTcpBbr2(bool enable)
    {
        try
        {
            var keyPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
            if (enable)
            {
                key?.SetValue("TcpCongestionProvider", "bbr2", RegistryValueKind.String);
            }
            else
            {
                key?.DeleteValue("TcpCongestionProvider", false);
            }
            _log.Info($"TCP BBR2已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置TCP BBR2失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetDepStatus()
    {
        var status = new SettingStatus { Name = "数据执行保护(DEP)", Description = "数据执行保护策略" };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows");
            var noExecute = key?.GetValue("NoExecute")?.ToString();
            status.IsEnabled = noExecute != null && !noExecute.Equals("AlwaysOff", StringComparison.OrdinalIgnoreCase);
            status.StatusText = noExecute switch
            {
                "OptIn" => "已启用 (基本程序)",
                "OptOut" => "已启用 (所有程序)",
                "AlwaysOn" => "已启用 (强制)",
                "AlwaysOff" => "已禁用",
                _ => "已启用"
            };
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetDep(bool enable)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bcdedit.exe",
                Arguments = enable ? "/set nx OptIn" : "/set nx AlwaysOff",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(10000);
            _log.Info($"数据执行保护(DEP)已{(enable ? "启用" : "禁用")}，需重启生效", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置数据执行保护失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetUacStatus()
    {
        var status = new SettingStatus { Name = "用户账户控制(UAC)", Description = "用户账户控制级别" };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
            var consentPromptBehaviorAdmin = key?.GetValue("ConsentPromptBehaviorAdmin");
            var enableLUA = key?.GetValue("EnableLUA");
            
            if (enableLUA != null && (int)enableLUA == 0)
            {
                status.IsEnabled = false;
                status.StatusText = "已禁用";
            }
            else
            {
                status.IsEnabled = true;
                status.StatusText = consentPromptBehaviorAdmin switch
                {
                    0 => "已启用 (从不通知)",
                    1 => "已启用 (安全桌面通知)",
                    2 => "已启用 (默认级别)",
                    5 => "已启用 (默认级别)",
                    _ => "已启用"
                };
            }
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetUac(bool enable)
    {
        try
        {
            var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
            key?.SetValue("EnableLUA", enable ? 1 : 0, RegistryValueKind.DWord);
            key?.SetValue("ConsentPromptBehaviorAdmin", enable ? 5 : 0, RegistryValueKind.DWord);
            _log.Info($"用户账户控制(UAC)已{(enable ? "启用" : "禁用")}，需重启生效", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置用户账户控制失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetReservedStorageStatus()
    {
        var status = new SettingStatus { Name = "Windows系统保留存储", Description = "系统保留存储空间" };
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-Command \"Get-WindowsReservedStorageState\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            var output = process?.StandardOutput.ReadToEnd();
            process?.WaitForExit(10000);
            status.IsEnabled = output?.Contains("Enabled") == true;
            status.StatusText = status.IsEnabled ? "已启用" : "已禁用";
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetReservedStorage(bool enable)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = enable 
                    ? "-Command \"Set-WindowsReservedStorageState -State Enabled\"" 
                    : "-Command \"Set-WindowsReservedStorageState -State Disabled\"",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(30000);
            _log.Info($"Windows系统保留存储已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置Windows系统保留存储失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetShowFileExtensionsStatus()
    {
        var status = new SettingStatus { Name = "显示已知文件扩展名", Description = "在资源管理器中显示文件扩展名" };
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            var hideFileExt = key?.GetValue("HideFileExt");
            status.IsEnabled = hideFileExt == null || (int)hideFileExt == 0;
            status.StatusText = status.IsEnabled ? "已显示" : "已隐藏";
            status.RequiresExplorerRestart = true;
        }
        catch
        {
            status.IsEnabled = false;
            status.StatusText = "已隐藏";
        }
        return status;
    }

    public bool SetShowFileExtensions(bool show)
    {
        try
        {
            var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
            key?.SetValue("HideFileExt", show ? 0 : 1, RegistryValueKind.DWord);
            _log.Info($"显示已知文件扩展名已{(show ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置显示文件扩展名失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetShowFullPathInTitleBarStatus()
    {
        var status = new SettingStatus { Name = "在标题栏显示完整路径", Description = "在资源管理器标题栏显示完整路径" };
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CabinetState");
            var fullPath = key?.GetValue("FullPath");
            status.IsEnabled = fullPath != null && (int)fullPath == 1;
            status.StatusText = status.IsEnabled ? "已启用" : "已禁用";
            status.RequiresExplorerRestart = true;
        }
        catch
        {
            status.IsEnabled = false;
            status.StatusText = "已禁用";
        }
        return status;
    }

    public bool SetShowFullPathInTitleBar(bool enable)
    {
        try
        {
            var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CabinetState";
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
            key?.SetValue("FullPath", enable ? 1 : 0, RegistryValueKind.DWord);
            _log.Info($"在标题栏显示完整路径已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置标题栏显示完整路径失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetShowHiddenFilesStatus()
    {
        var status = new SettingStatus { Name = "显示隐藏文件", Description = "在资源管理器中显示隐藏文件" };
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            var hidden = key?.GetValue("Hidden");
            status.IsEnabled = hidden != null && (int)hidden == 1;
            status.StatusText = status.IsEnabled ? "已显示" : "已隐藏";
            status.RequiresExplorerRestart = true;
        }
        catch
        {
            status.IsEnabled = false;
            status.StatusText = "已隐藏";
        }
        return status;
    }

    public bool SetShowHiddenFiles(bool show)
    {
        try
        {
            var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
            key?.SetValue("Hidden", show ? 1 : 2, RegistryValueKind.DWord);
            _log.Info($"显示隐藏文件已{(show ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置显示隐藏文件失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetOneDriveStatus()
    {
        var status = new SettingStatus { Name = "OneDrive", Description = "控制OneDrive文件同步功能" };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\OneDrive");
            var disableFileSyncNGSC = key?.GetValue("DisableFileSyncNGSC");
            var isDisabled = disableFileSyncNGSC != null && (int)disableFileSyncNGSC == 1;
            status.IsEnabled = !isDisabled;
            status.StatusText = isDisabled ? "已禁用" : "已启用";
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetOneDrive(bool enable)
    {
        try
        {
            var keyPath = @"SOFTWARE\Policies\Microsoft\Windows\OneDrive";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, true) ?? 
                           Registry.LocalMachine.CreateSubKey(keyPath);
            key?.SetValue("DisableFileSyncNGSC", enable ? 0 : 1, RegistryValueKind.DWord);
            _log.Info($"OneDrive已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置OneDrive失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetContextMenuOptimizationStatus()
    {
        var status = new SettingStatus { Name = "右键菜单优化", Description = "使用旧版右键菜单" };
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\CLASSES\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32");
            status.IsEnabled = key != null;
            status.StatusText = status.IsEnabled ? "已优化 (旧版菜单)" : "系统默认";
            status.RequiresExplorerRestart = true;
        }
        catch
        {
            status.IsEnabled = false;
            status.StatusText = "系统默认";
        }
        return status;
    }

    public bool SetContextMenuOptimization(bool enable)
    {
        try
        {
            if (enable)
            {
                var keyPath = @"SOFTWARE\CLASSES\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";
                using var key = Registry.CurrentUser.CreateSubKey(keyPath);
                key?.SetValue("", "", RegistryValueKind.String);
            }
            else
            {
                var keyPath = @"SOFTWARE\CLASSES\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
                Registry.CurrentUser.DeleteSubKeyTree(keyPath, false);
            }
            _log.Info($"右键菜单优化已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置右键菜单优化失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetTaskbarCombineStatus()
    {
        var status = new SettingStatus { Name = "任务栏合并模式", Description = "控制任务栏按钮合并方式" };
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            var taskbarGlomLevel = key?.GetValue("TaskbarGlomLevel");
            var value = taskbarGlomLevel != null ? (int)taskbarGlomLevel : 1;
            status.StatusText = value switch
            {
                0 => "从不合并",
                1 => "任务栏已满时合并",
                2 => "始终合并",
                _ => "默认"
            };
            status.IsEnabled = value != 2;
            status.RequiresExplorerRestart = true;
        }
        catch
        {
            status.IsEnabled = false;
            status.StatusText = "始终合并";
        }
        return status;
    }

    public bool SetTaskbarCombine(int mode)
    {
        try
        {
            var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
            key?.SetValue("TaskbarGlomLevel", mode, RegistryValueKind.DWord);
            key?.SetValue("MMTaskbarGlomLevel", mode, RegistryValueKind.DWord);
            _log.Info($"任务栏合并模式已更改", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置任务栏合并模式失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetHibernateStatus()
    {
        var status = new SettingStatus { Name = "系统休眠", Description = "控制系统休眠功能" };
        try
        {
            var hiberfilPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "hiberfil.sys");
            status.IsEnabled = File.Exists(hiberfilPath);
            status.StatusText = status.IsEnabled ? "已启用" : "已禁用";
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetHibernate(bool enable)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = enable ? "/hibernate on" : "/hibernate off",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(10000);
            _log.Info($"系统休眠已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置系统休眠失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetFastStartupStatus()
    {
        var status = new SettingStatus { Name = "快速启动", Description = "控制Windows快速启动功能" };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power");
            var hiberbootEnabled = key?.GetValue("HiberbootEnabled");
            status.IsEnabled = hiberbootEnabled != null && (int)hiberbootEnabled == 1;
            status.StatusText = status.IsEnabled ? "已启用" : "已禁用";
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetFastStartup(bool enable)
    {
        try
        {
            var keyPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Power";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
            key?.SetValue("HiberbootEnabled", enable ? 1 : 0, RegistryValueKind.DWord);
            _log.Info($"快速启动已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置快速启动失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetRemoteDesktopStatus()
    {
        var status = new SettingStatus { Name = "远程桌面", Description = "控制远程桌面服务" };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Terminal Server");
            var fDenyTSConnections = key?.GetValue("fDenyTSConnections");
            status.IsEnabled = fDenyTSConnections != null && (int)fDenyTSConnections == 0;
            status.StatusText = status.IsEnabled ? "已启用" : "已禁用";
        }
        catch
        {
            status.IsEnabled = false;
            status.StatusText = "已禁用";
        }
        return status;
    }

    public bool SetRemoteDesktop(bool enable)
    {
        try
        {
            var keyPath = @"SYSTEM\CurrentControlSet\Control\Terminal Server";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
            key?.SetValue("fDenyTSConnections", enable ? 0 : 1, RegistryValueKind.DWord);
            
            if (enable)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netsh.exe",
                    Arguments = "advfirewall firewall set rule group=\"remote desktop\" new enable=Yes",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi)?.WaitForExit(10000);
            }
            
            _log.Info($"远程桌面已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置远程桌面失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetFirewallStatus()
    {
        var status = new SettingStatus { Name = "防火墙", Description = "控制Windows防火墙" };
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh.exe",
                Arguments = "advfirewall show allprofiles state",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            var output = process?.StandardOutput.ReadToEnd();
            process?.WaitForExit(10000);
            status.IsEnabled = output?.Contains("ON") == true;
            status.StatusText = status.IsEnabled ? "已启用" : "已禁用";
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetFirewall(bool enable)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh.exe",
                Arguments = enable 
                    ? "advfirewall set allprofiles state on" 
                    : "advfirewall set allprofiles state off",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(10000);
            _log.Info($"防火墙已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置防火墙失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetSysMainStatus()
    {
        var status = new SettingStatus { Name = "SysMain服务", Description = "SuperFetch/SysMain服务" };
        try
        {
            var scManager = OpenSCManager(null, null, SC_MANAGER_CONNECT);
            if (scManager == IntPtr.Zero)
            {
                status.IsEnabled = true;
                status.StatusText = "正在运行";
                return status;
            }

            var serviceHandle = OpenService(scManager, "SysMain", SERVICE_QUERY_STATUS);
            if (serviceHandle == IntPtr.Zero)
            {
                CloseServiceHandle(scManager);
                status.IsEnabled = true;
                status.StatusText = "正在运行";
                return status;
            }

            var serviceStatus = new SERVICE_STATUS();
            if (QueryServiceStatus(serviceHandle, ref serviceStatus))
            {
                var isRunning = serviceStatus.dwCurrentState == 0x04;
                status.IsEnabled = isRunning;
                status.StatusText = isRunning ? "正在运行" : "已停止";
            }

            CloseServiceHandle(serviceHandle);
            CloseServiceHandle(scManager);
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "正在运行";
        }
        return status;
    }

    public bool SetSysMain(bool enable)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = enable 
                    ? "config SysMain start= auto && net start SysMain" 
                    : "config SysMain start= disabled && net stop SysMain",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(10000);
            _log.Info($"SysMain服务已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置SysMain服务失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetMemoryCompressionStatus()
    {
        var status = new SettingStatus { Name = "内存压缩", Description = "系统内存压缩功能" };
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-Command \"Get-MMAgent\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            var output = process?.StandardOutput.ReadToEnd();
            process?.WaitForExit(10000);
            status.IsEnabled = output?.Contains("MemoryCompression : True") == true;
            status.StatusText = status.IsEnabled ? "已启用" : "已禁用";
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetMemoryCompression(bool enable)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = enable 
                    ? "-Command \"Enable-MMAgent -MemoryCompression\"" 
                    : "-Command \"Disable-MMAgent -MemoryCompression\"",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(10000);
            _log.Info($"内存压缩已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置内存压缩失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetPrefetchStatus()
    {
        var status = new SettingStatus { Name = "预取", Description = "系统预取功能" };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters");
            var enablePrefetcher = key?.GetValue("EnablePrefetcher");
            var value = enablePrefetcher != null ? (int)enablePrefetcher : 3;
            status.IsEnabled = value > 0;
            status.StatusText = value switch
            {
                0 => "已禁用",
                1 => "已启用 (应用程序)",
                2 => "已启用 (启动)",
                3 => "已启用 (全部)",
                _ => "已启用"
            };
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "已启用";
        }
        return status;
    }

    public bool SetPrefetch(bool enable)
    {
        try
        {
            var keyPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
            key?.SetValue("EnablePrefetcher", enable ? 3 : 0, RegistryValueKind.DWord);
            key?.SetValue("EnableSuperfetch", enable ? 3 : 0, RegistryValueKind.DWord);
            _log.Info($"预取已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置预取失败: {ex.Message}", "Windows设置");
            return false;
        }
    }

    public SettingStatus GetDnsCacheStatus()
    {
        var status = new SettingStatus { Name = "DNS缓存", Description = "DNS客户端缓存服务" };
        try
        {
            var scManager = OpenSCManager(null, null, SC_MANAGER_CONNECT);
            if (scManager == IntPtr.Zero)
            {
                status.IsEnabled = true;
                status.StatusText = "正在运行";
                return status;
            }

            var serviceHandle = OpenService(scManager, "Dnscache", SERVICE_QUERY_STATUS);
            if (serviceHandle == IntPtr.Zero)
            {
                CloseServiceHandle(scManager);
                status.IsEnabled = true;
                status.StatusText = "正在运行";
                return status;
            }

            var serviceStatus = new SERVICE_STATUS();
            if (QueryServiceStatus(serviceHandle, ref serviceStatus))
            {
                var isRunning = serviceStatus.dwCurrentState == 0x04;
                status.IsEnabled = isRunning;
                status.StatusText = isRunning ? "正在运行" : "已停止";
            }

            CloseServiceHandle(serviceHandle);
            CloseServiceHandle(scManager);
        }
        catch
        {
            status.IsEnabled = true;
            status.StatusText = "正在运行";
        }
        return status;
    }

    public bool SetDnsCache(bool enable)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = enable 
                    ? "config Dnscache start= auto && net start Dnscache" 
                    : "config Dnscache start= disabled && net stop Dnscache",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(10000);
            _log.Info($"DNS缓存已{(enable ? "启用" : "禁用")}", "Windows设置");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置DNS缓存失败: {ex.Message}", "Windows设置");
            return false;
        }
    }
}
