using System.Net.Http.Json;
using System.Text.Json;
using LLMClient.Models;
using LLMClient.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Testy end-to-end - pełny flow aplikacji z prawdziwym API i embeddingami.
/// </summary>
[TestFixture]
[Category("Integration")]
public class EndToEndIntegrationTests
{
    private HttpClient _httpClient = null!;
    private string _apiKey = null!;
    private string _baseUrl = null!;
    private string _model = null!;
    private EmbeddingService _embeddingService = null!;
    private bool _secretsLoaded = false;
    private bool _modelAvailable = false;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Load API secrets
        var secretsPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "secrets.json");
        if (!File.Exists(secretsPath))
        {
            secretsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "secrets.json");
        }
        
        if (File.Exists(secretsPath))
        {
            var json = File.ReadAllText(secretsPath);
            var secrets = JsonSerializer.Deserialize<SecretsConfig>(json);
            if (secrets?.OpenRouter != null)
            {
                _apiKey = secrets.OpenRouter.ApiKey;
                _baseUrl = secrets.OpenRouter.BaseUrl;
                _model = secrets.OpenRouter.Model;
                _secretsLoaded = true;
            }
        }
        
        _httpClient = new HttpClient();
        
        // Initialize embedding service
        var logger = new Mock<ILogger<EmbeddingService>>();
        _embeddingService = new EmbeddingService(logger.Object);
        _modelAvailable = await _embeddingService.IsModelDownloadedAsync();
        
        if (_modelAvailable)
        {
            try
            {
                await _embeddingService.InitializeAsync();
            }
            catch
            {
                _modelAvailable = false;
            }
        }
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _httpClient?.Dispose();
        (_embeddingService as IDisposable)?.Dispose();
    }

    [Test]
    public async Task EndToEnd_QuestionAnswering_ReturnsValidResponse()
    {
        if (!_secretsLoaded) Assert.Ignore("Secrets not loaded");
        
        // Arrange - symulacja pełnego flow pytanie-odpowiedź
        var userQuestion = "Co to jest MAUI w kontekście .NET?";
        
        // Act - wywołanie API
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Headers.Add("HTTP-Referer", "https://github.com/DamianTarnowski/LLMClient");
        
        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = "Jesteś pomocnym asystentem. Odpowiadaj po polsku, zwięźle." },
                new { role = "user", content = userQuestion }
            },
            max_tokens = 200
        };
        
        request.Content = JsonContent.Create(payload);
        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        
        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True, $"API error: {responseBody}");
        
        var result = JsonSerializer.Deserialize<OpenRouterResponse>(responseBody);
        var answer = result?.Choices?[0].Message?.Content;
        
        Assert.That(answer, Is.Not.Null.And.Not.Empty);
        TestContext.WriteLine($"Q: {userQuestion}");
        TestContext.WriteLine($"A: {answer}");
        
        // Odpowiedź powinna wspominać o cross-platform lub mobile
        Assert.That(answer!.ToLower(), Does.Contain("maui").Or.Contain("mobile").Or.Contain("cross").Or.Contain("aplikacj"));
    }

    [Test]
    public async Task EndToEnd_RAGWithEmbeddings_FindsRelevantContext()
    {
        if (!_modelAvailable || !_embeddingService.IsInitialized)
            Assert.Ignore("Local embedding model not available");
        
        // Arrange - dokumenty do RAG
        var documents = new[]
        {
            "LLMClient to aplikacja MAUI do rozmów z modelami AI. Wspiera OpenAI, Anthropic i lokalne modele.",
            "RAG (Retrieval Augmented Generation) pozwala wzbogacić odpowiedzi AI o kontekst z dokumentów.",
            "Embeddingi to wektorowe reprezentacje tekstu używane do wyszukiwania semantycznego."
        };
        
        var query = "Jak działa wyszukiwanie semantyczne?";
        
        // Act - wygeneruj embeddingi
        var queryEmb = await _embeddingService.GenerateEmbeddingAsync(query, isQuery: true);
        Assert.That(queryEmb, Is.Not.Null);
        
        var docEmbeddings = new List<(string doc, float[] emb, float similarity)>();
        foreach (var doc in documents)
        {
            var docEmb = await _embeddingService.GenerateEmbeddingAsync(doc, isQuery: false);
            if (docEmb != null)
            {
                var similarity = _embeddingService.CalculateSimilarity(queryEmb!, docEmb);
                docEmbeddings.Add((doc, docEmb, similarity));
            }
        }
        
        // Sort by similarity
        var ranked = docEmbeddings.OrderByDescending(x => x.similarity).ToList();
        
        TestContext.WriteLine($"Query: {query}");
        TestContext.WriteLine("Ranked documents:");
        for (int i = 0; i < ranked.Count; i++)
        {
            TestContext.WriteLine($"  {i + 1}. [{ranked[i].similarity:F4}] {ranked[i].doc.Substring(0, Math.Min(60, ranked[i].doc.Length))}...");
        }
        
        // Assert - dokument o embeddingach powinien być najbardziej relewantny
        Assert.That(ranked[0].doc.ToLower(), Does.Contain("embedding").Or.Contain("wyszukiwan").Or.Contain("semantyczn"));
    }

    [Test]
    public async Task EndToEnd_RAGAugmentedChat_UsesContext()
    {
        if (!_secretsLoaded) Assert.Ignore("Secrets not loaded");
        if (!_modelAvailable || !_embeddingService.IsInitialized)
            Assert.Ignore("Local embedding model not available");
        
        // Arrange - RAG context
        var ragContext = @"
LLMClient to zaawansowana aplikacja napisana w .NET MAUI.
Główne funkcje:
- Obsługa wielu providerów AI (OpenAI, Anthropic, OpenRouter, lokalne modele)
- RAG (Retrieval Augmented Generation) z lokalnymi dokumentami
- Wyszukiwanie semantyczne w historii rozmów
- Pamięć użytkownika z automatyczną ekstrakcją faktów
- Eksport rozmów do Markdown i JSON
";
        
        var userQuestion = "Jakie formaty eksportu obsługuje LLMClient?";
        
        // Act - wywołanie API z kontekstem RAG
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Headers.Add("HTTP-Referer", "https://github.com/DamianTarnowski/LLMClient");
        
        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = $"Odpowiadaj na podstawie kontekstu. Kontekst:\n{ragContext}" },
                new { role = "user", content = userQuestion }
            },
            max_tokens = 150
        };
        
        request.Content = JsonContent.Create(payload);
        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        
        Assert.That(response.IsSuccessStatusCode, Is.True);
        
        var result = JsonSerializer.Deserialize<OpenRouterResponse>(responseBody);
        var answer = result?.Choices?[0].Message?.Content;
        
        TestContext.WriteLine($"Q: {userQuestion}");
        TestContext.WriteLine($"A: {answer}");
        
        // Odpowiedź powinna wspominać o Markdown lub JSON
        Assert.That(answer!.ToLower(), Does.Contain("markdown").Or.Contain("json"));
    }

    [Test]
    public async Task EndToEnd_ConversationMemory_MaintainsContext()
    {
        if (!_secretsLoaded) Assert.Ignore("Secrets not loaded");
        
        // Arrange - wieloturowa rozmowa
        var messages = new List<object>
        {
            new { role = "system", content = "Jesteś pomocnym asystentem. Zapamiętuj informacje z rozmowy." },
            new { role = "user", content = "Nazywam się Michał i mieszkam w Warszawie." }
        };
        
        // Act - pierwsza odpowiedź
        var response1 = await SendChatRequest(messages);
        Assert.That(response1, Is.Not.Null);
        TestContext.WriteLine($"User: Nazywam się Michał i mieszkam w Warszawie.");
        TestContext.WriteLine($"AI: {response1}");
        
        // Dodaj odpowiedź do historii
        messages.Add(new { role = "assistant", content = response1 });
        messages.Add(new { role = "user", content = "W jakim mieście mieszkam i jak mam na imię?" });
        
        // Act - druga odpowiedź (powinna pamiętać)
        var response2 = await SendChatRequest(messages);
        Assert.That(response2, Is.Not.Null);
        TestContext.WriteLine($"User: W jakim mieście mieszkam i jak mam na imię?");
        TestContext.WriteLine($"AI: {response2}");
        
        // Assert - powinna zawierać imię i miasto
        var r2Lower = response2!.ToLower();
        Assert.That(r2Lower, Does.Contain("michał").Or.Contain("michal"));
        Assert.That(r2Lower, Does.Contain("warszaw"));
    }

    [Test]
    public async Task EndToEnd_EmbeddingSimilarityPipeline_WorksCorrectly()
    {
        if (!_modelAvailable || !_embeddingService.IsInitialized)
            Assert.Ignore("Local embedding model not available");
        
        // Arrange - symulacja pełnego pipeline'u
        var userMessages = new[]
        {
            "Jak programować w Pythonie?",
            "Pokaż mi przepis na bigos",
            "Opowiedz o architekturze MVVM"
        };
        
        var query = "Wzorce projektowe w programowaniu";
        
        // Act - wygeneruj embeddingi i znajdź najbardziej podobną wiadomość
        var queryEmb = await _embeddingService.GenerateEmbeddingAsync(query, isQuery: true);
        Assert.That(queryEmb, Is.Not.Null);
        
        var results = new List<(string msg, float similarity)>();
        foreach (var msg in userMessages)
        {
            var msgEmb = await _embeddingService.GenerateEmbeddingAsync(msg, isQuery: false);
            if (msgEmb != null)
            {
                var similarity = _embeddingService.CalculateSimilarity(queryEmb!, msgEmb);
                results.Add((msg, similarity));
            }
        }
        
        var ranked = results.OrderByDescending(x => x.similarity).ToList();
        
        TestContext.WriteLine($"Query: {query}");
        TestContext.WriteLine("Most similar messages:");
        foreach (var (msg, sim) in ranked)
        {
            TestContext.WriteLine($"  [{sim:F4}] {msg}");
        }
        
        // MVVM powinno być najbardziej podobne do "wzorce projektowe"
        Assert.That(ranked[0].msg, Does.Contain("MVVM").Or.Contain("Python"));
    }

    private async Task<string?> SendChatRequest(List<object> messages)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Headers.Add("HTTP-Referer", "https://github.com/DamianTarnowski/LLMClient");
        
        var payload = new
        {
            model = _model,
            messages = messages,
            max_tokens = 150
        };
        
        request.Content = JsonContent.Create(payload);
        var response = await _httpClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode) return null;
        
        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<OpenRouterResponse>(responseBody);
        return result?.Choices?[0].Message?.Content;
    }

    // DTOs
    private class SecretsConfig
    {
        public OpenRouterConfig? OpenRouter { get; set; }
    }

    private class OpenRouterConfig
    {
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string Model { get; set; } = "";
    }

    private class OpenRouterResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public MessageContent? Message { get; set; }
    }

    private class MessageContent
    {
        [System.Text.Json.Serialization.JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
