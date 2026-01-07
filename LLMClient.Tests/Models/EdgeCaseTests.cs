using LLMClient.Core.Models;
using LLMClient.Core.Services;

namespace LLMClient.Tests.Models;

/// <summary>
/// Edge case tests for models - testing boundary conditions and unusual inputs
/// </summary>
[TestFixture]
public class ConversationEdgeCaseTests
{
    [Test]
    public void Conversation_MultipleMessages_LastMessageUpdates()
    {
        var conversation = new Conversation();
        conversation.Messages.Add(new Message { Content = "First" });
        conversation.Messages.Add(new Message { Content = "Second" });
        conversation.Messages.Add(new Message { Content = "Third" });
        
        Assert.That(conversation.LastMessage, Is.EqualTo("Third"));
        Assert.That(conversation.Messages.Count, Is.EqualTo(3));
    }

    [Test]
    public void Conversation_ClearMessages_ReturnsDefaultLastMessage()
    {
        var conversation = new Conversation();
        conversation.Messages.Add(new Message { Content = "Test" });
        conversation.Messages.Clear();
        
        Assert.That(conversation.LastMessage, Is.EqualTo("Brak wiadomości"));
    }

    [Test]
    public void Conversation_VeryLongTitle_AcceptsIt()
    {
        var conversation = new Conversation();
        var longTitle = new string('A', 10000);
        conversation.Title = longTitle;
        
        Assert.That(conversation.Title.Length, Is.EqualTo(10000));
    }

    [Test]
    public void Conversation_UnicodeTitle_HandlesCorrectly()
    {
        var conversation = new Conversation();
        conversation.Title = "测试标题 🎉 Ąę日本語";
        
        Assert.That(conversation.Title, Does.Contain("测试"));
        Assert.That(conversation.Title, Does.Contain("🎉"));
        Assert.That(conversation.Title, Does.Contain("日本語"));
    }

    [Test]
    public void Conversation_EmptyTitle_IsAllowed()
    {
        var conversation = new Conversation { Title = "" };
        Assert.That(conversation.Title, Is.Empty);
    }

    [Test]
    public void Conversation_NullishTitle_HandlesGracefully()
    {
        var conversation = new Conversation();
        conversation.Title = null!;
        // Should not throw
        Assert.Pass();
    }
}

[TestFixture]
public class MessageEdgeCaseTests
{
    [Test]
    public void Message_VeryLongContent_AcceptsIt()
    {
        var message = new Message();
        var longContent = new string('X', 100000);
        message.Content = longContent;
        
        Assert.That(message.Content.Length, Is.EqualTo(100000));
    }

    [Test]
    public void Message_UnicodeContent_HandlesCorrectly()
    {
        var message = new Message
        {
            Content = "Polski: ąęółżźćń\nChinese: 你好\nEmoji: 🚀🎯💡\nJapanese: こんにちは"
        };
        
        Assert.That(message.Content, Does.Contain("ąęółżźćń"));
        Assert.That(message.Content, Does.Contain("你好"));
        Assert.That(message.Content, Does.Contain("🚀"));
    }

    [Test]
    public void Message_MultilineContent_PreservesNewlines()
    {
        var message = new Message
        {
            Content = "Line 1\nLine 2\r\nLine 3\rLine 4"
        };
        
        Assert.That(message.Content, Does.Contain("\n"));
        Assert.That(message.Content.Split('\n').Length, Is.GreaterThan(1));
    }

    [Test]
    public void Message_EmptyEmbedding_HasEmbeddingIsFalse()
    {
        var message = new Message { Embedding = Array.Empty<byte>() };
        Assert.That(message.HasEmbedding, Is.False);
    }

    [Test]
    public void Message_LargeEmbedding_AcceptsIt()
    {
        var message = new Message();
        // 384 floats * 4 bytes = 1536 bytes (typical embedding size)
        var embedding = new byte[1536];
        new Random(42).NextBytes(embedding);
        message.Embedding = embedding;
        
        Assert.That(message.HasEmbedding, Is.True);
        Assert.That(message.Embedding.Length, Is.EqualTo(1536));
    }

    [Test]
    public void Message_TimestampPrecision_IsPreserved()
    {
        var message = new Message();
        var preciseTime = new DateTime(2025, 6, 15, 14, 30, 45, 123);
        message.Timestamp = preciseTime;
        
        Assert.That(message.Timestamp, Is.EqualTo(preciseTime));
        Assert.That(message.Timestamp.Millisecond, Is.EqualTo(123));
    }

    [Test]
    public void Message_FutureTimestamp_IsAllowed()
    {
        var message = new Message
        {
            Timestamp = DateTime.UtcNow.AddYears(100)
        };
        
        Assert.That(message.Timestamp.Year, Is.GreaterThan(2100));
    }

    [Test]
    public void Message_PastTimestamp_IsAllowed()
    {
        var message = new Message
        {
            Timestamp = new DateTime(1900, 1, 1)
        };
        
        Assert.That(message.Timestamp.Year, Is.EqualTo(1900));
    }

    [Test]
    public void Message_BothImagePathAndBase64_HasImageIsTrue()
    {
        var message = new Message
        {
            ImagePath = "/path/image.png",
            ImageBase64 = "base64data"
        };
        
        Assert.That(message.HasImage, Is.True);
    }

