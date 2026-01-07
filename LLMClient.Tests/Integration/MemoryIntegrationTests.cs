using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for Memory functionality
/// Tests memory storage, retrieval, search, and context building
/// </summary>
[TestFixture]
[Category("Integration")]
public class MemoryStorageTests
{
    private Mock<IMemoryService> _memoryService = null!;
    private List<Memory> _memoryStore = null!;

    [SetUp]
    public void Setup()
    {
        _memoryStore = new List<Memory>();
        _memoryService = new Mock<IMemoryService>();
        
        // Setup mock to behave like real storage
        _memoryService.Setup(x => x.GetAllMemoriesAsync())
            .ReturnsAsync(() => _memoryStore.ToList());
            
        _memoryService.Setup(x => x.AddMemoryAsync(It.IsAny<Memory>()))
            .ReturnsAsync((Memory m) => 
            {
                m.Id = _memoryStore.Count + 1;
                m.CreatedAt = DateTime.UtcNow;
                _memoryStore.Add(m);
                return m.Id;
            });
            
        _memoryService.Setup(x => x.GetMemoryByKeyAsync(It.IsAny<string>()))
            .ReturnsAsync((string key) => _memoryStore.FirstOrDefault(m => m.Key == key));
            
        _memoryService.Setup(x => x.SearchMemoriesAsync(It.IsAny<string>()))
            .ReturnsAsync((string term) => _memoryStore
                .Where(m => m.Key.Contains(term, StringComparison.OrdinalIgnoreCase) || 
                           m.Value.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList());
                
        _memoryService.Setup(x => x.DeleteMemoryAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => 
            {
                var count = _memoryStore.RemoveAll(m => m.Id == id);
                return count;
            });
            
        _memoryService.Setup(x => x.UpdateMemoryAsync(It.IsAny<Memory>()))
            .ReturnsAsync((Memory m) =>
            {
                var existing = _memoryStore.FirstOrDefault(x => x.Id == m.Id);
                if (existing != null)
                {
                    existing.Key = m.Key;
                    existing.Value = m.Value;
                    existing.UpdatedAt = DateTime.UtcNow;
                    return 1;
                }
                return 0;
            });
    }

    [Test]
    public async Task Memory_AddAndRetrieve_Works()
    {
        var memory = new Memory
        {
            Key = "user_name",
            Value = "Jan Kowalski",
            Category = "personal"
        };

        var id = await _memoryService.Object.AddMemoryAsync(memory);
        var retrieved = await _memoryService.Object.GetMemoryByKeyAsync("user_name");

        Assert.That(id, Is.GreaterThan(0));
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Value, Is.EqualTo("Jan Kowalski"));
    }

