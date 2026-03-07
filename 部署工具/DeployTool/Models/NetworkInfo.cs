namespace DeployTool.Models;

public class NetworkInfo
{
    public string AdapterName { get; set; } = "未知";
    public string Description { get; set; } = "未知";
    public string IpAddress { get; set; } = "未连接";
    public string MacAddress { get; set; } = "未知";
    public bool IsConnected { get; set; }
}
