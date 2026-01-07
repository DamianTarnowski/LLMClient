using LLMClient.Core.Models;

namespace LLMClient.Tests.Models;

/// <summary>
/// Tests for ErrorReport model and related classes
/// </summary>
[TestFixture]
public class ErrorReportTests
{
    [Test]
    public void ErrorReport_CreateNew_HasDefaultValues()
    {
        var report = new ErrorReport();
        
        Assert.That(report.ReportId, Is.Not.Null.And.Not.Empty);
        Assert.That(report.TimestampUtc, Is.Not.EqualTo(default(DateTime)));
        Assert.That(report.Severity, Is.EqualTo(ErrorSeverity.Error));
        Assert.That(report.Category, Is.EqualTo(ErrorCategory.Unknown));
        Assert.That(report.Status, Is.EqualTo(ReportStatus.Pending));
        Assert.That(report.Breadcrumbs, Is.Not.Null);
        Assert.That(report.CustomData, Is.Not.Null);
        Assert.That(report.Tags, Is.Not.Null);
    }

    [Test]
    public void ErrorReport_SetSeverity_UpdatesProperty()
    {
        var report = new ErrorReport { Severity = ErrorSeverity.Critical };
        Assert.That(report.Severity, Is.EqualTo(ErrorSeverity.Critical));
    }

    [Test]
    public void ErrorReport_SetCategory_UpdatesProperty()
    {
        var report = new ErrorReport { Category = ErrorCategory.Network };
        Assert.That(report.Category, Is.EqualTo(ErrorCategory.Network));
    }

    [Test]
    public void ErrorReport_AddBreadcrumbs_TracksProperly()
    {
        var report = new ErrorReport();
        report.Breadcrumbs.Add(new Breadcrumb { Message = "User clicked button" });
        report.Breadcrumbs.Add(new Breadcrumb { Message = "API call started" });
        report.Breadcrumbs.Add(new Breadcrumb { Message = "Error occurred" });
        
        Assert.That(report.Breadcrumbs.Count, Is.EqualTo(3));
    }

    [Test]
    public void ErrorReport_AddCustomData_TracksProperly()
    {
        var report = new ErrorReport();
        report.CustomData["model"] = "gpt-4";
        report.CustomData["prompt_length"] = "500";
        
        Assert.That(report.CustomData.Count, Is.EqualTo(2));
        Assert.That(report.CustomData["model"], Is.EqualTo("gpt-4"));
    }

    [Test]
    public void ErrorReport_AddTags_TracksProperly()
    {
        var report = new ErrorReport();
        report.Tags.Add("critical");
        report.Tags.Add("network");
        report.Tags.Add("production");
        
        Assert.That(report.Tags.Count, Is.EqualTo(3));
        Assert.That(report.Tags, Does.Contain("critical"));
    }

    [Test]
    public void ErrorReport_ReportId_IsUnique()
    {
        var report1 = new ErrorReport();
        var report2 = new ErrorReport();
        
        Assert.That(report1.ReportId, Is.Not.EqualTo(report2.ReportId));
    }

    [Test]
    public void ErrorReport_SetRetryInfo_UpdatesProperties()
    {
        var report = new ErrorReport
        {
            Status = ReportStatus.Retry,
            RetryCount = 3,
            LastRetryAttempt = DateTime.UtcNow
        };
        
        Assert.That(report.Status, Is.EqualTo(ReportStatus.Retry));
        Assert.That(report.RetryCount, Is.EqualTo(3));
        Assert.That(report.LastRetryAttempt, Is.Not.Null);
    }
}

[TestFixture]
public class ErrorSeverityTests
{
    [Test]
    public void ErrorSeverity_HasCorrectOrder()
    {
        Assert.That((int)ErrorSeverity.Debug, Is.LessThan((int)ErrorSeverity.Info));
        Assert.That((int)ErrorSeverity.Info, Is.LessThan((int)ErrorSeverity.Warning));
        Assert.That((int)ErrorSeverity.Warning, Is.LessThan((int)ErrorSeverity.Error));
        Assert.That((int)ErrorSeverity.Error, Is.LessThan((int)ErrorSeverity.Critical));
        Assert.That((int)ErrorSeverity.Critical, Is.LessThan((int)ErrorSeverity.Fatal));
    }

