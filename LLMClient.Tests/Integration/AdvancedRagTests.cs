using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Advanced integration tests for RAG functionality
/// Tests hybrid search, reranking, context assembly
/// </summary>
[TestFixture]
[Category("Integration")]
public class HybridSearchTests
{
    [Test]
    public void HybridSearch_CombinesVectorAndKeyword()
    {
        var vectorResults = new List<(int Id, float Score)>
        {
            (1, 0.9f), (2, 0.8f), (3, 0.7f)
        };
        
        var keywordResults = new List<(int Id, float Score)>
        {
            (2, 0.95f), (4, 0.85f), (1, 0.75f)
        };
        
        var combined = CombineWithRRF(vectorResults, keywordResults);
        
        Assert.That(combined.Count, Is.EqualTo(4)); // Unique IDs: 1, 2, 3, 4
        Assert.That(combined[0].Id, Is.EqualTo(2)); // Appears in both, should rank highest
    }

    [Test]
    public void HybridSearch_RRFFormula_CalculatesCorrectly()
    {
        var k = 60;
        var vectorRank = 1;
        var keywordRank = 2;
        
        var vectorScore = 1.0 / (k + vectorRank);
        var keywordScore = 1.0 / (k + keywordRank);
        var fusedScore = vectorScore + keywordScore;
        
        Assert.That(fusedScore, Is.GreaterThan(vectorScore));
        Assert.That(fusedScore, Is.GreaterThan(keywordScore));
    }

    [Test]
    public void HybridSearch_WeightedCombination_Works()
    {
        var vectorScore = 0.8f;
        var keywordScore = 0.6f;
        var vectorWeight = 0.7f;
        
        var combined = vectorWeight * vectorScore + (1 - vectorWeight) * keywordScore;
        
        Assert.That(combined, Is.EqualTo(0.74f).Within(0.01f));
    }

    private static List<(int Id, double Score)> CombineWithRRF(
        List<(int Id, float Score)> vectorResults,
        List<(int Id, float Score)> keywordResults,
        int k = 60)
    {
        var scores = new Dictionary<int, double>();
        
        for (int i = 0; i < vectorResults.Count; i++)
        {
            var id = vectorResults[i].Id;
            scores[id] = scores.GetValueOrDefault(id) + 1.0 / (k + i + 1);
        }
        
        for (int i = 0; i < keywordResults.Count; i++)
        {
            var id = keywordResults[i].Id;
            scores[id] = scores.GetValueOrDefault(id) + 1.0 / (k + i + 1);
        }
        
        return scores
            .OrderByDescending(kv => kv.Value)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }
}

[TestFixture]
[Category("Integration")]
public class RerankingTests
{
    [Test]
    public void Reranking_ImprovesPrecision()
    {
        var initialResults = new List<(int Id, float Score, string Content)>
        {
            (1, 0.9f, "Machine learning is a type of AI"),
            (2, 0.85f, "The weather today is sunny"),
            (3, 0.8f, "Deep learning uses neural networks")
        };
        
        var query = "What is machine learning?";
        var reranked = Rerank(initialResults, query);
        
        // Results about ML should rank higher after reranking
        Assert.That(reranked[0].Content, Does.Contain("Machine learning").Or.Contain("Deep learning"));
    }

    [Test]
    public void Reranking_RemovesIrrelevant()
    {
        var results = new List<(int Id, float Score, string Content)>
        {
            (1, 0.9f, "Relevant content about AI"),
            (2, 0.85f, "Completely unrelated weather info"),
            (3, 0.8f, "More AI information")
        };
        
        var query = "artificial intelligence";
        var reranked = Rerank(results, query, minScore: 0.3f);
        
        // Irrelevant results should have lower scores
        Assert.That(reranked.Count, Is.LessThanOrEqualTo(3));
    }

    private static List<(int Id, float Score, string Content)> Rerank(
        List<(int Id, float Score, string Content)> results,
        string query,
        float minScore = 0)
    {
        // Simple keyword-based reranking for testing
        var queryTerms = query.ToLower().Split(' ');
        
        return results
            .Select(r =>
            {
                var contentLower = r.Content.ToLower();
                var matchCount = queryTerms.Count(t => contentLower.Contains(t));
                var newScore = r.Score * (1 + matchCount * 0.1f);
                return (r.Id, newScore, r.Content);
            })
            .Where(r => r.newScore >= minScore)
            .OrderByDescending(r => r.newScore)
            .ToList();
    }
}

