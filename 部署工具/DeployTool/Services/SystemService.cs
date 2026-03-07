using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Reflection;
using Microsoft.Win32;
using DeployTool.Models;
using LibreHardwareMonitor.Hardware;

namespace DeployTool.Services;

public class SystemService : IDisposable
{
    private static SystemService? _instance;
    private static readonly object _instanceLock = new();
    
    public static SystemService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= new SystemService();
                }
            }
            return _instance;
        }
    }
    
    private static Computer? _sharedComputer;
    private static readonly object _lock = new();
    private static bool _isComputerOpen = false;
    
    public static Computer SharedComputer
    {
        get
        {
            if (_sharedComputer == null || !_isComputerOpen)
            {
                lock (_lock)
                {
                    if (_sharedComputer == null)
                    {
                        _sharedComputer = new Computer
                        {
                            IsCpuEnabled = true,
                            IsGpuEnabled = true,
                            IsMemoryEnabled = true,
                            IsMotherboardEnabled = true,
                            IsControllerEnabled = true,
                            IsNetworkEnabled = true,
                            IsStorageEnabled = true,
                            IsBatteryEnabled = true
                        };
                    }
                    if (!_isComputerOpen)
                    {
                        _sharedComputer.Open();
                        _isComputerOpen = true;
                    }
                }
            }
            return _sharedComputer;
        }
    }
    
    public static void CloseHardwareMonitor()
    {
        lock (_lock)
        {
            if (_sharedComputer != null && _isComputerOpen)
            {
                try
                {
                    _sharedComputer.Close();
                }
                catch { }
                _isComputerOpen = false;
            }
        }
    }

    private readonly LogService _log = LogService.Instance;
    private readonly string _baseDir;
    private readonly string _labelDir;
    private bool _disposed;

    public SystemService()
    {
        _baseDir = GetApplicationDirectory();
        _labelDir = Path.Combine(_baseDir, "Data", "Label");
        Directory.CreateDirectory(_labelDir);
        InitializeHardwareMonitor();
    }

    public string GetBaseDirectory() => _baseDir;
    public string GetLabelDirectory() => _labelDir;

    private static string GetApplicationDirectory()
    {
        var exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        var dir = Path.GetDirectoryName(exePath);
        return dir ?? AppDomain.CurrentDomain.BaseDirectory;
    }

    private void InitializeHardwareMonitor()
    {
        try
        {
            _log.Info("正在初始化硬件监控...", "系统信息");
            _log.Success("硬件监控初始化成功 (LibreHardwareMonitor)", "系统信息");
        }
        catch (Exception ex)
        {
            _log.Warning($"初始化硬件监控失败: {ex.GetType().Name} - {ex.Message}", "系统信息");
        }
    }

    public SystemInfo GetSystemInfo()
    {
        var info = new SystemInfo
        {
            Cpu = GetCpuInfo(),
            Memory = GetMemoryInfo(),
            Disks = GetDiskInfo(),
            Networks = GetNetworkInfo(),
            Gpu = GetGpuInfo(),
            Motherboard = GetMotherboardInfo(),
            WindowsVersion = GetWindowsVersion(),
            WindowsBuild = GetWindowsBuild(),
            SystemUptime = GetSystemUptime(),
            ComputerName = Environment.MachineName,
            UserName = Environment.UserName
        };

        var (winActivated, winStatus) = CheckWindowsActivationStatus();
        info.IsWindowsActivated = winActivated;
        info.WindowsActivationStatus = winStatus;

        var (officeActivated, officeStatus) = CheckOfficeActivationStatus();
        info.IsOfficeActivated = officeActivated;
        info.OfficeActivationStatus = officeStatus;

        info.CpuTemperature = GetCpuTemperature();

        return info;
    }

    public GpuInfo GetGpuInfo()
    {
        var info = new GpuInfo();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM, DriverVersion, VideoProcessor, VideoArchitecture FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                info.Name = obj["Name"]?.ToString()?.Trim() ?? "未知";
                info.MemoryMB = obj["AdapterRAM"] != null ? Convert.ToInt64(obj["AdapterRAM"]) / 1024 / 1024 : 0;
                info.DriverVersion = obj["DriverVersion"]?.ToString() ?? "";
                info.VideoProcessor = obj["VideoProcessor"]?.ToString() ?? "";
                if (obj["VideoArchitecture"] != null)
                {
                    var arch = Convert.ToUInt16(obj["VideoArchitecture"]);
                    info.Architecture = arch switch
                    {
                        1 => "Other",
                        2 => "Unknown",
                        3 => "CGA",
                        4 => "EGA",
                        5 => "VGA",
                        6 => "SVGA",
                        7 => "MDA",
                        8 => "HGC",
                        9 => "MCGA",
                        10 => "8514A",
                        11 => "XGA",
                        12 => "Linear Frame Buffer",
                        160 => "AGP",
                        _ => ""
                    };
                }
                break;
            }

            using var manufacturerSearcher = new ManagementObjectSearcher("SELECT Name, PNPDeviceID FROM Win32_VideoController");
            foreach (ManagementObject obj in manufacturerSearcher.Get())
            {
                var pnpId = obj["PNPDeviceID"]?.ToString() ?? "";
                if (pnpId.Contains("VEN_"))
                {
                    var vendorId = pnpId.Substring(pnpId.IndexOf("VEN_") + 4, 4);
                    info.Manufacturer = vendorId.ToUpper() switch
                    {
                        "10DE" => "NVIDIA",
                        "1002" => "AMD",
                        "8086" => "Intel",
                        "15AD" => "VMware",
                        "1414" => "Microsoft",
                        _ => vendorId
                    };
                }
                break;
            }

            info.Resolution = GetScreenResolution();
        }
        catch (Exception ex)
        {
            _log.Error($"获取显卡信息失败: {ex.Message}", "系统信息");
        }

        return info;
    }

    public MotherboardInfo GetMotherboardInfo()
    {
        var info = new MotherboardInfo();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
            foreach (ManagementObject obj in searcher.Get())
            {
                info.Manufacturer = obj["Manufacturer"]?.ToString() ?? "";
                info.Product = obj["Product"]?.ToString() ?? "";
                break;
            }

            using var biosSearcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion, ReleaseDate, Manufacturer FROM Win32_BIOS");
            foreach (ManagementObject obj in biosSearcher.Get())
            {
                info.BiosVersion = obj["SMBIOSBIOSVersion"]?.ToString() ?? "";
                info.BiosVendor = obj["Manufacturer"]?.ToString() ?? "";
                var releaseDate = obj["ReleaseDate"]?.ToString();
                if (!string.IsNullOrEmpty(releaseDate) && releaseDate.Length >= 8)
                {
                    try
                    {
                        var year = releaseDate.Substring(0, 4);
                        var month = releaseDate.Substring(4, 2);
                        var day = releaseDate.Substring(6, 2);
                        info.BiosDate = $"{year}-{month}-{day}";
                    }
                    catch { }
                }
                break;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"获取主板信息失败: {ex.Message}", "系统信息");
        }

        return info;
    }

    private string GetScreenResolution()
    {
        try
        {
            var width = System.Windows.SystemParameters.PrimaryScreenWidth;
            var height = System.Windows.SystemParameters.PrimaryScreenHeight;
            return $"{(int)width} x {(int)height}";
        }
        catch
        {
            return "未知";
        }
    }

    public string GetWindowsBuild()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT BuildNumber, CurrentBuild FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                var buildNumber = obj["BuildNumber"]?.ToString() ?? "";
                return $"Build {buildNumber}";
            }
        }
        catch { }
        return "未知";
    }

    public string GetSystemUptime()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                var lastBootUpTime = ManagementDateTimeConverter.ToDateTime(obj["LastBootUpTime"].ToString());
                var uptime = DateTime.Now - lastBootUpTime;
                return $"{(int)uptime.TotalDays}天 {uptime.Hours}小时 {uptime.Minutes}分钟";
            }
        }
        catch { }
        return "未知";
    }

    public (bool activated, string status) CheckWindowsActivationStatus()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LicenseStatus, Name FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL");
            foreach (ManagementObject obj in searcher.Get())
            {
                var status = Convert.ToInt32(obj["LicenseStatus"]);
                var name = obj["Name"]?.ToString() ?? "";
                
                if (status == 1)
                {
                    var edition = name.Contains("Professional") ? "专业版" :
                                 name.Contains("Enterprise") ? "企业版" :
                                 name.Contains("Home") ? "家庭版" :
                                 name.Contains("Education") ? "教育版" : "";
                    return (true, $"已激活 ({edition})");
                }
            }
            
            return (false, "未激活");
        }
        catch (Exception ex)
        {
            _log.Error($"检查Windows激活状态失败: {ex.Message}", "系统信息");
            return (false, "检测失败");
        }
    }

    public (bool activated, string status) CheckOfficeActivationStatus()
    {
        try
        {
            var officeAppIds = new[]
            {
                "0ff1ce15-a989-479d-af46-f275c6370663",
                "98a86f2d-9b12-4ea8-b7c8-2a932e683d3d"
            };

            var foundOffice = false;
            
            foreach (var appId in officeAppIds)
            {
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT LicenseStatus, Name FROM SoftwareLicensingProduct " +
                    $"WHERE ApplicationId = '{appId}' AND PartialProductKey IS NOT NULL");
                
                var results = searcher.Get();
                if (results.Count > 0)
                {
                    foundOffice = true;
                }
                
                foreach (ManagementObject obj in results)
                {
                    var status = Convert.ToInt32(obj["LicenseStatus"]);
                    var name = obj["Name"]?.ToString() ?? "";
                    
                    if (status == 1)
                    {
                        var officeName = name.Contains("2016") ? "Office 2016" :
                                        name.Contains("2019") ? "Office 2019" :
                                        name.Contains("2021") ? "Office 2021" :
                                        name.Contains("365") ? "Microsoft 365" : "Office";
                        return (true, $"{officeName} 已激活");
                    }
                }
            }
            
            if (!foundOffice)
            {
                return (false, "未安装 Office");
            }
            
            return (false, "未激活");
        }
        catch (Exception ex)
        {
            _log.Error($"检查Office激活状态失败: {ex.Message}", "系统信息");
            return (false, "检测失败");
        }
    }

    public CpuTemperatureInfo GetCpuTemperature()
    {
        var info = new CpuTemperatureInfo { IsAvailable = false };

        try
        {
            var computer = SharedComputer;
            computer.Accept(new UpdateVisitor());
            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && 
                            sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                        {
                            info.IsAvailable = true;
                            info.Temperature = sensor.Value ?? 0;
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.Warning($"获取CPU温度失败: {ex.Message}", "系统信息");
        }

        return info;
    }

    public CpuInfo GetCpuInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim() ?? "未知";
                var cores = Convert.ToInt32(obj["NumberOfCores"]);
                var logicalProcessors = Convert.ToInt32(obj["NumberOfLogicalProcessors"]);

                var generation = DetectCpuGeneration(name);
                var generationName = GetCpuGenerationName(generation);

                return new CpuInfo
                {
                    Name = name,
                    Cores = cores,
                    LogicalProcessors = logicalProcessors,
                    Generation = generation,
                    GenerationName = generationName
                };
            }
        }
        catch (Exception ex)
        {
            _log.Error($"获取 CPU 信息失败: {ex.Message}", "系统信息");
        }
        return new CpuInfo();
    }

    public int DetectCpuGeneration(string cpuName)
    {
        if (string.IsNullOrWhiteSpace(cpuName))
            return 0;

        cpuName = cpuName.ToUpperInvariant();

        var intelPattern = @"I[3579]-(\d{4,5})";
        var intelMatch = System.Text.RegularExpressions.Regex.Match(cpuName, intelPattern);
        if (intelMatch.Success)
        {
            var modelNumber = intelMatch.Groups[1].Value;
            if (modelNumber.Length >= 4)
            {
                var generationDigit = modelNumber[0];
                if (int.TryParse(generationDigit.ToString(), out var gen))
                {
                    return gen;
                }
            }
        }

        return 0;
    }

    public string GetCpuGenerationName(int generation)
    {
        return generation switch
        {
            0 => "未知",
            >= 1 and <= 6 => $"{generation}代或更早",
            >= 7 and <= 14 => $"第{generation}代",
            >= 15 => $"第{generation}代或更新",
            _ => "未知"
        };
    }

    public MemoryInfo GetMemoryInfo()
    {
        var info = new MemoryInfo();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                var totalKB = Convert.ToInt64(obj["TotalVisibleMemorySize"]);
                var freeKB = Convert.ToInt64(obj["FreePhysicalMemory"]);

                info.TotalSizeGB = Math.Round(totalKB / 1024.0 / 1024.0, 1);
                info.AvailableGB = Math.Round(freeKB / 1024.0 / 1024.0, 1);
                info.UsedPercentage = Math.Round((1 - (double)freeKB / totalKB) * 100, 1);
                break;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"获取内存信息失败: {ex.Message}", "系统信息");
        }

        return info;
    }

    public List<DiskInfo> GetDiskInfo()
    {
        var disks = new List<DiskInfo>();

        try
        {
            var allDrives = DriveInfo.GetDrives();

            foreach (var drive in allDrives)
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                {
                    var totalGB = Math.Round(drive.TotalSize / 1024.0 / 1024.0 / 1024.0, 1);
                    var freeGB = Math.Round(drive.TotalFreeSpace / 1024.0 / 1024.0 / 1024.0, 1);
                    var usedGB = totalGB - freeGB;
                    var usedPercentage = totalGB > 0 ? Math.Round(usedGB / totalGB * 100, 1) : 0;

                    disks.Add(new DiskInfo
                    {
                        DriveLetter = drive.Name,
                        VolumeLabel = string.IsNullOrEmpty(drive.VolumeLabel) ? "本地磁盘" : drive.VolumeLabel,
                        TotalSizeGB = totalGB,
                        FreeSizeGB = freeGB,
                        UsedSizeGB = usedGB,
                        UsedPercentage = usedPercentage,
                        DriveType = drive.DriveType.ToString()
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error($"获取磁盘信息失败: {ex.Message}", "系统信息");
        }

        return disks;
    }

    public List<NetworkInfo> GetNetworkInfo()
    {
        var networks = new List<NetworkInfo>();

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                {
                    var ipProps = nic.GetIPProperties();
                    var ipAddress = ipProps.UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?
                        .Address.ToString() ?? "无IP";

                    networks.Add(new NetworkInfo
                    {
                        AdapterName = nic.Name,
                        Description = nic.Description,
                        IpAddress = ipAddress,
                        MacAddress = nic.GetPhysicalAddress().ToString()
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error($"获取网络信息失败: {ex.Message}", "系统信息");
        }

        return networks;
    }

    public string GetWindowsVersion()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Caption, Version FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                return obj["Caption"]?.ToString() ?? "Windows";
            }
        }
        catch { }
        return "Windows";
    }

    public bool RenameUserAccount(string newName)
    {
        try
        {
            var currentName = Environment.UserName;
            if (currentName.Equals(newName, StringComparison.OrdinalIgnoreCase))
            {
                _log.Info("用户名已是目标名称，无需修改", "用户管理");
                return true;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "wmic",
                Arguments = $"useraccount where name='{currentName}' call rename name='{newName}'",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi);
            process?.WaitForExit();

            if (process?.ExitCode == 0)
            {
                _log.Success($"用户账户已重命名为: {newName}", "用户管理");
                return true;
            }
            
            _log.Error("重命名用户账户失败", "用户管理");
            return false;
        }
        catch (Exception ex)
        {
            _log.Error($"重命名用户账户失败: {ex.Message}", "用户管理");
            return false;
        }
    }

    public bool CreateUserAccount(string userName, string? password = null)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "net",
                Arguments = string.IsNullOrEmpty(password) 
                    ? $"user {userName} /add" 
                    : $"user {userName} {password} /add",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi);
            process?.WaitForExit();

            if (process?.ExitCode == 0)
            {
                _log.Success($"用户账户已创建: {userName}", "用户管理");
                return true;
            }
            
            _log.Error("创建用户账户失败", "用户管理");
            return false;
        }
        catch (Exception ex)
        {
            _log.Error($"创建用户账户失败: {ex.Message}", "用户管理");
            return false;
        }
    }

    public bool MoveUserFolders(string targetDrive)
    {
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var folders = new[] { "Desktop", "Documents", "Downloads", "Pictures", "Videos", "Music" };
            var targetRoot = Path.Combine(targetDrive, "Users", Environment.UserName);

            if (!Directory.Exists(targetRoot))
            {
                Directory.CreateDirectory(targetRoot);
            }

            foreach (var folder in folders)
            {
                var sourcePath = Path.Combine(userProfile, folder);
                var targetPath = Path.Combine(targetRoot, folder);

                if (Directory.Exists(sourcePath) && !Directory.Exists(targetPath))
                {
                    Directory.Move(sourcePath, targetPath);
                    _log.Info($"已移动 {folder} 到 {targetPath}", "用户管理");
                }
            }

            File.WriteAllText(Path.Combine(_labelDir, "USER_FOLDERS_MOVED.flag"), $"完成于 {DateTime.Now}");
            _log.Success("用户文件夹迁移完成", "用户管理");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"迁移用户文件夹失败: {ex.Message}", "用户管理");
            return false;
        }
    }

    public void RestartExplorer()
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("explorer"))
            {
                process.Kill();
            }
            Process.Start("explorer.exe");
            _log.Info("资源管理器已重启", "系统管理");
        }
        catch (Exception ex)
        {
            _log.Error($"重启资源管理器失败: {ex.Message}", "系统管理");
        }
    }

    public void RestartComputer(int delaySeconds = 0)
    {
        try
        {
            if (delaySeconds > 0)
            {
                Process.Start("shutdown", $"/r /t {delaySeconds}");
            }
            else
            {
                Process.Start("shutdown", "/r /t 0");
            }
        }
        catch (Exception ex)
        {
            _log.Error($"重启计算机失败: {ex.Message}", "系统管理");
        }
    }

    public bool CheckFlagFile(string fileName)
    {
        return File.Exists(Path.Combine(_labelDir, fileName));
    }

    public void CreateFlagFile(string fileName, string content = "")
    {
        try
        {
            var filePath = Path.Combine(_labelDir, fileName);
            File.WriteAllText(filePath, string.IsNullOrEmpty(content) ? $"完成于 {DateTime.Now}" : content);
        }
        catch (Exception ex)
        {
            _log.Error($"创建标记文件失败: {ex.Message}", "系统管理");
        }
    }

    public void DeleteFlagFile(string fileName)
    {
        try
        {
            var filePath = Path.Combine(_labelDir, fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _log.Error($"删除标记文件失败: {ex.Message}", "系统管理");
        }
    }

    private static readonly string CheckpointFileName = "checkpoint.json";
    
    public void SaveCheckpoint(CheckpointState checkpoint)
    {
        try
        {
            var filePath = Path.Combine(_labelDir, CheckpointFileName);
            var json = System.Text.Json.JsonSerializer.Serialize(checkpoint, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var sw = new StreamWriter(fs))
            {
                sw.Write(json);
                sw.Flush();
                fs.Flush(true);
            }
            
            _log.Info($"断点已保存: {checkpoint.ProcessName} - 进度 {checkpoint.CurrentTaskIndex + 1}/{checkpoint.TaskIds.Count}", "断点管理");
        }
        catch (Exception ex)
        {
            _log.Error($"保存断点失败: {ex.Message}", "断点管理");
        }
    }
    
    public CheckpointState? LoadCheckpoint()
    {
        try
        {
            var filePath = Path.Combine(_labelDir, CheckpointFileName);
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var checkpoint = System.Text.Json.JsonSerializer.Deserialize<CheckpointState>(json);
                if (checkpoint != null && !checkpoint.IsCompleted)
                {
                    return checkpoint;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error($"加载断点失败: {ex.Message}", "断点管理");
        }
        return null;
    }
    
    public void ClearCheckpoint()
    {
        try
        {
            var filePath = Path.Combine(_labelDir, CheckpointFileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _log.Info("断点已清除", "断点管理");
            }
        }
        catch (Exception ex)
        {
            _log.Error($"清除断点失败: {ex.Message}", "断点管理");
        }
    }
    
    public void MarkCheckpointCompleted()
    {
        try
        {
            var checkpoint = LoadCheckpoint();
            if (checkpoint != null)
            {
                checkpoint.IsCompleted = true;
                checkpoint.PauseTime = DateTime.Now;
                var filePath = Path.Combine(_labelDir, CheckpointFileName);
                var json = System.Text.Json.JsonSerializer.Serialize(checkpoint, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(filePath, json);
                _log.Success($"流程已完成: {checkpoint.ProcessName}", "断点管理");
            }
        }
        catch (Exception ex)
        {
            _log.Error($"标记断点完成失败: {ex.Message}", "断点管理");
        }
    }

    public static bool IsAdministrator()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    public double GetCpuLoad()
    {
        try
        {
            var computer = SharedComputer;
            computer.Accept(new UpdateVisitor());
            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("CPU Total", StringComparison.OrdinalIgnoreCase))
                        {
                            return sensor.Value ?? 0;
                        }
                    }
                }
            }
        }
        catch { }
        return 0;
    }

    public double GetGpuLoad()
    {
        try
        {
            var computer = SharedComputer;
            computer.Accept(new UpdateVisitor());
            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.GpuNvidia || 
                    hardware.HardwareType == HardwareType.GpuAmd || 
                    hardware.HardwareType == HardwareType.GpuIntel)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase))
                        {
                            return sensor.Value ?? 0;
                        }
                    }
                }
            }
        }
        catch { }
        return 0;
    }

    public (float current, float max) GetCpuFrequencies()
    {
        float currentFreq = 0;
        float maxFreq = 0;

        try
        {
            var computer = SharedComputer;
            computer.Accept(new UpdateVisitor());
            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Clock)
                        {
                            if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                            {
                                var value = sensor.Value ?? 0;
                                if (value > currentFreq) currentFreq = value;
                            }
                        }
                    }
                }
            }

            using var searcher = new ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                maxFreq = Convert.ToSingle(obj["MaxClockSpeed"]);
                break;
            }
        }
        catch { }

        return (currentFreq, maxFreq);
    }

    public List<FanInfo> GetFanSpeeds()
    {
        var fans = new List<FanInfo>();

        try
        {
            var computer = SharedComputer;
            computer.Accept(new UpdateVisitor());
            foreach (var hardware in computer.Hardware)
            {
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue)
                    {
                        fans.Add(new FanInfo
                        {
                            Name = sensor.Name,
                            Speed = (int)sensor.Value.Value
                        });
                    }
                }
                
                foreach (var subHardware in hardware.SubHardware)
                {
                    foreach (var sensor in subHardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue)
                        {
                            fans.Add(new FanInfo
                            {
                                Name = sensor.Name,
                                Speed = (int)sensor.Value.Value
                            });
                        }
                    }
                }
            }
        }
        catch { }

        return fans;
    }

    public double GetGpuTemperature()
    {
        try
        {
            var computer = SharedComputer;
            computer.Accept(new UpdateVisitor());
            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.GpuNvidia || 
                    hardware.HardwareType == HardwareType.GpuAmd || 
                    hardware.HardwareType == HardwareType.GpuIntel)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && 
                            sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                        {
                            return sensor.Value ?? 0;
                        }
                    }
                }
            }
        }
        catch { }
        return 0;
    }

    public double GetMemoryFrequency()
    {
        try
        {
            var computer = SharedComputer;
            computer.Accept(new UpdateVisitor());
            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Memory)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Clock)
                        {
                            var freq = sensor.Value ?? 0;
                            if (freq > 0) return freq;
                        }
                    }
                }
            }
            
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ConfiguredClockSpeed, Speed FROM Win32_PhysicalMemory");
                long totalSpeed = 0;
                int count = 0;
                foreach (ManagementObject obj in searcher.Get())
                {
                    var configuredSpeed = obj["ConfiguredClockSpeed"];
                    var speed = obj["Speed"];
                    
                    if (configuredSpeed != null && Convert.ToUInt32(configuredSpeed) > 0)
                    {
                        totalSpeed += Convert.ToUInt32(configuredSpeed);
                        count++;
                    }
                    else if (speed != null && Convert.ToUInt32(speed) > 0)
                    {
                        totalSpeed += Convert.ToUInt32(speed);
                        count++;
                    }
                }
                if (count > 0 && totalSpeed > 0)
                {
                    return totalSpeed / count;
                }
            }
            catch { }
        }
        catch { }
        return 0;
    }

    public List<StorageHealthInfo> GetStorageHealth()
    {
        var healthList = new List<StorageHealthInfo>();

        try
        {
            var computer = SharedComputer;
            computer.Accept(new UpdateVisitor());
            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Storage)
                {
                    var health = new StorageHealthInfo
                    {
                        Name = hardware.Name
                    };
                    
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.Name.Contains("Health", StringComparison.OrdinalIgnoreCase) ||
                            sensor.Name.Contains("Status", StringComparison.OrdinalIgnoreCase))
                        {
                            health.Status = sensor.Value?.ToString() ?? "Unknown";
                        }
                        else if (sensor.Name.Contains("Temperature", StringComparison.OrdinalIgnoreCase) && sensor.Value.HasValue)
                        {
                            health.Temperature = sensor.Value.Value;
                        }
                        else if (sensor.Name.Contains("Remaining Life", StringComparison.OrdinalIgnoreCase) && sensor.Value.HasValue)
                        {
                            health.RemainingLife = (int)sensor.Value.Value;
                        }
                        else if (sensor.Name.Contains("Life", StringComparison.OrdinalIgnoreCase) && 
                                 !sensor.Name.Contains("Remaining", StringComparison.OrdinalIgnoreCase) && sensor.Value.HasValue)
                        {
                            health.RemainingLife = (int)sensor.Value.Value;
                        }
                        else if (sensor.Name.Contains("Data Read", StringComparison.OrdinalIgnoreCase) && sensor.Value.HasValue)
                        {
                            health.DataRead = sensor.Value.Value;
                        }
                        else if (sensor.Name.Contains("Data Written", StringComparison.OrdinalIgnoreCase) && sensor.Value.HasValue)
                        {
                            health.DataWritten = sensor.Value.Value;
                        }
                        else if (sensor.Name.Contains("Power On Hours", StringComparison.OrdinalIgnoreCase) && sensor.Value.HasValue)
                        {
                            health.PowerOnHours = (int)sensor.Value.Value;
                        }
                        else if (sensor.Name.Contains("Power Cycle", StringComparison.OrdinalIgnoreCase) && sensor.Value.HasValue)
                        {
                            health.PowerCycleCount = (int)sensor.Value.Value;
                        }
                        else if (sensor.Name.Contains("Media Type", StringComparison.OrdinalIgnoreCase))
                        {
                            health.MediaType = sensor.Value?.ToString() ?? "";
                        }
                        else if (sensor.Name.Contains("Firmware", StringComparison.OrdinalIgnoreCase))
                        {
                            health.FirmwareVersion = sensor.Value?.ToString() ?? "";
                        }
                        else if (sensor.Name.Contains("Capacity", StringComparison.OrdinalIgnoreCase) && sensor.Value.HasValue)
                        {
                            health.TotalCapacity = (long)(sensor.Value.Value * 1024 * 1024 * 1024);
                        }
                    }
                    
                    if (string.IsNullOrEmpty(health.Status))
                    {
                        health.Status = "正常";
                    }
                    
                    if (string.IsNullOrEmpty(health.MediaType))
                    {
                        health.MediaType = hardware.Name.Contains("SSD", StringComparison.OrdinalIgnoreCase) ? "SSD" : "HDD";
                    }
                    
                    healthList.Add(health);
                }
            }
        }
        catch { }

        return healthList;
    }

    public List<NetworkSpeedInfo> GetNetworkSpeeds()
    {
        var result = new List<NetworkSpeedInfo>();

        try
        {
            var computer = SharedComputer;
            computer.Accept(new UpdateVisitor());
            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Network)
                {
                    var info = new NetworkSpeedInfo
                    {
                        Name = hardware.Name
                    };
                    
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Throughput)
                        {
                            if (sensor.Name.Contains("Upload", StringComparison.OrdinalIgnoreCase))
                            {
                                info.UploadSpeed = sensor.Value ?? 0;
                            }
                            else if (sensor.Name.Contains("Download", StringComparison.OrdinalIgnoreCase))
                            {
                                info.DownloadSpeed = sensor.Value ?? 0;
                            }
                        }
                        else if (sensor.SensorType == SensorType.Data)
                        {
                            if (sensor.Name.Contains("Upload", StringComparison.OrdinalIgnoreCase))
                            {
                                info.TotalUpload = sensor.Value ?? 0;
                            }
                            else if (sensor.Name.Contains("Download", StringComparison.OrdinalIgnoreCase))
                            {
                                info.TotalDownload = sensor.Value ?? 0;
                            }
                        }
                    }
                    
                    result.Add(info);
                }
            }
        }
        catch { }

        return result;
    }

    public BatteryInfo GetBatteryInfo()
    {
        var info = new BatteryInfo();

        try
        {
            var computer = SharedComputer;
            computer.Accept(new UpdateVisitor());
            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Battery)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.Name.Contains("Charge Level", StringComparison.OrdinalIgnoreCase))
                        {
                            info.ChargeLevel = (int)(sensor.Value ?? 0);
                        }
                        else if (sensor.Name.Contains("Status", StringComparison.OrdinalIgnoreCase))
                        {
                            info.Status = sensor.Value?.ToString() ?? "Unknown";
                        }
                        else if (sensor.Name.Contains("Degradation", StringComparison.OrdinalIgnoreCase))
                        {
                            info.Degradation = sensor.Value ?? 0;
                        }
                        else if (sensor.Name.Contains("Design Capacity", StringComparison.OrdinalIgnoreCase))
                        {
                            info.DesignCapacity = sensor.Value ?? 0;
                        }
                        else if (sensor.Name.Contains("Full Charged Capacity", StringComparison.OrdinalIgnoreCase))
                        {
                            info.FullChargedCapacity = sensor.Value ?? 0;
                        }
                        else if (sensor.Name.Contains("Current Capacity", StringComparison.OrdinalIgnoreCase) || 
                                 sensor.Name.Contains("Remaining Capacity", StringComparison.OrdinalIgnoreCase))
                        {
                            info.CurrentCapacity = sensor.Value ?? 0;
                        }
                        else if (sensor.Name.Contains("Voltage", StringComparison.OrdinalIgnoreCase))
                        {
                            info.Voltage = sensor.Value ?? 0;
                        }
                        else if (sensor.Name.Contains("Charge Rate", StringComparison.OrdinalIgnoreCase) || 
                                 sensor.Name.Contains("Discharge Rate", StringComparison.OrdinalIgnoreCase))
                        {
                            info.ChargeRate = sensor.Value ?? 0;
                        }
                        else if (sensor.Name.Contains("Cycle", StringComparison.OrdinalIgnoreCase) && sensor.Value.HasValue)
                        {
                            info.CycleCount = (int)sensor.Value.Value;
                        }
                    }
                    info.IsAvailable = true;
                    break;
                }
            }
        }
        catch { }

        return info;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
    
    ~SystemService()
    {
        Dispose();
    }
}

