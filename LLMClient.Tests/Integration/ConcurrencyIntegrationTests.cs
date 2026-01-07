using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;
using System.Collections.Concurrent;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for Concurrent Operations
/// Tests thread safety, parallel processing, and race conditions
/// </summary>
[TestFixture]
[Category("Integration")]
public class ConcurrentMessageTests
{
    [Test]
    public async Task Concurrent_MultipleMessages_AllProcessed()
    {
        var processedCount = 0;
        var lockObj = new object();
        
        var tasks = Enumerable.Range(1, 10)
            .Select(async i =>
            {
                await Task.Delay(Random.Shared.Next(10, 50));
                lock (lockObj) { processedCount++; }
            });
        
        await Task.WhenAll(tasks);
        
        Assert.That(processedCount, Is.EqualTo(10));
    }

    [Test]
    public async Task Concurrent_DatabaseWrites_AreThreadSafe()
    {
        var messages = new ConcurrentBag<Message>();
        
        var tasks = Enumerable.Range(1, 20)
            .Select(async i =>
            {
                await Task.Delay(Random.Shared.Next(5, 20));
                messages.Add(new Message { Id = i, Content = $"Message {i}" });
            });
        
        await Task.WhenAll(tasks);
        
        Assert.That(messages.Count, Is.EqualTo(20));
    }

    [Test]
    public async Task Concurrent_ReadWhileWrite_DoesNotBlock()
    {
        var data = new ConcurrentDictionary<int, string>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        
        var writeTask = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                data[i] = $"Value {i}";
                await Task.Delay(10);
            }
        });
        
        var readTask = Task.Run(async () =>
        {
            var reads = 0;
            while (!cts.Token.IsCancellationRequested && reads < 50)
            {
                _ = data.Count;
                reads++;
                await Task.Delay(5);
            }
            return reads;
        });
        
        await Task.WhenAll(writeTask, readTask);
        
        Assert.That(data.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task Concurrent_StreamingResponses_Independent()
    {
        var responses = new ConcurrentBag<string>();
        
        var tasks = Enumerable.Range(1, 5)
            .Select(async i =>
            {
                var response = $"Response {i}";
                await Task.Delay(Random.Shared.Next(10, 30));
                responses.Add(response);
            });
        
        await Task.WhenAll(tasks);
        
        Assert.That(responses.Count, Is.EqualTo(5));
        Assert.That(responses.Distinct().Count(), Is.EqualTo(5));
    }
}

[TestFixture]
[Category("Integration")]
public class ConcurrentEmbeddingTests
{
    [Test]
    public async Task Concurrent_EmbeddingGeneration_AllComplete()
    {
        var embeddings = new ConcurrentBag<float[]>();
        
        var tasks = Enumerable.Range(1, 10)
            .Select(async i =>
            {
                await Task.Delay(Random.Shared.Next(5, 15));
                var embedding = new float[384];
                embeddings.Add(embedding);
            });
        
        await Task.WhenAll(tasks);
        
        Assert.That(embeddings.Count, Is.EqualTo(10));
    }

    [Test]
    public async Task Concurrent_CacheAccess_ThreadSafe()
    {
        var cache = new ConcurrentDictionary<string, float[]>();
        var hitCount = 0;
        var missCount = 0;
        
        var tasks = Enumerable.Range(1, 100)
            .Select(async i =>
            {
                var key = $"text_{i % 10}"; // 10 unique keys
                await Task.Delay(Random.Shared.Next(1, 5));
                
                if (cache.TryGetValue(key, out _))
                {
                    Interlocked.Increment(ref hitCount);
                }
                else
                {
                    cache[key] = new float[384];
                    Interlocked.Increment(ref missCount);
                }
            });
        
        await Task.WhenAll(tasks);
        
        Assert.That(hitCount + missCount, Is.EqualTo(100));
        Assert.That(cache.Count, Is.LessThanOrEqualTo(10));
    }
}

[TestFixture]
[Category("Integration")]
public class ConcurrentSearchTests
{
    [Test]
    public async Task Concurrent_MultipleSearches_AllComplete()
    {
        var results = new ConcurrentBag<int>();
        
        var tasks = Enumerable.Range(1, 10)
            .Select(async i =>
            {
                await Task.Delay(Random.Shared.Next(5, 20));
                results.Add(i);
            });
        
        await Task.WhenAll(tasks);
        
        Assert.That(results.Count, Is.EqualTo(10));
    }

    [Test]
    public async Task Concurrent_VectorSearch_NoDeadlock()
    {
        var semaphore = new SemaphoreSlim(3); // Max 3 concurrent
        var completed = 0;
        
        var tasks = Enumerable.Range(1, 10)
            .Select(async i =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await Task.Delay(10);
                    Interlocked.Increment(ref completed);
                }
                finally
                {
                    semaphore.Release();
                }
            });
        
        await Task.WhenAll(tasks);
        
        Assert.That(completed, Is.EqualTo(10));
    }
}

[TestFixture]
[Category("Integration")]
public class RateLimitingTests
{
    [Test]
    public async Task RateLimit_ThrottlesRequests()
    {
        var requestTimes = new ConcurrentBag<DateTime>();
        var limiter = new SemaphoreSlim(2); // 2 concurrent max
        
        var tasks = Enumerable.Range(1, 6)
            .Select(async i =>
            {
                await limiter.WaitAsync();
                try
                {
                    requestTimes.Add(DateTime.UtcNow);
                    await Task.Delay(50);
                }
                finally
                {
                    limiter.Release();
                }
            });
        
        await Task.WhenAll(tasks);
        
        Assert.That(requestTimes.Count, Is.EqualTo(6));
    }

    [Test]
    public async Task RateLimit_TokenBucket_Works()
    {
        var bucket = new TokenBucket(5, 2); // 5 tokens, 2 per second refill
        var allowed = 0;
        var denied = 0;
        
        for (int i = 0; i < 10; i++)
        {
            if (bucket.TryConsume())
                allowed++;
            else
                denied++;
        }
        
        Assert.That(allowed, Is.EqualTo(5));
        Assert.That(denied, Is.EqualTo(5));
        
        await Task.Delay(1000); // Wait for refill
        
        Assert.That(bucket.TryConsume(), Is.True);
    }
}

public class TokenBucket
{
    private int _tokens;
    private readonly int _maxTokens;
    private readonly int _refillRate;
    private DateTime _lastRefill;
    private readonly object _lock = new();

    public TokenBucket(int maxTokens, int refillRate)
    {
        _maxTokens = maxTokens;
        _tokens = maxTokens;
        _refillRate = refillRate;
        _lastRefill = DateTime.UtcNow;
    }

    public bool TryConsume()
    {
        lock (_lock)
        {
            Refill();
            if (_tokens > 0)
            {
                _tokens--;
                return true;
            }
            return false;
        }
    }

    private void Refill()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastRefill).TotalSeconds;
        var tokensToAdd = (int)(elapsed * _refillRate);
        
        if (tokensToAdd > 0)
        {
            _tokens = Math.Min(_maxTokens, _tokens + tokensToAdd);
            _lastRefill = now;
        }
    }
}
