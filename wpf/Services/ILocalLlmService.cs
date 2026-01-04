namespace WPFLLM.Services;

public interface ILocalLlmService
{
    Task<bool> IsAvailableAsync();
    Task InitializeAsync(string modelId);
    IAsyncEnumerable<string> GenerateStreamAsync(string prompt, string? systemPrompt = null, CancellationToken cancellationToken = default);
    Task<bool> IsModelDownloadedAsync(string modelId);
    Task DownloadModelAsync(string modelId, IProgress<(long downloaded, long total, string status)>? progress = null, CancellationToken cancellationToken = default);
    void Dispose();
}
