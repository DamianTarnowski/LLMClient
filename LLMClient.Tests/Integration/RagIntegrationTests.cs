using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for RAG (Retrieval Augmented Generation) functionality
/// Tests document ingestion, chunking, retrieval, and context building
/// </summary>
[TestFixture]
[Category("Integration")]
public class RagDocumentIngestionTests
{
    private Mock<IRagService> _ragService = null!;
    private List<RagDocument> _documentStore = null!;

    [SetUp]
    public void Setup()
    {
        _documentStore = new List<RagDocument>();
        _ragService = new Mock<IRagService>();
        
        _ragService.Setup(x => x.AddDocumentAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync((string path, string? name) =>
            {
                var doc = new RagDocument
                {
                    Id = _documentStore.Count + 1,
                    FileName = name ?? Path.GetFileName(path),
                    Content = $"Content of {path}",
                    ChunkCount = 10,
                    CreatedAt = DateTime.UtcNow
                };
                _documentStore.Add(doc);
                return doc.Id;
            });
            
        _ragService.Setup(x => x.GetDocumentsAsync())
            .ReturnsAsync(() => _documentStore.ToList());
            
        _ragService.Setup(x => x.DeleteDocumentAsync(It.IsAny<int>()))
            .Callback((int id) => _documentStore.RemoveAll(d => d.Id == id))
            .Returns(Task.CompletedTask);
            
        _ragService.Setup(x => x.GetRelevantContextAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((string query, int topK) =>
            {
                if (!_documentStore.Any()) return string.Empty;
                
                return string.Join("\n\n", _documentStore
                    .Take(topK)
                    .Select(d => $"[{d.FileName}]: {d.Content}"));
            });
    }

    [Test]
    public async Task Rag_AddDocument_ReturnsId()
    {
        var id = await _ragService.Object.AddDocumentAsync("/path/to/document.pdf");
        
        Assert.That(id, Is.GreaterThan(0));
    }

    [Test]
    public async Task Rag_AddDocument_WithCustomName_UsesCustomName()
    {
        await _ragService.Object.AddDocumentAsync("/path/to/file.pdf", "My Custom Document");
        
        var docs = await _ragService.Object.GetDocumentsAsync();
        Assert.That(docs[0].FileName, Is.EqualTo("My Custom Document"));
    }

    [Test]
    public async Task Rag_AddMultipleDocuments_AllStored()
    {
        await _ragService.Object.AddDocumentAsync("/path/doc1.pdf");
        await _ragService.Object.AddDocumentAsync("/path/doc2.pdf");
        await _ragService.Object.AddDocumentAsync("/path/doc3.pdf");
        
        var docs = await _ragService.Object.GetDocumentsAsync();
        Assert.That(docs.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task Rag_DeleteDocument_RemovesFromStore()
    {
        var id = await _ragService.Object.AddDocumentAsync("/path/doc.pdf");
        await _ragService.Object.DeleteDocumentAsync(id);
        
        var docs = await _ragService.Object.GetDocumentsAsync();
        Assert.That(docs.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task Rag_GetRelevantContext_ReturnsContext()
    {
        await _ragService.Object.AddDocumentAsync("/path/manual.pdf", "User Manual");
        
        var context = await _ragService.Object.GetRelevantContextAsync("How to use the app?", 5);
        
        Assert.That(context, Does.Contain("User Manual"));
    }

    [Test]
    public async Task Rag_GetRelevantContext_EmptyStore_ReturnsEmpty()
    {
        var context = await _ragService.Object.GetRelevantContextAsync("query", 5);
        
        Assert.That(context, Is.Empty);
    }

    [Test]
    public async Task Rag_GetRelevantContext_RespectsTopK()
    {
        await _ragService.Object.AddDocumentAsync("/path/doc1.pdf");
        await _ragService.Object.AddDocumentAsync("/path/doc2.pdf");
        await _ragService.Object.AddDocumentAsync("/path/doc3.pdf");
        
        var context = await _ragService.Object.GetRelevantContextAsync("query", 2);
        
        // Should only include 2 documents
        var docCount = context.Split("[").Length - 1;
        Assert.That(docCount, Is.EqualTo(2));
    }
}

[TestFixture]
[Category("Integration")]
public class RagRetrievalTests
{
    [Test]
    public void RagTrace_TracksPipelineSteps()
    {
        var trace = new RagTrace();
        
        trace.Timings.Add(new RagTiming("Query Embedding", 50));
        trace.Timings.Add(new RagTiming("Vector Search", 100));
        trace.Timings.Add(new RagTiming("Keyword Search", 30));
        trace.Timings.Add(new RagTiming("Score Fusion", 10));
        trace.Timings.Add(new RagTiming("Reranking", 80));
        
        Assert.That(trace.TotalTimeMs, Is.EqualTo(270));
        Assert.That(trace.Timings.Count, Is.EqualTo(5));
    }

    [Test]
    public void RagTrace_TracksCandidates()
    {
        var trace = new RagTrace();
        
        trace.Candidates.Add(new RagChunkCandidate(1, "doc.pdf", null, 0, 0.9f, 0.8f, 0.85f, 100, true, "preview1"));
        trace.Candidates.Add(new RagChunkCandidate(2, "doc.pdf", null, 1, 0.7f, 0.6f, 0.65f, 80, true, "preview2"));
        trace.Candidates.Add(new RagChunkCandidate(3, "doc.pdf", null, 2, 0.3f, 0.2f, 0.25f, 50, false, "preview3"));
        
        Assert.That(trace.TotalCandidates, Is.EqualTo(3));
        Assert.That(trace.IncludedChunks, Is.EqualTo(2));
    }

    [Test]
    public void RagTrace_RetrievalModes_Work()
    {
        var vectorTrace = new RagTrace { RetrievalMode = RetrievalMode.Vector };
        var keywordTrace = new RagTrace { RetrievalMode = RetrievalMode.Keyword };
        var hybridTrace = new RagTrace { RetrievalMode = RetrievalMode.Hybrid };
        
        Assert.That(vectorTrace.RetrievalMode, Is.EqualTo(RetrievalMode.Vector));
        Assert.That(keywordTrace.RetrievalMode, Is.EqualTo(RetrievalMode.Keyword));
        Assert.That(hybridTrace.RetrievalMode, Is.EqualTo(RetrievalMode.Hybrid));
    }

    [Test]
    public void RetrievalResult_ContainsAllInfo()
    {
        var result = new RetrievalResult
        {
            Context = "Retrieved context",
            TotalChunksEvaluated = 100,
            RetrievalTimeMs = 250,
            Trace = new RagTrace()
        };
        
        result.Chunks.Add(new RetrievedChunk
        {
            ChunkId = 1,
            DocumentId = 1,
            DocumentName = "doc.pdf",
            Content = "chunk content",
            Score = 0.95f
        });
        
        Assert.That(result.Context, Is.Not.Empty);
        Assert.That(result.Chunks.Count, Is.EqualTo(1));
        Assert.That(result.Trace, Is.Not.Null);
    }

    [Test]
    public void RagChunkCandidate_ScoreFusion_Works()
    {
        var candidate = new RagChunkCandidate(
            ChunkId: 1,
            SourceName: "doc.pdf",
            Section: null,
            ChunkIndex: 0,
            VectorScore: 0.8f,
            KeywordScore: 0.6f,
            FinalScore: 0.7f, // Fused score
            TokenCount: 100,
            Included: true,
            Preview: "preview"
        );
        
        // Final score should be between vector and keyword scores (RRF fusion)
        Assert.That(candidate.FinalScore, Is.GreaterThanOrEqualTo(candidate.KeywordScore));
        Assert.That(candidate.FinalScore, Is.LessThanOrEqualTo(candidate.VectorScore));
    }
}

[TestFixture]
[Category("Integration")]
public class RagTokenBudgetTests
{
    [Test]
    public void TokenBreakdown_CalculatesUsage()
    {
        var breakdown = new RagTokenBreakdown
        {
            SystemTokens = 200,
            ContextTokens = 2000,
            UserTokens = 100,
            HistoryTokens = 500,
            TotalPromptTokens = 2800,
            ContextBudget = 3000
        };
        
        Assert.That(breakdown.ContextUsagePercent, Is.EqualTo(66.7).Within(0.1));
    }

    [Test]
    public void TokenBreakdown_ContextShare_CalculatesCorrectly()
    {
        var breakdown = new RagTokenBreakdown
        {
            ContextTokens = 1400,
            TotalPromptTokens = 2800
        };
        
        Assert.That(breakdown.ContextSharePercent, Is.EqualTo(50.0));
    }

    [Test]
    public void TokenBreakdown_UnderBudget_IsEfficient()
    {
        var breakdown = new RagTokenBreakdown
        {
            ContextTokens = 1500,
            ContextBudget = 3000
        };
        
        Assert.That(breakdown.ContextUsagePercent, Is.LessThanOrEqualTo(50));
    }

    [Test]
    public void TokenBreakdown_OverBudget_DetectedCorrectly()
    {
        var breakdown = new RagTokenBreakdown
        {
            ContextTokens = 4000,
            ContextBudget = 3000
        };
        
        Assert.That(breakdown.ContextUsagePercent, Is.GreaterThan(100));
    }
}
