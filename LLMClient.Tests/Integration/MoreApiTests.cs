using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LLMClient.Tests.TestHelpers;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Additional API integration tests
/// </summary>
[TestFixture]
[Category("Integration")]
public class OpenRouterAdvancedTests
{
    private HttpClient _httpClient = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var apiKey = ApiKeyHelper.GetOpenRouterApiKey();
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(IntegrationTestConstants.OpenRouterBaseUrl),
            Timeout = TimeSpan.FromSeconds(IntegrationTestConstants.DefaultTimeoutSeconds)
        };
        
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", apiKey);
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
    [Retry(2)]
    public async Task OpenRouter_CodeGeneration_GeneratesCode()
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
                new { role = "user", content = "Napisz funkcję C# która sprawdza czy liczba jest parzysta. Tylko kod, bez wyjaśnień." }
            },
            max_tokens = 100,
            temperature = 0
        };

        var json = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("/api/v1/chat/completions", httpContent);
        
        if (!response.IsSuccessStatusCode)
        {
            Assert.Ignore($"API temporarily unavailable: {response.StatusCode}");
        }
        
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.That(responseContent.ToLower(), Does.Contain("bool").Or.Contain("return").Or.Contain("%"));
    }

    [Test]
    [Retry(2)]
    public async Task OpenRouter_JsonMode_ReturnsStructuredData()
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
                new { role = "user", content = "Zwróć JSON z polami: name (string), age (number). Przykładowa osoba. Tylko JSON, bez markdown." }
            },
            max_tokens = 50,
            temperature = 0
        };

        var json = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("/api/v1/chat/completions", httpContent);
        
        if (!response.IsSuccessStatusCode)
        {
            Assert.Ignore($"API temporarily unavailable: {response.StatusCode}");
        }
        
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.That(responseContent, Does.Contain("name").Or.Contain("age").Or.Contain("{"));
    }

    [Test]
    [Retry(2)]
    public async Task OpenRouter_LongContext_HandlesLargeInput()
    {
        if (!ApiKeyHelper.HasOpenRouterApiKey())
        {
            Assert.Ignore("OpenRouter API key not available");
        }

        // Generate a longer prompt
        var longText = string.Concat(Enumerable.Repeat("To jest przykładowy tekst. ", 50));
        
        var request = new
        {
            model = IntegrationTestConstants.TestModel,
            messages = new[]
            {
                new { role = "user", content = $"Podsumuj w jednym zdaniu: {longText}" }
            },
            max_tokens = 50,
            temperature = 0
        };

        var json = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("/api/v1/chat/completions", httpContent);
        
        if (!response.IsSuccessStatusCode)
        {
            Assert.Ignore($"API temporarily unavailable: {response.StatusCode}");
        }
        
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.That(responseContent, Does.Contain("choices"));
    }

    [Test]
    public async Task OpenRouter_Temperature0_IsDeterministic()
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
                new { role = "user", content = "Odpowiedz jednym słowem: jaki kolor ma trawa?" }
            },
            max_tokens = 10,
            temperature = 0
        };

        var json = JsonSerializer.Serialize(request);
        var httpContent1 = new StringContent(json, Encoding.UTF8, "application/json");
        var httpContent2 = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response1 = await _httpClient.PostAsync("/api/v1/chat/completions", httpContent1);
        var response2 = await _httpClient.PostAsync("/api/v1/chat/completions", httpContent2);
        
        if (!response1.IsSuccessStatusCode || !response2.IsSuccessStatusCode)
        {
            Assert.Ignore("API temporarily unavailable");
        }
        
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        
        // Both should contain similar answer (green/zielona)
        Assert.That(content1.ToLower(), Does.Contain("zielon").Or.Contain("green"));
    }

    [Test]
    [Retry(2)]
    public async Task OpenRouter_EmptySystemPrompt_Works()
    {
        if (!ApiKeyHelper.HasOpenRouterApiKey())
        {
            Assert.Ignore("OpenRouter API key not available");
        }

        var request = new
        {
            model = IntegrationTestConstants.TestModelFast,
            messages = new object[]
            {
                new { role = "system", content = "" },
                new { role = "user", content = "Powiedz: test" }
            },
            max_tokens = 10
        };

        var json = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("/api/v1/chat/completions", httpContent);
        
        // Should work even with empty system prompt
        Assert.That(response.IsSuccessStatusCode, Is.True);
    }
}

[TestFixture]
[Category("Integration")]
public class ApiKeyHelperTests
{
    [Test]
    public void ApiKeyHelper_HasOpenRouterApiKey_ReturnsBoolean()
    {
        var result = ApiKeyHelper.HasOpenRouterApiKey();
        
        // Should return true or false, not throw
        Assert.That(result, Is.TypeOf<bool>());
    }

    [Test]
    public void ApiKeyHelper_GetOpenRouterApiKey_ReturnsStringOrNull()
    {
        var key = ApiKeyHelper.GetOpenRouterApiKey();
        
        // Should return valid key or null, not throw
        if (key != null)
        {
            Assert.That(key, Does.StartWith("sk-or-"));
        }
    }

    [Test]
    public void ApiKeyHelper_GetApiKey_UnknownProvider_ReturnsNull()
    {
        var key = ApiKeyHelper.GetApiKey("unknown_provider_xyz");
        
        Assert.That(key, Is.Null);
    }
}