    [Test]
    public void ErrorSeverity_AllValuesAreDefined()
    {
        var values = Enum.GetValues<ErrorSeverity>();
        Assert.That(values.Length, Is.EqualTo(6));
    }
}

[TestFixture]
public class ErrorCategoryTests
{
    [Test]
    public void ErrorCategory_HasAllExpectedCategories()
    {
        Assert.That(Enum.IsDefined(typeof(ErrorCategory), ErrorCategory.Unknown), Is.True);
        Assert.That(Enum.IsDefined(typeof(ErrorCategory), ErrorCategory.Network), Is.True);
        Assert.That(Enum.IsDefined(typeof(ErrorCategory), ErrorCategory.Database), Is.True);
        Assert.That(Enum.IsDefined(typeof(ErrorCategory), ErrorCategory.LocalModel), Is.True);
        Assert.That(Enum.IsDefined(typeof(ErrorCategory), ErrorCategory.Embedding), Is.True);
        Assert.That(Enum.IsDefined(typeof(ErrorCategory), ErrorCategory.RAG), Is.True);
        Assert.That(Enum.IsDefined(typeof(ErrorCategory), ErrorCategory.Api), Is.True);
    }

    [Test]
    public void ErrorCategory_AllValuesCount()
    {
        var values = Enum.GetValues<ErrorCategory>();
        Assert.That(values.Length, Is.EqualTo(16));
    }
}

[TestFixture]
public class ExceptionDetailsTests
{
    [Test]
    public void ExceptionDetails_CreateNew_HasDefaultValues()
    {
        var details = new ExceptionDetails();
        
        Assert.That(details.Type, Is.Empty);
        Assert.That(details.Message, Is.Empty);
        Assert.That(details.ParsedStackTrace, Is.Not.Null);
        Assert.That(details.Data, Is.Not.Null);
    }

    [Test]
    public void ExceptionDetails_SetFromException_CapturesInfo()
    {
        var details = new ExceptionDetails
        {
            Type = "NullReferenceException",
            FullTypeName = "System.NullReferenceException",
            Message = "Object reference not set to an instance of an object",
            StackTrace = "   at MyClass.MyMethod() in file.cs:line 42"
        };
        
        Assert.That(details.Type, Is.EqualTo("NullReferenceException"));
        Assert.That(details.FullTypeName, Does.Contain("System"));
        Assert.That(details.Message, Does.Contain("Object reference"));
    }

    [Test]
    public void ExceptionDetails_WithInnerException_TracksHierarchy()
    {
        var inner = new ExceptionDetails
        {
            Type = "IOException",
            Message = "File not found"
        };
        
        var outer = new ExceptionDetails
        {
            Type = "ApplicationException",
            Message = "Failed to load data",
            InnerException = inner
        };
        
        Assert.That(outer.InnerException, Is.Not.Null);
        Assert.That(outer.InnerException!.Type, Is.EqualTo("IOException"));
    }

    [Test]
    public void ExceptionDetails_WithAggregateExceptions_TracksAll()
    {
        var details = new ExceptionDetails
        {
            Type = "AggregateException",
            Message = "Multiple errors occurred",
            AggregateExceptions = new List<ExceptionDetails>
            {
                new() { Type = "TaskCanceledException", Message = "Task was cancelled" },
                new() { Type = "TimeoutException", Message = "Operation timed out" }
            }
        };
        
        Assert.That(details.AggregateExceptions, Is.Not.Null);
        Assert.That(details.AggregateExceptions!.Count, Is.EqualTo(2));
    }
}

[TestFixture]
public class StackFrameInfoTests
{
    [Test]
    public void StackFrameInfo_CreateNew_HasDefaultValues()
    {
        var frame = new StackFrameInfo();
        
        Assert.That(frame.LineNumber, Is.EqualTo(0));
        Assert.That(frame.ColumnNumber, Is.EqualTo(0));
        Assert.That(frame.RawFrame, Is.Empty);
    }

    [Test]
    public void StackFrameInfo_SetValues_UpdatesProperties()
    {
        var frame = new StackFrameInfo
        {
            FileName = "MyClass.cs",
            LineNumber = 42,
            ColumnNumber = 15,
            MethodName = "DoSomething",
            ClassName = "MyClass",
            Namespace = "MyApp.Services",
            AssemblyName = "MyApp.dll",
            IsAsync = true
        };
        
        Assert.That(frame.FileName, Is.EqualTo("MyClass.cs"));
        Assert.That(frame.LineNumber, Is.EqualTo(42));
        Assert.That(frame.MethodName, Is.EqualTo("DoSomething"));
        Assert.That(frame.IsAsync, Is.True);
    }
}

