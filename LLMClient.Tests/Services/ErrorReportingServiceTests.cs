using LLMClient.Models;
using LLMClient.Services;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LLMClient.Tests.Services;

[TestFixture]
public class ErrorReportingServiceTests
{
    private ErrorReportingService _service = null!;
    private Mock<HttpMessageHandler> _mockHttpHandler = null!;
    private HttpClient _httpClient = null!;

    [SetUp]
    public void SetUp()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _service = new ErrorReportingService(_httpClient);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    #region ErrorReport Model Tests

    [Test]
    public void ErrorReport_DefaultValues_AreCorrect()
    {
        // Act
        var report = new ErrorReport();

        // Assert
        Assert.That(report.ReportId, Is.Not.Null.And.Not.Empty);
        Assert.That(report.TimestampUtc, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1)));
        Assert.That(report.Severity, Is.EqualTo(ErrorSeverity.Error));
        Assert.That(report.Category, Is.EqualTo(ErrorCategory.Unknown));
        Assert.That(report.Status, Is.EqualTo(ReportStatus.Pending));
        Assert.That(report.RetryCount, Is.EqualTo(0));
    }

    [Test]
    public void ErrorReport_CanSetAllProperties()
    {
        // Arrange & Act
        var report = new ErrorReport
        {
            Severity = ErrorSeverity.Critical,
            Category = ErrorCategory.Database,
            Source = "TestSource",
            IsTerminating = true,
            IsHandled = false
        };

        // Assert
        Assert.That(report.Severity, Is.EqualTo(ErrorSeverity.Critical));
        Assert.That(report.Category, Is.EqualTo(ErrorCategory.Database));
        Assert.That(report.Source, Is.EqualTo("TestSource"));
        Assert.That(report.IsTerminating, Is.True);
        Assert.That(report.IsHandled, Is.False);
    }

    [Test]
    public void ExceptionDetails_CanStoreNestedExceptions()
    {
        // Arrange & Act
        var details = new ExceptionDetails
        {
            Type = "OuterException",
            Message = "Outer message",
            InnerException = new ExceptionDetails
            {
                Type = "InnerException",
                Message = "Inner message"
            }
        };

        // Assert
        Assert.That(details.InnerException, Is.Not.Null);
        Assert.That(details.InnerException.Type, Is.EqualTo("InnerException"));
    }

    [Test]
    public void ExceptionDetails_CanStoreAggregateExceptions()
    {
        // Arrange & Act
        var details = new ExceptionDetails
        {
            Type = "AggregateException",
            AggregateExceptions = new List<ExceptionDetails>
            {
                new() { Type = "Exception1", Message = "Error 1" },
                new() { Type = "Exception2", Message = "Error 2" }
            }
        };

        // Assert
        Assert.That(details.AggregateExceptions, Has.Count.EqualTo(2));
    }

    #endregion

    #region Breadcrumb Tests

    [Test]
    public void AddBreadcrumb_AddsBreadcrumbToReport()
    {
        // Arrange
        _service.Initialize(new ErrorReportingConfig { IncludeBreadcrumbs = true, MaxBreadcrumbs = 100 });

        // Act
        _service.AddBreadcrumb("User clicked button", BreadcrumbType.UserAction, "UI");
        _service.AddBreadcrumb("API call started", BreadcrumbType.Network, "API");

        // Assert - breadcrumbs are internal, but we can verify through a report
        var reportCreated = false;
        _service.OnErrorReported += (s, report) =>
        {
            reportCreated = true;
            Assert.That(report.Breadcrumbs.Count, Is.GreaterThanOrEqualTo(2));
        };
        
        _service.ReportError("Test error");
        Assert.That(reportCreated, Is.True);
    }

    [Test]
    public void AddBreadcrumb_RespectsMaxLimit()
    {
        // Arrange
        _service.Initialize(new ErrorReportingConfig { IncludeBreadcrumbs = true, MaxBreadcrumbs = 5 });

        // Act - add more than max
        for (int i = 0; i < 10; i++)
        {
            _service.AddBreadcrumb($"Breadcrumb {i}");
        }

        // Assert through report
        _service.OnErrorReported += (s, report) =>
        {
            Assert.That(report.Breadcrumbs.Count, Is.LessThanOrEqualTo(5));
        };
        
        _service.ReportError("Test");
    }

    [Test]
    public void Breadcrumb_DefaultValues()
    {
        // Act
        var breadcrumb = new Breadcrumb();

        // Assert
        Assert.That(breadcrumb.TimestampUtc, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1)));
        Assert.That(breadcrumb.Level, Is.EqualTo(BreadcrumbLevel.Info));
        Assert.That(breadcrumb.Category, Is.EqualTo(string.Empty));
    }

    #endregion

    #region ReportException Tests

    [Test]
    public void ReportException_CreatesReportWithExceptionDetails()
    {
        // Arrange
        _service.Initialize();
        ErrorReport? capturedReport = null;
        _service.OnErrorReported += (s, report) => capturedReport = report;

        var exception = new InvalidOperationException("Test exception message");

        // Act
        _service.ReportException(exception, ErrorSeverity.Error, ErrorCategory.Unknown);

        // Assert
        Assert.That(capturedReport, Is.Not.Null);
        Assert.That(capturedReport!.Exception, Is.Not.Null);
        Assert.That(capturedReport.Exception!.Type, Is.EqualTo("InvalidOperationException"));
        Assert.That(capturedReport.Exception.Message, Is.EqualTo("Test exception message"));
    }

    [Test]
    public void ReportException_IncludesInnerException()
    {
        // Arrange
        _service.Initialize();
        ErrorReport? capturedReport = null;
        _service.OnErrorReported += (s, report) => capturedReport = report;

        var innerException = new ArgumentException("Inner error");
        var outerException = new InvalidOperationException("Outer error", innerException);

        // Act
        _service.ReportException(outerException);

        // Assert
        Assert.That(capturedReport!.Exception!.InnerException, Is.Not.Null);
        Assert.That(capturedReport.Exception.InnerException!.Type, Is.EqualTo("ArgumentException"));
    }

    [Test]
    public void ReportException_IncludesAggregateExceptions()
    {
        // Arrange
        _service.Initialize();
        ErrorReport? capturedReport = null;
        _service.OnErrorReported += (s, report) => capturedReport = report;

        var aggregate = new AggregateException(
            new ArgumentException("Error 1"),
            new InvalidOperationException("Error 2")
        );

        // Act
        _service.ReportException(aggregate);

        // Assert
        Assert.That(capturedReport!.Exception!.AggregateExceptions, Is.Not.Null);
        Assert.That(capturedReport.Exception.AggregateExceptions!.Count, Is.EqualTo(2));
    }

    [Test]
    public void ReportException_RespectsMinimumSeverity()
    {
        // Arrange
        _service.Initialize(new ErrorReportingConfig { MinimumSeverity = ErrorSeverity.Error });
        var reportCreated = false;
        _service.OnErrorReported += (s, report) => reportCreated = true;

        // Act - report warning (below minimum)
        _service.ReportException(new Exception("Test"), ErrorSeverity.Warning);

        // Assert
        Assert.That(reportCreated, Is.False);
    }

    [Test]
    public void ReportException_AddsCustomData()
    {
        // Arrange
        _service.Initialize();
        ErrorReport? capturedReport = null;
        _service.OnErrorReported += (s, report) => capturedReport = report;

        var customData = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        // Act
        _service.ReportException(new Exception("Test"), customData: customData);

        // Assert
        Assert.That(capturedReport!.CustomData["key1"], Is.EqualTo("value1"));
        Assert.That(capturedReport.CustomData["key2"], Is.EqualTo("value2"));
    }

    #endregion

    #region ReportError Tests

    [Test]
    public void ReportError_CreatesManualErrorReport()
    {
        // Arrange
        _service.Initialize();
        ErrorReport? capturedReport = null;
        _service.OnErrorReported += (s, report) => capturedReport = report;

        // Act
        _service.ReportError("Something went wrong", ErrorSeverity.Warning, ErrorCategory.Configuration);

        // Assert
        Assert.That(capturedReport, Is.Not.Null);
        Assert.That(capturedReport!.Exception!.Type, Is.EqualTo("ManualError"));
        Assert.That(capturedReport.Exception.Message, Is.EqualTo("Something went wrong"));
        Assert.That(capturedReport.Severity, Is.EqualTo(ErrorSeverity.Warning));
        Assert.That(capturedReport.Category, Is.EqualTo(ErrorCategory.Configuration));
        Assert.That(capturedReport.IsHandled, Is.True);
    }

    #endregion

    #region UserContext Tests

    [Test]
    public void SetUserContext_UpdatesUserContext()
    {
        // Arrange
        _service.Initialize();
        ErrorReport? capturedReport = null;
        _service.OnErrorReported += (s, report) => capturedReport = report;

        // Act
        _service.SetUserContext(ctx =>
        {
            ctx.CurrentPage = "MainPage";
            ctx.IsConversationActive = true;
            ctx.TotalConversations = 5;
        });
        _service.ReportError("Test");

        // Assert
        Assert.That(capturedReport!.UserContext.CurrentPage, Is.EqualTo("MainPage"));
        Assert.That(capturedReport.UserContext.IsConversationActive, Is.True);
        Assert.That(capturedReport.UserContext.TotalConversations, Is.EqualTo(5));
    }

    #endregion

    #region Tags and CustomData Tests

    [Test]
    public void AddTag_AddsTagToReports()
    {
        // Arrange
        _service.Initialize();
        ErrorReport? capturedReport = null;
        _service.OnErrorReported += (s, report) => capturedReport = report;

        // Act
        _service.AddTag("production");
        _service.AddTag("android");
        _service.ReportError("Test");

        // Assert
        Assert.That(capturedReport!.Tags, Does.Contain("production"));
        Assert.That(capturedReport.Tags, Does.Contain("android"));
    }

    [Test]
    public void SetCustomData_AddsDataToReports()
    {
        // Arrange
        _service.Initialize();
        ErrorReport? capturedReport = null;
        _service.OnErrorReported += (s, report) => capturedReport = report;

        // Act
        _service.SetCustomData("userId", "anonymous_123");
        _service.SetCustomData("feature", "chat");
        _service.ReportError("Test");

        // Assert
        Assert.That(capturedReport!.CustomData["userId"], Is.EqualTo("anonymous_123"));
        Assert.That(capturedReport.CustomData["feature"], Is.EqualTo("chat"));
    }

    #endregion

    #region Configuration Tests

    [Test]
    public void Configure_UpdatesConfiguration()
    {
        // Arrange
        var config = new ErrorReportingConfig
        {
            ApiEndpoint = "https://api.example.com/errors",
            IsEnabled = true,
            MaxBreadcrumbs = 50
        };

        // Act
        _service.Configure(config);
        var retrieved = _service.GetConfig();

        // Assert
        Assert.That(retrieved.ApiEndpoint, Is.EqualTo("https://api.example.com/errors"));
        Assert.That(retrieved.MaxBreadcrumbs, Is.EqualTo(50));
    }

    [Test]
    public void ErrorReportingConfig_DefaultValues()
    {
        // Act
        var config = new ErrorReportingConfig();

        // Assert
        Assert.That(config.IsEnabled, Is.True);
        Assert.That(config.SendAutomatically, Is.True);
        Assert.That(config.IncludeDeviceInfo, Is.True);
        Assert.That(config.IncludeSystemState, Is.True);
        Assert.That(config.IncludeBreadcrumbs, Is.True);
        Assert.That(config.MaxBreadcrumbs, Is.EqualTo(100));
        Assert.That(config.MaxRetries, Is.EqualTo(3));
        Assert.That(config.MinimumSeverity, Is.EqualTo(ErrorSeverity.Warning));
    }

    #endregion

    #region DeviceDetails Tests

    [Test]
    public void DeviceDetails_DefaultValues()
    {
        // Act
        var details = new DeviceDetails();

        // Assert
        Assert.That(details.Platform, Is.EqualTo(string.Empty));
        Assert.That(details.ProcessorCount, Is.EqualTo(0));
    }

    #endregion

    #region SystemState Tests

    [Test]
    public void SystemState_DefaultValues()
    {
        // Act
        var state = new SystemState();

        // Assert
        Assert.That(state.GCTotalMemory, Is.EqualTo(0));
        Assert.That(state.ThreadCount, Is.EqualTo(0));
        Assert.That(state.ConnectionProfiles, Is.Empty);
    }

    #endregion

    #region StackFrameInfo Tests

    [Test]
    public void StackFrameInfo_CanStoreFrameDetails()
    {
        // Arrange & Act
        var frame = new StackFrameInfo
        {
            FileName = "TestFile.cs",
            LineNumber = 42,
            MethodName = "TestMethod",
            ClassName = "TestClass",
            Namespace = "LLMClient.Tests",
            IsAsync = true
        };

        // Assert
        Assert.That(frame.FileName, Is.EqualTo("TestFile.cs"));
        Assert.That(frame.LineNumber, Is.EqualTo(42));
        Assert.That(frame.IsAsync, Is.True);
    }

    #endregion

    #region Error Category Tests

    [Test]
    public void ErrorCategory_HasExpectedValues()
    {
        // Assert
        Assert.That(Enum.GetValues<ErrorCategory>(), Does.Contain(ErrorCategory.Network));
        Assert.That(Enum.GetValues<ErrorCategory>(), Does.Contain(ErrorCategory.Database));
        Assert.That(Enum.GetValues<ErrorCategory>(), Does.Contain(ErrorCategory.LocalModel));
        Assert.That(Enum.GetValues<ErrorCategory>(), Does.Contain(ErrorCategory.Embedding));
        Assert.That(Enum.GetValues<ErrorCategory>(), Does.Contain(ErrorCategory.RAG));
    }

    #endregion

    #region Severity Tests

    [Test]
    public void ErrorSeverity_IsOrderedCorrectly()
    {
        // Assert
        Assert.That(ErrorSeverity.Debug, Is.LessThan(ErrorSeverity.Info));
        Assert.That(ErrorSeverity.Info, Is.LessThan(ErrorSeverity.Warning));
        Assert.That(ErrorSeverity.Warning, Is.LessThan(ErrorSeverity.Error));
        Assert.That(ErrorSeverity.Error, Is.LessThan(ErrorSeverity.Critical));
        Assert.That(ErrorSeverity.Critical, Is.LessThan(ErrorSeverity.Fatal));
    }

    #endregion

    #region Serialization Tests

    [Test]
    public void ErrorReport_CanSerializeToJson()
    {
        // Arrange
        var report = new ErrorReport
        {
            Severity = ErrorSeverity.Error,
            Category = ErrorCategory.Database,
            Exception = new ExceptionDetails
            {
                Type = "SqlException",
                Message = "Connection failed"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(report);
        var deserialized = JsonSerializer.Deserialize<ErrorReport>(json);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.Severity, Is.EqualTo(ErrorSeverity.Error));
        Assert.That(deserialized.Exception!.Type, Is.EqualTo("SqlException"));
    }

    #endregion
}
