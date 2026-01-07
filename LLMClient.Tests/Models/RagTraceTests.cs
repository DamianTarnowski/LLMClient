using LLMClient.Core.Models;
using LLMClient.Core.Services;

namespace LLMClient.Tests.Models;

/// <summary>
/// Tests for RagTrace model - flight recorder for RAG pipeline
/// </summary>
[TestFixture]
public class RagTraceTests
{
    [Test]
    public void RagTrace_CreateNew_HasDefaultValues()
    {
        var trace = new RagTrace();
        
        Assert.That(trace.Query, Is.Empty);
        Assert.That(trace.Utc, Is.Not.EqualTo(default(DateTime)));
        Assert.That(trace.Candidates, Is.Not.Null);
        Assert.That(trace.Timings, Is.Not.Null);
        Assert.That(trace.RetrievalMode, Is.EqualTo(RetrievalMode.Hybrid));
    }

    [Test]
    public void RagTrace_AddCandidates_UpdatesCounts()
    {
        var trace = new RagTrace();
        trace.Candidates.Add(new RagChunkCandidate(1, "doc.pdf", null, 0, 0.8f, 0.5f, 0.7f, 100, true, "preview"));
        trace.Candidates.Add(new RagChunkCandidate(2, "doc.pdf", null, 1, 0.7f, 0.6f, 0.65f, 80, true, "preview2"));
        trace.Candidates.Add(new RagChunkCandidate(3, "doc.pdf", null, 2, 0.3f, 0.2f, 0.25f, 50, false, "preview3"));
        
        Assert.That(trace.TotalCandidates, Is.EqualTo(3));
        Assert.That(trace.IncludedChunks, Is.EqualTo(2));
    }

    [Test]
    public void RagTrace_AddTimings_CalculatesTotalTime()
    {
        var trace = new RagTrace();
        trace.Timings.Add(new RagTiming("Embedding", 100));
        trace.Timings.Add(new RagTiming("Search", 50));
        trace.Timings.Add(new RagTiming("Rerank", 30));
        
        Assert.That(trace.TotalTimeMs, Is.EqualTo(180));
    }

    [Test]
    public void RagTrace_SetRetrievalMode_UpdatesProperty()
    {
        var trace = new RagTrace { RetrievalMode = RetrievalMode.Vector };
        Assert.That(trace.RetrievalMode, Is.EqualTo(RetrievalMode.Vector));
        
        trace.RetrievalMode = RetrievalMode.Keyword;
        Assert.That(trace.RetrievalMode, Is.EqualTo(RetrievalMode.Keyword));
    }

    [Test]
    public void RagTrace_SetModel_UpdatesProperty()
    {
        var trace = new RagTrace
        {
            Model = "gpt-4-turbo",
            Provider = "OpenRouter"
        };
        
        Assert.That(trace.Model, Is.EqualTo("gpt-4-turbo"));
        Assert.That(trace.Provider, Is.EqualTo("OpenRouter"));
    }

    [Test]
    public void RagTrace_SetPerformanceMetrics_UpdatesProperties()
    {
        var trace = new RagTrace
        {
            TimeToFirstTokenMs = 250,
            TokensPerSecond = 45.5
        };
        
        Assert.That(trace.TimeToFirstTokenMs, Is.EqualTo(250));
        Assert.That(trace.TokensPerSecond, Is.EqualTo(45.5));
    }

    [Test]
    public void RagTrace_SetFusionFormula_UpdatesProperty()
    {
        var trace = new RagTrace { FusionFormula = "RRF(k=30)" };
        
        Assert.That(trace.FusionFormula, Is.EqualTo("RRF(k=30)"));
    }

    [Test]
    public void RagTrace_SetPromptPreview_UpdatesProperty()
    {
        var trace = new RagTrace
        {
            PromptPreview = "System: You are a helpful assistant.\nUser: Hello"
        };
        
        Assert.That(trace.PromptPreview, Does.Contain("System:"));
        Assert.That(trace.PromptPreview, Does.Contain("User:"));
    }
}

[TestFixture]
public class RagChunkCandidateTests
{
    [Test]
    public void RagChunkCandidate_Create_HasCorrectValues()
    {
        var candidate = new RagChunkCandidate(
            ChunkId: 1,
            SourceName: "document.pdf",
            Section: "Chapter 1",
            ChunkIndex: 5,
            VectorScore: 0.85f,
            KeywordScore: 0.72f,
            FinalScore: 0.78f,
            TokenCount: 150,
            Included: true,
            Preview: "This is the chunk content..."
        );
        
        Assert.That(candidate.ChunkId, Is.EqualTo(1));
        Assert.That(candidate.SourceName, Is.EqualTo("document.pdf"));
        Assert.That(candidate.Section, Is.EqualTo("Chapter 1"));
        Assert.That(candidate.ChunkIndex, Is.EqualTo(5));
        Assert.That(candidate.VectorScore, Is.EqualTo(0.85f).Within(0.01f));
        Assert.That(candidate.KeywordScore, Is.EqualTo(0.72f).Within(0.01f));
        Assert.That(candidate.FinalScore, Is.EqualTo(0.78f).Within(0.01f));
        Assert.That(candidate.TokenCount, Is.EqualTo(150));
        Assert.That(candidate.Included, Is.True);
        Assert.That(candidate.Preview, Does.Contain("chunk content"));
    }

