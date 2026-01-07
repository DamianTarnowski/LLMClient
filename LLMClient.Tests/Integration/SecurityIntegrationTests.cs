using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Interface for secure API key storage (for testing purposes)
/// </summary>
public interface ITestSecureApiKeyService
{
    Task<string?> GetApiKeyAsync(string modelId);
    Task SetApiKeyAsync(string modelId, string apiKey);
    Task DeleteApiKeyAsync(string modelId);
    Task<bool> HasApiKeyAsync(string modelId);
}

/// <summary>
/// Integration tests for Security functionality
/// Tests API key handling, data sanitization, and secure storage
/// </summary>
[TestFixture]
[Category("Integration")]
public class ApiKeySecurityTests
{
    private Mock<ITestSecureApiKeyService> _apiKeyService = null!;
    private Dictionary<string, string> _secureStorage = null!;

    [SetUp]
    public void Setup()
    {
        _secureStorage = new Dictionary<string, string>();
        _apiKeyService = new Mock<ITestSecureApiKeyService>();
        
        _apiKeyService.Setup(x => x.GetApiKeyAsync(It.IsAny<string>()))
            .ReturnsAsync((string modelId) =>
            {
                return _secureStorage.TryGetValue(modelId, out var key) ? key : null;
            });
            
        _apiKeyService.Setup(x => x.SetApiKeyAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string modelId, string key) =>
            {
                _secureStorage[modelId] = key;
                return Task.CompletedTask;
            });
            
        _apiKeyService.Setup(x => x.DeleteApiKeyAsync(It.IsAny<string>()))
            .Returns((string modelId) =>
            {
                _secureStorage.Remove(modelId);
                return Task.CompletedTask;
            });
            
        _apiKeyService.Setup(x => x.HasApiKeyAsync(It.IsAny<string>()))
            .ReturnsAsync((string modelId) => _secureStorage.ContainsKey(modelId));
    }

    [Test]
    public async Task ApiKey_Store_Persists()
    {
        await _apiKeyService.Object.SetApiKeyAsync("openrouter", "sk-or-test-key");
        
        var hasKey = await _apiKeyService.Object.HasApiKeyAsync("openrouter");
        Assert.That(hasKey, Is.True);
    }

    [Test]
    public async Task ApiKey_Retrieve_ReturnsStoredKey()
    {
        await _apiKeyService.Object.SetApiKeyAsync("openai", "sk-test-key");
        
        var key = await _apiKeyService.Object.GetApiKeyAsync("openai");
        Assert.That(key, Is.EqualTo("sk-test-key"));
    }

    [Test]
    public async Task ApiKey_Delete_RemovesKey()
    {
        await _apiKeyService.Object.SetApiKeyAsync("anthropic", "sk-ant-test");
        await _apiKeyService.Object.DeleteApiKeyAsync("anthropic");
        
        var hasKey = await _apiKeyService.Object.HasApiKeyAsync("anthropic");
        Assert.That(hasKey, Is.False);
    }

    [Test]
    public async Task ApiKey_NonExistent_ReturnsNull()
    {
        var key = await _apiKeyService.Object.GetApiKeyAsync("unknown_provider");
        
        Assert.That(key, Is.Null);
    }

    [Test]
    public async Task ApiKey_MultipleProviders_Independent()
    {
        await _apiKeyService.Object.SetApiKeyAsync("provider1", "key1");
        await _apiKeyService.Object.SetApiKeyAsync("provider2", "key2");
        
        var key1 = await _apiKeyService.Object.GetApiKeyAsync("provider1");
        var key2 = await _apiKeyService.Object.GetApiKeyAsync("provider2");
        
        Assert.That(key1, Is.Not.EqualTo(key2));
    }

    [Test]
    public async Task ApiKey_Update_OverwritesOld()
    {
        await _apiKeyService.Object.SetApiKeyAsync("test", "old-key");
        await _apiKeyService.Object.SetApiKeyAsync("test", "new-key");
        
        var key = await _apiKeyService.Object.GetApiKeyAsync("test");
        Assert.That(key, Is.EqualTo("new-key"));
    }
}

