using LLMClient.Models;
using LLMClient.Services;
using Moq;
using NUnit.Framework;

namespace LLMClient.Tests.Services;

public class RagServiceTests : IDisposable
{
    private readonly Mock<DatabaseService> _mockDatabase;
    private readonly Mock<IEmbeddingService> _mockEmbedding;
    private readonly string _testFilesPath;

    public RagServiceTests()
    {
        _mockDatabase = new Mock<DatabaseService>();
        _mockEmbedding = new Mock<IEmbeddingService>();
        _testFilesPath = Path.Combine(Path.GetTempPath(), "RagTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testFilesPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testFilesPath))
        {
            Directory.Delete(_testFilesPath, true);
        }
    }

    [Test]
    public async Task AddDocumentFromContentAsync_CreatesDocumentWithChunks()
    {
        // Arrange
        var savedDoc = new RagDocument();
        var savedChunks = new List<RagChunk>();

        _mockDatabase.Setup(x => x.SaveRagDocumentAsync(It.IsAny<RagDocument>()))
            .Callback<RagDocument>(d => { d.Id = 1; savedDoc = d; })
            .Returns(Task.CompletedTask);

        _mockDatabase.Setup(x => x.SaveRagChunksAsync(It.IsAny<int>(), It.IsAny<List<string>>()))
            .Returns(Task.CompletedTask);

        var service = new RagService(_mockDatabase.Object, _mockEmbedding.Object);
        var content = string.Join(" ", Enumerable.Repeat("Test content for chunking.", 100));

        // Act
        var result = await service.AddDocumentFromContentAsync("test.txt", content);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.FileName, Is.EqualTo("test.txt"));
        Assert.That(result.ChunkCount, Is.GreaterThan(0));
        _mockDatabase.Verify(x => x.SaveRagDocumentAsync(It.IsAny<RagDocument>()), Times.Once);
        _mockDatabase.Verify(x => x.SaveRagChunksAsync(It.IsAny<int>(), It.IsAny<List<string>>()), Times.Once);
    }

