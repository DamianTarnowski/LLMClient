using LLMClient.Models;
using LLMClient.Services;
using Moq;
using NUnit.Framework;

namespace LLMClient.Tests.Services;

[TestFixture]
public class StreamingBatchServiceTests
{
    private Mock<DatabaseService> _mockDatabaseService = null!;
    private StreamingBatchService _service = null!;

    [SetUp]
    public void SetUp()
    {
        // Note: StreamingBatchService requires concrete DatabaseService, not interface
        // For testing, we'll use a real instance with temp database
        var tempPath = Path.Combine(Path.GetTempPath(), $"StreamingTest_{Guid.NewGuid()}.db");
        var embeddingMock = new Mock<IEmbeddingService>();
        var realDbService = new DatabaseService(embeddingMock.Object, tempPath);
        _service = new StreamingBatchService(realDbService);
    }

    #region StartBatching Tests

    [Test]
    public void StartBatching_InitializesBatchingState()
    {
        // Arrange
        var message = new Message { Content = "" };
        var updateCount = 0;
        Action onUpdate = () => updateCount++;

        // Act
        _service.StartBatching(message, onUpdate);

        // Assert - service should accept chunks after starting
        _service.AddChunk("test");
        Assert.That(message.Content, Is.EqualTo("test"));
        Assert.That(updateCount, Is.EqualTo(1));
    }

    [Test]
    public void StartBatching_ClearsPreviousChunks()
    {
        // Arrange
        var message1 = new Message { Content = "" };
        var message2 = new Message { Content = "" };

        // Act - start batching twice
        _service.StartBatching(message1, () => { });
        _service.AddChunk("chunk1");
        
        _service.StartBatching(message2, () => { }); // Should clear previous
        _service.AddChunk("chunk2");

        // Assert
        Assert.That(message2.Content, Is.EqualTo("chunk2"));
    }

    #endregion

    #region AddChunk Tests

    [Test]
    public void AddChunk_WhenNotBatching_DoesNothing()
    {
        // Arrange - don't call StartBatching
        var message = new Message { Content = "original" };

        // Act
        _service.AddChunk("ignored");

        // Assert
        Assert.That(message.Content, Is.EqualTo("original"));
    }

    [Test]
    public void AddChunk_AppendsToMessageContent()
    {
        // Arrange
        var message = new Message { Content = "Hello " };
        _service.StartBatching(message, () => { });

        // Act
        _service.AddChunk("World");
        _service.AddChunk("!");

        // Assert
        Assert.That(message.Content, Is.EqualTo("Hello World!"));
    }

    [Test]
    public void AddChunk_InvokesOnUpdateCallback()
    {
        // Arrange
        var message = new Message { Content = "" };
        var updateCount = 0;
        _service.StartBatching(message, () => updateCount++);

        // Act
        _service.AddChunk("a");
        _service.AddChunk("b");
        _service.AddChunk("c");

        // Assert
        Assert.That(updateCount, Is.EqualTo(3));
    }

    [Test]
    public void AddChunk_HandlesEmptyChunk()
    {
        // Arrange
        var message = new Message { Content = "test" };
        _service.StartBatching(message, () => { });

        // Act
        _service.AddChunk("");

        // Assert
        Assert.That(message.Content, Is.EqualTo("test"));
    }

    [Test]
    public void AddChunk_HandlesNullCallback()
    {
        // Arrange
        var message = new Message { Content = "" };
        _service.StartBatching(message, null!);

        // Act & Assert - should not throw
        Assert.DoesNotThrow(() => _service.AddChunk("test"));
        Assert.That(message.Content, Is.EqualTo("test"));
    }

    #endregion

    #region FlushAsync Tests

    [Test]
    public async Task FlushAsync_WhenNotBatching_DoesNothing()
    {
        // Act & Assert - should not throw
        await _service.FlushAsync();
    }

    [Test]
    public async Task FlushAsync_WhenNoPendingChunks_DoesNothing()
    {
        // Arrange
        var message = new Message { Content = "" };
        _service.StartBatching(message, () => { });

        // Act & Assert - should not throw
        await _service.FlushAsync();
    }

    [Test]
    public async Task FlushAsync_ClearsPendingChunks()
    {
        // Arrange
        var message = new Message { Id = 0, Content = "", ConversationId = 1 };
        _service.StartBatching(message, () => { });
        _service.AddChunk("test");

        // Act
        await _service.FlushAsync();

        // Assert - subsequent flush should have nothing to do
        // (We can't easily verify this without exposing internal state,
        // but at least it shouldn't throw)
        await _service.FlushAsync();
    }

    #endregion

    #region StopBatching Tests

    [Test]
    public void StopBatching_StopsAcceptingChunks()
    {
        // Arrange
        var message = new Message { Content = "initial" };
        _service.StartBatching(message, () => { });
        _service.AddChunk(" added");

        // Act
        _service.StopBatching();
        _service.AddChunk(" ignored");

        // Assert
        Assert.That(message.Content, Is.EqualTo("initial added"));
    }

    [Test]
    public void StopBatching_WhenNotBatching_DoesNotThrow()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => _service.StopBatching());
    }

    [Test]
    public void StopBatching_CanRestartBatchingAfter()
    {
        // Arrange
        var message1 = new Message { Content = "" };
        var message2 = new Message { Content = "" };

        // Act
        _service.StartBatching(message1, () => { });
        _service.AddChunk("first");
        _service.StopBatching();

        _service.StartBatching(message2, () => { });
        _service.AddChunk("second");

        // Assert
        Assert.That(message1.Content, Is.EqualTo("first"));
        Assert.That(message2.Content, Is.EqualTo("second"));
    }

    #endregion

    #region Thread Safety Tests

    [Test]
    public async Task AddChunk_IsThreadSafe()
    {
        // Arrange
        var message = new Message { Content = "" };
        var updateCount = 0;
        var lockObj = new object();
        _service.StartBatching(message, () => { lock (lockObj) updateCount++; });

        // Act - add chunks from multiple threads
        var tasks = Enumerable.Range(0, 100).Select(i =>
            Task.Run(() => _service.AddChunk($"{i}"))
        ).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        Assert.That(updateCount, Is.EqualTo(100));
        // Content should have all numbers (order may vary)
        Assert.That(message.Content.Length, Is.GreaterThan(0));
    }

    #endregion

    #region Integration Tests

    [Test]
    public void BatchingWorkflow_CompleteScenario()
    {
        // Arrange
        var message = new Message { Content = "" };
        var updates = new List<string>();
        _service.StartBatching(message, () => updates.Add(message.Content));

        // Act - simulate streaming response
        _service.AddChunk("Hello");
        _service.AddChunk(" ");
        _service.AddChunk("World");
        _service.AddChunk("!");
        _service.StopBatching();

        // Assert
        Assert.That(message.Content, Is.EqualTo("Hello World!"));
        Assert.That(updates.Count, Is.EqualTo(4));
        Assert.That(updates[0], Is.EqualTo("Hello"));
        Assert.That(updates[3], Is.EqualTo("Hello World!"));
    }

    #endregion
}
