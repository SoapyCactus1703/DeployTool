namespace DeployTool.Models;

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public string LevelText => Level switch
    {
        LogLevel.Info => "INFO",
        LogLevel.Success => "SUCCESS",
        LogLevel.Warning => "WARNING",
        LogLevel.Error => "ERROR",
        _ => "INFO"
    };
}