    [Test]
    public async Task GetDocumentsAsync_ReturnsDocuments()
    {
        // Arrange
        var docs = new List<RagDocument>
        {
            new() { Id = 1, FileName = "doc1.pdf", ChunkCount = 5 },
            new() { Id = 2, FileName = "doc2.docx", ChunkCount = 3 }
        };

        _mockDatabase.Setup(x => x.GetRagDocumentsAsync())
            .ReturnsAsync(docs);

        var service = new RagService(_mockDatabase.Object, _mockEmbedding.Object);

        // Act
        var result = await service.GetDocumentsAsync();

        // Assert
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result.Any(d => d.FileName == "doc1.pdf"), Is.True);
    }

    [Test]
    public async Task DeleteDocumentAsync_CallsDatabaseDelete()
    {
        // Arrange
        _mockDatabase.Setup(x => x.DeleteRagDocumentAsync(It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var service = new RagService(_mockDatabase.Object, _mockEmbedding.Object);

        // Act
        await service.DeleteDocumentAsync(1);

        // Assert
        _mockDatabase.Verify(x => x.DeleteRagDocumentAsync(1), Times.Once);
    }

    [Test]
    public async Task GetRelevantContextAsync_KeywordMode_ReturnsMatchingChunks()
    {
        // Arrange
        var chunks = new List<RagChunk>
        {
            new() { Id = 1, DocumentId = 1, Content = "This is about machine learning and AI", ChunkIndex = 0 },
            new() { Id = 2, DocumentId = 1, Content = "This is about cooking recipes", ChunkIndex = 1 },
            new() { Id = 3, DocumentId = 1, Content = "Machine learning models are powerful", ChunkIndex = 2 }
        };

        _mockDatabase.Setup(x => x.GetAllRagChunksAsync())
            .ReturnsAsync(chunks);

        _mockEmbedding.Setup(x => x.IsInitialized).Returns(false);

        var service = new RagService(_mockDatabase.Object, _mockEmbedding.Object);

        // Act
        var result = await service.GetRelevantContextAsync("machine learning", topK: 2, minSimilarity: 0.3f, mode: RetrievalMode.Keyword);

        // Assert
        Assert.That(result, Is.Not.Empty);
        Assert.That(result.ToLower(), Does.Contain("machine learning"));
        Assert.That(result.ToLower(), Does.Not.Contain("cooking"));
    }

    [Test]
    public async Task GetRelevantContextWithTraceAsync_ReturnsTraceInfo()
    {
        // Arrange
        var chunks = new List<RagChunk>
        {
            new() { Id = 1, DocumentId = 1, Content = "Test content about programming", ChunkIndex = 0 }
        };
        var docs = new List<RagDocument>
        {
            new() { Id = 1, FileName = "test.txt" }
        };

        _mockDatabase.Setup(x => x.GetAllRagChunksAsync()).ReturnsAsync(chunks);
        _mockDatabase.Setup(x => x.GetRagDocumentsAsync()).ReturnsAsync(docs);
        _mockEmbedding.Setup(x => x.IsInitialized).Returns(false);

        var service = new RagService(_mockDatabase.Object, _mockEmbedding.Object);

        // Act
        var result = await service.GetRelevantContextWithTraceAsync("programming", topK: 3, minSimilarity: 0.3f, mode: RetrievalMode.Keyword);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Trace, Is.Not.Null);
        Assert.That(result.Trace!.Query, Is.EqualTo("programming"));
        Assert.That(result.Trace.Timings.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task GetPendingChunksCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var chunks = new List<RagChunk>
        {
            new() { Id = 1, Embedding = null },
            new() { Id = 2, Embedding = null },
            new() { Id = 3, Embedding = new byte[] { 1, 2, 3 } }
        };

        _mockDatabase.Setup(x => x.GetAllRagChunksAsync()).ReturnsAsync(chunks);

        var service = new RagService(_mockDatabase.Object, _mockEmbedding.Object);

        // Act
        var result = await service.GetPendingChunksCountAsync();

        // Assert
        Assert.That(result, Is.EqualTo(2));
    }

    [Test]
    public async Task GenerateEmbeddingsAsync_ProcessesPendingChunks()
    {
        // Arrange
        var chunks = new List<RagChunk>
        {
            new() { Id = 1, Content = "Test content", Embedding = null }
        };

        _mockDatabase.Setup(x => x.GetAllRagChunksAsync()).ReturnsAsync(chunks);
        _mockDatabase.Setup(x => x.UpdateRagChunkEmbeddingAsync(It.IsAny<RagChunk>()))
            .Returns(Task.CompletedTask);

        _mockEmbedding.Setup(x => x.IsInitialized).Returns(true);
        _mockEmbedding.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });

        var service = new RagService(_mockDatabase.Object, _mockEmbedding.Object);
        var progressMessages = new List<string>();
        var progress = new Progress<string>(msg => progressMessages.Add(msg));

        // Act
        await service.GenerateEmbeddingsAsync(progress);

        // Assert
        _mockDatabase.Verify(x => x.UpdateRagChunkEmbeddingAsync(It.IsAny<RagChunk>()), Times.Once);
    }

    [Test]
    public void ChunkText_CreatesOverlappingChunks()
    {
        // Arrange - use reflection to test private method
        var service = new RagService(_mockDatabase.Object, _mockEmbedding.Object);
        var longText = string.Join(" ", Enumerable.Range(1, 500).Select(i => $"Word{i}"));

        var methodInfo = typeof(RagService).GetMethod("ChunkText", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var chunks = methodInfo?.Invoke(null, new object[] { longText, 200, 50 }) as List<string>;

        // Assert
        Assert.That(chunks, Is.Not.Null);
        Assert.That(chunks!.Count, Is.GreaterThan(1), "Should create multiple chunks");
        
        // Verify overlap - some content should appear in consecutive chunks
        if (chunks.Count > 1)
        {
            var firstChunkEnd = chunks[0].Split(' ').TakeLast(10).ToArray();
            var secondChunkStart = chunks[1].Split(' ').Take(20).ToArray();
            var hasOverlap = firstChunkEnd.Intersect(secondChunkStart).Any();
            Assert.That(hasOverlap, Is.True, "Chunks should have overlapping content");
        }
    }
}
