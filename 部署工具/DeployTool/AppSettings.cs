using System.IO;
using System.Reflection;
using DeployTool.Services;

namespace DeployTool;

public static class AppSettings
{
    public static int CpuTempRefreshInterval { get; set; } = 1;
    public static bool EnableCpuTempMonitor { get; set; } = true;
    public static string NtpServer { get; set; } = "210.72.145.44";
    public static bool AutoSyncTime { get; set; } = true;
    public static double CpuTempWarningThreshold { get; set; } = 80.0;
    public static NetworkPreset? NetworkPreset { get; set; }
    
    public static string GiteeRepo { get; set; } = "soapycactus1703SoapyCactus1703/DeployTool";
    
    public static Dictionary<string, bool> TaskFlags { get; set; } = new();
    
    private static readonly string SettingsFilePath;
    
    static AppSettings()
    {
        var baseDir = GetApplicationDirectory();
        SettingsFilePath = Path.Combine(baseDir, "Data", "settings.json");
    }
    
    private static string GetApplicationDirectory()
    {
        var exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        var dir = Path.GetDirectoryName(exePath);
        return dir ?? AppDomain.CurrentDomain.BaseDirectory;
    }
    
    public static readonly Dictionary<string, string> FlagDescriptions = new()
    {
        { "DEPLOY_SUCCESS", "部署完成" },
        { "USER_RENAMED_REBOOT", "用户重命名重启" },
        { "USER_FOLDERS_MOVED", "用户文件夹迁移" },
        { "DOTNET_INSTALLED", ".NET SDK 安装" },
        { "DRIVER_INSTALLED", "显卡驱动安装" },
        { "SEEWO_INSTALLED", "seewo应用安装" },
        { "OFFICE_INSTALLED", "办公应用安装" },
        { "SYSTEM_OPTIMIZED", "系统优化" },
        { "WINDOWS_ACTIVATED", "系统激活" },
        { "OFFICE_ACTIVATED", "Office激活" },
        { "NTP_CONFIGURED", "NTP服务器配置" }
    };
    
    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettingsData>(json);
                if (settings != null)
                {
                    CpuTempRefreshInterval = settings.CpuTempRefreshInterval > 0 ? settings.CpuTempRefreshInterval : 5;
                    EnableCpuTempMonitor = settings.EnableCpuTempMonitor;
                    NtpServer = settings.NtpServer ?? "210.72.145.44";
                    AutoSyncTime = settings.AutoSyncTime;
                    CpuTempWarningThreshold = settings.CpuTempWarningThreshold > 0 ? settings.CpuTempWarningThreshold : 80.0;
                    TaskFlags = settings.TaskFlags ?? new Dictionary<string, bool>();
                    GiteeRepo = settings.GiteeRepo ?? "soapycactus1703SoapyCactus1703/DeployTool";
                }
            }
            
            UpdateService.Instance.SetGiteeRepo(GiteeRepo);
        }
        catch { }
    }
    
    public static void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var settings = new AppSettingsData
            {
                CpuTempRefreshInterval = CpuTempRefreshInterval,
                EnableCpuTempMonitor = EnableCpuTempMonitor,
                NtpServer = NtpServer,
                AutoSyncTime = AutoSyncTime,
                CpuTempWarningThreshold = CpuTempWarningThreshold,
                TaskFlags = TaskFlags,
                GiteeRepo = GiteeRepo
            };
            var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch { }
    }
    
    public static void SetFlag(string flagName, bool value = true)
    {
        TaskFlags[flagName] = value;
        Save();
    }
    
    public static bool GetFlag(string flagName)
    {
        return TaskFlags.TryGetValue(flagName, out var value) && value;
    }
    
    public static void ClearFlag(string flagName)
    {
        TaskFlags.Remove(flagName);
        Save();
    }
    
    public static void ClearAllFlags()
    {
        TaskFlags.Clear();
        Save();
    }
    
    public static string GetFlagDescription(string flagName)
    {
        return FlagDescriptions.TryGetValue(flagName, out var desc) ? desc : flagName;
    }
}

public class AppSettingsData
{
    public int CpuTempRefreshInterval { get; set; } = 5;
    public bool EnableCpuTempMonitor { get; set; } = true;
    public string NtpServer { get; set; } = "210.72.145.44";
    public bool AutoSyncTime { get; set; } = true;
    public double CpuTempWarningThreshold { get; set; } = 80.0;
    public Dictionary<string, bool> TaskFlags { get; set; } = new();
    public string GiteeRepo { get; set; } = "soapycactus1703SoapyCactus1703/DeployTool";
}

public class NetworkPreset
{
    public string Name { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string SubnetMask { get; set; } = "255.255.255.0";
    public string Gateway { get; set; } = "";
    public string PrimaryDns { get; set; } = "";
    public string SecondaryDns { get; set; } = "";
}