    [Test]
    public void RagChunkCandidate_WithMatchedTerms_TracksTerms()
    {
        var terms = new List<string> { "machine", "learning", "AI" };
        var candidate = new RagChunkCandidate(1, "doc.pdf", null, 0, 0.8f, 0.9f, 0.85f, 100, true, "preview", terms);
        
        Assert.That(candidate.MatchedTerms, Is.Not.Null);
        Assert.That(candidate.MatchedTerms!.Count, Is.EqualTo(3));
        Assert.That(candidate.MatchedTerms, Does.Contain("machine"));
    }

    [Test]
    public void RagChunkCandidate_Rank_CanBeSet()
    {
        var candidate = new RagChunkCandidate(1, "doc.pdf", null, 0, 0.8f, 0.5f, 0.7f, 100, true, "preview");
        candidate.Rank = 1;
        
        Assert.That(candidate.Rank, Is.EqualTo(1));
    }
}

[TestFixture]
public class RagTimingTests
{
    [Test]
    public void RagTiming_Create_HasCorrectValues()
    {
        var timing = new RagTiming("Vector Search", 150);
        
        Assert.That(timing.Name, Is.EqualTo("Vector Search"));
        Assert.That(timing.ElapsedMs, Is.EqualTo(150));
    }

    [Test]
    public void RagTiming_ToString_FormatsCorrectly()
    {
        var timing = new RagTiming("Embedding", 100);
        
        Assert.That(timing.ToString(), Is.EqualTo("Embedding: 100ms"));
    }

    [Test]
    public void RagTiming_MultiplePipelineSteps_TrackCorrectly()
    {
        var timings = new List<RagTiming>
        {
            new("Query Embedding", 50),
            new("Vector Search", 100),
            new("Keyword Search", 30),
            new("Score Fusion", 10),
            new("Reranking", 80)
        };
        
        var totalMs = timings.Sum(t => t.ElapsedMs);
        Assert.That(totalMs, Is.EqualTo(270));
        Assert.That(timings.Count, Is.EqualTo(5));
    }
}

[TestFixture]
public class RagTokenBreakdownTests
{
    [Test]
    public void RagTokenBreakdown_CreateNew_HasDefaultValues()
    {
        var breakdown = new RagTokenBreakdown();
        
        Assert.That(breakdown.SystemTokens, Is.EqualTo(0));
        Assert.That(breakdown.ContextTokens, Is.EqualTo(0));
        Assert.That(breakdown.ContextBudget, Is.EqualTo(3000)); // default
    }

    [Test]
    public void RagTokenBreakdown_SetValues_UpdatesProperties()
    {
        var breakdown = new RagTokenBreakdown
        {
            SystemTokens = 150,
            ContextTokens = 2000,
            UserTokens = 50,
            TotalPromptTokens = 2200
        };
        
        Assert.That(breakdown.SystemTokens, Is.EqualTo(150));
        Assert.That(breakdown.ContextTokens, Is.EqualTo(2000));
        Assert.That(breakdown.UserTokens, Is.EqualTo(50));
        Assert.That(breakdown.TotalPromptTokens, Is.EqualTo(2200));
    }

    [Test]
    public void RagTokenBreakdown_ContextUsagePercent_CalculatesCorrectly()
    {
        var breakdown = new RagTokenBreakdown
        {
            ContextTokens = 1500,
            ContextBudget = 3000
        };
        
        Assert.That(breakdown.ContextUsagePercent, Is.EqualTo(50.0));
    }

    [Test]
    public void RagTokenBreakdown_ContextSharePercent_CalculatesCorrectly()
    {
        var breakdown = new RagTokenBreakdown
        {
            ContextTokens = 500,
            TotalPromptTokens = 1000
        };
        
        Assert.That(breakdown.ContextSharePercent, Is.EqualTo(50.0));
    }
}

[TestFixture]
public class RetrievalResultTests
{
    [Test]
    public void RetrievalResult_CreateNew_HasDefaultValues()
    {
        var result = new RetrievalResult();
        
        Assert.That(result.Context, Is.Empty);
        Assert.That(result.Chunks, Is.Not.Null);
        Assert.That(result.Chunks.Count, Is.EqualTo(0));
    }

    [Test]
    public void RetrievalResult_SetValues_UpdatesProperties()
    {
        var result = new RetrievalResult
        {
            Context = "Retrieved context from documents",
            TotalChunksEvaluated = 100,
            RetrievalTimeMs = 250
        };
        
        Assert.That(result.Context, Does.Contain("Retrieved context"));
        Assert.That(result.TotalChunksEvaluated, Is.EqualTo(100));
        Assert.That(result.RetrievalTimeMs, Is.EqualTo(250));
    }
}

[TestFixture]
public class RetrievedChunkTests
{
    [Test]
    public void RetrievedChunk_CreateNew_HasDefaultValues()
    {
        var chunk = new RetrievedChunk();
        
        Assert.That(chunk.ChunkId, Is.EqualTo(0));
        Assert.That(chunk.DocumentName, Is.Empty);
        Assert.That(chunk.Content, Is.Empty);
    }

    [Test]
    public void RetrievedChunk_SetValues_UpdatesProperties()
    {
        var chunk = new RetrievedChunk
        {
            ChunkId = 42,
            DocumentId = 1,
            DocumentName = "manual.pdf",
            Content = "This is the chunk content",
            Score = 0.95f,
            ChunkIndex = 5
        };
        
        Assert.That(chunk.ChunkId, Is.EqualTo(42));
        Assert.That(chunk.DocumentId, Is.EqualTo(1));
        Assert.That(chunk.DocumentName, Is.EqualTo("manual.pdf"));
        Assert.That(chunk.Content, Does.Contain("chunk content"));
        Assert.That(chunk.Score, Is.EqualTo(0.95f).Within(0.01f));
        Assert.That(chunk.ChunkIndex, Is.EqualTo(5));
    }
}
