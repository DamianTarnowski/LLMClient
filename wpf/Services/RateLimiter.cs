using System.Collections.Concurrent;

namespace WPFLLM.Services;

/// <summary>
/// Token bucket rate limiter with sliding window.
/// Default: 60 requests per minute (OpenRouter free tier).
/// </summary>
public class RateLimiter : IRateLimiter
{
    private readonly int _maxRequests;
    private readonly TimeSpan _window;
    private readonly ConcurrentQueue<DateTime> _requestTimestamps = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public RateLimiter(int maxRequestsPerMinute = 60)
    {
        _maxRequests = maxRequestsPerMinute;
        _window = TimeSpan.FromMinutes(1);
    }

    public int RemainingRequests
    {
        get
        {
            CleanupOldTimestamps();
            return Math.Max(0, _maxRequests - _requestTimestamps.Count);
        }
    }

    public TimeSpan TimeUntilReset
    {
        get
        {
            if (_requestTimestamps.TryPeek(out var oldest))
            {
                var resetTime = oldest.Add(_window);
                var remaining = resetTime - DateTime.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            return TimeSpan.Zero;
        }
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            CleanupOldTimestamps();

            while (_requestTimestamps.Count >= _maxRequests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Wait until oldest request expires
                if (_requestTimestamps.TryPeek(out var oldest))
                {
                    var waitTime = oldest.Add(_window) - DateTime.UtcNow;
                    if (waitTime > TimeSpan.Zero)
                    {
                        await Task.Delay(waitTime + TimeSpan.FromMilliseconds(100), cancellationToken);
                    }
                }
                
                CleanupOldTimestamps();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void RecordRequest()
    {
        _requestTimestamps.Enqueue(DateTime.UtcNow);
    }

    private void CleanupOldTimestamps()
    {
        var cutoff = DateTime.UtcNow - _window;
        while (_requestTimestamps.TryPeek(out var oldest) && oldest < cutoff)
        {
            _requestTimestamps.TryDequeue(out _);
        }
    }
}
