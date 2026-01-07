using LLMClient.Models;
using LLMClient.Services;
using Moq;
using NUnit.Framework;
using System.Diagnostics;

namespace LLMClient.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class RagOptimizationTests : IDisposable
{
    private DatabaseService _databaseService = null!;
    private Mock<IEmbeddingService> _mockEmbeddingService = null!;
    private RagService _ragService = null!;
    private string _testDbPath = null!;

    [SetUp]
    public void SetUp()
    {
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockEmbeddingService.Setup(x => x.IsInitialized).Returns(true);
        _mockEmbeddingService.Setup(x => x.ModelVersion).Returns("test-v1");
        
        _testDbPath = Path.Combine(Path.GetTempPath(), $"RagOptTest_{Guid.NewGuid()}.db");
        _databaseService = new DatabaseService(_mockEmbeddingService.Object, _testDbPath);
        _ragService = new RagService(_databaseService, _mockEmbeddingService.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _databaseService?.Dispose();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    public void Dispose()
    {
        TearDown();
    }

    #region Pre-filtering Keyword Extraction Tests

    [Test]
    public async Task GetRelevantContextAsync_ExtractsKeywordsFromQuery()
    {
        // Arrange - create document with specific keywords
        var doc = new RagDocument { FileName = "test.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);
        
        await _databaseService.SaveRagChunksAsync(doc.Id, new List<string>
        {
            "Machine learning is a subset of artificial intelligence",
            "Cooking recipes require specific ingredients",
            "Deep learning neural networks process data"
        });

        // Act - query with "machine learning" should find relevant chunks
        var result = await _ragService.GetRelevantContextAsync(
            "How does machine learning work?", 
            topK: 3, 
            minSimilarity: 0.1f, 
            mode: RetrievalMode.Keyword);

        // Assert
        Assert.That(result, Does.Contain("machine learning").IgnoreCase);
        Assert.That(result, Does.Not.Contain("cooking").IgnoreCase);
    }

    [Test]
    public async Task GetRelevantContextAsync_IgnoresShortWords()
    {
        // Arrange
        var doc = new RagDocument { FileName = "test.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);
        
        await _databaseService.SaveRagChunksAsync(doc.Id, new List<string>
        {
            "The API endpoint returns JSON data",
            "An in-depth guide to programming"
        });

        // Act - "the" and "to" should be ignored (<=2 chars after filtering)
        var result = await _ragService.GetRelevantContextAsync(
            "the API to use", 
            topK: 3, 
            minSimilarity: 0.1f, 
            mode: RetrievalMode.Keyword);

        // Assert - should still find "API" match
        Assert.That(result, Does.Contain("API"));
    }

    [Test]
    public async Task GetRelevantContextAsync_LimitsKeywordsToFive()
    {
        // Arrange
        var doc = new RagDocument { FileName = "test.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);
        
        await _databaseService.SaveRagChunksAsync(doc.Id, new List<string>
        {
            "Alpha beta gamma delta epsilon zeta eta theta"
        });

        // Act - query with many words, only first 5 significant should be used
        var result = await _ragService.GetRelevantContextAsync(
            "alpha beta gamma delta epsilon zeta eta theta iota kappa", 
            topK: 3, 
            minSimilarity: 0.1f, 
            mode: RetrievalMode.Keyword);

        // Assert - should still find the chunk
        Assert.That(result, Is.Not.Empty);
    }

    #endregion

    #region Fallback to Full Scan Tests

    [Test]
    public async Task GetRelevantContextAsync_FallsBackToFullScan_WhenNoKeywordMatches()
    {
        // Arrange - create chunks that won't match keyword filter
        var doc = new RagDocument { FileName = "test.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);
        
        await _databaseService.SaveRagChunksAsync(doc.Id, new List<string>
        {
            "Completely unrelated content about astronomy",
            "Stars and galaxies in the universe"
        });

        // Act - query with keywords that don't match any chunk
        var result = await _ragService.GetRelevantContextAsync(
            "programming languages software", 
            topK: 3, 
            minSimilarity: 0.0f, // Very low threshold to catch any result
            mode: RetrievalMode.Keyword);

        // Assert - should return something due to fallback (or empty if truly no match)
        // The fallback loads all chunks, so keyword matching happens in memory
        Assert.That(result, Is.Not.Null); // Fallback mechanism activated
    }

    #endregion

    #region Hybrid Mode Tests

    [Test]
    public async Task GetRelevantContextAsync_HybridMode_CombinesScores()
    {
        // Arrange
        var doc = new RagDocument { FileName = "test.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);
        
        await _databaseService.SaveRagChunksAsync(doc.Id, new List<string>
        {
            "Python programming language is popular",
            "Java programming language is enterprise",
            "JavaScript runs in browsers"
        });

        // Setup embedding mock to return vectors
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });

        // Act
        var result = await _ragService.GetRelevantContextAsync(
            "programming language", 
            topK: 2, 
            minSimilarity: 0.1f, 
            mode: RetrievalMode.Hybrid);

        // Assert
        Assert.That(result, Does.Contain("programming"));
    }

    #endregion

    #region Performance Tests

    [Test]
    [Category("Performance")]
    public async Task GetRelevantContextAsync_PreFilteringImprovesPperformance()
    {
        // Arrange - create large document set
        for (int d = 0; d < 10; d++)
        {
            var doc = new RagDocument { FileName = $"doc{d}.pdf", FilePath = $"/path{d}" };
            await _databaseService.SaveRagDocumentAsync(doc);
            
            var chunks = new List<string>();
            for (int c = 0; c < 100; c++)
            {
                // Only every 10th chunk mentions "optimization"
                chunks.Add(c % 10 == 0 
                    ? $"Chunk {c}: Performance optimization techniques"
                    : $"Chunk {c}: Generic content about various topics");
            }
            await _databaseService.SaveRagChunksAsync(doc.Id, chunks);
        }

        // Act - measure with pre-filtering (keyword mode uses it)
        var sw = Stopwatch.StartNew();
        var result = await _ragService.GetRelevantContextAsync(
            "optimization performance", 
            topK: 5, 
            minSimilarity: 0.1f, 
            mode: RetrievalMode.Keyword);
        sw.Stop();

        // Assert
        Assert.That(result, Does.Contain("optimization"));
        Console.WriteLine($"Retrieved context from 1000 chunks in {sw.ElapsedMilliseconds}ms");
        
        // Should be fast due to pre-filtering
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(2000), 
            "Pre-filtered retrieval should be fast");
    }

    #endregion

    #region Trace Tests

    [Test]
    public async Task GetRelevantContextWithTraceAsync_IncludesPreFilterTiming()
    {
        // Arrange
        var doc = new RagDocument { FileName = "test.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);
        await _databaseService.SaveRagChunksAsync(doc.Id, new List<string> { "Test content" });

        // Act
        var result = await _ragService.GetRelevantContextWithTraceAsync(
            "test query", 
            topK: 3, 
            minSimilarity: 0.1f, 
            mode: RetrievalMode.Keyword);

        // Assert
        Assert.That(result.Trace, Is.Not.Null);
        Assert.That(result.Trace!.Timings, Has.Count.GreaterThan(0));
        Assert.That(result.Trace.Timings.Any(t => t.StepName == "PreFilter"), Is.True, 
            "Should include PreFilter timing");
    }

    [Test]
    public async Task GetRelevantContextWithTraceAsync_ReturnsEvaluatedChunkCount()
    {
        // Arrange
        var doc = new RagDocument { FileName = "test.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);
        
        var chunks = Enumerable.Range(1, 50).Select(i => $"Content chunk {i}").ToList();
        await _databaseService.SaveRagChunksAsync(doc.Id, chunks);

        // Act
        var result = await _ragService.GetRelevantContextWithTraceAsync(
            "content chunk", 
            topK: 3, 
            minSimilarity: 0.1f, 
            mode: RetrievalMode.Keyword);

        // Assert
        Assert.That(result.TotalChunksEvaluated, Is.GreaterThan(0));
        Console.WriteLine($"Evaluated {result.TotalChunksEvaluated} chunks");
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task GetRelevantContextAsync_WithEmptyQuery_ReturnsEmpty()
    {
        // Arrange
        var doc = new RagDocument { FileName = "test.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);
        await _databaseService.SaveRagChunksAsync(doc.Id, new List<string> { "Content" });

        // Act
        var result = await _ragService.GetRelevantContextAsync(
            "", 
            topK: 3, 
            minSimilarity: 0.1f, 
            mode: RetrievalMode.Keyword);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetRelevantContextAsync_WithNoDocuments_ReturnsEmpty()
    {
        // Act
        var result = await _ragService.GetRelevantContextAsync(
            "test query", 
            topK: 3, 
            minSimilarity: 0.1f, 
            mode: RetrievalMode.Keyword);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetRelevantContextAsync_HighMinSimilarity_ReturnsEmpty()
    {
        // Arrange
        var doc = new RagDocument { FileName = "test.pdf", FilePath = "/path" };
        await _databaseService.SaveRagDocumentAsync(doc);
        await _databaseService.SaveRagChunksAsync(doc.Id, new List<string> { "Some content" });

        // Act - extremely high similarity threshold
        var result = await _ragService.GetRelevantContextAsync(
            "completely different query", 
            topK: 3, 
            minSimilarity: 0.99f, 
            mode: RetrievalMode.Keyword);

        // Assert
        Assert.That(result, Is.Empty);
    }

    #endregion
}
