namespace LLMClient.Services;

public interface IIngestionService : IDisposable
{
    bool IsProcessing { get; }
    int QueueCount { get; }
    
    event EventHandler<IngestionProgressEventArgs>? ProgressChanged;
    event EventHandler<IngestionCompletedEventArgs>? ItemCompleted;
    event EventHandler<IngestionErrorEventArgs>? ErrorOccurred;
    
    Task EnqueueFileAsync(string filePath);
    Task EnqueueFilesAsync(IEnumerable<string> filePaths);
    void CancelAll();
    void Pause();
    void Resume();
}

/// <summary>
/// Progress event args using C# 12 primary constructor
/// </summary>
public sealed class IngestionProgressEventArgs(string fileName, string status, int currentItem, int totalItems) : EventArgs
{
    public string FileName { get; } = fileName;
    public string Status { get; } = status;
    public int CurrentItem { get; } = currentItem;
    public int TotalItems { get; } = totalItems;
    public double ProgressPercent => TotalItems > 0 ? (double)CurrentItem / TotalItems * 100 : 0;
}

/// <summary>
/// Completion event args using C# 12 primary constructor for immutability
/// </summary>
public sealed class IngestionCompletedEventArgs(
    string fileName,
    int documentId,
    int chunkCount,
    TimeSpan duration
) : EventArgs
{
    public string FileName { get; } = fileName;
    public int DocumentId { get; } = documentId;
    public int ChunkCount { get; } = chunkCount;
    public TimeSpan Duration { get; } = duration;
}

/// <summary>
/// Error event args with required properties (C# 11+)
/// </summary>
public sealed class IngestionErrorEventArgs : EventArgs
{
    public required string FileName { get; init; }
    public required string ErrorMessage { get; init; }
    public bool WillRetry { get; init; }
}
