namespace DeployTool.Models;

public class CheckpointState
{
    public string ProcessName { get; set; } = "";
    public List<int> TaskIds { get; set; } = new();
    public int CurrentTaskIndex { get; set; }
    public int CurrentTaskId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? PauseTime { get; set; }
    public bool RequiresReboot { get; set; }
    public int RebootTaskIndex { get; set; } = -1;
    public bool IsCompleted { get; set; }
    public string LogSessionId { get; set; } = "";
    
    public bool HasMoreTasks => TaskIds.Count > 0 && CurrentTaskIndex < TaskIds.Count - 1;
    public int NextTaskIndex => CurrentTaskIndex + 1;
    public int NextTaskId => TaskIds.Count > NextTaskIndex ? TaskIds[NextTaskIndex] : 0;
    public int Progress => TaskIds.Count > 0 ? (CurrentTaskIndex + 1) * 100 / TaskIds.Count : 0;
}
