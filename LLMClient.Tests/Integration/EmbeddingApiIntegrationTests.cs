using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NUnit.Framework;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Testy integracyjne API Embedding Service przez OpenRouter.
/// </summary>
[TestFixture]
[Category("Integration")]
public class EmbeddingApiIntegrationTests
{
    private HttpClient _httpClient = null!;
    private string _apiKey = null!;
    private string _baseUrl = null!;
    private bool _secretsLoaded = false;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
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
                _secretsLoaded = true;
            }
        }
        
        _httpClient = new HttpClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _httpClient?.Dispose();
    }

    private void EnsureSecretsLoaded()
    {
        if (!_secretsLoaded)
        {
            Assert.Ignore("Plik secrets.json nie został znaleziony.");
        }
    }

    [Test]
    public async Task GenerateEmbedding_SingleText_ReturnsVector()
    {
        EnsureSecretsLoaded();
        
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/embeddings");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Headers.Add("HTTP-Referer", "https://github.com/DamianTarnowski/LLMClient");
        
        var payload = new
        {
            model = "openai/text-embedding-3-small",
            input = "To jest testowy tekst do wygenerowania embeddingu."
        };
        
        request.Content = JsonContent.Create(payload);
        
        // Act
        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        
        TestContext.WriteLine($"Status: {response.StatusCode}");
        
        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True, $"API Error: {responseBody}");
        
        var result = JsonSerializer.Deserialize<EmbeddingResponse>(responseBody);
        Assert.That(result?.Data, Is.Not.Null.And.Not.Empty);
        Assert.That(result?.Data?[0].Embedding, Is.Not.Null.And.Not.Empty);
        
        var embeddingLength = result?.Data?[0].Embedding?.Length ?? 0;
        TestContext.WriteLine($"Embedding dimensions: {embeddingLength}");
        Assert.That(embeddingLength, Is.GreaterThan(100), "Embedding powinien mieć więcej niż 100 wymiarów");
    }

    [Test]
    public async Task GenerateEmbedding_MultipleTexts_ReturnsBatchVectors()
    {
        EnsureSecretsLoaded();
        
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/embeddings");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Headers.Add("HTTP-Referer", "https://github.com/DamianTarnowski/LLMClient");
        
        var texts = new[]
        {
            "Programowanie w C# jest przyjemne.",
            "Python jest świetny do machine learning.",
            "JavaScript dominuje w web development."
        };
        
        var payload = new
        {
            model = "openai/text-embedding-3-small",
            input = texts
        };
        
        request.Content = JsonContent.Create(payload);
        
        // Act
        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        
        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True, $"API Error: {responseBody}");
        
        var result = JsonSerializer.Deserialize<EmbeddingResponse>(responseBody);
        Assert.That(result?.Data?.Count, Is.EqualTo(3), "Powinny być 3 embeddingi");
        
        TestContext.WriteLine($"Generated {result?.Data?.Count} embeddings");
        foreach (var item in result?.Data ?? new List<EmbeddingData>())
        {
            TestContext.WriteLine($"  Index {item.Index}: {item.Embedding?.Length} dimensions");
        }
    }

    [Test]
    public async Task GenerateEmbedding_SimilarTexts_HaveHighSimilarity()
    {
        EnsureSecretsLoaded();
        
        // Arrange - dwa podobne teksty
        var text1 = "Koty są wspaniałymi zwierzętami domowymi.";
        var text2 = "Psy są doskonałymi towarzyszami człowieka.";
        var text3 = "Programowanie komputerów wymaga logicznego myślenia.";
        
        // Act - wygeneruj embeddingi
        var embedding1 = await GenerateEmbedding(text1);
        var embedding2 = await GenerateEmbedding(text2);
        var embedding3 = await GenerateEmbedding(text3);
        
        Assert.That(embedding1, Is.Not.Null);
        Assert.That(embedding2, Is.Not.Null);
        Assert.That(embedding3, Is.Not.Null);
        
        // Oblicz podobieństwo cosinusowe
        var similarity12 = CalculateCosineSimilarity(embedding1!, embedding2!);
        var similarity13 = CalculateCosineSimilarity(embedding1!, embedding3!);
        var similarity23 = CalculateCosineSimilarity(embedding2!, embedding3!);
        
        TestContext.WriteLine($"Similarity (koty vs psy): {similarity12:F4}");
        TestContext.WriteLine($"Similarity (koty vs programowanie): {similarity13:F4}");
        TestContext.WriteLine($"Similarity (psy vs programowanie): {similarity23:F4}");
        
        // Assert - teksty o zwierzętach powinny być bardziej podobne niż tekst o programowaniu
        Assert.That(similarity12, Is.GreaterThan(similarity13), 
            "Teksty o zwierzętach powinny być bardziej podobne");
        Assert.That(similarity12, Is.GreaterThan(similarity23), 
            "Teksty o zwierzętach powinny być bardziej podobne");
    }

    [Test]
    public async Task GenerateEmbedding_PolishAndEnglish_WorksForBoth()
    {
        EnsureSecretsLoaded();
        
        // Arrange
        var polishText = "Sztuczna inteligencja zmienia świat technologii.";
        var englishText = "Artificial intelligence is changing the technology world.";
        
        // Act
        var polishEmbedding = await GenerateEmbedding(polishText);
        var englishEmbedding = await GenerateEmbedding(englishText);
        
        Assert.That(polishEmbedding, Is.Not.Null);
        Assert.That(englishEmbedding, Is.Not.Null);
        
        // Podobne znaczenie powinno dawać wysoki similarity
        var similarity = CalculateCosineSimilarity(polishEmbedding!, englishEmbedding!);
        TestContext.WriteLine($"Polish-English similarity (same meaning): {similarity:F4}");
        
        // Assert - te same znaczenia w różnych językach powinny mieć umiarkowane podobieństwo
        Assert.That(similarity, Is.GreaterThan(0.5), 
            "Teksty o tym samym znaczeniu powinny mieć podobieństwo > 0.5");
    }

    [Test]
    public async Task GenerateEmbedding_ForRAGQuery_WorksCorrectly()
    {
        EnsureSecretsLoaded();
        
        // Arrange - symulacja RAG: dokumenty i zapytanie
        var documents = new[]
        {
            "MAUI to framework do tworzenia aplikacji cross-platform w .NET.",
            "React Native pozwala tworzyć aplikacje mobilne w JavaScript.",
            "Flutter używa języka Dart do budowy aplikacji mobilnych."
        };
        
        var query = "Jak tworzyć aplikacje mobilne w .NET?";
        
        // Act - wygeneruj embeddingi
        var queryEmbedding = await GenerateEmbedding(query);
        Assert.That(queryEmbedding, Is.Not.Null);
        
        var documentSimilarities = new List<(string doc, float similarity)>();
        
        foreach (var doc in documents)
        {
            var docEmbedding = await GenerateEmbedding(doc);
            Assert.That(docEmbedding, Is.Not.Null);
            
            var similarity = CalculateCosineSimilarity(queryEmbedding!, docEmbedding!);
            documentSimilarities.Add((doc, similarity));
        }
        
        // Sort by similarity
        var ranked = documentSimilarities.OrderByDescending(x => x.similarity).ToList();
        
        TestContext.WriteLine("RAG Ranking:");
        for (int i = 0; i < ranked.Count; i++)
        {
            TestContext.WriteLine($"  {i + 1}. [{ranked[i].similarity:F4}] {ranked[i].doc.Substring(0, Math.Min(50, ranked[i].doc.Length))}...");
        }
        
        // Assert - MAUI (.NET) powinno być najbardziej relewantne
        Assert.That(ranked[0].doc, Does.Contain("MAUI").Or.Contain(".NET"), 
            "Dokument o MAUI/.NET powinien być najbardziej relewantny dla zapytania o .NET");
    }

    private async Task<float[]?> GenerateEmbedding(string text)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/embeddings");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Headers.Add("HTTP-Referer", "https://github.com/DamianTarnowski/LLMClient");
        
        var payload = new
        {
            model = "openai/text-embedding-3-small",
            input = text
        };
        
        request.Content = JsonContent.Create(payload);
        
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;
        
        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<EmbeddingResponse>(responseBody);
        return result?.Data?[0].Embedding;
    }

    private static float CalculateCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        
        float dotProduct = 0;
        float normA = 0;
        float normB = 0;
        
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        
        var magnitude = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return magnitude > 0 ? dotProduct / magnitude : 0;
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

    private class EmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData>? Data { get; set; }
        
        [JsonPropertyName("model")]
        public string? Model { get; set; }
        
        [JsonPropertyName("usage")]
        public UsageInfo? Usage { get; set; }
    }

    private class EmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
        
        [JsonPropertyName("index")]
        public int Index { get; set; }
    }

    private class UsageInfo
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }
        
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}
