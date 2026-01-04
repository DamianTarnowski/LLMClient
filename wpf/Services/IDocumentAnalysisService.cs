using WPFLLM.Models;

namespace WPFLLM.Services;

public interface IDocumentAnalysisService
{
    Task<DocumentAnalysisResult> AnalyzeAsync(string text, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> AnalyzeStreamingAsync(string text, CancellationToken cancellationToken = default);
}
