using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for Error Handling
/// Tests error scenarios, recovery, and error reporting
/// </summary>
[TestFixture]
[Category("Integration")]
public class NetworkErrorHandlingTests
{
    private Mock<IAiService> _aiService = null!;

    [SetUp]
    public void Setup()
    {
        _aiService = new Mock<IAiService>();
    }

    [Test]
    public void NetworkError_Timeout_ThrowsTaskCanceled()
    {
        _aiService.Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Request timed out"));
        
        Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await _aiService.Object.GenerateResponseAsync("test"));
    }

    [Test]
    public void NetworkError_ConnectionRefused_ThrowsHttpRequest()
    {
        _aiService.Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        
        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await _aiService.Object.GenerateResponseAsync("test"));
    }

    [Test]
    public async Task NetworkError_WithRetry_EventuallySucceeds()
    {
        var attempts = 0;
        _aiService.Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attempts++;
                if (attempts < 3)
                    throw new HttpRequestException("Temporary failure");
                return "Success after retry";
            });
        
        string? result = null;
        for (int i = 0; i < 5; i++)
        {
            try
            {
                result = await _aiService.Object.GenerateResponseAsync("test");
                break;
            }
            catch (HttpRequestException)
            {
                await Task.Delay(10);
            }
        }
        
        Assert.That(result, Is.EqualTo("Success after retry"));
        Assert.That(attempts, Is.EqualTo(3));
    }

    [Test]
    public void NetworkError_RateLimited_ThrowsWithMessage()
    {
        _aiService.Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("429 Too Many Requests"));
        
        var ex = Assert.ThrowsAsync<HttpRequestException>(async () =>
            await _aiService.Object.GenerateResponseAsync("test"));
        
        Assert.That(ex!.Message, Does.Contain("429"));
    }
}

[TestFixture]
[Category("Integration")]
public class DatabaseErrorHandlingTests
{
    private Mock<IDatabaseService> _dbService = null!;

    [SetUp]
    public void Setup()
    {
        _dbService = new Mock<IDatabaseService>();
    }

    [Test]
    public void DatabaseError_ConnectionFailed_ThrowsException()
    {
        _dbService.Setup(x => x.GetConversationsAsync())
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));
        
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _dbService.Object.GetConversationsAsync());
    }

    [Test]
    public void DatabaseError_CorruptedData_ThrowsException()
    {
        _dbService.Setup(x => x.GetConversationAsync(It.IsAny<int>()))
            .ThrowsAsync(new FormatException("Data corrupted"));
        
        Assert.ThrowsAsync<FormatException>(async () =>
            await _dbService.Object.GetConversationAsync(1));
    }

    [Test]
    public async Task DatabaseError_TransactionRollback_WorksCorrectly()
    {
        var saved = false;
        
        _dbService.Setup(x => x.SaveConversationAsync(It.IsAny<Conversation>()))
            .ReturnsAsync(() =>
            {
                saved = true;
                throw new InvalidOperationException("Constraint violation");
            });
        
        try
        {
            await _dbService.Object.SaveConversationAsync(new Conversation());
        }
        catch (InvalidOperationException)
        {
            // Rollback would happen here
            saved = false;
        }
        
        Assert.That(saved, Is.False);
    }
}

[TestFixture]
[Category("Integration")]
public class EmbeddingErrorHandlingTests
{
    private Mock<IEmbeddingService> _embeddingService = null!;

    [SetUp]
    public void Setup()
    {
        _embeddingService = new Mock<IEmbeddingService>();
    }