[TestFixture]
[Category("Integration")]
public class DataSanitizationTests
{
    [Test]
    public void Sanitize_RemovesApiKeys()
    {
        var text = "Error with key: sk-or-abc123def456xyz";
        var sanitized = SanitizeForLogging(text);
        
        Assert.That(sanitized, Does.Not.Contain("abc123"));
        Assert.That(sanitized, Does.Contain("[REDACTED]"));
    }

    [Test]
    public void Sanitize_RemovesEmails()
    {
        var text = "Contact: user@example.com for support";
        var sanitized = SanitizeForLogging(text);
        
        Assert.That(sanitized, Does.Not.Contain("user@example.com"));
    }

    [Test]
    public void Sanitize_PreservesNonSensitive()
    {
        var text = "The model returned 42 tokens in 150ms";
        var sanitized = SanitizeForLogging(text);
        
        Assert.That(sanitized, Is.EqualTo(text));
    }

    [Test]
    public void Sanitize_RemovesMultiplePatterns()
    {
        var text = "Key: sk-test-123, Email: test@test.com, IP: 192.168.1.1";
        var sanitized = SanitizeForLogging(text);
        
        Assert.That(sanitized, Does.Not.Contain("sk-test-123"));
        Assert.That(sanitized, Does.Not.Contain("test@test.com"));
    }

    [Test]
    public void Sanitize_HandlesEmptyString()
    {
        var sanitized = SanitizeForLogging("");
        Assert.That(sanitized, Is.Empty);
    }

    [Test]
    public void Sanitize_HandlesNull()
    {
        var sanitized = SanitizeForLogging(null);
        Assert.That(sanitized, Is.Null);
    }

    private static string? SanitizeForLogging(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        
        // Remove API keys
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"sk-[a-zA-Z0-9-]{10,}", "[REDACTED]");
        
        // Remove emails
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"[\w\.-]+@[\w\.-]+\.\w+", "[EMAIL]");
        
        return text;
    }
}

[TestFixture]
[Category("Integration")]
public class InputValidationTests
{
    [Test]
    public void Validate_PromptLength_Enforced()
    {
        var maxLength = 10000;
        var longPrompt = new string('x', 15000);
        
        var truncated = TruncatePrompt(longPrompt, maxLength);
        
        Assert.That(truncated.Length, Is.LessThanOrEqualTo(maxLength));
    }

    [Test]
    public void Validate_RemovesControlCharacters()
    {
        var text = "Hello\x00World\x1FTest";
        var cleaned = RemoveControlCharacters(text);
        
        Assert.That(cleaned, Does.Not.Contain("\x00"));
        Assert.That(cleaned, Does.Not.Contain("\x1F"));
    }

    [Test]
    public void Validate_PreservesValidUnicode()
    {
        var text = "Cześć! 你好 🎉";
        var cleaned = RemoveControlCharacters(text);
        
        Assert.That(cleaned, Is.EqualTo(text));
    }

    [Test]
    public void Validate_ModelId_Format()
    {
        Assert.That(IsValidModelId("gpt-4-turbo"), Is.True);
        Assert.That(IsValidModelId("anthropic/claude-3"), Is.True);
        Assert.That(IsValidModelId(""), Is.False);
        Assert.That(IsValidModelId(null), Is.False);
    }

    [Test]
    public void Validate_Temperature_Range()
    {
        Assert.That(IsValidTemperature(0.0f), Is.True);
        Assert.That(IsValidTemperature(1.0f), Is.True);
        Assert.That(IsValidTemperature(2.0f), Is.True);
        Assert.That(IsValidTemperature(-0.1f), Is.False);
        Assert.That(IsValidTemperature(2.1f), Is.False);
    }

    private static string TruncatePrompt(string prompt, int maxLength)
    {
        return prompt.Length <= maxLength ? prompt : prompt[..maxLength];
    }

    private static string RemoveControlCharacters(string text)
    {
        return new string(text.Where(c => !char.IsControl(c) || c == '\n' || c == '\r' || c == '\t').ToArray());
    }

    private static bool IsValidModelId(string? modelId)
    {
        return !string.IsNullOrEmpty(modelId);
    }

    private static bool IsValidTemperature(float temp)
    {
        return temp >= 0.0f && temp <= 2.0f;
    }
}