[TestFixture]
public class DeviceDetailsTests
{
    [Test]
    public void DeviceDetails_CreateNew_HasDefaultValues()
    {
        var device = new DeviceDetails();
        
        Assert.That(device.Platform, Is.Empty);
        Assert.That(device.ProcessorCount, Is.EqualTo(0));
    }

    [Test]
    public void DeviceDetails_SetValues_UpdatesProperties()
    {
        var device = new DeviceDetails
        {
            Platform = "Windows",
            PlatformVersion = "10.0.19041",
            DeviceType = "Desktop",
            Manufacturer = "Microsoft",
            ProcessorCount = 8,
            Is64BitOperatingSystem = true,
            Is64BitProcess = true,
            ScreenWidth = 1920,
            ScreenHeight = 1080,
            CurrentCulture = "pl-PL"
        };
        
        Assert.That(device.Platform, Is.EqualTo("Windows"));
        Assert.That(device.ProcessorCount, Is.EqualTo(8));
        Assert.That(device.Is64BitOperatingSystem, Is.True);
        Assert.That(device.CurrentCulture, Is.EqualTo("pl-PL"));
    }
}

[TestFixture]
public class AppDetailsTests
{
    [Test]
    public void AppDetails_CreateNew_HasDefaultValues()
    {
        var app = new AppDetails();
        
        Assert.That(app.Name, Is.Empty);
        Assert.That(app.Version, Is.Empty);
    }

    [Test]
    public void AppDetails_SetValues_UpdatesProperties()
    {
        var app = new AppDetails
        {
            Name = "LLMClient",
            PackageName = "com.llmclient.app",
            Version = "1.0.0",
            BuildNumber = "100",
            Environment = "Release",
            ActiveLanguage = "pl",
            ActiveTheme = "Dark",
            IsLocalModelEnabled = true,
            LocalModelName = "Phi-3",
            RagDocumentCount = 5,
            ConversationCount = 10,
            MemoryCount = 25
        };
        
        Assert.That(app.Name, Is.EqualTo("LLMClient"));
        Assert.That(app.Version, Is.EqualTo("1.0.0"));
        Assert.That(app.IsLocalModelEnabled, Is.True);
        Assert.That(app.LocalModelName, Is.EqualTo("Phi-3"));
    }
}

[TestFixture]
public class SystemStateTests
{
    [Test]
    public void SystemState_CreateNew_HasDefaultValues()
    {
        var state = new SystemState();
        
        Assert.That(state.TotalMemoryBytes, Is.EqualTo(0));
        Assert.That(state.ConnectionProfiles, Is.Not.Null);
    }

    [Test]
    public void SystemState_SetMemoryInfo_UpdatesProperties()
    {
        var state = new SystemState
        {
            TotalMemoryBytes = 16L * 1024 * 1024 * 1024, // 16 GB
            UsedMemoryBytes = 8L * 1024 * 1024 * 1024,   // 8 GB
            AvailableMemoryBytes = 8L * 1024 * 1024 * 1024,
            MemoryUsagePercent = 50.0,
            GCTotalMemory = 500 * 1024 * 1024,
            GCCollectionCount0 = 100,
            GCCollectionCount1 = 20,
            GCCollectionCount2 = 5
        };
        
        Assert.That(state.MemoryUsagePercent, Is.EqualTo(50.0));
        Assert.That(state.GCCollectionCount0, Is.EqualTo(100));
    }

    [Test]
    public void SystemState_SetNetworkInfo_UpdatesProperties()
    {
        var state = new SystemState
        {
            NetworkAccess = "Internet",
            ConnectionProfiles = new List<string> { "WiFi", "Ethernet" },
            BatteryLevel = 85.0,
            BatteryState = "Charging",
            PowerSource = "AC"
        };
        
        Assert.That(state.NetworkAccess, Is.EqualTo("Internet"));
        Assert.That(state.ConnectionProfiles.Count, Is.EqualTo(2));
        Assert.That(state.BatteryLevel, Is.EqualTo(85.0));
    }
}

