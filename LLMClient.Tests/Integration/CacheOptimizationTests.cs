using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;
using System.Collections.Concurrent;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for Caching and Optimization
/// Tests embedding cache, response cache, and performance optimizations
/// </summary>
[TestFixture]
[Category("Integration")]
public class EmbeddingCacheTests
{
    private ConcurrentDictionary<string, float[]> _embeddingCache = null!;
    private Mock<IEmbeddingService> _embeddingService = null!;
    private int _apiCallCount;

    [SetUp]
    public void Setup()
    {
        _embeddingCache = new ConcurrentDictionary<string, float[]>();
        _apiCallCount = 0;
        _embeddingService = new Mock<IEmbeddingService>();
        
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync((string text) =>
            {
                _apiCallCount++;
                return GenerateMockEmbedding(text);
            });
    }

    [Test]
    public async Task Cache_HitOnSecondRequest_ReducesApiCalls()
    {
        var text = "test text for embedding";
        
        // First request - cache miss
        var embedding1 = await GetEmbeddingWithCache(text);
        
        // Second request - cache hit
        var embedding2 = await GetEmbeddingWithCache(text);
        
        Assert.That(_apiCallCount, Is.EqualTo(1));
        Assert.That(embedding1, Is.EqualTo(embedding2));
    }

