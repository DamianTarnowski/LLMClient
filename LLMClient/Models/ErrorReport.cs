using System.Text.Json.Serialization;

namespace LLMClient.Models;

/// <summary>
/// Kompleksowy model raportu błędu zawierający wszystkie szczegóły potrzebne do debugowania
/// </summary>
public class ErrorReport
{
    // === Identyfikacja ===
    public string ReportId { get; set; } = Guid.NewGuid().ToString();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public DateTime TimestampLocal { get; set; } = DateTime.Now;
    public string TimeZone { get; set; } = TimeZoneInfo.Local.Id;
    
    // === Klasyfikacja błędu ===
    public ErrorSeverity Severity { get; set; } = ErrorSeverity.Error;
    public ErrorCategory Category { get; set; } = ErrorCategory.Unknown;
    public string Source { get; set; } = string.Empty;
    public bool IsTerminating { get; set; }
    public bool IsHandled { get; set; }
    
    // === Szczegóły wyjątku ===
    public ExceptionDetails? Exception { get; set; }
    
    // === Informacje o urządzeniu ===
    public DeviceDetails Device { get; set; } = new();
    
    // === Informacje o aplikacji ===
    public AppDetails App { get; set; } = new();
    
    // === Stan systemu w momencie błędu ===
    public SystemState SystemState { get; set; } = new();
    
    // === Kontekst użytkownika (bez danych wrażliwych) ===
    public UserContext UserContext { get; set; } = new();
    
    // === Breadcrumbs - ślad akcji przed błędem ===
    public List<Breadcrumb> Breadcrumbs { get; set; } = new();
    
    // === Dodatkowe metadane ===
    public Dictionary<string, string> CustomData { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    
    // === Status wysyłki ===
    public ReportStatus Status { get; set; } = ReportStatus.Pending;
    public int RetryCount { get; set; }
    public DateTime? LastRetryAttempt { get; set; }
}

public enum ErrorSeverity
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4,
    Fatal = 5
}

public enum ErrorCategory
{
    Unknown = 0,
    Network = 1,
    Database = 2,
    UI = 3,
    LocalModel = 4,
    Embedding = 5,
    RAG = 6,
    Memory = 7,
    FileSystem = 8,
    Authentication = 9,
    Configuration = 10,
    Performance = 11,
    OutOfMemory = 12,
    Concurrency = 13,
    Serialization = 14,
    Api = 15
}

public enum ReportStatus
{
    Pending = 0,
    Sending = 1,
    Sent = 2,
    Failed = 3,
    Retry = 4
}

/// <summary>
/// Szczegóły wyjątku z pełnym stack trace
/// </summary>
public class ExceptionDetails
{
    public string Type { get; set; } = string.Empty;
    public string FullTypeName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? StackTrace { get; set; }
    public string? TargetSite { get; set; }
    public int HResult { get; set; }
    public string? HelpLink { get; set; }
    
    // Parsowany stack trace dla lepszej analizy
    public List<StackFrameInfo> ParsedStackTrace { get; set; } = new();
    
    // Inner exceptions (rekurencyjnie)
    public ExceptionDetails? InnerException { get; set; }
    
    // Dla AggregateException
    public List<ExceptionDetails>? AggregateExceptions { get; set; }
    
    // Dodatkowe dane z wyjątku
    public Dictionary<string, string> Data { get; set; } = new();
}

/// <summary>
/// Informacje o pojedynczej ramce stosu
/// </summary>
public class StackFrameInfo
{
    public string? FileName { get; set; }
    public int LineNumber { get; set; }
    public int ColumnNumber { get; set; }
    public string? MethodName { get; set; }
    public string? ClassName { get; set; }
    public string? Namespace { get; set; }
    public string? AssemblyName { get; set; }
    public bool IsAsync { get; set; }
    public string RawFrame { get; set; } = string.Empty;
}

