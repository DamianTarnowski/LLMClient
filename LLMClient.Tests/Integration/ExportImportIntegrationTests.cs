using LLMClient.Core.Models;
using System.Text.Json;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for Export/Import functionality
/// Tests conversation export, data backup, and restore operations
/// </summary>
[TestFixture]
[Category("Integration")]
public class ConversationExportTests
{
    [Test]
    public void Export_SingleConversation_ToJson()
    {
        var conversation = new Conversation
        {
            Id = 1,
            Title = "Test Conversation",
            CreatedAt = DateTime.UtcNow
        };
        conversation.Messages.Add(new Message { Content = "Hello", IsUser = true });
        conversation.Messages.Add(new Message { Content = "Hi there!", IsUser = false });
        
        var json = JsonSerializer.Serialize(conversation, new JsonSerializerOptions { WriteIndented = true });
        
        Assert.That(json, Does.Contain("Test Conversation"));
        Assert.That(json, Does.Contain("Hello"));
        Assert.That(json, Does.Contain("Hi there!"));
    }

    [Test]
    public void Export_MultipleConversations_ToJson()
    {
        var conversations = new List<Conversation>
        {
            new() { Id = 1, Title = "Chat 1" },
            new() { Id = 2, Title = "Chat 2" },
            new() { Id = 3, Title = "Chat 3" }
        };
        
        var json = JsonSerializer.Serialize(conversations);
        var deserialized = JsonSerializer.Deserialize<List<Conversation>>(json);
        
        Assert.That(deserialized!.Count, Is.EqualTo(3));
    }

    [Test]
    public void Export_ToMarkdown_FormatsCorrectly()
    {
        var conversation = new Conversation { Title = "AI Discussion" };
        conversation.Messages.Add(new Message { Content = "What is AI?", IsUser = true, Timestamp = DateTime.UtcNow });
        conversation.Messages.Add(new Message { Content = "AI is artificial intelligence...", IsUser = false, Timestamp = DateTime.UtcNow });
        
        var markdown = ExportToMarkdown(conversation);
        
        Assert.That(markdown, Does.Contain("# AI Discussion"));
        Assert.That(markdown, Does.Contain("**User:**"));
        Assert.That(markdown, Does.Contain("**Assistant:**"));
    }

    [Test]
    public void Export_ToPlainText_FormatsCorrectly()
    {
        var conversation = new Conversation { Title = "Simple Chat" };
        conversation.Messages.Add(new Message { Content = "Hello", IsUser = true });
        conversation.Messages.Add(new Message { Content = "Hi!", IsUser = false });
        
        var text = ExportToPlainText(conversation);
        
        Assert.That(text, Does.Contain("Simple Chat"));
        Assert.That(text, Does.Contain("User: Hello"));
        Assert.That(text, Does.Contain("Assistant: Hi!"));
    }

    [Test]
    public void Export_WithImages_IncludesImageInfo()
    {
        var conversation = new Conversation { Title = "Image Chat" };
        conversation.Messages.Add(new Message 
        { 
            Content = "Check this image", 
            IsUser = true,
            ImagePath = "/path/to/image.png"
        });
        
        var json = JsonSerializer.Serialize(conversation);
        
        Assert.That(json, Does.Contain("ImagePath"));
        Assert.That(json, Does.Contain("image.png"));
    }

    [Test]
    public void Export_EmptyConversation_Works()
    {
        var conversation = new Conversation { Title = "Empty" };
        
        var json = JsonSerializer.Serialize(conversation);
        var deserialized = JsonSerializer.Deserialize<Conversation>(json);
        
        Assert.That(deserialized!.Title, Is.EqualTo("Empty"));
        Assert.That(deserialized.Messages.Count, Is.EqualTo(0));
    }

    private static string ExportToMarkdown(Conversation conv)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {conv.Title}");
        sb.AppendLine();
        
        foreach (var msg in conv.Messages)
        {
            var role = msg.IsUser ? "User" : "Assistant";
            sb.AppendLine($"**{role}:** {msg.Content}");
            sb.AppendLine();
        }
        
        return sb.ToString();
    }

    private static string ExportToPlainText(Conversation conv)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(conv.Title);
        sb.AppendLine(new string('-', conv.Title.Length));
        
        foreach (var msg in conv.Messages)
        {
            var role = msg.IsUser ? "User" : "Assistant";
            sb.AppendLine($"{role}: {msg.Content}");
        }
        
        return sb.ToString();
    }
}

[TestFixture]
[Category("Integration")]
public class DataBackupTests
{
    [Test]
    public void Backup_AllData_CreatesPackage()
    {
        var backup = new BackupPackage
        {
            CreatedAt = DateTime.UtcNow,
            Version = "1.0.0",
            Conversations = new List<Conversation>
            {
                new() { Id = 1, Title = "Chat 1" },
                new() { Id = 2, Title = "Chat 2" }
            },
            Memories = new List<Memory>
            {
                new() { Key = "name", Value = "Jan" }
            }
        };
        
        var json = JsonSerializer.Serialize(backup);
        
        Assert.That(json, Does.Contain("Version"));
        Assert.That(json, Does.Contain("Conversations"));
        Assert.That(json, Does.Contain("Memories"));
    }

    [Test]
    public void Backup_Restore_PreservesData()
    {
        var original = new BackupPackage
        {
            CreatedAt = DateTime.UtcNow,
            Conversations = new List<Conversation> { new() { Title = "Test" } },
            Memories = new List<Memory> { new() { Key = "k", Value = "v" } }
        };
        
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<BackupPackage>(json);
        
        Assert.That(restored!.Conversations.Count, Is.EqualTo(1));
        Assert.That(restored.Memories.Count, Is.EqualTo(1));
    }

    [Test]
    public void Backup_SizeEstimate_Calculated()
    {
        var conversations = Enumerable.Range(1, 100)
            .Select(i => new Conversation { Id = i, Title = $"Chat {i}" })
            .ToList();
        
        var json = JsonSerializer.Serialize(conversations);
        var sizeKB = json.Length / 1024.0;
        
        Assert.That(sizeKB, Is.GreaterThan(0));
    }
}

public class BackupPackage
{
    public DateTime CreatedAt { get; set; }
    public string Version { get; set; } = "1.0.0";
    public List<Conversation> Conversations { get; set; } = new();
    public List<Memory> Memories { get; set; } = new();
    public List<RagDocument> Documents { get; set; } = new();
}
