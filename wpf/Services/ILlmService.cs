using WPFLLM.Models;

namespace WPFLLM.Services;

public interface ILlmService
{
    IAsyncEnumerable<string> StreamChatAsync(List<ChatMessage> messages, string? ragContext = null, CancellationToken cancellationToken = default);
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<float[]> GetEmbeddingAsync(string text, bool isQuery, CancellationToken cancellationToken = default);
}