public class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer)
    {
        computer.Traverse(this);
    }

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware subHardware in hardware.SubHardware)
        {
            subHardware.Accept(this);
        }
    }

    public void VisitSensor(ISensor sensor) { }

    public void VisitParameter(IParameter parameter) { }
}

public class FanInfo
{
    public string Name { get; set; } = "";
    public int Speed { get; set; }
}

public class StorageHealthInfo
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public double Temperature { get; set; }
    public int RemainingLife { get; set; } = -1;
    public double DataRead { get; set; }
    public double DataWritten { get; set; }
    public int PowerOnHours { get; set; } = -1;
    public int PowerCycleCount { get; set; } = -1;
    public string MediaType { get; set; } = "";
    public string FirmwareVersion { get; set; } = "";
    public long TotalCapacity { get; set; }
    public bool IsWarning => Status.Contains("警告") || Status.Contains("错误") || Status.Contains("Warning") || (RemainingLife >= 0 && RemainingLife < 10);
    public string FormattedDataRead => FormatDataSize(DataRead);
    public string FormattedDataWritten => FormatDataSize(DataWritten);
    public string FormattedPowerOnHours => PowerOnHours >= 0 ? $"{PowerOnHours} 小时 ({PowerOnHours / 24.0:F1} 天)" : "未知";
    
    private static string FormatDataSize(double bytes)
    {
        if (bytes <= 0) return "未知";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int unitIndex = 0;
        double size = bytes;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:F2} {units[unitIndex]}";
    }
}

