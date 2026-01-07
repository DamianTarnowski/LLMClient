using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for Conversation functionality
/// Tests conversation CRUD, message handling, and conversation context
/// </summary>
[TestFixture]
[Category("Integration")]
public class ConversationCrudTests
{
    private Mock<IDatabaseService> _dbService = null!;
    private List<Conversation> _conversationStore = null!;
    private Dictionary<int, List<Message>> _messageStore = null!;

    [SetUp]
    public void Setup()
    {
        _conversationStore = new List<Conversation>();
        _messageStore = new Dictionary<int, List<Message>>();
        _dbService = new Mock<IDatabaseService>();
        
        _dbService.Setup(x => x.GetConversationsAsync())
            .ReturnsAsync(() => _conversationStore.ToList());
            
        _dbService.Setup(x => x.GetConversationAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => _conversationStore.FirstOrDefault(c => c.Id == id));
            
        _dbService.Setup(x => x.SaveConversationAsync(It.IsAny<Conversation>()))
            .ReturnsAsync((Conversation c) =>
            {
                if (c.Id == 0)
                {
                    c.Id = _conversationStore.Count + 1;
                    c.CreatedAt = DateTime.UtcNow;
                    _conversationStore.Add(c);
                    _messageStore[c.Id] = new List<Message>();
                }
                return c.Id;
            });
            
        _dbService.Setup(x => x.DeleteConversationAsync(It.IsAny<int>()))
            .Callback((int id) =>
            {
                _conversationStore.RemoveAll(c => c.Id == id);
                _messageStore.Remove(id);
            })
            .Returns(Task.CompletedTask);
            
        _dbService.Setup(x => x.GetMessagesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((int convId, int limit, int offset) =>
            {
                if (!_messageStore.ContainsKey(convId)) return new List<Message>();
                return _messageStore[convId].Skip(offset).Take(limit).ToList();
            });
            
        _dbService.Setup(x => x.SaveMessageAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) =>
            {
                if (!_messageStore.ContainsKey(m.ConversationId))
                    _messageStore[m.ConversationId] = new List<Message>();
                    
                m.Id = _messageStore[m.ConversationId].Count + 1;
                m.Timestamp = DateTime.UtcNow;
                _messageStore[m.ConversationId].Add(m);
                return m.Id;
            });
    }

    [Test]
    public async Task Conversation_Create_ReturnsId()
    {
        var conversation = new Conversation { Title = "New Chat" };
        var id = await _dbService.Object.SaveConversationAsync(conversation);
        
        Assert.That(id, Is.GreaterThan(0));
    }

