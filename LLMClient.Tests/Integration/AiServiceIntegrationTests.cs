using System.Text.Json;
using LLMClient.Models;
using LLMClient.Services;
using NUnit.Framework;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Testy integracyjne AiService z prawdziwym API OpenRouter.
/// </summary>
[TestFixture]
[Category("Integration")]
public class AiServiceIntegrationTests
{
    private AiService _aiService = null!;
    private AiModel _testModel = null!;
    private bool _secretsLoaded = false;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
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
                _testModel = new AiModel
                {
                    Id = 9999, // Test ID
                    Name = "Test Model (Mistral)",
                    Provider = AiProvider.OpenRouter,
                    ModelId = secrets.OpenRouter.Model,
                    ApiKey = secrets.OpenRouter.ApiKey,
                    Endpoint = secrets.OpenRouter.BaseUrl,
                    IsActive = true,
                    SupportsStreaming = true
                };
                
                _aiService = new AiService(null, null, null);
                await _aiService.UpdateConfiguration(_testModel);
                _secretsLoaded = true;
            }
        }
    }

    private void EnsureSecretsLoaded()
    {
        if (!_secretsLoaded)
        {
            Assert.Ignore("Plik secrets.json nie został znaleziony. Pomiń testy integracyjne.");
        }
    }

    [Test]
    public async Task GetResponseAsync_SimpleQuestion_ReturnsAnswer()
    {
        EnsureSecretsLoaded();
        
        // Arrange
        var userMessage = "Odpowiedz jednym słowem: 2 + 2 = ?";
        var history = new List<Message>
        {
            new() { Content = userMessage, IsUser = true, Timestamp = DateTime.Now }
        };
        
        // Act
        var response = await _aiService.GetResponseAsync(userMessage, history);
        
        // Assert
        Assert.That(response, Is.Not.Null.And.Not.Empty);
        TestContext.WriteLine($"Response: {response}");
        Assert.That(response.ToLower(), Does.Contain("4").Or.Contain("cztery").Or.Contain("four"));
    }

    [Test]
    public async Task GetResponseAsync_ShortQuestion_ReturnsShortAnswer()
    {
        EnsureSecretsLoaded();
        
        // Arrange
        var userMessage = "Odpowiedz TAK lub NIE: Czy 5 jest większe od 3?";
        var history = new List<Message>
        {
            new() { Content = userMessage, IsUser = true, Timestamp = DateTime.Now }
        };
        
        // Act
        var response = await _aiService.GetResponseAsync(userMessage, history);
        
        // Assert
        Assert.That(response, Is.Not.Null.And.Not.Empty);
        TestContext.WriteLine($"Short answer response: {response}");
    }

    [Test]
    public async Task GetStreamingResponseAsync_ReceivesChunks()
    {
        EnsureSecretsLoaded();
        
        // Arrange
        var userMessage = "Wymień 5 kolorów.";
        var history = new List<Message>
        {
            new() { Content = userMessage, IsUser = true, Timestamp = DateTime.Now }
        };
        
        var chunks = new List<string>();
        
        // Act
        await foreach (var chunk in _aiService.GetStreamingResponseAsync(userMessage, history))
        {
            chunks.Add(chunk);
        }
        
        // Assert
        Assert.That(chunks, Is.Not.Empty);
        
        var fullResponse = string.Join("", chunks);
        TestContext.WriteLine($"Streaming response ({chunks.Count} chunks): {fullResponse}");
        
        Assert.That(fullResponse, Is.Not.Empty);
        Assert.That(chunks.Count, Is.GreaterThan(1), "Powinno być więcej niż 1 chunk");
    }

    [Test]
    public async Task GetResponseAsync_ConversationHistory_MaintainsContext()
    {
        EnsureSecretsLoaded();
        
        // Arrange - buduj historię rozmowy
        var history = new List<Message>
        {
            new() { Content = "Mam na imię Tomasz.", IsUser = true, Timestamp = DateTime.Now },
            new() { Content = "Miło mi Cię poznać, Tomasz!", IsUser = false, Timestamp = DateTime.Now.AddSeconds(1) }
        };
        
        var userMessage = "Jak mam na imię?";
        history.Add(new() { Content = userMessage, IsUser = true, Timestamp = DateTime.Now.AddSeconds(2) });
        
        // Act
        var response = await _aiService.GetResponseAsync(userMessage, history);
        
        // Assert
        Assert.That(response, Is.Not.Null.And.Not.Empty);
        TestContext.WriteLine($"Context-aware response: {response}");
        Assert.That(response.ToLower(), Does.Contain("tomasz"), "AI powinno pamiętać imię");
    }

    [Test]
    public async Task GetResponseAsync_PolishLanguage_RespondsInPolish()
    {
        EnsureSecretsLoaded();
        
        // Arrange
        var userMessage = "Opowiedz mi w dwóch zdaniach o Polsce.";
        var history = new List<Message>
        {
            new() { Content = userMessage, IsUser = true, Timestamp = DateTime.Now }
        };
        
        // Act
        var response = await _aiService.GetResponseAsync(userMessage, history);
        
        // Assert
        Assert.That(response, Is.Not.Null.And.Not.Empty);
        TestContext.WriteLine($"Polish response: {response}");
        
        // Sprawdź czy odpowiedź zawiera polskie znaki (oznaka odpowiedzi po polsku)
        var hasPolishChars = response.Any(c => "ąćęłńóśźżĄĆĘŁŃÓŚŹŻ".Contains(c));
        Assert.That(hasPolishChars, Is.True, "Odpowiedź powinna być po polsku");
    }

    // DTO
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
}
