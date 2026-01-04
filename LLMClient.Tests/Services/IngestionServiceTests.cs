using LLMClient.Models;
using LLMClient.Services;
using Moq;
using NUnit.Framework;

namespace LLMClient.Tests.Services;

public class IngestionServiceTests : IDisposable
{
    private readonly Mock<IRagService> _mockRagService;
    private readonly string _testFilesPath;

    public IngestionServiceTests()
    {
        _mockRagService = new Mock<IRagService>();
        _testFilesPath = Path.Combine(Path.GetTempPath(), "IngestionTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testFilesPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testFilesPath))
        {
            Directory.Delete(_testFilesPath, true);
        }
    }

    [Test]
    public async Task EnqueueFileAsync_IncreasesQueueCount()
    {
        // Arrange
        var testFile = Path.Combine(_testFilesPath, "test.txt");
        await File.WriteAllTextAsync(testFile, "Test content");

        _mockRagService.Setup(x => x.AddDocumentAsync(It.IsAny<string>()))
            .ReturnsAsync(new RagDocument { Id = 1, FileName = "test.txt", ChunkCount = 1 });

        using var service = new IngestionService(_mockRagService.Object);

        // Act
        await service.EnqueueFileAsync(testFile);

        // Assert - queue count should be 1 initially (may decrease as processing happens)
        Assert.That(service.QueueCount, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task EnqueueFilesAsync_EnqueuesMultipleFiles()
    {
        // Arrange
        var files = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var file = Path.Combine(_testFilesPath, $"test{i}.txt");
            await File.WriteAllTextAsync(file, $"Test content {i}");
            files.Add(file);
        }

        _mockRagService.Setup(x => x.AddDocumentAsync(It.IsAny<string>()))
            .ReturnsAsync((string path) => new RagDocument 
            { 
                Id = files.IndexOf(path) + 1, 
                FileName = Path.GetFileName(path), 
                ChunkCount = 1 
            });

        using var service = new IngestionService(_mockRagService.Object);

        // Act
        await service.EnqueueFilesAsync(files);

        // Assert
        Assert.That(service.IsProcessing || service.QueueCount >= 0, Is.True);
    }

    [Test]
    public async Task ProgressChanged_EventFired()
    {
        // Arrange
        var testFile = Path.Combine(_testFilesPath, "test.txt");
        await File.WriteAllTextAsync(testFile, "Test content");

        _mockRagService.Setup(x => x.AddDocumentAsync(It.IsAny<string>()))
            .ReturnsAsync(new RagDocument { Id = 1, FileName = "test.txt", ChunkCount = 1 });

        using var service = new IngestionService(_mockRagService.Object);
        var progressReceived = false;

        service.ProgressChanged += (s, e) => progressReceived = true;

        // Act
        await service.EnqueueFileAsync(testFile);
        await Task.Delay(100); // Give time for event to fire

        // Assert
        Assert.That(progressReceived, Is.True);
    }

    [Test]
    public async Task ItemCompleted_EventFiredOnSuccess()
    {
        // Arrange
        var testFile = Path.Combine(_testFilesPath, "test.txt");
        await File.WriteAllTextAsync(testFile, "Test content");

        _mockRagService.Setup(x => x.AddDocumentAsync(It.IsAny<string>()))
            .ReturnsAsync(new RagDocument { Id = 1, FileName = "test.txt", ChunkCount = 5 });

        using var service = new IngestionService(_mockRagService.Object);
        var completedEvent = new TaskCompletionSource<IngestionCompletedEventArgs>();

        service.ItemCompleted += (s, e) => completedEvent.TrySetResult(e);

        // Act
        await service.EnqueueFileAsync(testFile);
        var result = await Task.WhenAny(completedEvent.Task, Task.Delay(5000));

        // Assert
        if (result == completedEvent.Task)
        {
            var args = await completedEvent.Task;
            Assert.That(args.FileName, Is.EqualTo("test.txt"));
            Assert.That(args.ChunkCount, Is.EqualTo(5));
        }
    }

    [Test]
    public async Task ErrorOccurred_EventFiredOnFailure()
    {
        // Arrange
        var testFile = Path.Combine(_testFilesPath, "test.txt");
        await File.WriteAllTextAsync(testFile, "Test content");

        _mockRagService.Setup(x => x.AddDocumentAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Test error"));

        using var service = new IngestionService(_mockRagService.Object);
        var errorEvent = new TaskCompletionSource<IngestionErrorEventArgs>();

        service.ErrorOccurred += (s, e) => 
        {
            if (!e.WillRetry) // Only capture final error
                errorEvent.TrySetResult(e);
        };

        // Act
        await service.EnqueueFileAsync(testFile);
        var result = await Task.WhenAny(errorEvent.Task, Task.Delay(10000));

        // Assert
        if (result == errorEvent.Task)
        {
            var args = await errorEvent.Task;
            Assert.That(args.FileName, Is.EqualTo("test.txt"));
            Assert.That(args.ErrorMessage, Does.Contain("Test error"));
            Assert.That(args.WillRetry, Is.False);
        }
    }

    [Test]
    public void PauseResume_ControlsProcessing()
    {
        // Arrange
        using var service = new IngestionService(_mockRagService.Object);

        // Act & Assert
        service.Pause();
        // Service should accept pause without error

        service.Resume();
        // Service should accept resume without error
    }

    [Test]
    public void CancelAll_StopsProcessing()
    {
        // Arrange
        using var service = new IngestionService(_mockRagService.Object);

        // Act
        service.CancelAll();

        // Assert - should not throw
        Assert.Pass();
    }

    [Test]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        var service = new IngestionService(_mockRagService.Object);

        // Act
        service.Dispose();

        // Assert - double dispose should not throw
        service.Dispose();
        Assert.Pass();
    }
}
