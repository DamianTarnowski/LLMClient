namespace WPFLLM.Services;

public interface IRateLimiter
{
    Task WaitAsync(CancellationToken cancellationToken = default);
    void RecordRequest();
    int RemainingRequests { get; }
    TimeSpan TimeUntilReset { get; }
}
