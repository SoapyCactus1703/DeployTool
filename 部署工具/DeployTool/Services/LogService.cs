using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using DeployTool.Models;

namespace DeployTool.Services;

public sealed class LogService
{
    private static readonly Lazy<LogService> _instance = new(() => new LogService());
    public static LogService Instance => _instance.Value;

    private readonly string _logDirectory;
    private string _logFilePath;
    private readonly object _lock = new();
    private const long MaxLogSize = 10 * 1024 * 1024;

    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    public event Action<LogEntry>? LogAdded;

    private LogService()
    {
        var baseDir = GetApplicationDirectory();
        _logDirectory = Path.Combine(baseDir, "Data", "logs");
        
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
        
        _logFilePath = Path.Combine(_logDirectory, $"log_{DateTime.Now:yyyyMMdd}.txt");
    }

    private static string GetApplicationDirectory()
    {
        var exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        var dir = Path.GetDirectoryName(exePath);
        return dir ?? AppDomain.CurrentDomain.BaseDirectory;
    }

    public void Log(string message, LogLevel level, string category)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Message = message,
            Level = level,
            Category = category
        };

        Application.Current?.Dispatcher.Invoke(() =>
        {
            LogEntries.Add(entry);
            if (LogEntries.Count > 500)
            {
                LogEntries.RemoveAt(0);
            }
        });

        LogAdded?.Invoke(entry);

        WriteToFile(entry);
    }

    public void Info(string message, string category) => Log(message, LogLevel.Info, category);
    public void Success(string message, string category) => Log(message, LogLevel.Success, category);
    public void Warning(string message, string category) => Log(message, LogLevel.Warning, category);
    public void Error(string message, string category) => Log(message, LogLevel.Error, category);

    private void WriteToFile(LogEntry entry)
    {
        Task.Run(() =>
        {
            lock (_lock)
            {
                try
                {
                    if (!Directory.Exists(_logDirectory))
                    {
                        Directory.CreateDirectory(_logDirectory);
                    }

                    _logFilePath = Path.Combine(_logDirectory, $"log_{DateTime.Now:yyyyMMdd}.txt");
                    
                    CheckAndRotateLog();

                    var logLine = $"[{entry.Timestamp:HH:mm:ss}] [{entry.LevelText}] [{entry.Category}] {entry.Message}";
                    File.AppendAllText(_logFilePath, logLine + Environment.NewLine, System.Text.Encoding.UTF8);
                }
                catch
                {
                }
            }
        });
    }

    private void CheckAndRotateLog()
    {
        try
        {
            if (File.Exists(_logFilePath))
            {
                var fileInfo = new FileInfo(_logFilePath);
                if (fileInfo.Length > MaxLogSize)
                {
                    var backupPath = Path.Combine(_logDirectory, $"log_{DateTime.Now:yyyyMMdd}_backup_{DateTime.Now:HHmmss}.txt");
                    File.Move(_logFilePath, backupPath);
                }
            }
        }
        catch
        {
        }
    }

    public void Clear()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            LogEntries.Clear();
        });
    }

    public static SolidColorBrush GetLogColor(LogLevel level)
    {
        return level switch
        {
            LogLevel.Info => Brushes.Black,
            LogLevel.Success => Brushes.Green,
            LogLevel.Warning => Brushes.Orange,
            LogLevel.Error => Brushes.Red,
            _ => Brushes.Black
        };
    }
}
