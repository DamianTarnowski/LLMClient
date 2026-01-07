using System.Diagnostics;
using System.Text;

namespace LLMClient.Services;

/// <summary>
/// Global crash reporting and exception handling service.
/// Logs unhandled exceptions locally and provides crash recovery information.
/// </summary>
public class CrashReportingService
{
    private static readonly string CrashLogDirectory = Path.Combine(FileSystem.AppDataDirectory, "CrashLogs");
    private static readonly string LastCrashFile = Path.Combine(CrashLogDirectory, "last_crash.txt");
    private static readonly int MaxCrashLogs = 10;

    private static CrashReportingService? _instance;
    public static CrashReportingService Instance => _instance ??= new CrashReportingService();

    private CrashReportingService() { }

    /// <summary>
    /// Initialize crash reporting handlers. Call this in MauiProgram.cs or App.xaml.cs
    /// </summary>
    public void Initialize()
    {
        EnsureCrashLogDirectory();

        // .NET unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        // Task unobserved exceptions
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Debug.WriteLine("[CrashReporting] Initialized global exception handlers");
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        LogCrash("AppDomain.UnhandledException", exception, e.IsTerminating);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogCrash("TaskScheduler.UnobservedTaskException", e.Exception, isTerminating: false);
        e.SetObserved(); // Prevent app termination
    }

    private void OnMauiUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        LogCrash("MauiExceptions.UnhandledException", exception, isTerminating: false);
    }

    /// <summary>
    /// Log an exception manually (for caught exceptions you want to track)
    /// </summary>
    public void LogException(Exception exception, string context = "Manual")
    {
        LogCrash(context, exception, isTerminating: false);
    }

    private void LogCrash(string source, Exception? exception, bool isTerminating)
    {
        try
        {
            var crashInfo = BuildCrashReport(source, exception, isTerminating);
            var fileName = $"crash_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
            var filePath = Path.Combine(CrashLogDirectory, fileName);

            File.WriteAllText(filePath, crashInfo);
            File.WriteAllText(LastCrashFile, crashInfo);

            Debug.WriteLine($"[CrashReporting] Crash logged to: {filePath}");
            Debug.WriteLine(crashInfo);

            CleanupOldLogs();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CrashReporting] Failed to log crash: {ex.Message}");
        }
    }

    private string BuildCrashReport(string source, Exception? exception, bool isTerminating)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== LLMClient Crash Report ===");
        sb.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
        sb.AppendLine($"Source: {source}");
        sb.AppendLine($"Is Terminating: {isTerminating}");
        sb.AppendLine();

        sb.AppendLine("--- Device Info ---");
        sb.AppendLine($"Platform: {DeviceInfo.Platform}");
        sb.AppendLine($"OS Version: {DeviceInfo.VersionString}");
        sb.AppendLine($"Device Type: {DeviceInfo.DeviceType}");
        sb.AppendLine($"Manufacturer: {DeviceInfo.Manufacturer}");
        sb.AppendLine($"Model: {DeviceInfo.Model}");
        sb.AppendLine($"Idiom: {DeviceInfo.Idiom}");
        sb.AppendLine();

        sb.AppendLine("--- App Info ---");
        sb.AppendLine($"App Version: {AppInfo.VersionString}");
        sb.AppendLine($"Build: {AppInfo.BuildString}");
        sb.AppendLine($"Package: {AppInfo.PackageName}");
        sb.AppendLine();

        if (exception != null)
        {
            sb.AppendLine("--- Exception Details ---");
            AppendExceptionDetails(sb, exception, 0);
        }

        return sb.ToString();
    }

    private void AppendExceptionDetails(StringBuilder sb, Exception exception, int depth)
    {
        var indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}Type: {exception.GetType().FullName}");
        sb.AppendLine($"{indent}Message: {exception.Message}");
        sb.AppendLine($"{indent}Source: {exception.Source}");
        sb.AppendLine($"{indent}StackTrace:");
        
        if (!string.IsNullOrEmpty(exception.StackTrace))
        {
            foreach (var line in exception.StackTrace.Split('\n'))
            {
                sb.AppendLine($"{indent}  {line.Trim()}");
            }
        }

        if (exception is AggregateException aggregateException)
        {
            sb.AppendLine($"{indent}Inner Exceptions ({aggregateException.InnerExceptions.Count}):");
            foreach (var inner in aggregateException.InnerExceptions)
            {
                AppendExceptionDetails(sb, inner, depth + 1);
            }
        }
        else if (exception.InnerException != null)
        {
            sb.AppendLine($"{indent}Inner Exception:");
            AppendExceptionDetails(sb, exception.InnerException, depth + 1);
        }
    }

    /// <summary>
    /// Check if app crashed on last run
    /// </summary>
    public bool DidCrashOnLastRun()
    {
        return File.Exists(LastCrashFile);
    }

    /// <summary>
    /// Get the last crash report content
    /// </summary>
    public string? GetLastCrashReport()
    {
        if (!File.Exists(LastCrashFile))
            return null;

        try
        {
            return File.ReadAllText(LastCrashFile);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Clear the last crash flag (call after showing crash dialog)
    /// </summary>
    public void ClearLastCrash()
    {
        try
        {
            if (File.Exists(LastCrashFile))
                File.Delete(LastCrashFile);
        }
        catch { }
    }

    /// <summary>
    /// Get all crash logs for support/debugging
    /// </summary>
    public IEnumerable<string> GetAllCrashLogs()
    {
        if (!Directory.Exists(CrashLogDirectory))
            yield break;

        foreach (var file in Directory.GetFiles(CrashLogDirectory, "crash_*.txt").OrderByDescending(f => f))
        {
            yield return File.ReadAllText(file);
        }
    }

    /// <summary>
    /// Export all crash logs to a single file for support
    /// </summary>
    public async Task<string?> ExportCrashLogsAsync()
    {
        try
        {
            var logs = GetAllCrashLogs().ToList();
            if (logs.Count == 0)
                return null;

            var exportPath = Path.Combine(FileSystem.CacheDirectory, $"llmclient_crashlogs_{DateTime.UtcNow:yyyyMMdd}.txt");
            var content = string.Join("\n\n" + new string('=', 80) + "\n\n", logs);
            await File.WriteAllTextAsync(exportPath, content);
            return exportPath;
        }
        catch
        {
            return null;
        }
    }

    private void EnsureCrashLogDirectory()
    {
        if (!Directory.Exists(CrashLogDirectory))
            Directory.CreateDirectory(CrashLogDirectory);
    }

    private void CleanupOldLogs()
    {
        try
        {
            var files = Directory.GetFiles(CrashLogDirectory, "crash_*.txt")
                .OrderByDescending(f => f)
                .Skip(MaxCrashLogs)
                .ToList();

            foreach (var file in files)
            {
                File.Delete(file);
            }
        }
        catch { }
    }
}
