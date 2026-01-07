using LLMClient.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace LLMClient.Tests.TestHelpers;

/// <summary>
/// Bazowa klasa dla testów integracyjnych z prawdziwym API
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected string? OpenRouterApiKey { get; private set; }
    protected bool HasApiKey => !string.IsNullOrEmpty(OpenRouterApiKey);
    protected string TestDbPath { get; private set; } = null!;
    protected DatabaseService DatabaseService { get; private set; } = null!;
    protected Mock<IEmbeddingService> MockEmbeddingService { get; private set; } = null!;

    protected virtual void SetUp()
    {
        OpenRouterApiKey = ApiKeyHelper.GetOpenRouterApiKey();
        
        MockEmbeddingService = new Mock<IEmbeddingService>();
        MockEmbeddingService.Setup(x => x.IsInitialized).Returns(true);
        MockEmbeddingService.Setup(x => x.ModelVersion).Returns("test-v1");
        
        TestDbPath = Path.Combine(Path.GetTempPath(), $"IntegrationTest_{Guid.NewGuid()}.db");
        DatabaseService = new DatabaseService(MockEmbeddingService.Object, TestDbPath);
    }

    protected virtual void TearDown()
    {
        DatabaseService?.Dispose();
        
        if (File.Exists(TestDbPath))
        {
            try { File.Delete(TestDbPath); } catch { }
        }
    }

    public void Dispose()
    {
        TearDown();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Pomija test jeśli brak API key
    /// </summary>
    protected void SkipIfNoApiKey()
    {
        if (!HasApiKey)
        {
            Assert.Ignore("OpenRouter API key not configured. Set OPENROUTER_API_KEY env var or create ~/.llmclient/openrouter_api_key.txt");
        }
    }

    /// <summary>
    /// Tworzy prawdziwy AiService z OpenRouter API
    /// </summary>
    protected AiService CreateRealAiService()
    {
        SkipIfNoApiKey();
        
        // Ustaw API key w Preferences (symulacja konfiguracji użytkownika)
        // W testach musimy użyć innej metody
        var aiService = new AiService(null, null, DatabaseService);
        return aiService;
    }
}
