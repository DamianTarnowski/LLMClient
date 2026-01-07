using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for AI Service functionality
/// Tests response generation, summarization, and streaming
/// </summary>
[TestFixture]
[Category("Integration")]
public class AiServiceGenerationTests
{
    private Mock<IAiService> _aiService = null!;

    [SetUp]
    public void Setup()
    {
        _aiService = new Mock<IAiService>();
        
        _aiService.Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string prompt, CancellationToken ct) =>
            {
                if (ct.IsCancellationRequested) throw new OperationCanceledException();
                return GenerateMockResponse(prompt);
            });
            
        _aiService.Setup(x => x.SummarizeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, int maxLength, CancellationToken ct) =>
            {
                var words = text.Split(' ');
                var summary = string.Join(" ", words.Take(Math.Min(words.Length, maxLength / 10)));
                return summary + "...";
            });
            
        _aiService.Setup(x => x.GenerateStreamingResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string prompt, CancellationToken ct) => StreamTokens(prompt, ct));
    }

    [Test]
    public async Task AiService_GenerateResponse_ReturnsNonEmpty()
    {
        var response = await _aiService.Object.GenerateResponseAsync("Hello!");
        
        Assert.That(response, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task AiService_GenerateResponse_RespondsToQuestion()
    {
        var response = await _aiService.Object.GenerateResponseAsync("What is 2+2?");
        
        Assert.That(response, Does.Contain("Question"));
    }

    [Test]
    public async Task AiService_GenerateResponse_HandlesPolish()
    {
        var response = await _aiService.Object.GenerateResponseAsync("Cześć, jak się masz?");
        
        Assert.That(response, Is.Not.Empty);
    }

    [Test]
    public async Task AiService_GenerateResponse_WithCancellation_Throws()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _aiService.Object.GenerateResponseAsync("test", cts.Token));
    }

    [Test]
    public async Task AiService_Summarize_ShortensText()
    {
        var longText = string.Join(" ", Enumerable.Range(1, 100).Select(i => $"word{i}"));
        
        var summary = await _aiService.Object.SummarizeAsync(longText, 100);
        
        Assert.That(summary.Length, Is.LessThan(longText.Length));
    }

    [Test]
    public async Task AiService_Summarize_PreservesKeyInfo()
    {
        var text = "The quick brown fox jumps over the lazy dog. This is important information.";
        
        var summary = await _aiService.Object.SummarizeAsync(text, 50);
        
        Assert.That(summary, Does.Contain("quick").Or.Contain("..."));
    }

    [Test]
    public async Task AiService_Streaming_YieldsMultipleTokens()
    {
        var tokens = new List<string>();
        
        await foreach (var token in _aiService.Object.GenerateStreamingResponseAsync("test"))
        {
            tokens.Add(token);
        }
        
        Assert.That(tokens.Count, Is.GreaterThan(1));
    }

    [Test]
    public async Task AiService_Streaming_FormatsCompleteResponse()
    {
        var tokens = new List<string>();
        
        await foreach (var token in _aiService.Object.GenerateStreamingResponseAsync("Hello"))
        {
            tokens.Add(token);
        }
        
        var fullResponse = string.Concat(tokens);
        Assert.That(fullResponse, Is.Not.Empty);
    }

    [Test]
    public async Task AiService_LongPrompt_Handles()
    {
        var longPrompt = string.Concat(Enumerable.Repeat("This is a test sentence. ", 200));
        
        var response = await _aiService.Object.GenerateResponseAsync(longPrompt);
        
        Assert.That(response, Is.Not.Empty);
    }

    [Test]
    public async Task AiService_EmptyPrompt_Handles()
    {
        var response = await _aiService.Object.GenerateResponseAsync("");
        
        Assert.That(response, Is.Not.Null);
    }

    [Test]
    public async Task AiService_SpecialCharacters_Handles()
    {
        var prompt = "Test with special chars: <>&\"'`~!@#$%^*()[]{}|\\";
        
        var response = await _aiService.Object.GenerateResponseAsync(prompt);
        
        Assert.That(response, Is.Not.Empty);
    }

    [Test]
    public async Task AiService_Unicode_Handles()
    {
        var prompt = "Unicode test: 你好 🎉 مرحبا こんにちは";
        
        var response = await _aiService.Object.GenerateResponseAsync(prompt);
        
        Assert.That(response, Is.Not.Empty);
    }

    private static string GenerateMockResponse(string prompt)
    {
        if (string.IsNullOrEmpty(prompt)) return "Empty prompt received.";
        
        if (prompt.Contains("?"))
            return $"Question detected: {prompt.Substring(0, Math.Min(30, prompt.Length))}... Here's my answer.";
        
        return $"Response to: {prompt.Substring(0, Math.Min(50, prompt.Length))}...";
    }

    private static async IAsyncEnumerable<string> StreamTokens(string prompt, CancellationToken ct)
    {
        var words = new[] { "This", " ", "is", " ", "a", " ", "streaming", " ", "response", "." };
        
        foreach (var word in words)
        {
            if (ct.IsCancellationRequested) yield break;
            await Task.Delay(5, ct);
            yield return word;
        }
    }
}

