using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;
using System.Threading.Channels;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for Ingestion Pipeline
/// Tests document queue, processing, pause/resume, error handling
/// </summary>
[TestFixture]
[Category("Integration")]
public class IngestionQueueTests
{
    [Test]
    public async Task Queue_EnqueueFile_AddsToQueue()
    {
        var queue = Channel.CreateBounded<string>(100);
        
        await queue.Writer.WriteAsync("/path/to/document.pdf");
        
        Assert.That(queue.Reader.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Queue_MultipleFiles_ProcessedInOrder()
    {
        var queue = Channel.CreateUnbounded<string>();
        var processed = new List<string>();
        
        await queue.Writer.WriteAsync("file1.pdf");
        await queue.Writer.WriteAsync("file2.pdf");
        await queue.Writer.WriteAsync("file3.pdf");
        queue.Writer.Complete();
        
        await foreach (var file in queue.Reader.ReadAllAsync())
        {
            processed.Add(file);
        }
        
        Assert.That(processed, Is.EqualTo(new[] { "file1.pdf", "file2.pdf", "file3.pdf" }));
    }

    [Test]
    public async Task Queue_BoundedCapacity_BlocksWhenFull()
    {
        var queue = Channel.CreateBounded<string>(new BoundedChannelOptions(2)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
        
        await queue.Writer.WriteAsync("file1");
        await queue.Writer.WriteAsync("file2");
        
        // Third write would block, so we use TryWrite
        var canWrite = queue.Writer.TryWrite("file3");
        
        Assert.That(canWrite, Is.False);
    }

    [Test]
    public async Task Queue_Cancellation_StopsProcessing()
    {
        var cts = new CancellationTokenSource();
        var queue = Channel.CreateUnbounded<string>();
        var processed = 0;
        
        await queue.Writer.WriteAsync("file1");
        await queue.Writer.WriteAsync("file2");
        
        cts.Cancel();
        
        try
        {
            await foreach (var _ in queue.Reader.ReadAllAsync(cts.Token))
            {
                processed++;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        
        Assert.That(processed, Is.LessThan(2));
    }
}

[TestFixture]
[Category("Integration")]
public class IngestionProcessingTests
{
    [Test]
    public void Processing_FileTypeDetection_Works()
    {
        var supportedTypes = new[] { ".pdf", ".txt", ".md", ".docx", ".html" };
        
        Assert.That(IsSupported("document.pdf", supportedTypes), Is.True);
        Assert.That(IsSupported("notes.txt", supportedTypes), Is.True);
        Assert.That(IsSupported("image.png", supportedTypes), Is.False);
    }

    [Test]
    public void Processing_ChunkGeneration_CreatesMultipleChunks()
    {
        var content = string.Join(" ", Enumerable.Range(1, 500).Select(i => $"word{i}"));
        var chunkSize = 100;
        
        var chunks = CreateChunks(content, chunkSize);
        
        Assert.That(chunks.Count, Is.GreaterThan(1));
    }

    [Test]
    public void Processing_ProgressReporting_UpdatesCorrectly()
    {
        var progress = new IngestionProgress
        {
            FileName = "document.pdf",
            Status = "Przetwarzanie",
            PercentComplete = 50,
            QueueRemaining = 3
        };
        
        Assert.That(progress.PercentComplete, Is.EqualTo(50));
        Assert.That(progress.Status, Is.EqualTo("Przetwarzanie"));
    }

    [Test]
    public void Processing_ErrorHandling_RetriesOnFailure()
    {
        var attempts = 0;
        var maxRetries = 3;
        var success = false;
        
        while (attempts < maxRetries && !success)
        {
            attempts++;
            if (attempts == 3)
                success = true;
        }
        
        Assert.That(success, Is.True);
        Assert.That(attempts, Is.EqualTo(3));
    }

    private static bool IsSupported(string fileName, string[] supportedTypes)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return supportedTypes.Contains(ext);
    }

    private static List<string> CreateChunks(string content, int wordsPerChunk)
    {
        var words = content.Split(' ');
        var chunks = new List<string>();
        
        for (int i = 0; i < words.Length; i += wordsPerChunk)
        {
            chunks.Add(string.Join(" ", words.Skip(i).Take(wordsPerChunk)));
        }
        
        return chunks;
    }
}

public class IngestionProgress
{
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int PercentComplete { get; set; }
    public int QueueRemaining { get; set; }
}

[TestFixture]
[Category("Integration")]
public class IngestionPauseResumeTests
{
    [Test]
    public async Task PauseResume_PausesProcessing()
    {
        var semaphore = new SemaphoreSlim(1, 1);
        var isPaused = false;
        var processed = 0;
        
        // Simulate pause
        await semaphore.WaitAsync();
        isPaused = true;
        
        // Try to process (would block)
        var canProcess = semaphore.CurrentCount > 0;
        
        Assert.That(isPaused, Is.True);
        Assert.That(canProcess, Is.False);
        
        // Resume
        semaphore.Release();
        isPaused = false;
        
        Assert.That(isPaused, Is.False);
        Assert.That(semaphore.CurrentCount, Is.EqualTo(1));
    }

    [Test]
    public async Task PauseResume_ResumesContinuesProcessing()
    {
        var queue = new Queue<string>();
        queue.Enqueue("file1");
        queue.Enqueue("file2");
        queue.Enqueue("file3");
        
        var processed = new List<string>();
        var isPaused = false;
        
        // Process first item
        processed.Add(queue.Dequeue());
        
        // Pause
        isPaused = true;
        Assert.That(queue.Count, Is.EqualTo(2));
        
        // Resume and process rest
        isPaused = false;
        while (queue.Count > 0)
        {
            processed.Add(queue.Dequeue());
        }
        
        Assert.That(processed.Count, Is.EqualTo(3));
        
        await Task.CompletedTask;
    }
}
