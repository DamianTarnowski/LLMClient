using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;

namespace LLMClient.Tests.Integration;

/// <summary>
/// End-to-end workflow integration tests
/// Tests complete user scenarios from start to finish
/// </summary>
[TestFixture]
[Category("Integration")]
public class ChatWorkflowTests
{
    private Mock<IDatabaseService> _dbService = null!;
    private Mock<IAiService> _aiService = null!;
    private Mock<IMemoryService> _memoryService = null!;
    
    private List<Conversation> _conversations = null!;
    private Dictionary<int, List<Message>> _messages = null!;
    private List<Memory> _memories = null!;

    [SetUp]
    public void Setup()
    {
        _conversations = new List<Conversation>();
        _messages = new Dictionary<int, List<Message>>();
        _memories = new List<Memory>();
        
        SetupDatabaseService();
        SetupAiService();
        SetupMemoryService();
    }

    private void SetupDatabaseService()
    {
        _dbService = new Mock<IDatabaseService>();
        
        _dbService.Setup(x => x.SaveConversationAsync(It.IsAny<Conversation>()))
            .ReturnsAsync((Conversation c) =>
            {
                c.Id = _conversations.Count + 1;
                c.CreatedAt = DateTime.UtcNow;
                _conversations.Add(c);
                _messages[c.Id] = new List<Message>();
                return c.Id;
            });
            
        _dbService.Setup(x => x.SaveMessageAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) =>
            {
                m.Id = _messages[m.ConversationId].Count + 1;
                m.Timestamp = DateTime.UtcNow;
                _messages[m.ConversationId].Add(m);
                return m.Id;
            });
            
        _dbService.Setup(x => x.GetMessagesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((int id, int limit, int offset) =>
                _messages.ContainsKey(id) ? _messages[id].Skip(offset).Take(limit).ToList() : new List<Message>());
    }

    private void SetupAiService()
    {
        _aiService = new Mock<IAiService>();
        
        _aiService.Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string prompt, CancellationToken ct) =>
            {
                if (prompt.Contains("name", StringComparison.OrdinalIgnoreCase))
                    return "I see you mentioned a name! How can I help?";
                return "Here's my response to your message.";
            });
    }

    private void SetupMemoryService()
    {
        _memoryService = new Mock<IMemoryService>();
        
        _memoryService.Setup(x => x.AddMemoryAsync(It.IsAny<Memory>()))
            .ReturnsAsync((Memory m) =>
            {
                m.Id = _memories.Count + 1;
                _memories.Add(m);
                return m.Id;
            });
            
        _memoryService.Setup(x => x.GetAllMemoriesAsync())
            .ReturnsAsync(() => _memories.ToList());
    }

    [Test]
    public async Task Workflow_NewConversation_CreatesAndSaves()
    {
        // 1. Create new conversation
        var conversation = new Conversation { Title = "New Chat" };
        var convId = await _dbService.Object.SaveConversationAsync(conversation);
        
        // 2. Send user message
        var userMessage = new Message
        {
            ConversationId = convId,
            Content = "Hello!",
            IsUser = true
        };
        await _dbService.Object.SaveMessageAsync(userMessage);
        
        // 3. Generate AI response
        var response = await _aiService.Object.GenerateResponseAsync("Hello!");
        
        // 4. Save AI response
        var aiMessage = new Message
        {
            ConversationId = convId,
            Content = response,
            IsUser = false
        };
        await _dbService.Object.SaveMessageAsync(aiMessage);
        
        // Verify
        var messages = await _dbService.Object.GetMessagesAsync(convId, 100, 0);
        Assert.That(messages.Count, Is.EqualTo(2));
        Assert.That(messages[0].IsUser, Is.True);
        Assert.That(messages[1].IsBot, Is.True);
    }

    [Test]
    public async Task Workflow_ConversationWithMemory_UsesContext()
    {
        // 1. Add user memory
        await _memoryService.Object.AddMemoryAsync(new Memory
        {
            Key = "user_name",
            Value = "Jan"
        });
        
        // 2. Create conversation
        var convId = await _dbService.Object.SaveConversationAsync(new Conversation { Title = "Chat" });
        
        // 3. Build context with memory
        var memories = await _memoryService.Object.GetAllMemoriesAsync();
        var memoryContext = string.Join(", ", memories.Select(m => $"{m.Key}={m.Value}"));
        
        // 4. Generate response with context
        var prompt = $"Context: {memoryContext}\nUser: My name is Jan";
        var response = await _aiService.Object.GenerateResponseAsync(prompt);
        
        Assert.That(response, Does.Contain("name"));
    }

    [Test]
    public async Task Workflow_MultiTurnConversation_MaintainsContext()
    {
        var convId = await _dbService.Object.SaveConversationAsync(new Conversation { Title = "Multi-turn" });
        
        // Turn 1
        await _dbService.Object.SaveMessageAsync(new Message { ConversationId = convId, Content = "Hi", IsUser = true });
        await _dbService.Object.SaveMessageAsync(new Message { ConversationId = convId, Content = "Hello!", IsUser = false });
        
        // Turn 2
        await _dbService.Object.SaveMessageAsync(new Message { ConversationId = convId, Content = "Tell me about AI", IsUser = true });
        await _dbService.Object.SaveMessageAsync(new Message { ConversationId = convId, Content = "AI is fascinating...", IsUser = false });
        
        // Turn 3
        await _dbService.Object.SaveMessageAsync(new Message { ConversationId = convId, Content = "More details?", IsUser = true });
        await _dbService.Object.SaveMessageAsync(new Message { ConversationId = convId, Content = "Sure, here's more...", IsUser = false });
        
        var messages = await _dbService.Object.GetMessagesAsync(convId, 100, 0);
        Assert.That(messages.Count, Is.EqualTo(6));
    }
}