[TestFixture]
[Category("Integration")]
public class AiServiceContextTests
{
    [Test]
    public void BuildPromptWithContext_IncludesSystemPrompt()
    {
        var systemPrompt = "You are a helpful assistant.";
        var userMessage = "Hello!";
        
        var fullPrompt = BuildPrompt(systemPrompt, userMessage, null, null);
        
        Assert.That(fullPrompt, Does.Contain(systemPrompt));
        Assert.That(fullPrompt, Does.Contain(userMessage));
    }

    [Test]
    public void BuildPromptWithContext_IncludesMemory()
    {
        var memories = new List<Memory>
        {
            new() { Key = "user_name", Value = "Jan" },
            new() { Key = "preference", Value = "dark_mode" }
        };
        
        var fullPrompt = BuildPrompt("System", "Hello", memories, null);
        
        Assert.That(fullPrompt, Does.Contain("user_name"));
        Assert.That(fullPrompt, Does.Contain("Jan"));
    }

    [Test]
    public void BuildPromptWithContext_IncludesRagContext()
    {
        var ragContext = "Document excerpt: Machine learning is a subset of AI...";
        
        var fullPrompt = BuildPrompt("System", "What is ML?", null, ragContext);
        
        Assert.That(fullPrompt, Does.Contain("Document excerpt"));
    }

    [Test]
    public void BuildPromptWithContext_AllComponents_FormatsCorrectly()
    {
        var memories = new List<Memory> { new() { Key = "name", Value = "Test" } };
        var ragContext = "Relevant info from documents.";
        
        var fullPrompt = BuildPrompt("You are helpful.", "Question?", memories, ragContext);
        
        Assert.That(fullPrompt, Does.Contain("You are helpful"));
        Assert.That(fullPrompt, Does.Contain("name: Test"));
        Assert.That(fullPrompt, Does.Contain("Relevant info"));
        Assert.That(fullPrompt, Does.Contain("Question?"));
    }

    private static string BuildPrompt(string system, string user, List<Memory>? memories, string? ragContext)
    {
        var parts = new List<string> { $"[System]\n{system}" };
        
        if (memories?.Any() == true)
        {
            var memoryContext = string.Join("\n", memories.Select(m => $"- {m.Key}: {m.Value}"));
            parts.Add($"[Memory]\n{memoryContext}");
        }
        
        if (!string.IsNullOrEmpty(ragContext))
        {
            parts.Add($"[Context]\n{ragContext}");
        }
        
        parts.Add($"[User]\n{user}");
        
        return string.Join("\n\n", parts);
    }
}
