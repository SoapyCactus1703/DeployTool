namespace DeployTool.Models;

public class CpuInfo
{
    public string Name { get; set; } = "未知";
    public int Cores { get; set; }
    public int LogicalProcessors { get; set; }
    public int Generation { get; set; }
    public string GenerationName { get; set; } = "未知";
}
