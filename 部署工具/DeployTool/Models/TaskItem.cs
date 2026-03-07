namespace DeployTool.Models;

public class TaskItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresRestart { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsExecuting { get; set; }
}