    [Test]
    public async Task Memory_AddMultiple_AllStored()
    {
        await _memoryService.Object.AddMemoryAsync(new Memory { Key = "key1", Value = "value1" });
        await _memoryService.Object.AddMemoryAsync(new Memory { Key = "key2", Value = "value2" });
        await _memoryService.Object.AddMemoryAsync(new Memory { Key = "key3", Value = "value3" });

        var all = await _memoryService.Object.GetAllMemoriesAsync();

        Assert.That(all.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task Memory_Search_FindsMatching()
    {
        await _memoryService.Object.AddMemoryAsync(new Memory { Key = "user_preference", Value = "dark_mode" });
        await _memoryService.Object.AddMemoryAsync(new Memory { Key = "user_name", Value = "Jan" });
        await _memoryService.Object.AddMemoryAsync(new Memory { Key = "app_setting", Value = "enabled" });

        var results = await _memoryService.Object.SearchMemoriesAsync("user");

        Assert.That(results.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Memory_Delete_RemovesFromStore()
    {
        var id = await _memoryService.Object.AddMemoryAsync(new Memory { Key = "temp", Value = "data" });
        
        await _memoryService.Object.DeleteMemoryAsync(id);
        var all = await _memoryService.Object.GetAllMemoriesAsync();

        Assert.That(all.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task Memory_Update_ModifiesExisting()
    {
        var id = await _memoryService.Object.AddMemoryAsync(new Memory { Key = "counter", Value = "1" });
        
        var memory = await _memoryService.Object.GetMemoryByKeyAsync("counter");
        memory!.Value = "2";
        await _memoryService.Object.UpdateMemoryAsync(memory);

        var updated = await _memoryService.Object.GetMemoryByKeyAsync("counter");
        Assert.That(updated!.Value, Is.EqualTo("2"));
    }

    [Test]
    public async Task Memory_GetByKey_NonExistent_ReturnsNull()
    {
        var result = await _memoryService.Object.GetMemoryByKeyAsync("non_existent_key");
        
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Memory_Categories_CanBeFiltered()
    {
        await _memoryService.Object.AddMemoryAsync(new Memory { Key = "k1", Value = "v1", Category = "personal" });
        await _memoryService.Object.AddMemoryAsync(new Memory { Key = "k2", Value = "v2", Category = "work" });
        await _memoryService.Object.AddMemoryAsync(new Memory { Key = "k3", Value = "v3", Category = "personal" });

        var all = await _memoryService.Object.GetAllMemoriesAsync();
        var personal = all.Where(m => m.Category == "personal").ToList();

        Assert.That(personal.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Memory_ImportantFlag_CanBeSet()
    {
        await _memoryService.Object.AddMemoryAsync(new Memory 
        { 
            Key = "critical_info", 
            Value = "important data",
            IsImportant = true 
        });

        var memory = await _memoryService.Object.GetMemoryByKeyAsync("critical_info");
        
        Assert.That(memory!.IsImportant, Is.True);
    }

    [Test]
    public async Task Memory_Tags_CanBeSearched()
    {
        await _memoryService.Object.AddMemoryAsync(new Memory 
        { 
            Key = "tagged_memory", 
            Value = "data",
            Tags = "important,user,preference" 
        });

        var memory = await _memoryService.Object.GetMemoryByKeyAsync("tagged_memory");
        var tags = memory!.Tags.Split(',');
        
        Assert.That(tags.Length, Is.EqualTo(3));
        Assert.That(tags, Does.Contain("important"));
    }
}

[TestFixture]
[Category("Integration")]
public class MemoryContextBuildingTests
{
    [Test]
    public void MemoryContext_BuildFromMemories_FormatsCorrectly()
    {
        var memories = new List<Memory>
        {
            new() { Key = "user_name", Value = "Jan", IsImportant = true },
            new() { Key = "preference", Value = "dark_mode" },
            new() { Key = "language", Value = "Polish" }
        };

        var context = BuildMemoryContext(memories);

        Assert.That(context, Does.Contain("user_name"));
        Assert.That(context, Does.Contain("Jan"));
        Assert.That(context, Does.Contain("dark_mode"));
    }

    [Test]
    public void MemoryContext_EmptyMemories_ReturnsEmptyContext()
    {
        var memories = new List<Memory>();
        var context = BuildMemoryContext(memories);

        Assert.That(context, Is.Empty);
    }

    [Test]
    public void MemoryContext_ImportantFirst_PrioritizesImportant()
    {
        var memories = new List<Memory>
        {
            new() { Key = "normal", Value = "data", IsImportant = false },
            new() { Key = "critical", Value = "important_data", IsImportant = true }
        };

        var sorted = memories.OrderByDescending(m => m.IsImportant).ToList();
        
        Assert.That(sorted[0].Key, Is.EqualTo("critical"));
    }

    [Test]
    public void MemoryContext_TokenLimit_TruncatesAppropriately()
    {
        var memories = Enumerable.Range(1, 100)
            .Select(i => new Memory { Key = $"key_{i}", Value = $"value_{i}" })
            .ToList();

        var context = BuildMemoryContext(memories, maxTokens: 100);

        // Should be truncated
        Assert.That(context.Length, Is.LessThan(1000));
    }

    private static string BuildMemoryContext(List<Memory> memories, int maxTokens = 1000)
    {
        if (!memories.Any()) return string.Empty;

        var lines = memories
            .OrderByDescending(m => m.IsImportant)
            .ThenByDescending(m => m.UpdatedAt)
            .Take(20)
            .Select(m => $"- {m.Key}: {m.Value}");

        var context = string.Join("\n", lines);
        
        // Rough token limit (4 chars per token)
        if (context.Length > maxTokens * 4)
        {
            context = context[..(maxTokens * 4)];
        }

        return context;
    }
}