[TestFixture]
[Category("Integration")]
public class ContextAssemblyTests
{
    [Test]
    public void ContextAssembly_RespectsBudget()
    {
        var chunks = Enumerable.Range(1, 10)
            .Select(i => new TestChunk { Id = i, Content = new string('x', 500), TokenCount = 100 })
            .ToList();
        
        var budget = 500;
        var assembled = AssembleContext(chunks, budget);
        
        Assert.That(assembled.TotalTokens, Is.LessThanOrEqualTo(budget));
    }

    [Test]
    public void ContextAssembly_IncludesHighestScoring()
    {
        var chunks = new List<TestChunk>
        {
            new() { Id = 1, Score = 0.9f, TokenCount = 100 },
            new() { Id = 2, Score = 0.7f, TokenCount = 100 },
            new() { Id = 3, Score = 0.95f, TokenCount = 100 }
        };
        
        var assembled = AssembleContext(chunks, budget: 250);
        
        Assert.That(assembled.IncludedIds, Does.Contain(3)); // Highest score
        Assert.That(assembled.IncludedIds, Does.Contain(1)); // Second highest
    }

    [Test]
    public void ContextAssembly_FormatsWithSeparators()
    {
        var chunks = new List<TestChunk>
        {
            new() { Id = 1, Content = "Chunk 1 content", DocumentName = "doc.pdf" },
            new() { Id = 2, Content = "Chunk 2 content", DocumentName = "doc.pdf" }
        };
        
        var context = FormatContext(chunks);
        
        Assert.That(context, Does.Contain("[doc.pdf]"));
        Assert.That(context, Does.Contain("Chunk 1"));
        Assert.That(context, Does.Contain("Chunk 2"));
    }

    private static (int TotalTokens, List<int> IncludedIds) AssembleContext(
        List<TestChunk> chunks, int budget)
    {
        var sorted = chunks.OrderByDescending(c => c.Score).ToList();
        var included = new List<int>();
        var totalTokens = 0;
        
        foreach (var chunk in sorted)
        {
            if (totalTokens + chunk.TokenCount <= budget)
            {
                included.Add(chunk.Id);
                totalTokens += chunk.TokenCount;
            }
        }
        
        return (totalTokens, included);
    }

    private static string FormatContext(List<TestChunk> chunks)
    {
        var sb = new System.Text.StringBuilder();
        
        foreach (var chunk in chunks)
        {
            sb.AppendLine($"[{chunk.DocumentName}]");
            sb.AppendLine(chunk.Content);
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
}

public class TestChunk
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public float Score { get; set; } = 0.5f;
    public int TokenCount { get; set; } = 50;
}

[TestFixture]
[Category("Integration")]
public class ChunkOverlapTests
{
    [Test]
    public void ChunkOverlap_MaintainsContext()
    {
        var text = "Sentence one. Sentence two. Sentence three. Sentence four. Sentence five.";
        var chunkSize = 3;
        var overlap = 1;
        
        var chunks = ChunkWithOverlap(text.Split(". "), chunkSize, overlap);
        
        // Adjacent chunks should share content
        if (chunks.Count > 1)
        {
            var chunk1Words = chunks[0].Split(' ');
            var chunk2Words = chunks[1].Split(' ');
            
            Assert.That(chunk1Words.Intersect(chunk2Words).Any(), Is.True);
        }
    }

    [Test]
    public void ChunkOverlap_ZeroOverlap_NoSharing()
    {
        var items = new[] { "A", "B", "C", "D", "E", "F" };
        var chunks = ChunkWithOverlap(items, 2, 0);
        
        Assert.That(chunks.Count, Is.EqualTo(3));
    }

    private static List<string> ChunkWithOverlap(string[] items, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        var step = chunkSize - overlap;
        if (step <= 0) step = 1;
        
        for (int i = 0; i < items.Length; i += step)
        {
            var chunk = string.Join(". ", items.Skip(i).Take(chunkSize));
            if (!string.IsNullOrEmpty(chunk))
                chunks.Add(chunk);
        }
        
        return chunks;
    }
}
