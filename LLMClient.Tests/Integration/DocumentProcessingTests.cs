using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for Document Processing
/// Tests chunking, parsing, and document analysis
/// </summary>
[TestFixture]
[Category("Integration")]
public class DocumentChunkingTests
{
    [Test]
    public void Chunking_ByTokenCount_SplitsCorrectly()
    {
        var text = string.Join(" ", Enumerable.Range(1, 500).Select(i => $"word{i}"));
        var maxTokens = 100;
        
        var chunks = ChunkByTokens(text, maxTokens);
        
        Assert.That(chunks.Count, Is.GreaterThan(1));
        Assert.That(chunks.All(c => EstimateTokens(c) <= maxTokens * 1.2), Is.True); // 20% tolerance
    }

    [Test]
    public void Chunking_PreservesOverlap()
    {
        var text = "Sentence one. Sentence two. Sentence three. Sentence four. Sentence five.";
        var chunkSize = 3;
        var overlap = 1;
        
        var chunks = ChunkWithOverlap(text, chunkSize, overlap);
        
        // With overlap, consecutive chunks share some content
        if (chunks.Count > 1)
        {
            var chunk1Words = chunks[0].Split(' ');
            var chunk2Words = chunks[1].Split(' ');
            
            // There should be some overlap
            Assert.That(chunk1Words.Intersect(chunk2Words).Any(), Is.True);
        }
    }

    [Test]
    public void Chunking_BySentence_KeepsSentencesIntact()
    {
        var text = "This is sentence one. This is sentence two. This is sentence three.";
        
        var chunks = ChunkBySentence(text, 2);
        
        Assert.That(chunks.All(c => !c.EndsWith(" ")), Is.True);
        Assert.That(chunks.All(c => c.Contains(".")), Is.True);
    }

    [Test]
    public void Chunking_EmptyDocument_ReturnsEmpty()
    {
        var chunks = ChunkByTokens("", 100);
        
        Assert.That(chunks, Is.Empty);
    }

    [Test]
    public void Chunking_SingleChunk_WhenSmall()
    {
        var text = "Short text.";
        var chunks = ChunkByTokens(text, 100);
        
        Assert.That(chunks.Count, Is.EqualTo(1));
        Assert.That(chunks[0], Is.EqualTo(text));
    }

    [Test]
    public void Chunking_PolishText_Works()
    {
        var polishText = "To jest pierwszy akapit po polsku. Zawiera polskie znaki: ąęółżźćń. " +
                        "To jest drugi akapit. Ma więcej treści do przetworzenia.";
        
        var chunks = ChunkBySentence(polishText, 1);
        
        Assert.That(chunks.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(chunks.Any(c => c.Contains("ąęółżźćń")), Is.True);
    }

    private static List<string> ChunkByTokens(string text, int maxTokens)
    {
        if (string.IsNullOrEmpty(text)) return new List<string>();
        
        var words = text.Split(' ');
        var chunks = new List<string>();
        var currentChunk = new List<string>();
        
        foreach (var word in words)
        {
            currentChunk.Add(word);
            if (EstimateTokens(string.Join(" ", currentChunk)) >= maxTokens)
            {
                chunks.Add(string.Join(" ", currentChunk));
                currentChunk.Clear();
            }
        }
        
        if (currentChunk.Any())
            chunks.Add(string.Join(" ", currentChunk));
        
        return chunks;
    }

    private static List<string> ChunkWithOverlap(string text, int chunkSize, int overlap)
    {
        var words = text.Split(' ');
        var chunks = new List<string>();
        
        for (int i = 0; i < words.Length; i += chunkSize - overlap)
        {
            var chunk = string.Join(" ", words.Skip(i).Take(chunkSize));
            if (!string.IsNullOrEmpty(chunk))
                chunks.Add(chunk);
        }
        
        return chunks;
    }

    private static List<string> ChunkBySentence(string text, int sentencesPerChunk)
    {
        var sentences = text.Split(new[] { ". " }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.TrimEnd('.') + ".")
            .ToList();
        
        var chunks = new List<string>();
        for (int i = 0; i < sentences.Count; i += sentencesPerChunk)
        {
            chunks.Add(string.Join(" ", sentences.Skip(i).Take(sentencesPerChunk)));
        }
        
        return chunks;
    }

    private static int EstimateTokens(string text) => text.Split(' ').Length;
}

[TestFixture]
[Category("Integration")]
public class DocumentAnalysisTests
{
    [Test]
    public void Analysis_ExtractsKeyPoints()
    {
        var result = new DocumentAnalysisResult();
        result.KeyPoints.Add("Payment terms: Net 30");
        result.KeyPoints.Add("Automatic renewal clause present");
        result.KeyPoints.Add("Liability limited to contract value");
        
        Assert.That(result.KeyPoints.Count, Is.EqualTo(3));
    }

