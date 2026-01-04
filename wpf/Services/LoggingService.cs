using System.Collections.Concurrent;
using System.IO;

namespace WPFLLM.Services;

public class LoggingService : ILoggingService, IDisposable
{
    private readonly string _logFilePath;
    private readonly ConcurrentQueue<string> _logQueue = new();
    private readonly Timer _flushTimer;
    private readonly object _writeLock = new();
    private bool _disposed;

    public LoggingService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logFolder = Path.Combine(appData, "WPFLLM", "logs");
        Directory.CreateDirectory(logFolder);
        
        var logFileName = $"wpfllm_{DateTime.Now:yyyy-MM-dd}.log";
        _logFilePath = Path.Combine(logFolder, logFileName);
        
        // Flush logs every 5 seconds
        _flushTimer = new Timer(_ => FlushAsync().Wait(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        
        LogInfo("Application started");
        CleanupOldLogs(logFolder, maxAgeDays: 7);
    }

    public void LogInfo(string message) => Log("INFO", message);
    public void LogWarning(string message) => Log("WARN", message);
    public void LogError(string message, Exception? exception = null)
    {
        var fullMessage = exception != null 
            ? $"{message} | Exception: {exception.Message}\n{exception.StackTrace}" 
            : message;
        Log("ERROR", fullMessage);
    }
    public void LogDebug(string message) => Log("DEBUG", message);

    public void LogApiCall(string endpoint, int statusCode, long durationMs)
    {
        Log("API", $"{endpoint} | Status: {statusCode} | Duration: {durationMs}ms");
    }

    private void Log(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{level}] {message}";
        _logQueue.Enqueue(logEntry);
        System.Diagnostics.Debug.WriteLine(logEntry);
    }

    public async Task FlushAsync()
    {
        if (_logQueue.IsEmpty) return;

        var entries = new List<string>();
        while (_logQueue.TryDequeue(out var entry))
        {
            entries.Add(entry);
        }

        if (entries.Count == 0) return;

        try
        {
            lock (_writeLock)
            {
                File.AppendAllLines(_logFilePath, entries);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write log: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    private static void CleanupOldLogs(string logFolder, int maxAgeDays)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-maxAgeDays);
            foreach (var file in Directory.GetFiles(logFolder, "wpfllm_*.log"))
            {
                if (File.GetCreationTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch { /* Ignore cleanup errors */ }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        LogInfo("Application closing");
        FlushAsync().Wait();
        _flushTimer.Dispose();
    }
}
