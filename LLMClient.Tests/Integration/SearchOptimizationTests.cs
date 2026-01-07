using LLMClient.Models;
using LLMClient.Services;
using Moq;
using NUnit.Framework;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace LLMClient.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class SearchOptimizationTests : IDisposable
{
    private DatabaseService _databaseService = null!;
    private Mock<IEmbeddingService> _mockEmbeddingService = null!;
    private string _testDbPath = null!;

    [SetUp]
    public void SetUp()
    {
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockEmbeddingService.Setup(x => x.IsInitialized).Returns(true);
        _mockEmbeddingService.Setup(x => x.ModelVersion).Returns("test-v1");
        
        _testDbPath = Path.Combine(Path.GetTempPath(), $"SearchOptTest_{Guid.NewGuid()}.db");
        _databaseService = new DatabaseService(_mockEmbeddingService.Object, _testDbPath);
    }

    [TearDown]
    public void TearDown()
    {
        _databaseService?.Dispose();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    public void Dispose()
    {
        TearDown();
    }

    #region Pre-filtering Performance Tests

    [Test]
    public async Task GetRagChunksWithKeywordFilterAsync_PerformsBetterThanFullScan()
    {
        // Arrange - create many chunks
        var doc = new RagDocument { FileName = "large_doc.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);
        
        var chunks = new List<string>();
        for (int i = 0; i < 1000; i++)
        {
            chunks.Add(i % 10 == 0 
                ? $"Chunk {i}: This is about machine learning and artificial intelligence." 
                : $"Chunk {i}: This is generic content about various topics like cooking and gardening.");
        }
        await _databaseService.SaveRagChunksAsync(doc.Id, chunks);

        // Act - measure pre-filtered query
        var swFiltered = Stopwatch.StartNew();
        var filteredResults = await _databaseService.GetRagChunksWithKeywordFilterAsync(
            new[] { "machine", "learning" }, limit: 500);
        swFiltered.Stop();

        // Act - measure full scan
        var swFullScan = Stopwatch.StartNew();
        var allChunks = await _databaseService.GetAllRagChunksAsync();
        swFullScan.Stop();

        // Assert
        Assert.That(filteredResults.Count, Is.LessThan(allChunks.Count), 
            "Filtered results should be smaller than full scan");
        Assert.That(filteredResults.Count, Is.EqualTo(100), 
            "Should find ~100 chunks with 'machine learning' (every 10th)");
        
        // Log performance (not asserting time as it varies by machine)
        Console.WriteLine($"Pre-filtered: {swFiltered.ElapsedMilliseconds}ms, returned {filteredResults.Count} chunks");
        Console.WriteLine($"Full scan: {swFullScan.ElapsedMilliseconds}ms, returned {allChunks.Count} chunks");
    }

    [Test]
    public async Task GetRagChunksWithKeywordFilterAsync_RespectsLimit()
    {
        // Arrange
        var doc = new RagDocument { FileName = "test.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);
        
        var chunks = Enumerable.Range(1, 200).Select(i => $"Test content {i}").ToList();
        await _databaseService.SaveRagChunksAsync(doc.Id, chunks);

        // Act
        var results = await _databaseService.GetRagChunksWithKeywordFilterAsync(
            new[] { "test" }, limit: 50);

        // Assert
        Assert.That(results.Count, Is.EqualTo(50));
    }

    [Test]
    public async Task GetRagChunksWithKeywordFilterAsync_WithEmptyKeywords_ReturnsEmptyList()
    {
        // Arrange
        var doc = new RagDocument { FileName = "test.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);
        await _databaseService.SaveRagChunksAsync(doc.Id, new List<string> { "content" });

        // Act
        var results = await _databaseService.GetRagChunksWithKeywordFilterAsync(
            Array.Empty<string>(), limit: 100);

        // Assert
        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task GetRagChunksWithKeywordFilterAsync_MatchesAnyKeyword()
    {
        // Arrange
        var doc = new RagDocument { FileName = "test.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);
        
        await _databaseService.SaveRagChunksAsync(doc.Id, new List<string>
        {
            "This mentions apple only",
            "This mentions banana only",
            "This mentions cherry only",
            "This mentions nothing relevant"
        });

        // Act
        var results = await _databaseService.GetRagChunksWithKeywordFilterAsync(
            new[] { "apple", "banana" }, limit: 100);

        // Assert
        Assert.That(results.Count, Is.EqualTo(2));
    }

    #endregion

    #region Text Search Tests

    [Test]
    public async Task TextSearchAcrossConversationsAsync_FindsMatchingMessages()
    {
        // Arrange
        var conv = new Conversation { Title = "Test Conv" };
        await _databaseService.SaveConversationAsync(conv);

        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conv.Id,
            Content = "How do I configure the API settings?"
        });
        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conv.Id,
            Content = "The weather is nice today"
        });

        // Act
        var results = await _databaseService.TextSearchAcrossConversationsAsync("API");

        // Assert
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].message.Content, Does.Contain("API"));
    }

    [Test]
    public async Task TextSearchAcrossConversationsAsync_IsCaseInsensitive()
    {
        // Arrange
        var conv = new Conversation { Title = "Test" };
        await _databaseService.SaveConversationAsync(conv);

        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conv.Id,
            Content = "HELLO world"
        });

        // Act
        var results = await _databaseService.TextSearchAcrossConversationsAsync("hello");

        // Assert
        Assert.That(results.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task TextSearchAcrossConversationsAsync_ReturnsConversationTitle()
    {
        // Arrange
        var conv = new Conversation { Title = "My Special Conversation" };
        await _databaseService.SaveConversationAsync(conv);

        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conv.Id,
            Content = "Search term here"
        });

        // Act
        var results = await _databaseService.TextSearchAcrossConversationsAsync("search");

        // Assert
        Assert.That(results[0].conversationTitle, Is.EqualTo("My Special Conversation"));
    }

    #endregion

    #region Lazy Loading Tests

    [Test]
    public async Task GetConversationsAsync_DoesNotLoadImageBase64()
    {
        // Arrange
        var conv = new Conversation { Title = "Test" };
        await _databaseService.SaveConversationAsync(conv);

        var largeImage = new string('X', 50000); // 50KB
        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conv.Id,
            Content = "Message with large image",
            ImageBase64 = largeImage
        });

        // Act
        var conversations = await _databaseService.GetConversationsAsync();

        // Assert
        var message = conversations[0].Messages.First();
        Assert.That(message.ImageBase64, Is.Null, "ImageBase64 should be lazy loaded");
    }

    [Test]
    public async Task GetConversationsAsync_DoesNotLoadEmbedding()
    {
        // Arrange
        var conv = new Conversation { Title = "Test" };
        await _databaseService.SaveConversationAsync(conv);

        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conv.Id,
            Content = "Message with embedding",
            Embedding = new byte[6144] // 6KB embedding
        });

        // Act
        var conversations = await _databaseService.GetConversationsAsync();

        // Assert
        var message = conversations[0].Messages.First();
        Assert.That(message.Embedding, Is.Null, "Embedding should be lazy loaded");
    }

    [Test]
    public async Task GetMessageImageBase64Async_LoadsImageOnDemand()
    {
        // Arrange
        var conv = new Conversation { Title = "Test" };
        await _databaseService.SaveConversationAsync(conv);

        var imageData = "base64ImageDataHere123";
        var message = new Message
        {
            ConversationId = conv.Id,
            Content = "Test",
            ImageBase64 = imageData
        };
        await _databaseService.SaveMessageAsync(message);

        // Act
        var loadedImage = await _databaseService.GetMessageImageBase64Async(message.Id);

        // Assert
        Assert.That(loadedImage, Is.EqualTo(imageData));
    }

    [Test]
    public async Task GetMessageEmbeddingAsync_LoadsEmbeddingOnDemand()
    {
        // Arrange
        var conv = new Conversation { Title = "Test" };
        await _databaseService.SaveConversationAsync(conv);

        var embedding = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var message = new Message
        {
            ConversationId = conv.Id,
            Content = "Test",
            Embedding = embedding
        };
        await _databaseService.SaveMessageAsync(message);

        // Act
        var loadedEmbedding = await _databaseService.GetMessageEmbeddingAsync(message.Id);

        // Assert
        Assert.That(loadedEmbedding, Is.EqualTo(embedding));
    }

    #endregion

    #region Embedding Stats Tests

    [Test]
    public async Task GetEmbeddingStatsAsync_CountsCorrectly()
    {
        // Arrange
        var conv = new Conversation { Title = "Test" };
        await _databaseService.SaveConversationAsync(conv);

        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conv.Id,
            Content = "No embedding"
        });
        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conv.Id,
            Content = "Has embedding",
            Embedding = new byte[] { 1, 2, 3 }
        });
        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conv.Id,
            Content = "Also has embedding",
            Embedding = new byte[] { 4, 5, 6 }
        });

        // Act
        var (withEmbeddings, total) = await _databaseService.GetEmbeddingStatsAsync();

        // Assert
        Assert.That(total, Is.EqualTo(3));
        Assert.That(withEmbeddings, Is.EqualTo(2));
    }

    #endregion

    #region Large Dataset Tests

    [Test]
    [Category("Performance")]
    public async Task GetConversationsAsync_HandlesLargeDataset()
    {
        // Arrange - create 100 conversations with 50 messages each
        for (int c = 0; c < 100; c++)
        {
            var conv = new Conversation { Title = $"Conversation {c}" };
            await _databaseService.SaveConversationAsync(conv);
            
            for (int m = 0; m < 50; m++)
            {
                await _databaseService.SaveMessageAsync(new Message
                {
                    ConversationId = conv.Id,
                    Content = $"Message {m} in conversation {c}"
                });
            }
        }

        // Act
        var sw = Stopwatch.StartNew();
        var conversations = await _databaseService.GetConversationsAsync();
        sw.Stop();

        // Assert
        Assert.That(conversations.Count, Is.EqualTo(100));
        Console.WriteLine($"Loaded 100 conversations with 5000 total messages in {sw.ElapsedMilliseconds}ms");
        
        // Should be reasonably fast (under 5 seconds even on slow machines)
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(5000), 
            "Loading should complete in reasonable time");
    }

    #endregion
}
