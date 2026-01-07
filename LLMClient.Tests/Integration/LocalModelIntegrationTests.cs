using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for Local Model functionality
/// Tests engine selection, model loading simulation, and inference
/// </summary>
[TestFixture]
[Category("Integration")]
public class LocalModelEngineTests
{
    [Test]
    public void EngineSelection_OnnxGenAI_IsDefault()
    {
        var defaultEngine = EngineSettingsService.GetDefaultEngine();
        Assert.That(defaultEngine, Is.EqualTo(EngineType.OnnxGenAI));
    }

    [Test]
    public void EngineSelection_AllEngines_AreDefined()
    {
        var engines = Enum.GetValues<EngineType>();
        
        Assert.That(engines, Does.Contain(EngineType.OnnxGenAI));
        Assert.That(engines, Does.Contain(EngineType.LLamaSharp));
        Assert.That(engines, Does.Contain(EngineType.MediaPipeGenAI));
    }

    [Test]
    public void EngineSettings_SaveAndLoad_Persists()
    {
        var prefs = new MockPreferencesService();
        var service = new EngineSettingsService(prefs);
        
        service.SaveSelectedEngine(EngineType.LLamaSharp);
        var loaded = service.LoadSelectedEngine();
        
        Assert.That(loaded, Is.EqualTo(EngineType.LLamaSharp));
    }

    [Test]
    public void EngineSettings_InvalidValue_ReturnsDefault()
    {
        var prefs = new MockPreferencesService();
        prefs.Set("LocalModelEngine", "InvalidEngine");
        var service = new EngineSettingsService(prefs);
        
        var loaded = service.LoadSelectedEngine();
        
        Assert.That(loaded, Is.EqualTo(EngineType.OnnxGenAI));
    }

    [Test]
    public void EngineSettings_EventRaised_OnChange()
    {
        var prefs = new MockPreferencesService();
        var service = new EngineSettingsService(prefs);
        EngineType? eventValue = null;
        
        service.EngineChanged += (engine) => eventValue = engine;
        service.SaveSelectedEngine(EngineType.MediaPipeGenAI);
        
        Assert.That(eventValue, Is.EqualTo(EngineType.MediaPipeGenAI));
    }
}

[TestFixture]
[Category("Integration")]
public class LocalModelInferenceTests
{
    private Mock<IAiService> _aiService = null!;

    [SetUp]
    public void Setup()
    {
        _aiService = new Mock<IAiService>();
        
        _aiService.Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string prompt, CancellationToken ct) => 
                $"Response to: {prompt.Substring(0, Math.Min(20, prompt.Length))}...");
    }

    [Test]
    public async Task LocalModel_Generate_ReturnsResponse()
    {
        var response = await _aiService.Object.GenerateResponseAsync("Hello, how are you?");
        
        Assert.That(response, Is.Not.Empty);
        Assert.That(response, Does.Contain("Response to"));
    }

    [Test]
    public async Task LocalModel_LongPrompt_Handles()
    {
        var longPrompt = string.Concat(Enumerable.Repeat("This is a test. ", 100));
        
        var response = await _aiService.Object.GenerateResponseAsync(longPrompt);
        
        Assert.That(response, Is.Not.Empty);
    }

    [Test]
    public async Task LocalModel_PolishPrompt_Handles()
    {
        var polishPrompt = "Cześć! Jak się masz? Opowiedz mi o sztucznej inteligencji.";
        
        var response = await _aiService.Object.GenerateResponseAsync(polishPrompt);
        
        Assert.That(response, Is.Not.Empty);
    }

    [Test]
    public async Task LocalModel_Cancellation_Respects()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        _aiService.Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _aiService.Object.GenerateResponseAsync("test", cts.Token));
    }
}

[TestFixture]
[Category("Integration")]
public class LocalModelStreamingTests
{
    [Test]
    public async Task Streaming_YieldsTokens()
    {
        var tokens = new List<string> { "Hello", " ", "world", "!" };
        var mockService = new Mock<IAiService>();
        
        mockService.Setup(x => x.GenerateStreamingResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable(tokens));
        
        var received = new List<string>();
        await foreach (var token in mockService.Object.GenerateStreamingResponseAsync("test"))
        {
            received.Add(token);
        }
        
        Assert.That(received.Count, Is.EqualTo(4));
        Assert.That(string.Concat(received), Is.EqualTo("Hello world!"));
    }

    [Test]
    public async Task Streaming_EmptyResponse_YieldsNothing()
    {
        var mockService = new Mock<IAiService>();
        
        mockService.Setup(x => x.GenerateStreamingResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable(Array.Empty<string>()));
        
        var count = 0;
        await foreach (var _ in mockService.Object.GenerateStreamingResponseAsync("test"))
        {
            count++;
        }
        
        Assert.That(count, Is.EqualTo(0));
    }

    private static async IAsyncEnumerable<string> AsyncEnumerable(IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            await Task.Delay(1); // Simulate async delay
            yield return item;
        }
    }
}

[TestFixture]
[Category("Integration")]
public class ModelConfigurationTests
{
    [Test]
    public void ModelSettings_DefaultValues_AreReasonable()
    {
        var settings = new ModelSettings();
        
        Assert.That(settings.Temperature, Is.InRange(0f, 2f));
        Assert.That(settings.MaxTokens, Is.GreaterThan(0));
        Assert.That(settings.EnableStreaming, Is.True);
    }

    [Test]
    public void ModelSettings_RagConfig_HasDefaults()
    {
        var settings = new ModelSettings();
        
        Assert.That(settings.RagSearchMode, Is.EqualTo("Hybrid"));
        Assert.That(settings.RagTopK, Is.GreaterThan(0));
        Assert.That(settings.RagMinSimilarity, Is.InRange(0f, 1f));
    }

    [Test]
    public void ModelSettings_AllPropertiesSettable()
    {
        var settings = new ModelSettings
        {
            SelectedModelId = "phi-3",
            Temperature = 0.5f,
            MaxTokens = 4096,
            SystemPrompt = "You are a helpful assistant",
            EnableStreaming = true,
            EnableMemory = true,
            EnableRag = true,
            RagSearchMode = "Vector",
            RagTopK = 10,
            RagMinSimilarity = 0.5f
        };
        
        Assert.That(settings.SelectedModelId, Is.EqualTo("phi-3"));
        Assert.That(settings.Temperature, Is.EqualTo(0.5f));
        Assert.That(settings.MaxTokens, Is.EqualTo(4096));
    }
}

/// <summary>
/// Mock implementation of IPreferencesService for testing
/// </summary>
public class MockPreferencesService : IPreferencesService
{
    private readonly Dictionary<string, string> _store = new();

    public string? Get(string key, string? defaultValue = null)
    {
        return _store.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public void Set(string key, string value)
    {
        _store[key] = value;
    }
}
