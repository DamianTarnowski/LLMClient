using System.Threading.Channels;
using System.IO;
using WPFLLM.Models;

namespace WPFLLM.Services;

public class IngestionService : IIngestionService, IDisposable
{
    private readonly IRagService _ragService;
    private readonly Channel<IngestionJob> _channel;
    private readonly CancellationTokenSource _cts;
    private readonly Task _processingTask;
    private readonly SemaphoreSlim _pauseSemaphore;
    
    private int _queueCount;
    private bool _isPaused;
    private bool _disposed;

    public bool IsProcessing => _queueCount > 0;
    public int QueueCount => _queueCount;

    public event EventHandler<IngestionProgressEventArgs>? ProgressChanged;
    public event EventHandler<IngestionCompletedEventArgs>? ItemCompleted;
    public event EventHandler<IngestionErrorEventArgs>? ErrorOccurred;

    public IngestionService(IRagService ragService)
    {
        _ragService = ragService;
        _cts = new CancellationTokenSource();
        _pauseSemaphore = new SemaphoreSlim(1, 1);
        
        // Bounded channel with backpressure
        _channel = Channel.CreateBounded<IngestionJob>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        _processingTask = Task.Run(ProcessQueueAsync);
    }

    public async Task EnqueueFileAsync(string filePath)
    {
        if (_disposed) return;
        
        var job = new IngestionJob
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            EnqueuedAt = DateTime.UtcNow
        };

        Interlocked.Increment(ref _queueCount);
        await _channel.Writer.WriteAsync(job, _cts.Token);
        
        ProgressChanged?.Invoke(this, new IngestionProgressEventArgs
        {
            FileName = job.FileName,
            Status = "Queued",
            CurrentItem = 0,
            TotalItems = _queueCount
        });
    }

    public async Task EnqueueFilesAsync(IEnumerable<string> filePaths)
    {
        foreach (var path in filePaths)
        {
            await EnqueueFileAsync(path);
        }
    }

    public void CancelAll()
    {
        _cts.Cancel();
    }

    public void Pause()
    {
        if (!_isPaused)
        {
            _isPaused = true;
            _pauseSemaphore.Wait();
        }
    }

    public void Resume()
    {
        if (_isPaused)
        {
            _isPaused = false;
            _pauseSemaphore.Release();
        }
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var job in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                // Check for pause
                await _pauseSemaphore.WaitAsync(_cts.Token);
                _pauseSemaphore.Release();

                await ProcessJobWithRetryAsync(job);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
    }

    private async Task ProcessJobWithRetryAsync(IngestionJob job, int maxRetries = 2)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var attempts = 0;

        while (attempts <= maxRetries)
        {
            try
            {
                ProgressChanged?.Invoke(this, new IngestionProgressEventArgs
                {
                    FileName = job.FileName,
                    Status = attempts > 0 ? $"Retrying ({attempts}/{maxRetries})..." : "Processing...",
                    CurrentItem = 1,
                    TotalItems = _queueCount
                });

                var document = await _ragService.AddDocumentAsync(job.FilePath);
                
                sw.Stop();
                Interlocked.Decrement(ref _queueCount);

                ItemCompleted?.Invoke(this, new IngestionCompletedEventArgs
                {
                    FileName = job.FileName,
                    DocumentId = document.Id,
                    ChunkCount = 0, // Could be enhanced to return chunk count
                    Duration = sw.Elapsed
                });

                return;
            }
            catch (Exception ex) when (attempts < maxRetries)
            {
                attempts++;
                
                ErrorOccurred?.Invoke(this, new IngestionErrorEventArgs
                {
                    FileName = job.FileName,
                    ErrorMessage = ex.Message,
                    WillRetry = true
                });

                await Task.Delay(1000 * attempts, _cts.Token); // Exponential backoff
            }
            catch (Exception ex)
            {
                Interlocked.Decrement(ref _queueCount);
                
                ErrorOccurred?.Invoke(this, new IngestionErrorEventArgs
                {
                    FileName = job.FileName,
                    ErrorMessage = ex.Message,
                    WillRetry = false
                });

                return;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _channel.Writer.Complete();
        _cts.Cancel();
        _cts.Dispose();
        _pauseSemaphore.Dispose();
        
        GC.SuppressFinalize(this);
    }

    private class IngestionJob
    {
        public string FilePath { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public DateTime EnqueuedAt { get; init; }
    }
}