public class NetworkSpeedInfo
{
    public string Name { get; set; } = "";
    public double UploadSpeed { get; set; }
    public double DownloadSpeed { get; set; }
    public double TotalUpload { get; set; }
    public double TotalDownload { get; set; }
    public string FormattedUploadSpeed => FormatSpeed(UploadSpeed);
    public string FormattedDownloadSpeed => FormatSpeed(DownloadSpeed);
    public string FormattedTotalUpload => FormatDataSize(TotalUpload);
    public string FormattedTotalDownload => FormatDataSize(TotalDownload);
    
    private static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond <= 0) return "0 B/s";
        string[] units = { "B/s", "KB/s", "MB/s", "GB/s" };
        int unitIndex = 0;
        double speed = bytesPerSecond;
        while (speed >= 1024 && unitIndex < units.Length - 1)
        {
            speed /= 1024;
            unitIndex++;
        }
        return $"{speed:F2} {units[unitIndex]}";
    }
    
    private static string FormatDataSize(double bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int unitIndex = 0;
        double size = bytes;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:F2} {units[unitIndex]}";
    }
}

public class BatteryInfo
{
    public bool IsAvailable { get; set; }
    public int ChargeLevel { get; set; }
    public string Status { get; set; } = "";
    public double Degradation { get; set; }
    public double DesignCapacity { get; set; }
    public double FullChargedCapacity { get; set; }
    public double CurrentCapacity { get; set; }
    public double Voltage { get; set; }
    public double ChargeRate { get; set; }
    public int CycleCount { get; set; } = -1;
    public string FormattedStatus => GetFormattedStatus();
    public string FormattedDegradation => Degradation > 0 ? $"{Degradation:F1}%" : "未知";
    public string FormattedDesignCapacity => DesignCapacity > 0 ? $"{DesignCapacity:F0} mWh" : "未知";
    public string FormattedFullChargedCapacity => FullChargedCapacity > 0 ? $"{FullChargedCapacity:F0} mWh" : "未知";
    public string FormattedCurrentCapacity => CurrentCapacity > 0 ? $"{CurrentCapacity:F0} mWh" : "未知";
    public string FormattedVoltage => Voltage > 0 ? $"{Voltage / 1000:F2} V" : "未知";
    public string FormattedChargeRate => ChargeRate > 0 ? $"{ChargeRate:F0} mW" : "未充电";
    public string FormattedCycleCount => CycleCount >= 0 ? $"{CycleCount} 次" : "未知";
    public double HealthPercentage => DesignCapacity > 0 && FullChargedCapacity > 0 ? Math.Round(FullChargedCapacity / DesignCapacity * 100, 1) : 0;
    public string FormattedHealth => HealthPercentage > 0 ? $"{HealthPercentage:F1}%" : "未知";
    public bool IsHealthWarning => HealthPercentage > 0 && HealthPercentage < 80;
    
    private string GetFormattedStatus()
    {
        if (string.IsNullOrEmpty(Status)) return "未知";
        return Status.ToLower() switch
        {
            "charging" or "charge" => "充电中",
            "discharging" or "discharge" => "放电中",
            "full" or "fully charged" => "已充满",
            "idle" => "空闲",
            _ => Status
        };
    }
}