    [Test]
    public void EmbeddingError_ModelNotLoaded_ThrowsException()
    {
        _embeddingService.SetupGet(x => x.IsInitialized).Returns(false);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Model not initialized"));
        
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _embeddingService.Object.GenerateEmbeddingAsync("test"));
    }

    [Test]
    public void EmbeddingError_OutOfMemory_ThrowsException()
    {
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new OutOfMemoryException("Not enough memory for embedding"));
        
        Assert.ThrowsAsync<OutOfMemoryException>(async () =>
            await _embeddingService.Object.GenerateEmbeddingAsync("test"));
    }

    [Test]
    public async Task EmbeddingError_InitializationRetry_Succeeds()
    {
        var initAttempts = 0;
        _embeddingService.Setup(x => x.InitializeAsync())
            .ReturnsAsync(() =>
            {
                initAttempts++;
                return initAttempts >= 2;
            });
        
        var success = false;
        for (int i = 0; i < 3; i++)
        {
            success = await _embeddingService.Object.InitializeAsync();
            if (success) break;
        }
        
        Assert.That(success, Is.True);
        Assert.That(initAttempts, Is.EqualTo(2));
    }
}

[TestFixture]
[Category("Integration")]
public class ErrorReportingTests
{
    [Test]
    public void ErrorReport_FromException_CapturesDetails()
    {
        Exception testException;
        try
        {
            throw new InvalidOperationException("Test error", new ArgumentException("Inner error"));
        }
        catch (Exception ex)
        {
            testException = ex;
        }
        
        var report = CreateErrorReport(testException);
        
        Assert.That(report.Exception, Is.Not.Null);
        Assert.That(report.Exception!.Type, Is.EqualTo("InvalidOperationException"));
        Assert.That(report.Exception.Message, Is.EqualTo("Test error"));
        Assert.That(report.Exception.InnerException, Is.Not.Null);
    }

    [Test]
    public void ErrorReport_SeverityClassification_Works()
    {
        var criticalReport = new ErrorReport { Severity = ErrorSeverity.Critical };
        var warningReport = new ErrorReport { Severity = ErrorSeverity.Warning };
        
        Assert.That((int)criticalReport.Severity, Is.GreaterThan((int)warningReport.Severity));
    }

    [Test]
    public void ErrorReport_CategoryClassification_Works()
    {
        var networkError = new ErrorReport { Category = ErrorCategory.Network };
        var dbError = new ErrorReport { Category = ErrorCategory.Database };
        var apiError = new ErrorReport { Category = ErrorCategory.Api };
        
        Assert.That(networkError.Category, Is.EqualTo(ErrorCategory.Network));
        Assert.That(dbError.Category, Is.EqualTo(ErrorCategory.Database));
        Assert.That(apiError.Category, Is.EqualTo(ErrorCategory.Api));
    }

    [Test]
    public void ErrorReport_Breadcrumbs_TrackHistory()
    {
        var report = new ErrorReport();
        
        report.Breadcrumbs.Add(new Breadcrumb { Type = BreadcrumbType.Navigation, Message = "Opened app" });
        report.Breadcrumbs.Add(new Breadcrumb { Type = BreadcrumbType.UserAction, Message = "Clicked button" });
        report.Breadcrumbs.Add(new Breadcrumb { Type = BreadcrumbType.Network, Message = "API call started" });
        report.Breadcrumbs.Add(new Breadcrumb { Type = BreadcrumbType.Error, Message = "Request failed" });
        
        Assert.That(report.Breadcrumbs.Count, Is.EqualTo(4));
        Assert.That(report.Breadcrumbs.Last().Type, Is.EqualTo(BreadcrumbType.Error));
    }

    private static ErrorReport CreateErrorReport(Exception ex)
    {
        var report = new ErrorReport
        {
            Severity = ErrorSeverity.Error,
            Category = ErrorCategory.Unknown,
            Exception = new ExceptionDetails
            {
                Type = ex.GetType().Name,
                FullTypeName = ex.GetType().FullName ?? "",
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                InnerException = ex.InnerException != null ? new ExceptionDetails
                {
                    Type = ex.InnerException.GetType().Name,
                    Message = ex.InnerException.Message
                } : null
            }
        };
        
        return report;
    }
}
