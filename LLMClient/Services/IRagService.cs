using LLMClient.Models;

namespace LLMClient.Services;

public enum RetrievalMode
{
    Vector,
    Keyword,
    Hybrid
}

public interface IRagService
{
    Task<RagDocument> AddDocumentAsync(string filePath);
    Task<RagDocument> AddDocumentFromContentAsync(string fileName, string content);
    Task<List<RagDocument>> GetDocumentsAsync();
    Task DeleteDocumentAsync(int documentId);
    Task<string> GetRelevantContextAsync(string query, int topK = 3, float minSimilarity = 0.5f, RetrievalMode mode = RetrievalMode.Hybrid);
    Task GenerateEmbeddingsAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
    Task<int> GetPendingChunksCountAsync();
}
