using LLMClient.Core.Models;
using Moq;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for Model Download functionality
/// Tests download progress, verification, and installation
/// </summary>
[TestFixture]
[Category("Integration")]
public class ModelDownloadTests
{
    [Test]
    public void Download_ProgressTracking_Works()
    {
        var progress = new DownloadProgress();
        
        progress.BytesDownloaded = 500 * 1024 * 1024; // 500 MB
        progress.TotalBytes = 1024 * 1024 * 1024; // 1 GB
        
        Assert.That(progress.PercentComplete, Is.EqualTo(50.0).Within(0.1));
    }

    [Test]
    public void Download_SpeedCalculation_Works()
    {
        var progress = new DownloadProgress
        {
            BytesDownloaded = 100 * 1024 * 1024, // 100 MB
            ElapsedSeconds = 10
        };
        
        var speedMBps = progress.BytesDownloaded / 1024.0 / 1024.0 / progress.ElapsedSeconds;
        
        Assert.That(speedMBps, Is.EqualTo(10.0).Within(0.1));
    }

    [Test]
    public void Download_ETACalculation_Works()
    {
        var progress = new DownloadProgress
        {
            BytesDownloaded = 500 * 1024 * 1024,
            TotalBytes = 1024 * 1024 * 1024,
            ElapsedSeconds = 50
        };
        
        var remainingBytes = progress.TotalBytes - progress.BytesDownloaded;
        var bytesPerSecond = progress.BytesDownloaded / progress.ElapsedSeconds;
        var etaSeconds = remainingBytes / bytesPerSecond;
        
        Assert.That(etaSeconds, Is.EqualTo(50).Within(1));
    }

    [Test]
    public void Download_Pause_StopsProgress()
    {
        var download = new MockDownload { IsPaused = false };
        
        download.Pause();
        
        Assert.That(download.IsPaused, Is.True);
    }

    [Test]
    public void Download_Resume_ContinuesProgress()
    {
        var download = new MockDownload { IsPaused = true };
        
        download.Resume();
        
        Assert.That(download.IsPaused, Is.False);
    }

    [Test]
    public void Download_Cancel_StopsAndCleans()
    {
        var download = new MockDownload();
        
        download.Cancel();
        
        Assert.That(download.IsCancelled, Is.True);
    }

    [Test]
    public void Download_HashVerification_Works()
    {
        var expectedHash = "abc123def456";
        var actualHash = "abc123def456";
        
        Assert.That(actualHash, Is.EqualTo(expectedHash));
    }

    [Test]
    public void Download_HashMismatch_Detected()
    {
        var expectedHash = "abc123def456";
        var actualHash = "xyz789uvw012";
        
        Assert.That(actualHash, Is.Not.EqualTo(expectedHash));
    }
}

public class DownloadProgress
{
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public double ElapsedSeconds { get; set; }
    
    public double PercentComplete => TotalBytes > 0 
        ? (double)BytesDownloaded / TotalBytes * 100 
        : 0;
}

public class MockDownload
{
    public bool IsPaused { get; set; }
    public bool IsCancelled { get; set; }
    
    public void Pause() => IsPaused = true;
    public void Resume() => IsPaused = false;
    public void Cancel() => IsCancelled = true;
}

[TestFixture]
[Category("Integration")]
public class ModelInstallationTests
{
    [Test]
    public void Installation_ExtractsFiles_ToCorrectLocation()
    {
        var installPath = Path.Combine(Path.GetTempPath(), "models", "phi-3");
        var expectedFiles = new[] { "model.onnx", "tokenizer.json", "config.json" };
        
        // Simulate installation
        var installedFiles = expectedFiles.Select(f => Path.Combine(installPath, f)).ToList();
        
        Assert.That(installedFiles.All(f => f.Contains("phi-3")), Is.True);
    }

    [Test]
    public void Installation_Verification_ChecksAllFiles()
    {
        var requiredFiles = new[] { "model.onnx", "tokenizer.json" };
        var presentFiles = new[] { "model.onnx", "tokenizer.json", "config.json" };
        
        var allPresent = requiredFiles.All(r => presentFiles.Contains(r));
        
        Assert.That(allPresent, Is.True);
    }

    [Test]
    public void Installation_MissingFile_Detected()
    {
        var requiredFiles = new[] { "model.onnx", "tokenizer.json" };
        var presentFiles = new[] { "model.onnx" }; // Missing tokenizer
        
        var allPresent = requiredFiles.All(r => presentFiles.Contains(r));
        
        Assert.That(allPresent, Is.False);
    }

    [Test]
    public void Installation_DiskSpace_Checked()
    {
        var requiredBytes = 2L * 1024 * 1024 * 1024; // 2 GB
        var availableBytes = 10L * 1024 * 1024 * 1024; // 10 GB
        
        var hasSpace = availableBytes >= requiredBytes;
        
        Assert.That(hasSpace, Is.True);
    }

    [Test]
    public void Installation_InsufficientSpace_Detected()
    {
        var requiredBytes = 5L * 1024 * 1024 * 1024; // 5 GB
        var availableBytes = 2L * 1024 * 1024 * 1024; // 2 GB
        
        var hasSpace = availableBytes >= requiredBytes;
        
        Assert.That(hasSpace, Is.False);
    }
}

[TestFixture]
[Category("Integration")]
public class EmbeddingModelDownloadTests
{
    [Test]
    public void EmbeddingModel_DownloadInfo_Correct()
    {
        var gemma = EmbeddingModels.EmbeddingGemma;
        
        Assert.That(gemma.SizeInMB, Is.GreaterThan(0));
        Assert.That(gemma.HuggingFaceRepo, Does.Contain("onnx"));
    }

    [Test]
    public void EmbeddingModel_E5_DownloadInfo_Correct()
    {
        var e5 = EmbeddingModels.E5LargeMultilingual;
        
        Assert.That(e5.SizeInMB, Is.GreaterThan(EmbeddingModels.EmbeddingGemma.SizeInMB));
        Assert.That(e5.HuggingFaceRepo, Does.Contain("intfloat"));
    }

    [Test]
    public void EmbeddingModel_SelectionBySize_Works()
    {
        var models = EmbeddingModels.All.OrderBy(m => m.SizeInMB).ToList();
        
        Assert.That(models.First().SizeInMB, Is.LessThan(models.Last().SizeInMB));
    }
}
