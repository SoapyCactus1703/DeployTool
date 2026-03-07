namespace DeployTool.Models;

public class ExecutionState
{
    public Dictionary<int, TaskResult> CompletedTasks { get; set; } = new();
    public DateTime StartTime { get; set; }
}

public class TaskResult
{
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Error { get; set; }
}
