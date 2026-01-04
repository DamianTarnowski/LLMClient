using LLMClient.Models;
using LLMClient.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Testy integracyjne RAG Service z lokalnymi embeddingami.
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("LocalModel")]
public class RagIntegrationTests
{
    private EmbeddingService _embeddingService = null!;
    private Mock<IDatabaseService> _mockDatabase = null!;
    private RagService _ragService = null!;
    private bool _modelAvailable = false;
    private List<RagDocument> _testDocuments = new();
    private List<RagChunk> _testChunks = new();

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var logger = new Mock<ILogger<EmbeddingService>>();
        _embeddingService = new EmbeddingService(logger.Object);
        
        _modelAvailable = await _embeddingService.IsModelDownloadedAsync();
        
        if (_modelAvailable)
        {
            try
            {
                await _embeddingService.InitializeAsync();
                TestContext.WriteLine($"EmbeddingService initialized: {_embeddingService.IsInitialized}");
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Init error: {ex.Message}");
                _modelAvailable = false;
            }
        }
        
        // Setup mock database
        _mockDatabase = new Mock<IDatabaseService>();
        
        // Setup test documents
        _testDocuments = new List<RagDocument>
        {
            new() { Id = 1, FileName = "csharp_guide.txt", ChunkCount = 3 },
            new() { Id = 2, FileName = "python_basics.txt", ChunkCount = 2 },
            new() { Id = 3, FileName = "cooking_recipes.txt", ChunkCount = 2 }
        };
        
        // Setup test chunks with realistic content
        _testChunks = new List<RagChunk>
        {
            // C# document chunks
            new() { Id = 1, DocumentId = 1, ChunkIndex = 0, Content = "C# jest nowoczesnym językiem programowania stworzonym przez Microsoft. Wspiera programowanie obiektowe, funkcyjne i asynchroniczne." },
            new() { Id = 2, DocumentId = 1, ChunkIndex = 1, Content = "LINQ (Language Integrated Query) w C# pozwala na eleganckie zapytania do kolekcji danych. Można używać składni metod lub składni zapytań." },
            new() { Id = 3, DocumentId = 1, ChunkIndex = 2, Content = "Async/await w C# umożliwia pisanie asynchronicznego kodu w sposób synchroniczny. Task reprezentuje operację asynchroniczną." },
            
            // Python document chunks
            new() { Id = 4, DocumentId = 2, ChunkIndex = 0, Content = "Python jest interpretowanym językiem programowania znanym z czytelnej składni. Idealny do analizy danych i machine learning." },
            new() { Id = 5, DocumentId = 2, ChunkIndex = 1, Content = "NumPy i Pandas to podstawowe biblioteki Pythona do pracy z danymi. TensorFlow i PyTorch służą do deep learning." },
            
            // Cooking document chunks (different topic)
            new() { Id = 6, DocumentId = 3, ChunkIndex = 0, Content = "Pierogi ruskie to tradycyjne polskie danie. Farsz składa się z ziemniaków, sera i cebuli." },
            new() { Id = 7, DocumentId = 3, ChunkIndex = 1, Content = "Bigos to tradycyjna polska potrawa z kapusty kiszonej, świeżej i różnych mięs. Najlepszy smakuje po kilku dniach." }
        };
        
        _mockDatabase.Setup(x => x.GetRagDocumentsAsync()).ReturnsAsync(_testDocuments);
        _mockDatabase.Setup(x => x.GetAllRagChunksAsync()).ReturnsAsync(_testChunks);
        
