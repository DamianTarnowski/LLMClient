using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LLMClient.Models;

namespace LLMClient.Services;

/// <summary>
/// Rozbudowany serwis raportowania błędów z szczegółowym zbieraniem danych,
/// breadcrumbs, kolejkowaniem i wysyłaniem do API.
/// </summary>
public interface IErrorReportingService
{
    void Initialize(ErrorReportingConfig? config = null);
    void ReportException(Exception exception, ErrorSeverity severity = ErrorSeverity.Error, 
        ErrorCategory category = ErrorCategory.Unknown, Dictionary<string, string>? customData = null);
    void ReportError(string message, ErrorSeverity severity = ErrorSeverity.Error,
        ErrorCategory category = ErrorCategory.Unknown, Dictionary<string, string>? customData = null);
    void AddBreadcrumb(string message, BreadcrumbType type = BreadcrumbType.Debug, 
        string category = "", Dictionary<string, string>? data = null, BreadcrumbLevel level = BreadcrumbLevel.Info);
    void SetUserContext(Action<UserContext> configure);
    void AddTag(string tag);
    void SetCustomData(string key, string value);
    Task<bool> FlushAsync();
    Task<List<ErrorReport>> GetPendingReportsAsync();
    Task<bool> SendReportAsync(ErrorReport report);
    void Configure(ErrorReportingConfig config);
    ErrorReportingConfig GetConfig();
    event EventHandler<ErrorReport>? OnErrorReported;
}

public class ErrorReportingService : IErrorReportingService
{
    private static readonly string ReportsDirectory = Path.Combine(FileSystem.AppDataDirectory, "ErrorReports");
    private static readonly string ConfigFile = Path.Combine(FileSystem.AppDataDirectory, "error_reporting_config.json");
    
    private readonly ConcurrentQueue<Breadcrumb> _breadcrumbs = new();
    private readonly ConcurrentDictionary<string, string> _customData = new();
    private readonly ConcurrentBag<string> _tags = new();
    private readonly HttpClient _httpClient;
    private readonly object _lock = new();
    
    private ErrorReportingConfig _config = new();
    private UserContext _userContext = new();
    private DateTime _appStartTime = DateTime.UtcNow;
    private string _sessionId = Guid.NewGuid().ToString("N")[..16];
    private bool _isInitialized;
    
    public event EventHandler<ErrorReport>? OnErrorReported;

    public ErrorReportingService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public ErrorReportingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public void Initialize(ErrorReportingConfig? config = null)
    {
        if (_isInitialized) return;
        
        EnsureDirectoryExists();
        LoadConfig();
        
        if (config != null)
        {
            _config = config;
            SaveConfig();
        }

        // Register global exception handlers
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _isInitialized = true;
        
        AddBreadcrumb("ErrorReportingService initialized", BreadcrumbType.System, "Startup");
        Debug.WriteLine("[ErrorReporting] Service initialized");
    }

    #region Exception Handlers

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        if (exception != null)
        {
            ReportException(exception, 
                e.IsTerminating ? ErrorSeverity.Fatal : ErrorSeverity.Critical,
                CategorizeException(exception),
                new Dictionary<string, string> { ["source"] = "AppDomain.UnhandledException" });
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ReportException(e.Exception, ErrorSeverity.Error, 
            CategorizeException(e.Exception),
            new Dictionary<string, string> { ["source"] = "TaskScheduler.UnobservedTaskException" });
        e.SetObserved();
    }

    #endregion

    #region Public API

