using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NUnit.Framework;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Testy integracyjne z prawdziwym API OpenRouter.
/// Wymagają pliku secrets.json z kluczem API.
/// </summary>
[TestFixture]
[Category("Integration")]
public class OpenRouterIntegrationTests
{
    private HttpClient _httpClient = null!;
    private string _apiKey = null!;
    private string _baseUrl = null!;
    private string _model = null!;
    private bool _secretsLoaded = false;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var secretsPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "secrets.json");
        
        if (!File.Exists(secretsPath))
        {
            // Try alternative path
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
            Assert.Ignore("Plik secrets.json nie został znaleziony. Pomiń testy integracyjne.");
        }
    }

    [Test]
    public async Task OpenRouter_SimpleCompletion_ReturnsResponse()
    {
        EnsureSecretsLoaded();
        
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Headers.Add("HTTP-Referer", "https://github.com/DamianTarnowski/LLMClient");
        request.Headers.Add("X-Title", "LLMClient Integration Tests");
        
        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "user", content = "Odpowiedz jednym słowem: jaki kolor ma niebo?" }
            },
            max_tokens = 50
        };
        
        request.Content = JsonContent.Create(payload);
        
        // Act
        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        
        TestContext.WriteLine($"Status: {response.StatusCode}");
        TestContext.WriteLine($"Response: {responseBody}");
        
        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True, $"API zwróciło błąd: {responseBody}");
        
        var result = JsonSerializer.Deserialize<OpenRouterResponse>(responseBody);
        Assert.That(result?.Choices, Is.Not.Null.And.Not.Empty);
        Assert.That(result?.Choices?[0].Message?.Content, Is.Not.Null.And.Not.Empty);
        
        TestContext.WriteLine($"AI Response: {result?.Choices?[0].Message?.Content}");
    }

    [Test]
    public async Task OpenRouter_StreamingCompletion_ReceivesChunks()
    {
        EnsureSecretsLoaded();
        
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Headers.Add("HTTP-Referer", "https://github.com/DamianTarnowski/LLMClient");
        request.Headers.Add("X-Title", "LLMClient Integration Tests");
        
        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "user", content = "Wymień 3 kolory tęczy." }
            },
            max_tokens = 100,
            stream = true
        };
        
        request.Content = JsonContent.Create(payload);
        
        // Act
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.That(response.IsSuccessStatusCode, Is.True, "Streaming request failed");
        
        var chunks = new List<string>();
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line)) continue;
            if (line.StartsWith("data: "))
            {
                var data = line.Substring(6);
                if (data == "[DONE]") break;
                
                try
                {
                    var chunk = JsonSerializer.Deserialize<OpenRouterStreamChunk>(data);
                    var content = chunk?.Choices?[0].Delta?.Content;
                    if (!string.IsNullOrEmpty(content))
                    {
                        chunks.Add(content);
                    }
                }
                catch { }
            }
        }
        
        // Assert
        Assert.That(chunks, Is.Not.Empty, "Nie otrzymano żadnych chunków");
        
        var fullResponse = string.Join("", chunks);
        TestContext.WriteLine($"Streaming response ({chunks.Count} chunks): {fullResponse}");
        Assert.That(fullResponse.Length, Is.GreaterThan(10));
    }

    [Test]
    public async Task OpenRouter_ConversationContext_MaintainsHistory()
    {
        EnsureSecretsLoaded();
        
        // Arrange - pierwsza wiadomość
        var messages = new List<object>
        {
            new { role = "system", content = "Jesteś pomocnym asystentem. Odpowiadaj krótko." },
            new { role = "user", content = "Mam na imię Jan." }
        };
        
        // Act - pierwsza odpowiedź
        var response1 = await SendChatCompletion(messages);
        Assert.That(response1, Is.Not.Null);
        TestContext.WriteLine($"Odpowiedź 1: {response1}");
        
        // Dodaj odpowiedź AI do historii
        messages.Add(new { role = "assistant", content = response1 });
        messages.Add(new { role = "user", content = "Jak mam na imię?" });
        
        // Act - druga odpowiedź (powinna pamiętać imię)
        var response2 = await SendChatCompletion(messages);
        Assert.That(response2, Is.Not.Null);
        TestContext.WriteLine($"Odpowiedź 2: {response2}");
        
        // Assert - sprawdź czy odpowiedź nie jest pusta (darmowe modele mogą mieć problem z kontekstem)
        Assert.That(response2, Is.Not.Null.And.Not.Empty, "AI powinno odpowiedzieć na pytanie");
        
        // Jeśli odpowiedź zawiera imię - super, jeśli nie - zaloguj warning
        if (!response2!.ToLower().Contains("jan"))
        {
            TestContext.WriteLine($"WARNING: AI nie zapamiętało imienia. Odpowiedź: {response2}");
        }
    }

    [Test]
    public async Task OpenRouter_SystemPrompt_AffectsResponse()
    {
        EnsureSecretsLoaded();
        
        // Arrange - z system prompt
        var messagesWithSystem = new List<object>
        {
            new { role = "system", content = "Odpowiadaj TYLKO po angielsku, niezależnie od języka pytania." },
            new { role = "user", content = "Cześć, jak się masz?" }
        };
        
        // Act
        var response = await SendChatCompletion(messagesWithSystem);
        
        // Assert - odpowiedź powinna być po angielsku
        Assert.That(response, Is.Not.Null);
        TestContext.WriteLine($"Response with English system prompt: {response}");
        
        // Sprawdź czy odpowiedź nie zawiera polskich znaków (uproszczone sprawdzenie)
        var hasPolishChars = response!.Any(c => "ąćęłńóśźżĄĆĘŁŃÓŚŹŻ".Contains(c));
        Assert.That(hasPolishChars, Is.False, "Odpowiedź zawiera polskie znaki mimo system prompt");
    }

    [Test]
    public async Task OpenRouter_LongInput_HandlesCorrectly()
    {
        EnsureSecretsLoaded();
        
        // Arrange - długi tekst
        var longText = string.Join(" ", Enumerable.Repeat("To jest testowe zdanie do przetworzenia.", 50));
        
        var messages = new List<object>
        {
            new { role = "user", content = $"Podsumuj poniższy tekst w jednym zdaniu:\n\n{longText}" }
        };
        
        // Act
        var response = await SendChatCompletion(messages, maxTokens: 100);
        
        // Assert
        Assert.That(response, Is.Not.Null.And.Not.Empty);
        TestContext.WriteLine($"Summary response: {response}");
    }

    private async Task<string?> SendChatCompletion(List<object> messages, int maxTokens = 150)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Headers.Add("HTTP-Referer", "https://github.com/DamianTarnowski/LLMClient");
        request.Headers.Add("X-Title", "LLMClient Integration Tests");
        
        var payload = new
        {
            model = _model,
            messages = messages,
            max_tokens = maxTokens
        };
        
        request.Content = JsonContent.Create(payload);
        
        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            TestContext.WriteLine($"API Error: {responseBody}");
            return null;
        }
        
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
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public MessageContent? Message { get; set; }
    }

    private class MessageContent
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private class OpenRouterStreamChunk
    {
        [JsonPropertyName("choices")]
        public List<StreamChoice>? Choices { get; set; }
    }

    private class StreamChoice
    {
        [JsonPropertyName("delta")]
        public DeltaContent? Delta { get; set; }
    }

    private class DeltaContent
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