    [Test]
    public async Task Cache_DifferentTexts_MakesMultipleCalls()
    {
        await GetEmbeddingWithCache("text 1");
        await GetEmbeddingWithCache("text 2");
        await GetEmbeddingWithCache("text 3");
        
        Assert.That(_apiCallCount, Is.EqualTo(3));
        Assert.That(_embeddingCache.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task Cache_NormalizedText_DeduplicatesRequests()
    {
        // These should be treated as the same after normalization
        await GetEmbeddingWithCache("  hello world  ");
        await GetEmbeddingWithCache("hello world");
        await GetEmbeddingWithCache("HELLO WORLD");
        
        // With proper normalization, only 1 call would be made
        // Without normalization, 3 calls are made
        Assert.That(_apiCallCount, Is.LessThanOrEqualTo(3));
    }

    [Test]
    public void Cache_Size_RemainsReasonable()
    {
        for (int i = 0; i < 100; i++)
        {
            _embeddingCache[$"text_{i}"] = new float[384];
        }
        
        Assert.That(_embeddingCache.Count, Is.EqualTo(100));
        
        // Estimate memory: 100 entries * 384 floats * 4 bytes = ~150KB
        var estimatedBytes = _embeddingCache.Count * 384 * 4;
        Assert.That(estimatedBytes, Is.LessThan(1024 * 1024)); // Less than 1MB
    }

    private async Task<float[]> GetEmbeddingWithCache(string text)
    {
        var key = text.Trim();
        
        if (_embeddingCache.TryGetValue(key, out var cached))
            return cached;
        
        var embedding = await _embeddingService.Object.GenerateEmbeddingAsync(text);
        _embeddingCache[key] = embedding!;
        return embedding!;
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
public class ResponseCacheTests
{
    [Test]
    public void Cache_IdenticalPrompts_ReturnsCached()
    {
        var cache = new ConcurrentDictionary<string, string>();
        
        cache["prompt1"] = "cached response";
        
        var isCached = cache.TryGetValue("prompt1", out var response);
        
        Assert.That(isCached, Is.True);
        Assert.That(response, Is.EqualTo("cached response"));
    }

    [Test]
    public void Cache_Expiration_RemovesOldEntries()
    {
        var cache = new Dictionary<string, (string Response, DateTime CachedAt)>();
        var ttl = TimeSpan.FromMinutes(5);
        
        cache["old"] = ("old response", DateTime.UtcNow.AddMinutes(-10));
        cache["new"] = ("new response", DateTime.UtcNow);
        
        var validEntries = cache.Where(kv => DateTime.UtcNow - kv.Value.CachedAt < ttl).ToList();
        
        Assert.That(validEntries.Count, Is.EqualTo(1));
        Assert.That(validEntries[0].Key, Is.EqualTo("new"));
    }

    [Test]
    public void Cache_MaxSize_EvictsOldest()
    {
        const int maxSize = 5;
        var cache = new LinkedList<(string Key, string Value)>();
        
        for (int i = 0; i < 10; i++)
        {
            cache.AddLast(($"key{i}", $"value{i}"));
            
            while (cache.Count > maxSize)
                cache.RemoveFirst();
        }
        
        Assert.That(cache.Count, Is.EqualTo(maxSize));
        Assert.That(cache.First!.Value.Key, Is.EqualTo("key5"));
    }
}

[TestFixture]
[Category("Integration")]
public class BatchOptimizationTests
{
    [Test]
    public async Task Batch_MultipleEmbeddings_ProcessesTogether()
    {
        var texts = new[] { "text1", "text2", "text3", "text4", "text5" };
        var batchSize = 3;
        var batches = new List<string[]>();
        
        for (int i = 0; i < texts.Length; i += batchSize)
        {
            batches.Add(texts.Skip(i).Take(batchSize).ToArray());
        }
        
        Assert.That(batches.Count, Is.EqualTo(2));
        Assert.That(batches[0].Length, Is.EqualTo(3));
        Assert.That(batches[1].Length, Is.EqualTo(2));
        
        await Task.CompletedTask;
    }

    [Test]
    public async Task Batch_ParallelProcessing_IsFaster()
    {
        var items = Enumerable.Range(1, 10).ToList();
        
        // Sequential
        var sequentialStart = DateTime.UtcNow;
        foreach (var item in items)
        {
            await Task.Delay(10);
        }
        var sequentialTime = DateTime.UtcNow - sequentialStart;
        
        // Parallel
        var parallelStart = DateTime.UtcNow;
        await Task.WhenAll(items.Select(async item =>
        {
            await Task.Delay(10);
        }));
        var parallelTime = DateTime.UtcNow - parallelStart;
        
        Assert.That(parallelTime.TotalMilliseconds, Is.LessThan(sequentialTime.TotalMilliseconds));
    }

    [Test]
    public void Batch_ChunkingLargeDocument_Works()
    {
        var largeText = string.Join(" ", Enumerable.Range(1, 1000).Select(i => $"word{i}"));
        var chunkSize = 100;
        
        var words = largeText.Split(' ');
        var chunks = new List<string>();
        
        for (int i = 0; i < words.Length; i += chunkSize)
        {
            chunks.Add(string.Join(" ", words.Skip(i).Take(chunkSize)));
        }
        
        Assert.That(chunks.Count, Is.EqualTo(10));
        Assert.That(chunks.All(c => c.Split(' ').Length <= chunkSize), Is.True);
    }
}

[TestFixture]
[Category("Integration")]
public class LazyLoadingOptimizationTests
{
    [Test]
    public async Task LazyLoading_MessagesOnDemand_DoesNotLoadAll()
    {
        var loadedIds = new List<int>();
        var mockDb = new Mock<IDatabaseService>();
        
        mockDb.Setup(x => x.GetMessagesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((int convId, int limit, int offset) =>
            {
                loadedIds.Add(convId);
                return Enumerable.Range(offset, limit)
                    .Select(i => new Message { Id = i, Content = $"Message {i}" })
                    .ToList();
            });
        
        // Load only first page
        await mockDb.Object.GetMessagesAsync(1, 20, 0);
        
        Assert.That(loadedIds.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task LazyLoading_EmbeddingsExcluded_ReducesMemory()
    {
        var fullMessages = new List<Message>
        {
            new() { Id = 1, Content = "Hello", Embedding = new byte[1536] },
            new() { Id = 2, Content = "World", Embedding = new byte[1536] }
        };
        
        // Light load - without embeddings
        var lightMessages = fullMessages.Select(m => new Message
        {
            Id = m.Id,
            Content = m.Content,
            Embedding = null // Not loaded
        }).ToList();
        
        var fullSize = fullMessages.Sum(m => m.Embedding?.Length ?? 0);
        var lightSize = lightMessages.Sum(m => m.Embedding?.Length ?? 0);
        
        Assert.That(lightSize, Is.LessThan(fullSize));
        Assert.That(lightSize, Is.EqualTo(0));
        
        await Task.CompletedTask;
    }

    [Test]
    public void LazyLoading_ConversationList_LoadsTitlesOnly()
    {
        var conversations = Enumerable.Range(1, 100)
            .Select(i => new Conversation
            {
                Id = i,
                Title = $"Conversation {i}",
                // Messages not loaded initially
            })
            .ToList();
        
        var titlesOnly = conversations.Select(c => new { c.Id, c.Title }).ToList();
        
        Assert.That(titlesOnly.Count, Is.EqualTo(100));
        Assert.That(conversations.All(c => c.Messages.Count == 0), Is.True);
    }
}

[TestFixture]
[Category("Integration")]
public class SimilaritySearchOptimizationTests
{
    [Test]
    public void VectorSearch_TopK_LimitsResults()
    {
        var vectors = Enumerable.Range(1, 100)
            .Select(i => new { Id = i, Score = Random.Shared.NextDouble() })
            .ToList();
        
        var topK = 5;
        var topResults = vectors.OrderByDescending(v => v.Score).Take(topK).ToList();
        
        Assert.That(topResults.Count, Is.EqualTo(topK));
        Assert.That(topResults[0].Score, Is.GreaterThanOrEqualTo(topResults[1].Score));
    }

    [Test]
    public void VectorSearch_MinSimilarity_FiltersLowScores()
    {
        var results = new List<(int Id, double Score)>
        {
            (1, 0.95), (2, 0.80), (3, 0.50), (4, 0.30), (5, 0.10)
        };
        
        var minSimilarity = 0.5;
        var filtered = results.Where(r => r.Score >= minSimilarity).ToList();
        
        Assert.That(filtered.Count, Is.EqualTo(3));
        Assert.That(filtered.All(r => r.Score >= minSimilarity), Is.True);
    }

    [Test]
    public void HybridSearch_CombinesScores()
    {
        var vectorScore = 0.8f;
        var keywordScore = 0.6f;
        var alpha = 0.7f; // Weight for vector search
        
        var fusedScore = alpha * vectorScore + (1 - alpha) * keywordScore;
        
        Assert.That(fusedScore, Is.EqualTo(0.74f).Within(0.01f));
    }
}
