namespace DeployTool.Models;

public class SystemInfo
{
    public CpuInfo Cpu { get; set; } = new();
    public MemoryInfo Memory { get; set; } = new();
    public List<DiskInfo> Disks { get; set; } = new();
    public List<NetworkInfo> Networks { get; set; } = new();
    public GpuInfo Gpu { get; set; } = new();
    public MotherboardInfo Motherboard { get; set; } = new();
    public bool IsWindowsActivated { get; set; }
    public string WindowsActivationStatus { get; set; } = "未激活";
    public bool IsOfficeActivated { get; set; }
    public string OfficeActivationStatus { get; set; } = "未安装/未激活";
    public string WindowsVersion { get; set; } = string.Empty;
    public string WindowsBuild { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string SystemUptime { get; set; } = string.Empty;
    public CpuTemperatureInfo CpuTemperature { get; set; } = new();
}

public class GpuInfo
{
    public string Name { get; set; } = "未知";
    public string DriverVersion { get; set; } = "未知";
    public long MemoryMB { get; set; }
    public string Resolution { get; set; } = "未知";
    public string Architecture { get; set; } = string.Empty;
    public string BiosVersion { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = "未知";
    public string VideoProcessor { get; set; } = "未知";
    public int CoreClock { get; set; }
    public int MemoryClock { get; set; }
    public string FormattedMemory => MemoryMB > 0 ? $"{MemoryMB} MB" : "未知";
    public string FormattedCoreClock => CoreClock > 0 ? $"{CoreClock} MHz" : "未知";
    public string FormattedMemoryClock => MemoryClock > 0 ? $"{MemoryClock} MHz" : "未知";
}

public class MotherboardInfo
{
    public string Manufacturer { get; set; } = "未知";
    public string Product { get; set; } = "未知";
    public string BiosVersion { get; set; } = "未知";
    public string BiosDate { get; set; } = "未知";
    public string BiosVendor { get; set; } = "未知";
    public string Chipset { get; set; } = string.Empty;
}

public class CpuTemperatureInfo
{
    private double _temperature;
    
    public bool IsAvailable { get; set; }
    public double Temperature 
    { 
        get => _temperature;
        set
        {
            _temperature = value;
            IsWarning = IsAvailable && _temperature >= AppSettings.CpuTempWarningThreshold;
        }
    }
    public int FanSpeed { get; set; }
    public List<double> TemperatureHistory { get; set; } = new();
    public bool IsWarning { get; private set; }
}
