namespace DeployTool.Models;

public class DiskInfo
{
    public string DriveLetter { get; set; } = "未知";
    public string VolumeLabel { get; set; } = "本地磁盘";
    public double TotalSizeGB { get; set; }
    public double FreeSizeGB { get; set; }
    public double UsedSizeGB { get; set; }
    public double UsedPercentage { get; set; }
    public string DriveType { get; set; } = "HDD";
    public bool IsSystemDrive { get; set; }
    public bool IsHighUsage => UsedPercentage >= 90;
}