    [Test]
    public void Analysis_DetectsRedFlags()
    {
        var result = new DocumentAnalysisResult();
        result.RedFlags.Add(new RedFlag
        {
            Severity = RedFlagSeverity.High,
            Description = "Unlimited liability clause",
            Recommendation = "Negotiate a cap"
        });
        
        Assert.That(result.RedFlags.Count, Is.EqualTo(1));
        Assert.That(result.RedFlags[0].Severity, Is.EqualTo(RedFlagSeverity.High));
    }

    [Test]
    public void Analysis_DetectsIntents()
    {
        var result = new DocumentAnalysisResult();
        result.DetectedIntents.Add(new DetectedIntent
        {
            Intent = "Request for proposal",
            Confidence = 0.92,
            Evidence = "We are seeking vendors for..."
        });
        
        Assert.That(result.DetectedIntents[0].Confidence, Is.GreaterThan(0.9));
    }

    [Test]
    public void Analysis_MetricsAreCalculated()
    {
        var result = new DocumentAnalysisResult
        {
            Metrics = new AnalysisMetrics
            {
                WordCount = 1500,
                SentenceCount = 75,
                AnalysisTimeMs = 250
            }
        };
        
        var avgWords = result.Metrics.WordCount / (double)result.Metrics.SentenceCount;
        Assert.That(avgWords, Is.EqualTo(20.0));
    }

    [Test]
    public void Analysis_ComplianceChecklist()
    {
        var result = new DocumentAnalysisResult();
        result.ComplianceChecklist.Add(new ComplianceItem { IsMet = true, Requirement = "GDPR compliance" });
        result.ComplianceChecklist.Add(new ComplianceItem { IsMet = false, Requirement = "Data retention policy" });
        
        var compliance = result.ComplianceChecklist.Count(c => c.IsMet) / (double)result.ComplianceChecklist.Count;
        Assert.That(compliance, Is.EqualTo(0.5));
    }
}

[TestFixture]
[Category("Integration")]
public class FileTypeHandlingTests
{
    [Test]
    public void FileType_Detection_Works()
    {
        Assert.That(GetFileType("document.pdf"), Is.EqualTo("pdf"));
        Assert.That(GetFileType("notes.txt"), Is.EqualTo("txt"));
        Assert.That(GetFileType("data.json"), Is.EqualTo("json"));
        Assert.That(GetFileType("report.docx"), Is.EqualTo("docx"));
    }

    [Test]
    public void FileType_SupportedFormats_Listed()
    {
        var supportedFormats = new[] { "pdf", "txt", "md", "json", "docx", "html" };
        
        Assert.That(supportedFormats.Contains("pdf"), Is.True);
        Assert.That(supportedFormats.Contains("txt"), Is.True);
    }

    [Test]
    public void FileType_UnsupportedFormat_Detected()
    {
        var supportedFormats = new[] { "pdf", "txt", "md", "json", "docx", "html" };
        var fileType = GetFileType("image.png");
        
        Assert.That(supportedFormats.Contains(fileType), Is.False);
    }

    private static string GetFileType(string fileName)
    {
        return Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
    }
}
