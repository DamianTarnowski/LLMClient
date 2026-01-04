namespace WPFLLM.Services;

public interface ILoggingService
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? exception = null);
    void LogDebug(string message);
    void LogApiCall(string endpoint, int statusCode, long durationMs);
    Task FlushAsync();
}
