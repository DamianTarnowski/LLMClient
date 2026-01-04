namespace WPFLLM.Models;

/// <summary>
/// Flight recorder for RAG pipeline - captures all debug information for a single query
/// </summary>
public sealed class RagTrace
{
    public string Query { get; init; } = "";
    public DateTime Utc { get; init; } = DateTime.UtcNow;
    
    /// <summary>All candidate chunks considered (not just selected)</summary>
    public List<RagChunkCandidate> Candidates { get; } = [];
    
    /// <summary>Timing measurements for each pipeline step</summary>
    public List<RagTiming> Timings { get; } = [];
    
    /// <summary>Token breakdown for the prompt</summary>
    public RagTokenBreakdown? Tokens { get; set; }
    
    /// <summary>The full prompt sent to the model (optional, for debug)</summary>
    public string? PromptPreview { get; set; }
    
    /// <summary>Model used for generation</summary>
    public string? Model { get; set; }
    
    /// <summary>API provider (OpenAI, OpenRouter, Local, etc.)</summary>
    public string? Provider { get; set; }
    
    /// <summary>Time to receive first token (streaming latency)</summary>
    public long? TimeToFirstTokenMs { get; set; }
    
    /// <summary>Tokens per second during generation</summary>
    public double? TokensPerSecond { get; set; }
    
    /// <summary>Retrieval mode used</summary>
    public RetrievalMode RetrievalMode { get; set; } = RetrievalMode.Hybrid;
    
    /// <summary>Fusion formula description</summary>
    public string FusionFormula { get; set; } = "RRF(k=60)";
    
    /// <summary>Total pipeline time</summary>
    public long TotalTimeMs => Timings.Sum(t => t.ElapsedMs);
    
    /// <summary>Number of chunks included in context</summary>
    public int IncludedChunks => Candidates.Count(c => c.Included);
    
    /// <summary>Number of total candidates evaluated</summary>
    public int TotalCandidates => Candidates.Count;
}

/// <summary>
/// Single candidate chunk with full scoring information
/// </summary>
public sealed record RagChunkCandidate(
    long ChunkId,
    string SourceName,
    string? Section,
    int? ChunkIndex,
    float VectorScore,
    float KeywordScore,
    float FinalScore,
    int TokenCount,
    bool Included,
    string Preview,
    List<string>? MatchedTerms = null
)
{
    /// <summary>Rank in the final sorted list (1-based)</summary>
    public int Rank { get; set; }
}

/// <summary>
/// Timing measurement for a single pipeline step
/// </summary>
public sealed record RagTiming(
    string Name,
    long ElapsedMs
)
{
    public override string ToString() => $"{Name}: {ElapsedMs}ms";
}

/// <summary>
/// Token breakdown for prompt analysis
/// </summary>
public sealed record RagTokenBreakdown
{
    public int SystemTokens { get; init; }
    public int UserTokens { get; init; }
    public int ContextTokens { get; init; }
    public int HistoryTokens { get; init; }
    public int TotalPromptTokens { get; init; }
    public int CompletionTokens { get; set; }
    
    /// <summary>Context budget limit</summary>
    public int ContextBudget { get; init; } = 3000;
    
    /// <summary>Whether token counts are approximate</summary>
    public bool IsApproximate { get; init; }
    
    /// <summary>Context usage percentage</summary>
    public double ContextUsagePercent => ContextBudget > 0 
        ? Math.Round(100.0 * ContextTokens / ContextBudget, 1) 
        : 0;
    
    /// <summary>Context share of total prompt</summary>
    public double ContextSharePercent => TotalPromptTokens > 0 
        ? Math.Round(100.0 * ContextTokens / TotalPromptTokens, 1) 
        : 0;
}
