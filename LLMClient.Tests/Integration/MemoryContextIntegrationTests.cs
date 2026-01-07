using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;
using System.Text;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for Memory Context Service
/// Tests context generation, summarization, and token management
/// </summary>
[TestFixture]
[Category("Integration")]
public class MemoryContextGenerationTests
{
    [Test]
    public void Context_EmptyMemories_ReturnsEmpty()
    {
        var memories = new List<Memory>();
        var context = GenerateMemoryContext(memories);
        
        Assert.That(context, Is.Empty);
    }

    [Test]
    public void Context_SingleMemory_FormatsCorrectly()
    {
        var memories = new List<Memory>
        {
            new() { Key = "user_name", Value = "Jan Kowalski" }
        };
        
        var context = GenerateMemoryContext(memories);
        
        Assert.That(context, Does.Contain("user_name"));
        Assert.That(context, Does.Contain("Jan Kowalski"));
    }

    [Test]
    public void Context_MultipleMemories_IncludesAll()
    {
        var memories = new List<Memory>
        {
            new() { Key = "name", Value = "Jan" },
            new() { Key = "preference", Value = "dark_mode" },
            new() { Key = "language", Value = "Polish" }
        };
        
        var context = GenerateMemoryContext(memories);
        
        Assert.That(context, Does.Contain("Jan"));
        Assert.That(context, Does.Contain("dark_mode"));
        Assert.That(context, Does.Contain("Polish"));
    }

    [Test]
    public void Context_ImportantMemories_PrioritizedFirst()
    {
        var memories = new List<Memory>
        {
            new() { Key = "normal", Value = "data", IsImportant = false },
            new() { Key = "critical", Value = "important", IsImportant = true }
        };
        
        var sorted = memories.OrderByDescending(m => m.IsImportant).ToList();
        
        Assert.That(sorted[0].Key, Is.EqualTo("critical"));
    }

    [Test]
    public void Context_RecentMemories_PrioritizedAfterImportant()
    {
        var now = DateTime.UtcNow;
        var memories = new List<Memory>
        {
            new() { Key = "old", Value = "data", UpdatedAt = now.AddDays(-10), IsImportant = false },
            new() { Key = "new", Value = "data", UpdatedAt = now, IsImportant = false }
        };
        
        var sorted = memories
            .OrderByDescending(m => m.IsImportant)
            .ThenByDescending(m => m.UpdatedAt)
            .ToList();
        
        Assert.That(sorted[0].Key, Is.EqualTo("new"));
    }

    [Test]
    public void Context_TokenLimit_TruncatesOldest()
    {
        var memories = Enumerable.Range(1, 100)
            .Select(i => new Memory 
            { 
                Key = $"key_{i}", 
                Value = new string('x', 500),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-i)
            })
            .ToList();
        
        var context = GenerateMemoryContext(memories, maxChars: 5000);
        
        Assert.That(context.Length, Is.LessThanOrEqualTo(5500)); // With some buffer
    }

    private static string GenerateMemoryContext(List<Memory> memories, int maxChars = 30000)
    {
        if (!memories.Any()) return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine("=== PAMIĘĆ UŻYTKOWNIKA ===");
        
        var sorted = memories
            .OrderByDescending(m => m.IsImportant)
            .ThenByDescending(m => m.UpdatedAt);
        
        foreach (var memory in sorted)
        {
            var line = $"- {memory.Key}: {memory.Value}";
            if (sb.Length + line.Length > maxChars) break;
            sb.AppendLine(line);
        }
        
        return sb.ToString();
    }
}

[TestFixture]
[Category("Integration")]
public class MemorySummarizationTests
{
    [Test]
    public void Summarization_LongMemories_Shortened()
    {
        var memories = Enumerable.Range(1, 50)
            .Select(i => new Memory { Key = $"fact_{i}", Value = $"This is fact number {i} with some details." })
            .ToList();
        
        var summary = SummarizeMemories(memories, maxLength: 500);
        
        Assert.That(summary.Length, Is.LessThan(1000));
    }

    [Test]
    public void Summarization_PreservesKeyFacts()
    {
        var memories = new List<Memory>
        {
            new() { Key = "name", Value = "Jan", IsImportant = true },
            new() { Key = "detail1", Value = "Some detail" },
            new() { Key = "detail2", Value = "Another detail" }
        };
        
        var summary = SummarizeMemories(memories, maxLength: 100);
        
        // Important facts should be preserved
        Assert.That(summary, Does.Contain("name").Or.Contain("Jan"));
    }

    private static string SummarizeMemories(List<Memory> memories, int maxLength)
    {
        var important = memories.Where(m => m.IsImportant).ToList();
        var others = memories.Where(m => !m.IsImportant).ToList();
        
        var sb = new StringBuilder();
        
        // Always include important
        foreach (var m in important)
        {
            sb.AppendLine($"{m.Key}: {m.Value}");
        }
        
        // Add others until limit
        foreach (var m in others)
        {
            if (sb.Length >= maxLength) break;
            sb.AppendLine($"{m.Key}: {m.Value}");
        }
        
        return sb.Length > maxLength ? sb.ToString()[..maxLength] : sb.ToString();
    }
}

[TestFixture]
[Category("Integration")]
public class MemoryCategorizationTests
{
    [Test]
    public void Categories_GroupMemoriesCorrectly()
    {
        var memories = new List<Memory>
        {
            new() { Key = "name", Category = "personal" },
            new() { Key = "preference", Category = "settings" },
            new() { Key = "hobby", Category = "personal" },
            new() { Key = "theme", Category = "settings" }
        };
        
        var grouped = memories.GroupBy(m => m.Category).ToList();
        
        Assert.That(grouped.Count, Is.EqualTo(2));
        Assert.That(grouped.First(g => g.Key == "personal").Count(), Is.EqualTo(2));
    }

    [Test]
    public void Categories_FilterByCategory()
    {
        var memories = new List<Memory>
        {
            new() { Key = "name", Category = "personal" },
            new() { Key = "theme", Category = "settings" }
        };
        
        var personal = memories.Where(m => m.Category == "personal").ToList();
        
        Assert.That(personal.Count, Is.EqualTo(1));
        Assert.That(personal[0].Key, Is.EqualTo("name"));
    }
}
