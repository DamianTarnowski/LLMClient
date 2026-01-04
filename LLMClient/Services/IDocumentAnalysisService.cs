using LLMClient.Models;

namespace LLMClient.Services;

public interface IDocumentAnalysisService
{
    Task<DocumentAnalysisResult> AnalyzeAsync(string text, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> AnalyzeStreamingAsync(string text, CancellationToken cancellationToken = default);
}
