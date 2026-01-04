namespace WPFLLM.Services;

public interface IIngestionService
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

public class IngestionProgressEventArgs : EventArgs
{
    public string FileName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int CurrentItem { get; init; }
    public int TotalItems { get; init; }
    public double ProgressPercent => TotalItems > 0 ? (double)CurrentItem / TotalItems * 100 : 0;
}

public class IngestionCompletedEventArgs : EventArgs
{
    public string FileName { get; init; } = string.Empty;
    public long DocumentId { get; init; }
    public int ChunkCount { get; init; }
    public TimeSpan Duration { get; init; }
}

public class IngestionErrorEventArgs : EventArgs
{
    public string FileName { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public bool WillRetry { get; init; }
}
