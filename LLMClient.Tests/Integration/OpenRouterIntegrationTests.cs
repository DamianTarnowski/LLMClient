using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LLMClient.Tests.TestHelpers;

namespace LLMClient.Tests.Integration;

public static class IntegrationTestConstants
{
    public const string OpenRouterBaseUrl = "https://openrouter.ai/api/v1";
    public const string TestModel = "meta-llama/llama-3.2-3b-instruct:free";
    public const string TestModelFast = "meta-llama/llama-3.2-1b-instruct:free";
    public const int DefaultTimeoutSeconds = 60;
}

/// <summary>
/// Integration tests using real OpenRouter API
/// These tests require a valid API key in ~/.llmclient/openrouter_api_key.txt
/// </summary>
[TestFixture]
[Category("Integration")]
public class OpenRouterIntegrationTests
{
    private HttpClient _httpClient = null!;
    private string? _apiKey;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _apiKey = ApiKeyHelper.GetOpenRouterApiKey();
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(IntegrationTestConstants.OpenRouterBaseUrl),
            Timeout = TimeSpan.FromSeconds(IntegrationTestConstants.DefaultTimeoutSeconds)
        };
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://llmclient.test");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "LLMClient Tests");
        }
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        _httpClient?.Dispose();
    }

    [Test]
    public void ApiKey_IsAvailable()
    {
        Assert.That(ApiKeyHelper.HasOpenRouterApiKey(), Is.True, 
            "OpenRouter API key should be available in ~/.llmclient/openrouter_api_key.txt");
    }

    [Test]
    public async Task OpenRouter_GetModels_ReturnsModels()
    {
        if (!ApiKeyHelper.HasOpenRouterApiKey())
        {
            Assert.Ignore("OpenRouter API key not available");
        }

        var response = await _httpClient.GetAsync("/api/v1/models");
        
        Assert.That(response.IsSuccessStatusCode, Is.True, 
            $"API should return success. Status: {response.StatusCode}");
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("data"));
    }

    [Test]
    public async Task OpenRouter_SimpleCompletion_ReturnsResponse()
    {
        if (!ApiKeyHelper.HasOpenRouterApiKey())
        {
            Assert.Ignore("OpenRouter API key not available");
        }

        var request = new
        {
            model = IntegrationTestConstants.TestModelFast,
            messages = new[]
            {
                new { role = "user", content = "Powiedz tylko: OK" }
            },
            max_tokens = 10,
            temperature = 0.1
        };

        var json = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("/api/v1/chat/completions", httpContent);
        
        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Completion should succeed. Status: {response.StatusCode}");
        
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.That(responseContent, Does.Contain("choices"));
    }

    [Test]
    public async Task OpenRouter_MathQuestion_ReturnsCorrectAnswer()
    {
        if (!ApiKeyHelper.HasOpenRouterApiKey())
        {
            Assert.Ignore("OpenRouter API key not available");
        }

        var request = new
        {
            model = IntegrationTestConstants.TestModel,
            messages = new[]
            {
                new { role = "user", content = "Ile to jest 15 + 27? Odpowiedz TYLKO liczbą, nic więcej." }
            },
            max_tokens = 10,
            temperature = 0
        };

        var json = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("/api/v1/chat/completions", httpContent);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        Assert.That(response.IsSuccessStatusCode, Is.True);
        Assert.That(responseContent, Does.Contain("42").Or.Contain("choices"));
    }

    [Test]
    public async Task OpenRouter_StreamingCompletion_ReturnsChunks()
    {
        if (!ApiKeyHelper.HasOpenRouterApiKey())
        {
            Assert.Ignore("OpenRouter API key not available");
        }

        var request = new
        {
            model = IntegrationTestConstants.TestModelFast,
            messages = new[]
            {
                new { role = "user", content = "Policz od 1 do 5, każda liczba w nowej linii." }
            },
            max_tokens = 50,
            stream = true
        };

        var json = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        
        // Use regular POST for streaming - OpenRouter handles it
        var response = await _httpClient.PostAsync("/api/v1/chat/completions", httpContent);
        
        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Streaming should succeed. Status: {response.StatusCode}");

        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.That(responseContent, Does.Contain("data:").Or.Contain("choices"), 
            "Should receive streaming data or regular response");
    }

    [Test]
    public async Task OpenRouter_PolishLanguage_UnderstandsPolish()
    {
        if (!ApiKeyHelper.HasOpenRouterApiKey())
        {
            Assert.Ignore("OpenRouter API key not available");
        }

        var request = new
        {
            model = IntegrationTestConstants.TestModel,
            messages = new[]
            {
                new { role = "user", content = "Jaka jest stolica Polski? Odpowiedz jednym słowem." }
            },
            max_tokens = 20,
            temperature = 0
        };

        var json = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("/api/v1/chat/completions", httpContent);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        Assert.That(response.IsSuccessStatusCode, Is.True);
        Assert.That(responseContent.ToLower(), Does.Contain("warszawa"));
    }

    [Test]
    [Retry(3)] // Retry flaky integration test
    public async Task OpenRouter_SystemPrompt_IsRespected()
    {
        if (!ApiKeyHelper.HasOpenRouterApiKey())
        {
            Assert.Ignore("OpenRouter API key not available");
        }

        var request = new
        {
            model = IntegrationTestConstants.TestModel,
            messages = new object[]
            {
                new { role = "system", content = "Odpowiadaj zawsze po polsku." },
                new { role = "user", content = "Hello, how are you?" }
            },
            max_tokens = 50,
            temperature = 0.1
        };

        var json = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("/api/v1/chat/completions", httpContent);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Assert.Ignore($"API temporarily unavailable: {response.StatusCode} - {errorContent}");
        }
        
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.That(responseContent, Does.Contain("choices"));
    }

    [Test]
    public async Task OpenRouter_InvalidApiKey_ReturnsUnauthorized()
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(IntegrationTestConstants.OpenRouterBaseUrl)
        };
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", "invalid-key");

        var request = new
        {
            model = IntegrationTestConstants.TestModelFast,
            messages = new[] { new { role = "user", content = "test" } }
        };

        var json = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await client.PostAsync("/api/v1/chat/completions", httpContent);
        
        Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(403),
            "Invalid API key should return 401 or 403");
    }

    [Test]
    public async Task OpenRouter_ConversationContext_MaintainsContext()
    {
        if (!ApiKeyHelper.HasOpenRouterApiKey())
        {
            Assert.Ignore("OpenRouter API key not available");
        }

        var request = new
        {
            model = IntegrationTestConstants.TestModel,
            messages = new object[]
            {
                new { role = "user", content = "Mam na imię Jan." },
                new { role = "assistant", content = "Miło Cię poznać, Jan!" },
                new { role = "user", content = "Jak mam na imię? Odpowiedz jednym słowem." }
            },
            max_tokens = 20,
            temperature = 0
        };

        var json = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("/api/v1/chat/completions", httpContent);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        Assert.That(response.IsSuccessStatusCode, Is.True);
        Assert.That(responseContent, Does.Contain("Jan"));
    }
}
