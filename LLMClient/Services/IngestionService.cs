using System.Threading.Channels;
using LLMClient.Models;

namespace LLMClient.Services;

public class IngestionService : IIngestionService
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

        var job = new IngestionJob(filePath, Path.GetFileName(filePath), DateTime.UtcNow);

        Interlocked.Increment(ref _queueCount);
        await _channel.Writer.WriteAsync(job, _cts.Token);

        ProgressChanged?.Invoke(this, new IngestionProgressEventArgs(
            job.FileName, "W kolejce", 0, _queueCount));
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
                ProgressChanged?.Invoke(this, new IngestionProgressEventArgs(
                    job.FileName,
                    attempts > 0 ? $"Ponowna próba ({attempts}/{maxRetries})..." : "Przetwarzanie...",
                    1,
                    _queueCount));

                var document = await _ragService.AddDocumentAsync(job.FilePath);

                sw.Stop();
                Interlocked.Decrement(ref _queueCount);

                ItemCompleted?.Invoke(this, new IngestionCompletedEventArgs(
                    job.FileName, document.Id, document.ChunkCount, sw.Elapsed));

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

    /// <summary>
    /// Internal job record using C# 12 primary constructor
    /// </summary>
    private sealed record IngestionJob(string FilePath, string FileName, DateTime EnqueuedAt);
}
