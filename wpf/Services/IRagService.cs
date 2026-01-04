using WPFLLM.Models;

namespace WPFLLM.Services;

public interface IRagService
{
    Task<RagDocument> AddDocumentAsync(string filePath);
    Task<List<RagDocument>> GetDocumentsAsync();
    Task DeleteDocumentAsync(long documentId);
    Task<string> GetRelevantContextAsync(string query, int topK = 3, double minSimilarity = 0.75);
    Task GenerateEmbeddingsAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Advanced retrieval with debug metrics and hybrid search support
    /// </summary>
    /// <param name="hybridBalance">0 = only keyword, 1 = only vector, 0.5 = balanced</param>
    Task<RetrievalResult> RetrieveAsync(string query, int topK = 5, double minSimilarity = 0.7, 
        RetrievalMode mode = RetrievalMode.Hybrid, double rrfK = 60.0, double hybridBalance = 0.5);
    
    /// <summary>
    /// Advanced retrieval with full RagTrace for debug panel (flight recorder)
    /// </summary>
    /// <param name="hybridBalance">0 = only keyword, 1 = only vector, 0.5 = balanced</param>
    Task<(RetrievalResult Result, RagTrace Trace)> RetrieveWithTraceAsync(
        string query, 
        int topK = 5, 
        double minSimilarity = 0.7, 
        RetrievalMode mode = RetrievalMode.Hybrid,
        double rrfK = 60.0,
        double hybridBalance = 0.5,
        CancellationToken cancellationToken = default);
}
