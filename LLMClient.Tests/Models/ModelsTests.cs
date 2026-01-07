using LLMClient.Core.Models;
using LLMClient.Core.Services;

namespace LLMClient.Tests.Models;

[TestFixture]
public class ConversationTests
{
    [Test]
    public void Conversation_CreateNew_HasDefaultValues()
    {
        var conversation = new Conversation();
        
        Assert.That(conversation.Id, Is.EqualTo(0));
        Assert.That(conversation.Title, Is.Empty);
        Assert.That(conversation.Messages, Is.Not.Null);
        Assert.That(conversation.Messages.Count, Is.EqualTo(0));
    }

    [Test]
    public void Conversation_SetTitle_UpdatesProperty()
    {
        var conversation = new Conversation();
        conversation.Title = "Test Conversation";
        
        Assert.That(conversation.Title, Is.EqualTo("Test Conversation"));
    }

    [Test]
    public void Conversation_SetCreatedAt_UpdatesProperty()
    {
        var conversation = new Conversation();
        var date = new DateTime(2025, 1, 1, 12, 0, 0);
        conversation.CreatedAt = date;
        
        Assert.That(conversation.CreatedAt, Is.EqualTo(date));
    }

    [Test]
    public void Conversation_AddMessage_UpdatesLastMessage()
    {
        var conversation = new Conversation();
        var message = new Message { Content = "Hello" };
        conversation.Messages.Add(message);
        
        Assert.That(conversation.LastMessage, Is.EqualTo("Hello"));
    }

    [Test]
    public void Conversation_EmptyMessages_ReturnsDefaultLastMessage()
    {
        var conversation = new Conversation();
        
        Assert.That(conversation.LastMessage, Is.EqualTo("Brak wiadomości"));
    }

    [Test]
    public void Conversation_PropertyChanged_RaisesEvent()
    {
        var conversation = new Conversation();
        var propertyChanged = false;
        conversation.PropertyChanged += (s, e) => propertyChanged = true;
        
        conversation.Title = "New Title";
        
        Assert.That(propertyChanged, Is.True);
    }
}

[TestFixture]
public class MessageTests
{
    [Test]
    public void Message_CreateNew_HasDefaultValues()
    {
        var message = new Message();
        
        Assert.That(message.Id, Is.EqualTo(0));
        Assert.That(message.Content, Is.Empty);
        Assert.That(message.IsUser, Is.False);
        Assert.That(message.IsBot, Is.True);
    }

    [Test]
    public void Message_SetIsUser_UpdatesIsBot()
    {
        var message = new Message { IsUser = true };
        
        Assert.That(message.IsUser, Is.True);
        Assert.That(message.IsBot, Is.False);
    }

    [Test]
    public void Message_SetContent_UpdatesProperty()
    {
        var message = new Message();
        message.Content = "Test content";
        
        Assert.That(message.Content, Is.EqualTo("Test content"));
    }

    [Test]
    public void Message_SetTimestamp_UpdatesProperty()
    {
        var message = new Message();
        var timestamp = DateTime.UtcNow;
        message.Timestamp = timestamp;
        
        Assert.That(message.Timestamp, Is.EqualTo(timestamp));
    }

    [Test]
    public void Message_NoImage_HasImageIsFalse()
    {
        var message = new Message();
        
        Assert.That(message.HasImage, Is.False);
    }

    [Test]
    public void Message_WithImagePath_HasImageIsTrue()
    {
        var message = new Message { ImagePath = "/path/to/image.png" };
        
        Assert.That(message.HasImage, Is.True);
    }

    [Test]
    public void Message_WithImageBase64_HasImageIsTrue()
    {
        var message = new Message { ImageBase64 = "base64data" };
        
        Assert.That(message.HasImage, Is.True);
    }

    [Test]
    public void Message_NoEmbedding_HasEmbeddingIsFalse()
    {
        var message = new Message();
        
        Assert.That(message.HasEmbedding, Is.False);
    }

    [Test]
    public void Message_WithEmbedding_HasEmbeddingIsTrue()
    {
        var message = new Message { Embedding = new byte[] { 1, 2, 3, 4 } };
        
        Assert.That(message.HasEmbedding, Is.True);
    }

    [Test]
    public void Message_PropertyChanged_RaisesEvent()
    {
        var message = new Message();
        string? changedProperty = null;
        message.PropertyChanged += (s, e) => changedProperty = e.PropertyName;
        
        message.Content = "New content";
        
        Assert.That(changedProperty, Is.EqualTo("Content"));
    }
}