[TestFixture]
public class UserContextTests
{
    [Test]
    public void UserContext_CreateNew_HasDefaultValues()
    {
        var context = new UserContext();
        
        Assert.That(context.SessionId, Is.Empty);
        Assert.That(context.IsConversationActive, Is.False);
    }

    [Test]
    public void UserContext_SetValues_UpdatesProperties()
    {
        var context = new UserContext
        {
            SessionId = "session-12345",
            SessionDuration = TimeSpan.FromMinutes(30),
            CurrentPage = "ChatPage",
            PreviousPage = "SettingsPage",
            LastAction = "SendMessage",
            IsConversationActive = true,
            IsStreaming = true,
            HasApiKeyConfigured = true,
            HasLocalModelDownloaded = true,
            TotalConversations = 10,
            TotalMessages = 500
        };
        
        Assert.That(context.SessionId, Is.EqualTo("session-12345"));
        Assert.That(context.SessionDuration.TotalMinutes, Is.EqualTo(30));
        Assert.That(context.IsConversationActive, Is.True);
        Assert.That(context.TotalMessages, Is.EqualTo(500));
    }
}

[TestFixture]
public class BreadcrumbTests
{
    [Test]
    public void Breadcrumb_CreateNew_HasDefaultValues()
    {
        var breadcrumb = new Breadcrumb();
        
        Assert.That(breadcrumb.TimestampUtc, Is.Not.EqualTo(default(DateTime)));
        Assert.That(breadcrumb.Category, Is.Empty);
        Assert.That(breadcrumb.Message, Is.Empty);
        Assert.That(breadcrumb.Level, Is.EqualTo(BreadcrumbLevel.Info));
    }

    [Test]
    public void Breadcrumb_SetValues_UpdatesProperties()
    {
        var breadcrumb = new Breadcrumb
        {
            Type = BreadcrumbType.UserAction,
            Category = "UI",
            Message = "User clicked Send button",
            Level = BreadcrumbLevel.Info,
            Data = new Dictionary<string, string>
            {
                ["button_id"] = "send_btn",
                ["screen"] = "chat"
            }
        };
        
        Assert.That(breadcrumb.Type, Is.EqualTo(BreadcrumbType.UserAction));
        Assert.That(breadcrumb.Category, Is.EqualTo("UI"));
        Assert.That(breadcrumb.Data, Is.Not.Null);
        Assert.That(breadcrumb.Data!.Count, Is.EqualTo(2));
    }

    [Test]
    public void BreadcrumbType_HasAllExpectedTypes()
    {
        var values = Enum.GetValues<BreadcrumbType>();
        Assert.That(values.Length, Is.EqualTo(7));
        Assert.That(values, Does.Contain(BreadcrumbType.Navigation));
        Assert.That(values, Does.Contain(BreadcrumbType.UserAction));
        Assert.That(values, Does.Contain(BreadcrumbType.Network));
    }
}

[TestFixture]
public class ErrorReportingConfigTests
{
    [Test]
    public void ErrorReportingConfig_CreateNew_HasDefaultValues()
    {
        var config = new ErrorReportingConfig();
        
        Assert.That(config.IsEnabled, Is.True);
        Assert.That(config.SendAutomatically, Is.True);
        Assert.That(config.IncludeDeviceInfo, Is.True);
        Assert.That(config.MaxBreadcrumbs, Is.EqualTo(100));
        Assert.That(config.MaxRetries, Is.EqualTo(3));
        Assert.That(config.MinimumSeverity, Is.EqualTo(ErrorSeverity.Warning));
    }

    [Test]
    public void ErrorReportingConfig_SetValues_UpdatesProperties()
    {
        var config = new ErrorReportingConfig
        {
            ApiEndpoint = "https://api.example.com/errors",
            ApiKey = "secret-key",
            IsEnabled = true,
            SendAutomatically = false,
            MaxBreadcrumbs = 50,
            MaxRetries = 5,
            RetryDelay = TimeSpan.FromMinutes(10),
            MinimumSeverity = ErrorSeverity.Error
        };
        
        Assert.That(config.ApiEndpoint, Is.EqualTo("https://api.example.com/errors"));
        Assert.That(config.MaxBreadcrumbs, Is.EqualTo(50));
        Assert.That(config.RetryDelay.TotalMinutes, Is.EqualTo(10));
        Assert.That(config.MinimumSeverity, Is.EqualTo(ErrorSeverity.Error));
    }
}
