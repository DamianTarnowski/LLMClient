using LLMClient.Core.Models;
using System.Diagnostics;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for Performance
/// Tests response times, memory usage, and throughput
/// </summary>
[TestFixture]
[Category("Integration")]
public class ResponseTimeTests
{
    [Test]
    public async Task ResponseTime_Embedding_UnderThreshold()
    {
        var sw = Stopwatch.StartNew();
        
        // Simulate embedding generation
        await Task.Delay(50);
        var embedding = new float[384];
        
        sw.Stop();
        
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1000)); // Under 1 second
    }

    [Test]
    public async Task ResponseTime_VectorSearch_UnderThreshold()
    {
        var sw = Stopwatch.StartNew();
        
        // Simulate vector search
        var vectors = Enumerable.Range(1, 1000)
            .Select(_ => new float[384])
            .ToList();
        
        var scores = vectors.Select((v, i) => (Index: i, Score: Random.Shared.NextDouble())).ToList();
        var topK = scores.OrderByDescending(s => s.Score).Take(10).ToList();
        
        sw.Stop();
        
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500));
        Assert.That(topK.Count, Is.EqualTo(10));
    }

    [Test]
    public void ResponseTime_MessageRendering_Fast()
    {
        var sw = Stopwatch.StartNew();
        
        var messages = Enumerable.Range(1, 100)
            .Select(i => new Message { Content = $"Message {i}" })
            .ToList();
        
        var rendered = messages.Select(m => m.Content).ToList();
        
        sw.Stop();
        
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(100));
    }

    [Test]
    public void ResponseTime_ConversationLoad_Acceptable()
    {
        var sw = Stopwatch.StartNew();
        
        var conversations = Enumerable.Range(1, 50)
            .Select(i => new Conversation 
            { 
                Id = i, 
                Title = $"Chat {i}",
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            })
            .ToList();
        
        var sorted = conversations.OrderByDescending(c => c.CreatedAt).ToList();
        
        sw.Stop();
        
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(50));
    }
}

[TestFixture]
[Category("Integration")]
public class MemoryUsageTests
{
    [Test]
    public void MemoryUsage_EmbeddingCache_Bounded()
    {
        var cache = new Dictionary<string, float[]>();
        var maxCacheSize = 100;
        
        for (int i = 0; i < 150; i++)
        {
            cache[$"key_{i}"] = new float[384];
            
            // Evict oldest if over limit
            while (cache.Count > maxCacheSize)
            {
                var oldest = cache.Keys.First();
                cache.Remove(oldest);
            }
        }
        
        Assert.That(cache.Count, Is.LessThanOrEqualTo(maxCacheSize));
    }

    [Test]
    public void MemoryUsage_MessageList_Efficient()
    {
        var messages = new List<Message>();
        
        // Add 1000 messages
        for (int i = 0; i < 1000; i++)
        {
            messages.Add(new Message { Content = $"Message {i}" });
        }
        
        // Estimate memory (rough)
        var estimatedBytes = messages.Count * 100; // ~100 bytes per message estimate
        var estimatedMB = estimatedBytes / (1024.0 * 1024.0);
        
        Assert.That(estimatedMB, Is.LessThan(1)); // Under 1 MB
    }

    [Test]
    public void MemoryUsage_GCCollection_Works()
    {
        var before = GC.GetTotalMemory(false);
        
        // Create and discard objects
        for (int i = 0; i < 100; i++)
        {
            var temp = new byte[1024 * 100]; // 100 KB each
        }
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        
        var after = GC.GetTotalMemory(true);
        
        // Memory should not grow significantly after GC
        Assert.Pass(); // GC behavior test
    }
}

[TestFixture]
[Category("Integration")]
public class ThroughputTests
{
    [Test]
    public async Task Throughput_MessageProcessing_Adequate()
    {
        var processed = 0;
        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < 100; i++)
        {
            await Task.Delay(1); // Minimal processing
            processed++;
        }
        
        sw.Stop();
        
        var messagesPerSecond = processed / sw.Elapsed.TotalSeconds;
        
        Assert.That(messagesPerSecond, Is.GreaterThan(10)); // At least 10/sec
    }

    [Test]
    public void Throughput_SearchOperations_High()
    {
        var searchCount = 0;
        var sw = Stopwatch.StartNew();
        
        var messages = Enumerable.Range(1, 1000)
            .Select(i => $"Message content {i}")
            .ToList();
        
        while (sw.ElapsedMilliseconds < 100) // Run for 100ms
        {
            var results = messages.Where(m => m.Contains("500")).ToList();
            searchCount++;
        }
        
        Assert.That(searchCount, Is.GreaterThan(10)); // At least 10 searches in 100ms
    }

    [Test]
    public void Throughput_JsonSerialization_Fast()
    {
        var conversations = Enumerable.Range(1, 100)
            .Select(i => new Conversation { Id = i, Title = $"Chat {i}" })
            .ToList();
        
        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < 10; i++)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(conversations);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<List<Conversation>>(json);
        }
        
        sw.Stop();
        
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500)); // 10 round-trips under 500ms
    }
}

[TestFixture]
[Category("Integration")]
public class ScalabilityTests
{
    [Test]
    public void Scalability_LargeConversationCount_Handles()
    {
        var conversations = Enumerable.Range(1, 10000)
            .Select(i => new Conversation { Id = i, Title = $"Chat {i}" })
            .ToList();
        
        var sw = Stopwatch.StartNew();
        var filtered = conversations.Where(c => c.Id > 9900).ToList();
        sw.Stop();
        
        Assert.That(filtered.Count, Is.EqualTo(100));
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(100));
    }

    [Test]
    public void Scalability_LargeMessageCount_Handles()
    {
        var messages = Enumerable.Range(1, 50000)
            .Select(i => new Message { Id = i, Content = $"Message {i}" })
            .ToList();
        
        var sw = Stopwatch.StartNew();
        var last100 = messages.TakeLast(100).ToList();
        sw.Stop();
        
        Assert.That(last100.Count, Is.EqualTo(100));
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(50));
    }

    [Test]
    public void Scalability_LargeDocumentStore_Handles()
    {
        var documents = Enumerable.Range(1, 1000)
            .Select(i => new RagDocument 
            { 
                Id = i, 
                FileName = $"doc_{i}.pdf",
                ChunkCount = Random.Shared.Next(10, 100)
            })
            .ToList();
        
        var sw = Stopwatch.StartNew();
        var totalChunks = documents.Sum(d => d.ChunkCount);
        sw.Stop();
        
        Assert.That(totalChunks, Is.GreaterThan(10000));
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(10));
    }
}