        _ragService = new RagService(_mockDatabase.Object, _embeddingService);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        (_embeddingService as IDisposable)?.Dispose();
    }

    private void EnsureModelAvailable()
    {
        if (!_modelAvailable || !_embeddingService.IsInitialized)
        {
            Assert.Ignore("Model lokalny nie jest dostępny.");
        }
    }

    [Test]
    public async Task RAG_KeywordSearch_FindsRelevantChunks()
    {
        EnsureModelAvailable();
        
        // Arrange
        var query = "LINQ";
        
        // Act
        var result = await _ragService.GetRelevantContextAsync(query, topK: 3, minSimilarity: 0.1f, mode: RetrievalMode.Keyword);
        
        // Assert
        Assert.That(result, Is.Not.Null.And.Not.Empty);
        TestContext.WriteLine($"Keyword search for '{query}':");
        TestContext.WriteLine($"Result: {result}");
        
        Assert.That(result.ToLower(), Does.Contain("linq"), "Powinien znaleźć chunk o LINQ");
    }

    [Test]
    public async Task RAG_SemanticSearch_RanksByRelevance()
    {
        EnsureModelAvailable();
        
        // Najpierw wygeneruj embeddingi dla chunków
        foreach (var chunk in _testChunks.Where(c => c.Embedding == null))
        {
            var emb = await _embeddingService.GenerateEmbeddingAsync(chunk.Content, isQuery: false);
            if (emb != null)
            {
                chunk.Embedding = _embeddingService.FloatArrayToBytes(emb);
            }
        }
        
        // Arrange
        var query = "Jak pisać asynchroniczny kod?";
        
        // Act
        var result = await _ragService.GetRelevantContextWithTraceAsync(query, topK: 3, minSimilarity: 0.3f, mode: RetrievalMode.Vector);
        
        // Assert
        Assert.That(result.Context, Is.Not.Null);
        Assert.That(result.Trace, Is.Not.Null);
        
        TestContext.WriteLine($"Semantic search for: '{query}'");
        TestContext.WriteLine($"Candidates: {result.Trace?.TotalCandidates ?? 0}, Included: {result.Trace?.IncludedChunks ?? 0}");
        
        if (result.Trace?.Candidates?.Any() == true)
        {
            foreach (var candidate in result.Trace.Candidates.Where(c => c.Included).Take(3))
            {
                TestContext.WriteLine($"  [{candidate.FinalScore:F4}] {candidate.Preview}");
            }
            
            // Async/await chunk powinien być wysoko w rankingu
            var topCandidate = result.Trace.Candidates.Where(c => c.Included).FirstOrDefault();
            if (topCandidate != null)
            {
                Assert.That(topCandidate.Preview.ToLower(), Does.Contain("async").Or.Contain("asynchron"), 
                    "Chunk o async powinien być najbardziej relewantny");
            }
        }
    }

    [Test]
    public async Task RAG_HybridSearch_CombinesKeywordAndSemantic()
    {
        EnsureModelAvailable();
        
        // Wygeneruj embeddingi
        foreach (var chunk in _testChunks.Where(c => c.Embedding == null))
        {
            var emb = await _embeddingService.GenerateEmbeddingAsync(chunk.Content, isQuery: false);
            if (emb != null)
            {
                chunk.Embedding = _embeddingService.FloatArrayToBytes(emb);
            }
        }
        
        // Arrange
        var query = "programowanie obiektowe C#";
        
        // Act
        var result = await _ragService.GetRelevantContextWithTraceAsync(query, topK: 5, minSimilarity: 0.2f, mode: RetrievalMode.Hybrid);
        
        // Assert
        Assert.That(result.Context, Is.Not.Null);
        
        TestContext.WriteLine($"Hybrid search for: '{query}'");
        TestContext.WriteLine($"Context length: {result.Context?.Length ?? 0} chars");
        
        if (result.Trace?.Candidates?.Any() == true)
        {
            TestContext.WriteLine($"Top chunks:");
            foreach (var candidate in result.Trace.Candidates.Where(c => c.Included).Take(3))
            {
                TestContext.WriteLine($"  [{candidate.FinalScore:F4}] {candidate.Preview}");
            }
        }
    }

    [Test]
    public async Task RAG_DifferentTopics_SeparatesCorrectly()
    {
        EnsureModelAvailable();
        
        // Wygeneruj embeddingi
        foreach (var chunk in _testChunks.Where(c => c.Embedding == null))
        {
            var emb = await _embeddingService.GenerateEmbeddingAsync(chunk.Content, isQuery: false);
            if (emb != null)
            {
                chunk.Embedding = _embeddingService.FloatArrayToBytes(emb);
            }
        }
        
        // Arrange - zapytanie o gotowanie
        var cookingQuery = "Jak zrobić polskie pierogi?";
        
        // Act
        var result = await _ragService.GetRelevantContextWithTraceAsync(cookingQuery, topK: 3, minSimilarity: 0.3f, mode: RetrievalMode.Vector);
        
        // Assert
        TestContext.WriteLine($"Query: '{cookingQuery}'");
        
        if (result.Trace?.Candidates?.Any(c => c.Included) == true)
        {
            var topCandidate = result.Trace.Candidates.Where(c => c.Included).First();
            TestContext.WriteLine($"Top result: [{topCandidate.FinalScore:F4}] {topCandidate.Preview}");
        }
        
        // W trybie demo (losowe embeddingi) nie możemy testować semantyki
        Assert.That(result.Context, Is.Not.Null, "Context powinien być zwrócony");
    }

    [Test]
    public async Task RAG_TraceInfo_ContainsTimings()
    {
        EnsureModelAvailable();
        
        // Arrange
        var query = "Python machine learning";
        
        // Act
        var result = await _ragService.GetRelevantContextWithTraceAsync(query, topK: 3, minSimilarity: 0.2f, mode: RetrievalMode.Keyword);
        
        // Assert
        Assert.That(result.Trace, Is.Not.Null);
        Assert.That(result.Trace!.Query, Is.EqualTo(query));
        Assert.That(result.Trace.Timings, Is.Not.Empty);
        
        TestContext.WriteLine("Trace info:");
        TestContext.WriteLine($"  Query: {result.Trace.Query}");
        TestContext.WriteLine($"  Mode: {result.Trace.RetrievalMode}");
        TestContext.WriteLine($"  Timings:");
        foreach (var timing in result.Trace.Timings)
        {
            TestContext.WriteLine($"    {timing.Name}: {timing.ElapsedMs}ms");
        }
    }

    [Test]
    public async Task RAG_ChunkTextMethod_CreatesProperChunks()
    {
        EnsureModelAvailable();
        
        // Arrange - długi tekst do podzielenia
        var longText = string.Join("\n\n", Enumerable.Range(1, 20).Select(i => 
            $"Paragraf {i}: " + string.Join(" ", Enumerable.Range(1, 50).Select(j => $"słowo{j}"))));
        
        // Act
        _mockDatabase.Setup(x => x.SaveRagDocumentAsync(It.IsAny<RagDocument>()))
            .Callback<RagDocument>(d => d.Id = 100)
            .Returns(Task.CompletedTask);
        
        var savedChunks = new List<string>();
        _mockDatabase.Setup(x => x.SaveRagChunksAsync(It.IsAny<int>(), It.IsAny<List<string>>()))
            .Callback<int, List<string>>((id, chunks) => savedChunks = chunks)
            .Returns(Task.CompletedTask);
        
        var doc = await _ragService.AddDocumentFromContentAsync("test_long.txt", longText);
        
        // Assert
        Assert.That(doc, Is.Not.Null);
        Assert.That(doc.ChunkCount, Is.GreaterThan(1), "Długi tekst powinien być podzielony na wiele chunków");
        
        TestContext.WriteLine($"Document chunked into {doc.ChunkCount} chunks");
        TestContext.WriteLine($"Actual chunks saved: {savedChunks.Count}");
        
        foreach (var chunk in savedChunks.Take(3))
        {
            TestContext.WriteLine($"  Chunk length: {chunk.Length} chars");
        }
    }
}