    [Test]
    public void Message_SpecialCharactersInImagePath_AcceptsIt()
    {
        var message = new Message
        {
            ImagePath = "/путь/к/图片/imagem_ąę.png"
        };
        
        Assert.That(message.HasImage, Is.True);
        Assert.That(message.ImagePath, Does.Contain("图片"));
    }
}

[TestFixture]
public class MemoryEdgeCaseTests
{
    [Test]
    public void Memory_SpecialCharactersInKey_AcceptsIt()
    {
        var memory = new Memory
        {
            Key = "user:preference:theme:dark-mode",
            Value = "enabled"
        };
        
        Assert.That(memory.Key, Does.Contain(":"));
    }

    [Test]
    public void Memory_JsonValueContent_AcceptsIt()
    {
        var memory = new Memory
        {
            Key = "settings",
            Value = "{\"theme\":\"dark\",\"fontSize\":14,\"enabled\":true}"
        };
        
        Assert.That(memory.Value, Does.Contain("theme"));
        Assert.That(memory.Value, Does.StartWith("{"));
    }

    [Test]
    public void Memory_MultipleTags_SeparatedCorrectly()
    {
        var memory = new Memory
        {
            Tags = "important,user-preference,settings,ui"
        };
        
        var tags = memory.Tags?.Split(',');
        Assert.That(tags?.Length, Is.EqualTo(4));
    }

    [Test]
    public void Memory_EmptyCategory_IsAllowed()
    {
        var memory = new Memory { Category = "" };
        Assert.That(memory.Category, Is.Empty);
    }
}

[TestFixture]
public class AiModelEdgeCaseTests
{
    [Test]
    public void AiModel_AllProviders_AreDefined()
    {
        var providers = Enum.GetValues<AiProvider>();
        
        Assert.That(providers, Does.Contain(AiProvider.OpenAI));
        Assert.That(providers, Does.Contain(AiProvider.Anthropic));
        Assert.That(providers, Does.Contain(AiProvider.Gemini));
        Assert.That(providers, Does.Contain(AiProvider.OpenRouter));
        Assert.That(providers, Does.Contain(AiProvider.LocalModel));
        Assert.That(providers.Length, Is.GreaterThanOrEqualTo(5));
    }

    [Test]
    public void AiModel_LongModelId_AcceptsIt()
    {
        var model = new AiModel
        {
            ModelId = "organization/model-name-with-many-parts-v1.2.3-beta-4k-context-instruct-chat"
        };
        
        Assert.That(model.ModelId.Length, Is.GreaterThan(50));
    }

    [Test]
    public void AiModel_SpecialModelName_AcceptsIt()
    {
        var model = new AiModel
        {
            Name = "GPT-4 Turbo (128K) 🚀"
        };
        
        Assert.That(model.Name, Does.Contain("🚀"));
    }
}

[TestFixture]
public class RagDocumentEdgeCaseTests
{
    [Test]
    public void RagDocument_VeryLongContent_AcceptsIt()
    {
        var doc = new RagDocument();
        var longContent = new string('X', 1_000_000); // 1MB of text
        doc.Content = longContent;
        
        Assert.That(doc.Content.Length, Is.EqualTo(1_000_000));
    }

    [Test]
    public void RagDocument_UnicodeFileName_AcceptsIt()
    {
        var doc = new RagDocument
        {
            FileName = "文档_документ_📄.pdf"
        };
        
        Assert.That(doc.FileName, Does.Contain("文档"));
        Assert.That(doc.FileName, Does.Contain("документ"));
    }

    [Test]
    public void RagDocument_ZeroChunks_IsAllowed()
    {
        var doc = new RagDocument { ChunkCount = 0 };
        Assert.That(doc.ChunkCount, Is.EqualTo(0));
    }

    [Test]
    public void RagDocument_LargeChunkCount_AcceptsIt()
    {
        var doc = new RagDocument { ChunkCount = 100000 };
        Assert.That(doc.ChunkCount, Is.EqualTo(100000));
    }
}

[TestFixture]
public class PropertyChangedTests
{
    [Test]
    public void Conversation_MultiplePropertyChanges_AllEventsRaised()
    {
        var conversation = new Conversation();
        var changedProperties = new List<string>();
        conversation.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);
        
        conversation.Title = "Title 1";
        conversation.Title = "Title 2";
        conversation.CreatedAt = DateTime.Now;
        
        Assert.That(changedProperties.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(changedProperties, Does.Contain("Title"));
        Assert.That(changedProperties, Does.Contain("CreatedAt"));
    }

    [Test]
    public void Message_MultiplePropertyChanges_AllEventsRaised()
    {
        var message = new Message();
        var changedProperties = new List<string>();
        message.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);
        
        message.Content = "Content";
        message.IsUser = true;
        message.Timestamp = DateTime.Now;
        message.ImagePath = "/path";
        
        Assert.That(changedProperties, Does.Contain("Content"));
        Assert.That(changedProperties, Does.Contain("IsUser"));
        Assert.That(changedProperties, Does.Contain("IsBot")); // Should be raised when IsUser changes
        Assert.That(changedProperties, Does.Contain("HasImage")); // Should be raised when ImagePath changes
    }

    [Test]
    public void RagDocument_PropertyChanged_RaisesEvent()
    {
        var doc = new RagDocument();
        string? lastProperty = null;
        doc.PropertyChanged += (s, e) => lastProperty = e.PropertyName;
        
        doc.FileName = "test.pdf";
        
        Assert.That(lastProperty, Is.EqualTo("FileName"));
    }
}
