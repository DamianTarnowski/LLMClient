using LLMClient.Models;
using LLMClient.Services;
using Moq;
using NUnit.Framework;
using System.Collections.ObjectModel;

namespace LLMClient.Tests.Services;

[TestFixture]
public class DatabaseServiceTests
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
        
        // Use temp path for test database
        _testDbPath = Path.Combine(Path.GetTempPath(), $"LLMClientTest_{Guid.NewGuid()}.db");
        _databaseService = new DatabaseService(_mockEmbeddingService.Object, _testDbPath);
    }

    [TearDown]
    public void TearDown()
    {
        _databaseService?.Dispose();
        
        // Clean up test database file
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    #region Conversation Tests

    [Test]
    public async Task SaveConversationAsync_NewConversation_AssignsId()
    {
        // Arrange
        var conversation = new Conversation
        {
            Title = "Test Conversation",
            CreatedAt = DateTime.Now
        };

        // Act
        var id = await _databaseService.SaveConversationAsync(conversation);

        // Assert
        Assert.That(id, Is.GreaterThan(0));
        Assert.That(conversation.Id, Is.EqualTo(id));
    }

    [Test]
    public async Task SaveConversationAsync_ExistingConversation_UpdatesRecord()
    {
        // Arrange
        var conversation = new Conversation
        {
            Title = "Original Title",
            CreatedAt = DateTime.Now
        };
        await _databaseService.SaveConversationAsync(conversation);
        var originalId = conversation.Id;

        // Act
        conversation.Title = "Updated Title";
        await _databaseService.SaveConversationAsync(conversation);

        // Assert
        var retrieved = await _databaseService.GetConversationAsync(originalId);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Title, Is.EqualTo("Updated Title"));
        Assert.That(retrieved.Id, Is.EqualTo(originalId));
    }

    [Test]
    public async Task GetConversationsAsync_ReturnsAllConversations()
    {
        // Arrange
        await _databaseService.SaveConversationAsync(new Conversation { Title = "Conv 1" });
        await _databaseService.SaveConversationAsync(new Conversation { Title = "Conv 2" });
        await _databaseService.SaveConversationAsync(new Conversation { Title = "Conv 3" });

        // Act
        var conversations = await _databaseService.GetConversationsAsync();

        // Assert
        Assert.That(conversations.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task GetConversationAsync_WithValidId_ReturnsConversation()
    {
        // Arrange
        var conversation = new Conversation { Title = "Test" };
        await _databaseService.SaveConversationAsync(conversation);

        // Act
        var retrieved = await _databaseService.GetConversationAsync(conversation.Id);

        // Assert
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Title, Is.EqualTo("Test"));
    }

    [Test]
    public async Task GetConversationAsync_WithInvalidId_ReturnsNull()
    {
        // Act
        var retrieved = await _databaseService.GetConversationAsync(99999);

        // Assert
        Assert.That(retrieved, Is.Null);
    }

    [Test]
    public async Task DeleteConversationAsync_RemovesConversationAndMessages()
    {
        // Arrange
        var conversation = new Conversation { Title = "To Delete" };
        await _databaseService.SaveConversationAsync(conversation);
        
        var message = new Message
        {
            ConversationId = conversation.Id,
            Content = "Test message",
            IsUser = true
        };
        await _databaseService.SaveMessageAsync(message);

        // Act
        await _databaseService.DeleteConversationAsync(conversation.Id);

        // Assert
        var retrieved = await _databaseService.GetConversationAsync(conversation.Id);
        Assert.That(retrieved, Is.Null);
        
        var messages = await _databaseService.GetMessagesAsync(conversation.Id);
        Assert.That(messages, Is.Empty);
    }

    #endregion

    #region Message Tests

    [Test]
    public async Task SaveMessageAsync_NewMessage_AssignsId()
    {
        // Arrange
        var conversation = new Conversation { Title = "Test" };
        await _databaseService.SaveConversationAsync(conversation);

        var message = new Message
        {
            ConversationId = conversation.Id,
            Content = "Hello world",
            IsUser = true,
            Timestamp = DateTime.Now
        };

        // Act
        var id = await _databaseService.SaveMessageAsync(message);

        // Assert
        Assert.That(id, Is.GreaterThan(0));
        Assert.That(message.Id, Is.EqualTo(id));
    }

    [Test]
    public async Task GetMessagesAsync_ReturnsMessagesForConversation()
    {
        // Arrange
        var conversation = new Conversation { Title = "Test" };
        await _databaseService.SaveConversationAsync(conversation);

        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conversation.Id,
            Content = "Message 1",
            IsUser = true,
            Timestamp = DateTime.Now
        });
        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conversation.Id,
            Content = "Message 2",
            IsUser = false,
            Timestamp = DateTime.Now.AddSeconds(1)
        });

        // Act
        var messages = await _databaseService.GetMessagesAsync(conversation.Id);

        // Assert
        Assert.That(messages.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task GetMessagesAsync_RespectsLimitAndOffset()
    {
        // Arrange
        var conversation = new Conversation { Title = "Test" };
        await _databaseService.SaveConversationAsync(conversation);

        for (int i = 0; i < 10; i++)
        {
            await _databaseService.SaveMessageAsync(new Message
            {
                ConversationId = conversation.Id,
                Content = $"Message {i}",
                IsUser = true,
                Timestamp = DateTime.Now.AddSeconds(i)
            });
        }

        // Act
        var messages = await _databaseService.GetMessagesAsync(conversation.Id, limit: 3, offset: 2);

        // Assert
        Assert.That(messages.Count, Is.EqualTo(3));
        Assert.That(messages[0].Content, Is.EqualTo("Message 2"));
    }

    [Test]
    public async Task DeleteMessageAsync_RemovesMessage()
    {
        // Arrange
        var conversation = new Conversation { Title = "Test" };
        await _databaseService.SaveConversationAsync(conversation);

        var message = new Message
        {
            ConversationId = conversation.Id,
            Content = "To Delete",
            IsUser = true
        };
        await _databaseService.SaveMessageAsync(message);

        // Act
        await _databaseService.DeleteMessageAsync(message);

        // Assert
        var messages = await _databaseService.GetMessagesAsync(conversation.Id);
        Assert.That(messages.Any(m => m.Content == "To Delete"), Is.False);
    }

    #endregion

    #region Memory Tests

    [Test]
    public async Task AddMemoryAsync_CreatesMemory()
    {
        // Arrange
        var memory = new Memory
        {
            Key = "test_key",
            Value = "test_value",
            Category = "test_category"
        };

        // Act
        await _databaseService.AddMemoryAsync(memory);

        // Assert
        var retrieved = await _databaseService.GetMemoryByKeyAsync("test_key");
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Value, Is.EqualTo("test_value"));
    }

    [Test]
    public async Task GetAllMemoriesAsync_ReturnsAllMemories()
    {
        // Arrange
        await _databaseService.AddMemoryAsync(new Memory { Key = "key1", Value = "value1" });
        await _databaseService.AddMemoryAsync(new Memory { Key = "key2", Value = "value2" });

        // Act
        var memories = await _databaseService.GetAllMemoriesAsync();

        // Assert
        Assert.That(memories.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task SearchMemoriesAsync_FindsMatchingMemories()
    {
        // Arrange
        await _databaseService.AddMemoryAsync(new Memory { Key = "user_preference", Value = "dark mode" });
        await _databaseService.AddMemoryAsync(new Memory { Key = "api_key", Value = "secret" });
        await _databaseService.AddMemoryAsync(new Memory { Key = "user_name", Value = "John" });

        // Act
        var results = await _databaseService.SearchMemoriesAsync("user");

        // Assert
        Assert.That(results.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task GetMemoriesByCategoryAsync_FiltersCorrectly()
    {
        // Arrange
        await _databaseService.AddMemoryAsync(new Memory { Key = "k1", Value = "v1", Category = "settings" });
        await _databaseService.AddMemoryAsync(new Memory { Key = "k2", Value = "v2", Category = "settings" });
        await _databaseService.AddMemoryAsync(new Memory { Key = "k3", Value = "v3", Category = "data" });

        // Act
        var results = await _databaseService.GetMemoriesByCategoryAsync("settings");

        // Assert
        Assert.That(results.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task UpsertMemoryAsync_CreatesNewMemory()
    {
        // Act
        await _databaseService.UpsertMemoryAsync("new_key", "new_value", "category");

        // Assert
        var memory = await _databaseService.GetMemoryByKeyAsync("new_key");
        Assert.That(memory, Is.Not.Null);
        Assert.That(memory!.Value, Is.EqualTo("new_value"));
    }

    [Test]
    public async Task UpsertMemoryAsync_UpdatesExistingMemory()
    {
        // Arrange
        await _databaseService.UpsertMemoryAsync("key", "original_value", "category");

        // Act
        await _databaseService.UpsertMemoryAsync("key", "updated_value", "category");

        // Assert
        var memory = await _databaseService.GetMemoryByKeyAsync("key");
        Assert.That(memory!.Value, Is.EqualTo("updated_value"));
    }

    [Test]
    public async Task DeleteMemoryAsync_RemovesMemory()
    {
        // Arrange
        var memory = new Memory { Key = "to_delete", Value = "value" };
        await _databaseService.AddMemoryAsync(memory);

        // Act
        await _databaseService.DeleteMemoryAsync(memory.Id);

        // Assert
        var retrieved = await _databaseService.GetMemoryByKeyAsync("to_delete");
        Assert.That(retrieved, Is.Null);
    }

    #endregion

    #region RAG Document Tests

    [Test]
    public async Task SaveRagDocumentAsync_CreatesDocument()
    {
        // Arrange
        var doc = new RagDocument
        {
            FileName = "test.pdf",
            FilePath = "/path/to/test.pdf",
            ChunkCount = 5
        };

        // Act
        await _databaseService.SaveRagDocumentAsync(doc);

        // Assert
        var docs = await _databaseService.GetRagDocumentsAsync();
        Assert.That(docs.Count, Is.EqualTo(1));
        Assert.That(docs[0].FileName, Is.EqualTo("test.pdf"));
    }

    [Test]
    public async Task DeleteRagDocumentAsync_RemovesDocumentAndChunks()
    {
        // Arrange
        var doc = new RagDocument { FileName = "test.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);
        await _databaseService.SaveRagChunksAsync(doc.Id, new List<string> { "chunk1", "chunk2" });

        // Act
        await _databaseService.DeleteRagDocumentAsync(doc.Id);

        // Assert
        var docs = await _databaseService.GetRagDocumentsAsync();
        Assert.That(docs, Is.Empty);
        
        var chunks = await _databaseService.GetRagChunksByDocumentAsync(doc.Id);
        Assert.That(chunks, Is.Empty);
    }

    [Test]
    public async Task SaveRagChunksAsync_CreatesChunks()
    {
        // Arrange
        var doc = new RagDocument { FileName = "test.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);

        // Act
        await _databaseService.SaveRagChunksAsync(doc.Id, new List<string> { "Chunk 1", "Chunk 2", "Chunk 3" });

        // Assert
        var chunks = await _databaseService.GetRagChunksByDocumentAsync(doc.Id);
        Assert.That(chunks.Count, Is.EqualTo(3));
        Assert.That(chunks[0].ChunkIndex, Is.EqualTo(0));
        Assert.That(chunks[1].ChunkIndex, Is.EqualTo(1));
    }

    [Test]
    public async Task GetAllRagChunksAsync_ReturnsAllChunks()
    {
        // Arrange
        var doc1 = new RagDocument { FileName = "doc1.pdf", FilePath = "/path1" };
        var doc2 = new RagDocument { FileName = "doc2.pdf", FilePath = "/path2" };
        await _databaseService.SaveRagDocumentAsync(doc1);
        await _databaseService.SaveRagDocumentAsync(doc2);
        
        await _databaseService.SaveRagChunksAsync(doc1.Id, new List<string> { "chunk1", "chunk2" });
        await _databaseService.SaveRagChunksAsync(doc2.Id, new List<string> { "chunk3" });

        // Act
        var chunks = await _databaseService.GetAllRagChunksAsync();

        // Assert
        Assert.That(chunks.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task GetRagChunksWithKeywordFilterAsync_FiltersCorrectly()
    {
        // Arrange
        var doc = new RagDocument { FileName = "test.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);
        
        await _databaseService.SaveRagChunksAsync(doc.Id, new List<string>
        {
            "This is about machine learning and AI",
            "This is about cooking recipes",
            "Machine learning models are powerful"
        });

        // Act
        var results = await _databaseService.GetRagChunksWithKeywordFilterAsync(
            new[] { "machine", "learning" }, limit: 10);

        // Assert
        Assert.That(results.Count, Is.EqualTo(2));
        Assert.That(results.All(c => c.Content.ToLower().Contains("machine")), Is.True);
    }

    [Test]
    public async Task GetRagChunksWithKeywordFilterAsync_RespectsLimit()
    {
        // Arrange
        var doc = new RagDocument { FileName = "test.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);
        
        var chunks = Enumerable.Range(1, 20).Select(i => $"Test chunk {i}").ToList();
        await _databaseService.SaveRagChunksAsync(doc.Id, chunks);

        // Act
        var results = await _databaseService.GetRagChunksWithKeywordFilterAsync(
            new[] { "test" }, limit: 5);

        // Assert
        Assert.That(results.Count, Is.EqualTo(5));
    }

    #endregion

    #region Embedding Stats Tests

    [Test]
    public async Task GetEmbeddingStatsAsync_ReturnsCorrectCounts()
    {
        // Arrange
        var conversation = new Conversation { Title = "Test" };
        await _databaseService.SaveConversationAsync(conversation);

        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conversation.Id,
            Content = "No embedding",
            Embedding = null
        });
        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conversation.Id,
            Content = "Has embedding",
            Embedding = new byte[] { 1, 2, 3 }
        });

        // Act
        var (withEmbeddings, total) = await _databaseService.GetEmbeddingStatsAsync();

        // Assert
        Assert.That(total, Is.EqualTo(2));
        Assert.That(withEmbeddings, Is.EqualTo(1));
    }

    [Test]
    public async Task ClearAllEmbeddingsAsync_RemovesAllEmbeddings()
    {
        // Arrange
        var conversation = new Conversation { Title = "Test" };
        await _databaseService.SaveConversationAsync(conversation);

        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conversation.Id,
            Content = "Message 1",
            Embedding = new byte[] { 1, 2, 3 },
            EmbeddingVersion = "v1"
        });
        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conversation.Id,
            Content = "Message 2",
            Embedding = new byte[] { 4, 5, 6 },
            EmbeddingVersion = "v1"
        });

        // Act
        var cleared = await _databaseService.ClearAllEmbeddingsAsync();

        // Assert
        Assert.That(cleared, Is.EqualTo(2));
        
        var (withEmbeddings, _) = await _databaseService.GetEmbeddingStatsAsync();
        Assert.That(withEmbeddings, Is.EqualTo(0));
    }

    #endregion

    #region Lazy Loading Tests

    [Test]
    public async Task GetConversationsAsync_DoesNotLoadLargeFields()
    {
        // Arrange
        var conversation = new Conversation { Title = "Test" };
        await _databaseService.SaveConversationAsync(conversation);

        var largeImage = new string('X', 10000); // 10KB base64
        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conversation.Id,
            Content = "Message with image",
            ImageBase64 = largeImage,
            Embedding = new byte[1536 * 4] // 6KB embedding
        });

        // Act
        var conversations = await _databaseService.GetConversationsAsync();

        // Assert - messages should have null ImageBase64 and Embedding (lazy loaded)
        var message = conversations[0].Messages.First();
        Assert.That(message.ImageBase64, Is.Null, "ImageBase64 should be lazy loaded");
        Assert.That(message.Embedding, Is.Null, "Embedding should be lazy loaded");
    }

    [Test]
    public async Task GetMessageImageBase64Async_LoadsImageOnDemand()
    {
        // Arrange
        var conversation = new Conversation { Title = "Test" };
        await _databaseService.SaveConversationAsync(conversation);

        var imageData = "base64ImageDataHere";
        var message = new Message
        {
            ConversationId = conversation.Id,
            Content = "Message with image",
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
        var conversation = new Conversation { Title = "Test" };
        await _databaseService.SaveConversationAsync(conversation);

        var embeddingData = new byte[] { 1, 2, 3, 4, 5 };
        var message = new Message
        {
            ConversationId = conversation.Id,
            Content = "Message with embedding",
            Embedding = embeddingData
        };
        await _databaseService.SaveMessageAsync(message);

        // Act
        var loadedEmbedding = await _databaseService.GetMessageEmbeddingAsync(message.Id);

        // Assert
        Assert.That(loadedEmbedding, Is.EqualTo(embeddingData));
    }

    #endregion

    #region Text Search Tests

    [Test]
    public async Task TextSearchAcrossConversationsAsync_FindsMatches()
    {
        // Arrange
        var conv1 = new Conversation { Title = "Conversation 1" };
        var conv2 = new Conversation { Title = "Conversation 2" };
        await _databaseService.SaveConversationAsync(conv1);
        await _databaseService.SaveConversationAsync(conv2);

        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conv1.Id,
            Content = "This message contains the search term"
        });
        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conv2.Id,
            Content = "Another message with search in it"
        });
        await _databaseService.SaveMessageAsync(new Message
        {
            ConversationId = conv2.Id,
            Content = "No match here"
        });

        // Act
        var results = await _databaseService.TextSearchAcrossConversationsAsync("search");

        // Assert
        Assert.That(results.Count, Is.EqualTo(2));
        Assert.That(results.All(r => r.message.Content!.ToLower().Contains("search")), Is.True);
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

    #endregion

    #region Model Settings Tests

    [Test]
    public async Task SaveModelSettingsAsync_CreatesSettings()
    {
        // Arrange
        var settings = new ModelSettings
        {
            DefaultModelId = "test-model",
            Temperature = 0.7f,
            MaxTokens = 1000
        };

        // Act
        var success = await _databaseService.SaveModelSettingsAsync(settings);

        // Assert
        Assert.That(success, Is.True);
        
        var retrieved = await _databaseService.GetModelSettingsAsync();
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.DefaultModelId, Is.EqualTo("test-model"));
    }

    [Test]
    public async Task GetModelSettingsAsync_ReturnsNullWhenNoSettings()
    {
        // Act
        var settings = await _databaseService.GetModelSettingsAsync();

        // Assert
        Assert.That(settings, Is.Null);
    }

    #endregion
}
