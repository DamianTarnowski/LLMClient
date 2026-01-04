namespace WPFLLM.Models;

/// <summary>
/// Result from RAG retrieval with debug metrics
/// </summary>
public class RetrievalResult
{
    public List<RetrievedChunk> Chunks { get; set; } = [];
    public RetrievalMetrics Metrics { get; set; } = new();
    public string CombinedContext => string.Join("\n\n---\n\n", Chunks.Select(c => c.Content));
}

/// <summary>
/// Single retrieved chunk with scoring information
/// </summary>
public class RetrievedChunk
{
    public long ChunkId { get; set; }
    public long DocumentId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    
    // Scoring
    public double VectorScore { get; set; }
    public double KeywordScore { get; set; }
    public double FusedScore { get; set; }
    
    // For highlighting
    public List<string> MatchedTerms { get; set; } = [];
}

/// <summary>
/// Metrics for RAG debug panel
/// </summary>
public class RetrievalMetrics
{
    public RetrievalMode Mode { get; set; } = RetrievalMode.Hybrid;
    public int TopK { get; set; }
    public double MinSimilarity { get; set; }
    
    // Timing
    public long RetrievalTimeMs { get; set; }
    public long EmbeddingTimeMs { get; set; }
    public long TotalTimeMs { get; set; }
    
    // Counts
    public int TotalChunksSearched { get; set; }
    public int VectorMatches { get; set; }
    public int KeywordMatches { get; set; }
    public int FinalResults { get; set; }
}

public enum RetrievalMode
{
    Vector,
    Keyword,
    Hybrid
}