[TestFixture]
public class MemoryTests
{
    [Test]
    public void Memory_CreateNew_HasDefaultValues()
    {
        var memory = new Memory();
        
        Assert.That(memory.Id, Is.EqualTo(0));
        Assert.That(memory.Key, Is.Null.Or.Empty);
        Assert.That(memory.Value, Is.Null.Or.Empty);
    }

    [Test]
    public void Memory_SetProperties_UpdatesValues()
    {
        var memory = new Memory
        {
            Key = "test_key",
            Value = "test_value",
            Category = "test_category",
            Tags = "tag1,tag2",
            IsImportant = true
        };
        
        Assert.That(memory.Key, Is.EqualTo("test_key"));
        Assert.That(memory.Value, Is.EqualTo("test_value"));
        Assert.That(memory.Category, Is.EqualTo("test_category"));
        Assert.That(memory.Tags, Is.EqualTo("tag1,tag2"));
        Assert.That(memory.IsImportant, Is.True);
    }
}

[TestFixture]
public class AiModelTests
{
    [Test]
    public void AiModel_CreateNew_HasDefaultValues()
    {
        var model = new AiModel();
        
        Assert.That(model.Name, Is.Null.Or.Empty);
        Assert.That(model.Provider, Is.EqualTo(AiProvider.OpenAI));
    }

    [Test]
    public void AiModel_SetProperties_UpdatesValues()
    {
        var model = new AiModel
        {
            Name = "GPT-4",
            Provider = AiProvider.OpenRouter,
            ModelId = "gpt-4-turbo"
        };
        
        Assert.That(model.Name, Is.EqualTo("GPT-4"));
        Assert.That(model.Provider, Is.EqualTo(AiProvider.OpenRouter));
        Assert.That(model.ModelId, Is.EqualTo("gpt-4-turbo"));
    }

    [Test]
    public void AiProvider_HasExpectedValues()
    {
        Assert.That(Enum.IsDefined(typeof(AiProvider), AiProvider.OpenAI), Is.True);
        Assert.That(Enum.IsDefined(typeof(AiProvider), AiProvider.OpenRouter), Is.True);
        Assert.That(Enum.IsDefined(typeof(AiProvider), AiProvider.Gemini), Is.True);
        Assert.That(Enum.IsDefined(typeof(AiProvider), AiProvider.LocalModel), Is.True);
    }
}

[TestFixture]
public class RagDocumentTests
{
    [Test]
    public void RagDocument_CreateNew_HasDefaultValues()
    {
        var doc = new RagDocument();
        
        Assert.That(doc.Id, Is.EqualTo(0));
        Assert.That(doc.FileName, Is.Null.Or.Empty);
    }

    [Test]
    public void RagDocument_SetProperties_UpdatesValues()
    {
        var doc = new RagDocument
        {
            FileName = "document.pdf",
            Content = "Sample content",
            ChunkCount = 10
        };
        
        Assert.That(doc.FileName, Is.EqualTo("document.pdf"));
        Assert.That(doc.Content, Is.EqualTo("Sample content"));
        Assert.That(doc.ChunkCount, Is.EqualTo(10));
    }
}

[TestFixture]
public class ServiceInterfaceTests
{
    [Test]
    public void RetrievalMode_HasAllValues()
    {
        var values = Enum.GetValues<RetrievalMode>();
        Assert.That(values.Length, Is.EqualTo(3));
        Assert.That(values, Does.Contain(RetrievalMode.Vector));
        Assert.That(values, Does.Contain(RetrievalMode.Keyword));
        Assert.That(values, Does.Contain(RetrievalMode.Hybrid));
    }

    [Test]
    public void SearchResult_CreateNew_HasDefaultValues()
    {
        var result = new SearchResult();
        
        Assert.That(result.Message, Is.Null);
        Assert.That(result.StartIndex, Is.EqualTo(0));
        Assert.That(result.Length, Is.EqualTo(0));
        Assert.That(result.HighlightedContent, Is.Null);
    }

    [Test]
    public void SearchResult_SetProperties_UpdatesValues()
    {
        var message = new Message { Content = "Test" };
        var result = new SearchResult
        {
            Message = message,
            StartIndex = 5,
            Length = 10,
            HighlightedContent = "<em>Test</em>"
        };
        
        Assert.That(result.Message, Is.EqualTo(message));
        Assert.That(result.StartIndex, Is.EqualTo(5));
        Assert.That(result.Length, Is.EqualTo(10));
        Assert.That(result.HighlightedContent, Is.EqualTo("<em>Test</em>"));
    }
}