    public void ReportException(Exception exception, ErrorSeverity severity = ErrorSeverity.Error,
        ErrorCategory category = ErrorCategory.Unknown, Dictionary<string, string>? customData = null)
    {
        if (!_config.IsEnabled || severity < _config.MinimumSeverity) return;

        try
        {
            var report = BuildReport(severity, category, customData);
            report.Exception = BuildExceptionDetails(exception);
            report.IsHandled = false;
            
            if (category == ErrorCategory.Unknown)
            {
                report.Category = CategorizeException(exception);
            }

            ProcessReport(report);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ErrorReporting] Failed to report exception: {ex.Message}");
        }
    }

    public void ReportError(string message, ErrorSeverity severity = ErrorSeverity.Error,
        ErrorCategory category = ErrorCategory.Unknown, Dictionary<string, string>? customData = null)
    {
        if (!_config.IsEnabled || severity < _config.MinimumSeverity) return;

        try
        {
            var report = BuildReport(severity, category, customData);
            report.Exception = new ExceptionDetails
            {
                Type = "ManualError",
                Message = message
            };
            report.IsHandled = true;

            ProcessReport(report);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ErrorReporting] Failed to report error: {ex.Message}");
        }
    }

    public void AddBreadcrumb(string message, BreadcrumbType type = BreadcrumbType.Debug,
        string category = "", Dictionary<string, string>? data = null, BreadcrumbLevel level = BreadcrumbLevel.Info)
    {
        if (!_config.IncludeBreadcrumbs) return;

        var breadcrumb = new Breadcrumb
        {
            TimestampUtc = DateTime.UtcNow,
            Type = type,
            Category = category,
            Message = message,
            Data = data,
            Level = level
        };

        _breadcrumbs.Enqueue(breadcrumb);

        // Trim old breadcrumbs
        while (_breadcrumbs.Count > _config.MaxBreadcrumbs)
        {
            _breadcrumbs.TryDequeue(out _);
        }
    }

    public void SetUserContext(Action<UserContext> configure)
    {
        configure(_userContext);
    }

    public void AddTag(string tag)
    {
        _tags.Add(tag);
    }

    public void SetCustomData(string key, string value)
    {
        _customData[key] = value;
    }

    public void Configure(ErrorReportingConfig config)
    {
        _config = config;
        SaveConfig();
    }

    public ErrorReportingConfig GetConfig() => _config;

    #endregion

    #region Report Building

    private ErrorReport BuildReport(ErrorSeverity severity, ErrorCategory category, 
        Dictionary<string, string>? customData)
    {
        var report = new ErrorReport
        {
            Severity = severity,
            Category = category,
            Source = GetCallerInfo(),
            Device = _config.IncludeDeviceInfo ? CollectDeviceDetails() : new DeviceDetails(),
            App = CollectAppDetails(),
            SystemState = _config.IncludeSystemState ? CollectSystemState() : new SystemState(),
            UserContext = CloneUserContext(),
            Breadcrumbs = _config.IncludeBreadcrumbs ? _breadcrumbs.ToList() : new List<Breadcrumb>(),
            Tags = _tags.ToList(),
            CustomData = new Dictionary<string, string>(_customData)
        };

        if (customData != null)
        {
            foreach (var kvp in customData)
            {
                report.CustomData[kvp.Key] = kvp.Value;
            }
        }

        return report;
    }

    private ExceptionDetails BuildExceptionDetails(Exception exception)
    {
        var details = new ExceptionDetails
        {
            Type = exception.GetType().Name,
            FullTypeName = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            Source = exception.Source,
            StackTrace = exception.StackTrace,
            TargetSite = exception.TargetSite?.ToString(),
            HResult = exception.HResult,
            HelpLink = exception.HelpLink,
            ParsedStackTrace = ParseStackTrace(exception.StackTrace)
        };

        // Extract exception.Data
        foreach (var key in exception.Data.Keys)
        {
            try
            {
                details.Data[key?.ToString() ?? "null"] = exception.Data[key]?.ToString() ?? "null";
            }
            catch { }
        }

        // Handle inner exceptions
        if (exception is AggregateException aggregateException)
        {
            details.AggregateExceptions = aggregateException.InnerExceptions
                .Select(BuildExceptionDetails)
                .ToList();
        }
        else if (exception.InnerException != null)
        {
            details.InnerException = BuildExceptionDetails(exception.InnerException);
        }

        return details;
    }

    private List<StackFrameInfo> ParseStackTrace(string? stackTrace)
    {
        var frames = new List<StackFrameInfo>();
        if (string.IsNullOrEmpty(stackTrace)) return frames;

        var lines = stackTrace.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var regex = new Regex(@"^\s*at\s+(?<method>.+?)(?:\s+in\s+(?<file>.+?):line\s+(?<line>\d+))?$", 
            RegexOptions.Compiled);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            var match = regex.Match(trimmedLine);
            
            var frame = new StackFrameInfo { RawFrame = trimmedLine };

            if (match.Success)
            {
                var methodPart = match.Groups["method"].Value;
                frame.MethodName = methodPart;
                
                // Parse method name to extract class and namespace
                var lastDot = methodPart.LastIndexOf('.');
                if (lastDot > 0)
                {
                    var beforeMethod = methodPart[..lastDot];
                    var methodName = methodPart[(lastDot + 1)..];
                    
                    // Remove generic parameters for cleaner name
                    var genericIndex = methodName.IndexOf('[');
                    if (genericIndex > 0)
                        methodName = methodName[..genericIndex];
                    
                    var parenIndex = methodName.IndexOf('(');
                    if (parenIndex > 0)
                        methodName = methodName[..parenIndex];
                    
                    frame.MethodName = methodName;
                    
                    var classLastDot = beforeMethod.LastIndexOf('.');
                    if (classLastDot > 0)
                    {
                        frame.ClassName = beforeMethod[(classLastDot + 1)..];
                        frame.Namespace = beforeMethod[..classLastDot];
                    }
                    else
                    {
                        frame.ClassName = beforeMethod;
                    }
                }

                if (match.Groups["file"].Success)
                {
                    frame.FileName = match.Groups["file"].Value;
                }
                
                if (match.Groups["line"].Success && int.TryParse(match.Groups["line"].Value, out var lineNum))
                {
                    frame.LineNumber = lineNum;
                }

                frame.IsAsync = methodPart.Contains("MoveNext") || methodPart.Contains("AsyncStateMachine");
            }

            frames.Add(frame);
        }

        return frames;
    }

    #endregion

    #region Data Collection

    private DeviceDetails CollectDeviceDetails()
    {
        var details = new DeviceDetails();
        
        try
        {
            details.Platform = DeviceInfo.Platform.ToString();
            details.PlatformVersion = DeviceInfo.VersionString;
            details.DeviceType = DeviceInfo.DeviceType.ToString();
            details.Manufacturer = DeviceInfo.Manufacturer;
            details.Model = DeviceInfo.Model;
            details.Name = DeviceInfo.Name;
            details.Idiom = DeviceInfo.Idiom.ToString();
            
            details.Architecture = RuntimeInformation.ProcessArchitecture.ToString();
            details.ProcessorCount = Environment.ProcessorCount;
            details.Is64BitOperatingSystem = Environment.Is64BitOperatingSystem;
            details.Is64BitProcess = Environment.Is64BitProcess;
            
            var mainDisplay = DeviceDisplay.MainDisplayInfo;
            details.ScreenWidth = mainDisplay.Width;
            details.ScreenHeight = mainDisplay.Height;
            details.ScreenDensity = mainDisplay.Density;
            details.DisplayOrientation = mainDisplay.Orientation.ToString();
            
            details.CurrentCulture = System.Globalization.CultureInfo.CurrentCulture.Name;
            details.CurrentUICulture = System.Globalization.CultureInfo.CurrentUICulture.Name;
            details.TimeZoneId = TimeZoneInfo.Local.Id;
            details.TimeZoneOffsetMinutes = (int)TimeZoneInfo.Local.BaseUtcOffset.TotalMinutes;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ErrorReporting] Error collecting device details: {ex.Message}");
        }

        return details;
    }

    private AppDetails CollectAppDetails()
    {
        var details = new AppDetails();
        
        try
        {
            details.Name = AppInfo.Name;
            details.PackageName = AppInfo.PackageName;
            details.Version = AppInfo.VersionString;
            details.BuildNumber = AppInfo.BuildString;
            
            #if DEBUG
            details.Environment = "Debug";
            #else
            details.Environment = "Release";
            #endif
            
            details.Uptime = DateTime.UtcNow - _appStartTime;
            details.StartTimeUtc = _appStartTime;
            
            details.ActiveLanguage = Preferences.Get("AppLanguage", "en-US");
            details.ActiveTheme = Preferences.Get("AppTheme", "System");
            details.IsLocalModelEnabled = Preferences.Get("UseLocalModel", false);
            details.LocalModelName = Preferences.Get("LocalModelName", null as string);
            details.IsEmbeddingEnabled = Preferences.Get("EmbeddingsEnabled", true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ErrorReporting] Error collecting app details: {ex.Message}");
        }

        return details;
    }

    private SystemState CollectSystemState()
    {
        var state = new SystemState();
        
        try
        {
            // GC info
            state.GCTotalMemory = GC.GetTotalMemory(false);
            state.GCCollectionCount0 = GC.CollectionCount(0);
            state.GCCollectionCount1 = GC.CollectionCount(1);
            state.GCCollectionCount2 = GC.CollectionCount(2);
            
            // Thread info
            ThreadPool.GetAvailableThreads(out var workerThreads, out var completionPortThreads);
            state.ThreadPoolWorkerThreads = workerThreads;
            state.ThreadPoolCompletionPortThreads = completionPortThreads;
            state.ThreadCount = Process.GetCurrentProcess().Threads.Count;
            
            // Battery
            try
            {
                state.BatteryLevel = Battery.ChargeLevel;
                state.BatteryState = Battery.State.ToString();
                state.PowerSource = Battery.PowerSource.ToString();
            }
            catch { }
            
            // Network
            try
            {
                state.NetworkAccess = Connectivity.NetworkAccess.ToString();
                state.ConnectionProfiles = Connectivity.ConnectionProfiles.Select(p => p.ToString()).ToList();
            }
            catch { }
            
            // Storage sizes
            try
            {
                var appDataDir = FileSystem.AppDataDirectory;
                if (Directory.Exists(appDataDir))
                {
                    state.AppDataSizeBytes = GetDirectorySize(appDataDir);
                }
                
                var cacheDir = FileSystem.CacheDirectory;
                if (Directory.Exists(cacheDir))
                {
                    state.CacheSizeBytes = GetDirectorySize(cacheDir);
                }
                
                var dbPath = Path.Combine(appDataDir, "llmclient.db");
                if (File.Exists(dbPath))
                {
                    state.DatabaseSizeBytes = new FileInfo(dbPath).Length;
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ErrorReporting] Error collecting system state: {ex.Message}");
        }

        return state;
    }

    private long GetDirectorySize(string path)
    {
        try
        {
            return new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length);
        }
        catch
        {
            return 0;
        }
    }

    private UserContext CloneUserContext()
    {
        return new UserContext
        {
            SessionId = _sessionId,
            SessionDuration = DateTime.UtcNow - _appStartTime,
            CurrentPage = _userContext.CurrentPage,
            PreviousPage = _userContext.PreviousPage,
            LastAction = _userContext.LastAction,
            IsConversationActive = _userContext.IsConversationActive,
            IsStreaming = _userContext.IsStreaming,
            IsSearchActive = _userContext.IsSearchActive,
            IsRagActive = _userContext.IsRagActive,
            HasApiKeyConfigured = _userContext.HasApiKeyConfigured,
            HasLocalModelDownloaded = _userContext.HasLocalModelDownloaded,
            TotalConversations = _userContext.TotalConversations,
            TotalMessages = _userContext.TotalMessages
        };
    }

    private string GetCallerInfo()
    {
        try
        {
            var stackTrace = new StackTrace(true);
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                var method = frame?.GetMethod();
                var declaringType = method?.DeclaringType;
                
                if (declaringType != null && 
                    !declaringType.FullName!.Contains("ErrorReportingService") &&
                    !declaringType.FullName.StartsWith("System."))
                {
                    return $"{declaringType.FullName}.{method?.Name}";
                }
            }
        }
        catch { }
        
        return "Unknown";
    }

    private ErrorCategory CategorizeException(Exception exception)
    {
        return exception switch
        {
            HttpRequestException => ErrorCategory.Network,
            System.Net.Sockets.SocketException => ErrorCategory.Network,
            System.Net.WebException => ErrorCategory.Network,
            SQLite.SQLiteException => ErrorCategory.Database,
            OutOfMemoryException => ErrorCategory.OutOfMemory,
            StackOverflowException => ErrorCategory.Performance,
            IOException => ErrorCategory.FileSystem,
            UnauthorizedAccessException => ErrorCategory.FileSystem,
            JsonException => ErrorCategory.Serialization,
            System.Xml.XmlException => ErrorCategory.Serialization,
            InvalidOperationException when exception.Message.Contains("thread") => ErrorCategory.Concurrency,
            AggregateException => ErrorCategory.Concurrency,
            _ when exception.GetType().Name.Contains("Embedding") => ErrorCategory.Embedding,
            _ when exception.GetType().Name.Contains("Model") => ErrorCategory.LocalModel,
            _ when exception.GetType().Name.Contains("Rag") => ErrorCategory.RAG,
            _ when exception.GetType().Name.Contains("Memory") => ErrorCategory.Memory,
            _ => ErrorCategory.Unknown
        };
    }

    #endregion

    #region Report Processing & Sending

    private void ProcessReport(ErrorReport report)
    {
        // Save locally first
        SaveReportLocally(report);
        
        // Notify listeners
        OnErrorReported?.Invoke(this, report);
        
        Debug.WriteLine($"[ErrorReporting] Report created: {report.ReportId} - {report.Severity} - {report.Category}");
        
        // Try to send if auto-send is enabled
        if (_config.SendAutomatically && !string.IsNullOrEmpty(_config.ApiEndpoint))
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000); // Small delay to avoid blocking
                await SendReportAsync(report);
            });
        }
    }

    private void SaveReportLocally(ErrorReport report)
    {
        try
        {
            EnsureDirectoryExists();
            var fileName = $"report_{report.TimestampUtc:yyyyMMdd_HHmmss}_{report.ReportId[..8]}.json";
            var filePath = Path.Combine(ReportsDirectory, fileName);
            
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            File.WriteAllText(filePath, json);
            
            // Cleanup old reports (keep last 50)
            CleanupOldReports(50);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ErrorReporting] Failed to save report locally: {ex.Message}");
        }
    }

    public async Task<bool> SendReportAsync(ErrorReport report)
    {
        if (string.IsNullOrEmpty(_config.ApiEndpoint))
        {
            Debug.WriteLine("[ErrorReporting] No API endpoint configured");
            return false;
        }

        try
        {
            report.Status = ReportStatus.Sending;
            
            var request = new HttpRequestMessage(HttpMethod.Post, _config.ApiEndpoint)
            {
                Content = JsonContent.Create(report, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };
            
            if (!string.IsNullOrEmpty(_config.ApiKey))
            {
                request.Headers.Add("X-Api-Key", _config.ApiKey);
            }
            
            request.Headers.Add("X-App-Version", AppInfo.VersionString);
            request.Headers.Add("X-Report-Id", report.ReportId);

            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                report.Status = ReportStatus.Sent;
                DeleteLocalReport(report.ReportId);
                Debug.WriteLine($"[ErrorReporting] Report sent successfully: {report.ReportId}");
                return true;
            }
            else
            {
                report.Status = ReportStatus.Failed;
                report.RetryCount++;
                report.LastRetryAttempt = DateTime.UtcNow;
                Debug.WriteLine($"[ErrorReporting] Failed to send report: {response.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            report.Status = ReportStatus.Failed;
            report.RetryCount++;
            report.LastRetryAttempt = DateTime.UtcNow;
            Debug.WriteLine($"[ErrorReporting] Exception sending report: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> FlushAsync()
    {
        var reports = await GetPendingReportsAsync();
        var allSuccess = true;
        
        foreach (var report in reports.Where(r => r.RetryCount < _config.MaxRetries))
        {
            var success = await SendReportAsync(report);
            if (!success) allSuccess = false;
            
            await Task.Delay(500); // Rate limiting
        }
        
        return allSuccess;
    }

    public async Task<List<ErrorReport>> GetPendingReportsAsync()
    {
        var reports = new List<ErrorReport>();
        
        try
        {
            if (!Directory.Exists(ReportsDirectory))
                return reports;

            var files = Directory.GetFiles(ReportsDirectory, "report_*.json");
            
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var report = JsonSerializer.Deserialize<ErrorReport>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    
                    if (report != null)
                    {
                        reports.Add(report);
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ErrorReporting] Error loading pending reports: {ex.Message}");
        }

        return reports.OrderBy(r => r.TimestampUtc).ToList();
    }

    private void DeleteLocalReport(string reportId)
    {
        try
        {
            var files = Directory.GetFiles(ReportsDirectory, $"report_*_{reportId[..8]}.json");
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }
        catch { }
    }

    private void CleanupOldReports(int maxToKeep)
    {
        try
        {
            var files = Directory.GetFiles(ReportsDirectory, "report_*.json")
                .OrderByDescending(f => f)
                .Skip(maxToKeep)
                .ToList();

            foreach (var file in files)
            {
                try { File.Delete(file); } catch { }
            }
        }
        catch { }
    }

    #endregion

    #region Configuration

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                var json = File.ReadAllText(ConfigFile);
                var loaded = JsonSerializer.Deserialize<ErrorReportingConfig>(json);
                if (loaded != null)
                {
                    _config = loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ErrorReporting] Error loading config: {ex.Message}");
        }
    }

    private void SaveConfig()
    {
        try
        {
            EnsureDirectoryExists();
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ErrorReporting] Error saving config: {ex.Message}");
        }
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(ReportsDirectory))
        {
            Directory.CreateDirectory(ReportsDirectory);
        }
    }

    #endregion
}