    [Test]
    public async Task Conversation_GetById_ReturnsConversation()
    {
        var conversation = new Conversation { Title = "Test Chat" };
        var id = await _dbService.Object.SaveConversationAsync(conversation);
        
        var retrieved = await _dbService.Object.GetConversationAsync(id);
        
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Title, Is.EqualTo("Test Chat"));
    }

    [Test]
    public async Task Conversation_GetAll_ReturnsAll()
    {
        await _dbService.Object.SaveConversationAsync(new Conversation { Title = "Chat 1" });
        await _dbService.Object.SaveConversationAsync(new Conversation { Title = "Chat 2" });
        await _dbService.Object.SaveConversationAsync(new Conversation { Title = "Chat 3" });
        
        var all = await _dbService.Object.GetConversationsAsync();
        
        Assert.That(all.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task Conversation_Delete_RemovesConversation()
    {
        var id = await _dbService.Object.SaveConversationAsync(new Conversation { Title = "To Delete" });
        await _dbService.Object.DeleteConversationAsync(id);
        
        var retrieved = await _dbService.Object.GetConversationAsync(id);
        Assert.That(retrieved, Is.Null);
    }

    [Test]
    public async Task Conversation_Delete_RemovesMessages()
    {
        var convId = await _dbService.Object.SaveConversationAsync(new Conversation { Title = "Chat" });
        await _dbService.Object.SaveMessageAsync(new Message { ConversationId = convId, Content = "Hello" });
        
        await _dbService.Object.DeleteConversationAsync(convId);
        
        var messages = await _dbService.Object.GetMessagesAsync(convId, 100, 0);
        Assert.That(messages.Count, Is.EqualTo(0));
    }
}

[TestFixture]
[Category("Integration")]
public class MessageHandlingTests
{
    private Mock<IDatabaseService> _dbService = null!;
    private Dictionary<int, List<Message>> _messageStore = null!;

    [SetUp]
    public void Setup()
    {
        _messageStore = new Dictionary<int, List<Message>> { [1] = new List<Message>() };
        _dbService = new Mock<IDatabaseService>();
        
        _dbService.Setup(x => x.SaveMessageAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) =>
            {
                m.Id = _messageStore[1].Count + 1;
                m.Timestamp = DateTime.UtcNow;
                _messageStore[1].Add(m);
                return m.Id;
            });
            
        _dbService.Setup(x => x.GetMessagesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((int convId, int limit, int offset) =>
                _messageStore[1].Skip(offset).Take(limit).ToList());
    }

    [Test]
    public async Task Message_Add_SavesWithTimestamp()
    {
        var message = new Message
        {
            ConversationId = 1,
            Content = "Hello!",
            IsUser = true
        };
        
        await _dbService.Object.SaveMessageAsync(message);
        
        var messages = await _dbService.Object.GetMessagesAsync(1, 100, 0);
        Assert.That(messages[0].Timestamp, Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public async Task Message_UserAndBot_AlternatesCorrectly()
    {
        await _dbService.Object.SaveMessageAsync(new Message { ConversationId = 1, Content = "Hi", IsUser = true });
        await _dbService.Object.SaveMessageAsync(new Message { ConversationId = 1, Content = "Hello!", IsUser = false });
        await _dbService.Object.SaveMessageAsync(new Message { ConversationId = 1, Content = "How are you?", IsUser = true });
        
        var messages = await _dbService.Object.GetMessagesAsync(1, 100, 0);
        
        Assert.That(messages[0].IsUser, Is.True);
        Assert.That(messages[1].IsBot, Is.True);
        Assert.That(messages[2].IsUser, Is.True);
    }

    [Test]
    public async Task Message_WithImage_HasImageIsTrue()
    {
        var message = new Message
        {
            ConversationId = 1,
            Content = "Check this image",
            ImageBase64 = "base64data..."
        };
        
        await _dbService.Object.SaveMessageAsync(message);
        
        var messages = await _dbService.Object.GetMessagesAsync(1, 100, 0);
        Assert.That(messages[0].HasImage, Is.True);
    }

    [Test]
    public async Task Message_Pagination_Works()
    {
        for (int i = 0; i < 20; i++)
        {
            await _dbService.Object.SaveMessageAsync(new Message 
            { 
                ConversationId = 1, 
                Content = $"Message {i}" 
            });
        }
        
        var page1 = await _dbService.Object.GetMessagesAsync(1, 10, 0);
        var page2 = await _dbService.Object.GetMessagesAsync(1, 10, 10);
        
        Assert.That(page1.Count, Is.EqualTo(10));
        Assert.That(page2.Count, Is.EqualTo(10));
        Assert.That(page1[0].Content, Is.Not.EqualTo(page2[0].Content));
    }
}

[TestFixture]
[Category("Integration")]
public class ConversationContextTests
{
    [Test]
    public void ConversationContext_BuildHistory_FormatsCorrectly()
    {
        var messages = new List<Message>
        {
            new() { Content = "Hi there", IsUser = true },
            new() { Content = "Hello! How can I help?", IsUser = false },
            new() { Content = "Tell me about AI", IsUser = true }
        };
        
        var history = BuildChatHistory(messages);
        
        Assert.That(history.Count, Is.EqualTo(3));
        Assert.That(history[0].role, Is.EqualTo("user"));
        Assert.That(history[1].role, Is.EqualTo("assistant"));
    }

    [Test]
    public void ConversationContext_TruncateHistory_KeepsRecent()
    {
        var messages = Enumerable.Range(1, 50)
            .Select(i => new Message 
            { 
                Content = $"Message {i}", 
                IsUser = i % 2 == 1 
            })
            .ToList();
        
        var history = BuildChatHistory(messages, maxMessages: 10);
        
        Assert.That(history.Count, Is.EqualTo(10));
        Assert.That(history.Last().content, Does.Contain("50"));
    }

    [Test]
    public void ConversationContext_EmptyHistory_ReturnsEmpty()
    {
        var messages = new List<Message>();
        var history = BuildChatHistory(messages);
        
        Assert.That(history, Is.Empty);
    }

    private static List<(string role, string content)> BuildChatHistory(
        List<Message> messages, 
        int maxMessages = 100)
    {
        return messages
            .TakeLast(maxMessages)
            .Select(m => (role: m.IsUser ? "user" : "assistant", content: m.Content))
            .ToList();
    }
}