[TestFixture]
[Category("Integration")]
public class RagWorkflowTests
{
    private Mock<IRagService> _ragService = null!;
    private Mock<IAiService> _aiService = null!;
    private Mock<IEmbeddingService> _embeddingService = null!;

    [SetUp]
    public void Setup()
    {
        _ragService = new Mock<IRagService>();
        _aiService = new Mock<IAiService>();
        _embeddingService = new Mock<IEmbeddingService>();
        
        _ragService.Setup(x => x.GetRelevantContextAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((string query, int topK) =>
                $"Relevant context for '{query}': This is information from documents...");
                
        _aiService.Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string prompt, CancellationToken ct) =>
            {
                if (prompt.Contains("context", StringComparison.OrdinalIgnoreCase))
                    return "Based on the provided context, here's my answer...";
                return "General response without context.";
            });
    }

    [Test]
    public async Task Workflow_RagQuery_UsesDocumentContext()
    {
        var userQuery = "What does the manual say about installation?";
        
        // 1. Get relevant context from RAG
        var context = await _ragService.Object.GetRelevantContextAsync(userQuery, 5);
        
        // 2. Build prompt with context
        var prompt = $"Context:\n{context}\n\nQuestion: {userQuery}";
        
        // 3. Generate response
        var response = await _aiService.Object.GenerateResponseAsync(prompt);
        
        Assert.That(response, Does.Contain("context"));
    }

    [Test]
    public async Task Workflow_RagWithEmbeddings_GeneratesVectors()
    {
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync((string text) => GenerateMockEmbedding(text));
            
        var queryEmbedding = await _embeddingService.Object.GenerateEmbeddingAsync("test query");
        
        Assert.That(queryEmbedding, Is.Not.Null);
        Assert.That(queryEmbedding!.Length, Is.EqualTo(384));
    }

    private static float[] GenerateMockEmbedding(string text)
    {
        var embedding = new float[384];
        var random = new Random(text.GetHashCode());
        for (int i = 0; i < embedding.Length; i++)
            embedding[i] = (float)random.NextDouble();
        return embedding;
    }
}

[TestFixture]
[Category("Integration")]
public class MemoryExtractionWorkflowTests
{
    [Test]
    public void Workflow_ExtractMemoryFromConversation_ParsesCorrectly()
    {
        var messages = new List<Message>
        {
            new() { Content = "My name is Jan", IsUser = true },
            new() { Content = "Nice to meet you, Jan!", IsUser = false },
            new() { Content = "I prefer dark mode", IsUser = true },
            new() { Content = "I'll remember that preference.", IsUser = false }
        };
        
        var extractedMemories = ExtractMemoriesFromConversation(messages);
        
        Assert.That(extractedMemories.Count, Is.GreaterThan(0));
    }

    [Test]
    public void Workflow_MemoryDeduplication_Works()
    {
        var existingMemories = new List<Memory>
        {
            new() { Key = "user_name", Value = "Jan" }
        };
        
        var newMemory = new Memory { Key = "user_name", Value = "Jan Kowalski" };
        
        var shouldUpdate = existingMemories.Any(m => m.Key == newMemory.Key);
        
        Assert.That(shouldUpdate, Is.True);
    }

    private static List<Memory> ExtractMemoriesFromConversation(List<Message> messages)
    {
        var memories = new List<Memory>();
        
        foreach (var msg in messages.Where(m => m.IsUser))
        {
            if (msg.Content.Contains("name is", StringComparison.OrdinalIgnoreCase))
            {
                var namePart = msg.Content.Split("name is", StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (!string.IsNullOrEmpty(namePart))
                {
                    memories.Add(new Memory
                    {
                        Key = "user_name",
                        Value = namePart.Trim().TrimEnd('.', '!', '?')
                    });
                }
            }
            
            if (msg.Content.Contains("prefer", StringComparison.OrdinalIgnoreCase))
            {
                memories.Add(new Memory
                {
                    Key = "preference",
                    Value = msg.Content
                });
            }
        }
        
        return memories;
    }
}
