namespace WPFLLM.Models;

public class RagDocument
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RagChunk
{
    public long Id { get; set; }
    public long DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string? Embedding { get; set; }
}