/// <summary>
/// Szczegółowe informacje o urządzeniu
/// </summary>
public class DeviceDetails
{
    // Podstawowe info
    public string Platform { get; set; } = string.Empty;
    public string PlatformVersion { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Idiom { get; set; } = string.Empty;
    
    // Hardware
    public string Architecture { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public bool Is64BitOperatingSystem { get; set; }
    public bool Is64BitProcess { get; set; }
    
    // Display
    public double ScreenWidth { get; set; }
    public double ScreenHeight { get; set; }
    public double ScreenDensity { get; set; }
    public string DisplayOrientation { get; set; } = string.Empty;
    
    // Locale
    public string CurrentCulture { get; set; } = string.Empty;
    public string CurrentUICulture { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public int TimeZoneOffsetMinutes { get; set; }
}

/// <summary>
/// Informacje o aplikacji
/// </summary>
public class AppDetails
{
    public string Name { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string BuildNumber { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty; // Debug/Release
    
    // Czas działania
    public TimeSpan Uptime { get; set; }
    public DateTime StartTimeUtc { get; set; }
    
    // Konfiguracja
    public string ActiveLanguage { get; set; } = string.Empty;
    public string ActiveTheme { get; set; } = string.Empty;
    public bool IsLocalModelEnabled { get; set; }
    public string? LocalModelName { get; set; }
    public bool IsEmbeddingEnabled { get; set; }
    public int RagDocumentCount { get; set; }
    public int ConversationCount { get; set; }
    public int MemoryCount { get; set; }
}

/// <summary>
/// Stan systemu w momencie błędu
/// </summary>
public class SystemState
{
    // Pamięć
    public long TotalMemoryBytes { get; set; }
    public long UsedMemoryBytes { get; set; }
    public long AvailableMemoryBytes { get; set; }
    public double MemoryUsagePercent { get; set; }
    public long GCTotalMemory { get; set; }
    public int GCCollectionCount0 { get; set; }
    public int GCCollectionCount1 { get; set; }
    public int GCCollectionCount2 { get; set; }
    
    // Wątki
    public int ThreadCount { get; set; }
    public int ThreadPoolWorkerThreads { get; set; }
    public int ThreadPoolCompletionPortThreads { get; set; }
    
    // Bateria (mobile)
    public double BatteryLevel { get; set; }
    public string BatteryState { get; set; } = string.Empty;
    public string PowerSource { get; set; } = string.Empty;
    
    // Sieć
    public string NetworkAccess { get; set; } = string.Empty;
    public List<string> ConnectionProfiles { get; set; } = new();
    
    // Storage
    public long AppDataSizeBytes { get; set; }
    public long CacheSizeBytes { get; set; }
    public long DatabaseSizeBytes { get; set; }
}

/// <summary>
/// Kontekst użytkownika (bez danych wrażliwych!)
/// </summary>
public class UserContext
{
    // Sesja
    public string SessionId { get; set; } = string.Empty;
    public TimeSpan SessionDuration { get; set; }
    
    // Aktywność
    public string? CurrentPage { get; set; }
    public string? PreviousPage { get; set; }
    public string? LastAction { get; set; }
    
    // Stan UI
    public bool IsConversationActive { get; set; }
    public bool IsStreaming { get; set; }
    public bool IsSearchActive { get; set; }
    public bool IsRagActive { get; set; }
    
    // Preferencje (nie dane!)
    public bool HasApiKeyConfigured { get; set; }
    public bool HasLocalModelDownloaded { get; set; }
    public int TotalConversations { get; set; }
    public int TotalMessages { get; set; }
}

/// <summary>
/// Breadcrumb - pojedyncza akcja/zdarzenie przed błędem
/// </summary>
public class Breadcrumb
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public BreadcrumbType Type { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string>? Data { get; set; }
    public BreadcrumbLevel Level { get; set; } = BreadcrumbLevel.Info;
}

public enum BreadcrumbType
{
    Navigation = 0,
    UserAction = 1,
    Network = 2,
    Database = 3,
    System = 4,
    Error = 5,
    Debug = 6
}

public enum BreadcrumbLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

/// <summary>
/// Konfiguracja serwisu raportowania
/// </summary>
public class ErrorReportingConfig
{
    public string? ApiEndpoint { get; set; }
    public string? ApiKey { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool SendAutomatically { get; set; } = true;
    public bool IncludeDeviceInfo { get; set; } = true;
    public bool IncludeSystemState { get; set; } = true;
    public bool IncludeBreadcrumbs { get; set; } = true;
    public int MaxBreadcrumbs { get; set; } = 100;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(5);
    public ErrorSeverity MinimumSeverity { get; set; } = ErrorSeverity.Warning;
}
