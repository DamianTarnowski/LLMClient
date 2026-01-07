using LLMClient.Core.Models;

namespace LLMClient.Tests.Models;

/// <summary>
/// Tests for EngineSettings and related classes
/// </summary>
[TestFixture]
public class EngineTypeTests
{
    [Test]
    public void EngineType_AllValuesAreDefined()
    {
        var values = Enum.GetValues<EngineType>();
        Assert.That(values.Length, Is.EqualTo(3));
    }

    [Test]
    public void EngineType_HasExpectedEngines()
    {
        Assert.That(Enum.IsDefined(typeof(EngineType), EngineType.OnnxGenAI), Is.True);
        Assert.That(Enum.IsDefined(typeof(EngineType), EngineType.LLamaSharp), Is.True);
        Assert.That(Enum.IsDefined(typeof(EngineType), EngineType.MediaPipeGenAI), Is.True);
    }
}

[TestFixture]
public class EngineSettingsServiceTests
{
    [Test]
    public void EngineSettingsService_CreateWithoutPreferences_Works()
    {
        var service = new EngineSettingsService();
        
        // Should not throw
        Assert.Pass();
    }

    [Test]
    public void EngineSettingsService_GetDefaultEngine_ReturnsOnnx()
    {
        var defaultEngine = EngineSettingsService.GetDefaultEngine();
        
        Assert.That(defaultEngine, Is.EqualTo(EngineType.OnnxGenAI));
    }

    [Test]
    public void EngineSettingsService_LoadSelectedEngine_WithoutPreferences_ReturnsDefault()
    {
        var service = new EngineSettingsService(null);
        var engine = service.LoadSelectedEngine();
        
        Assert.That(engine, Is.EqualTo(EngineType.OnnxGenAI));
    }

    [Test]
    public void EngineSettingsService_EngineChanged_EventCanBeSubscribed()
    {
        var service = new EngineSettingsService();
        EngineType? changedTo = null;
        
        service.EngineChanged += (engine) => changedTo = engine;
        
        // Event should be subscribable
        Assert.Pass();
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

[TestFixture]
public class EngineSettingsServiceWithMockTests
{
    private MockPreferencesService _mockPreferences = null!;
    private EngineSettingsService _service = null!;

    [SetUp]
    public void Setup()
    {
        _mockPreferences = new MockPreferencesService();
        _service = new EngineSettingsService(_mockPreferences);
    }

    [Test]
    public void SaveSelectedEngine_PersistsValue()
    {
        _service.SaveSelectedEngine(EngineType.LLamaSharp);
        var loaded = _service.LoadSelectedEngine();
        
        Assert.That(loaded, Is.EqualTo(EngineType.LLamaSharp));
    }

    [Test]
    public void SaveSelectedEngine_RaisesEvent()
    {
        EngineType? eventEngine = null;
        _service.EngineChanged += (engine) => eventEngine = engine;
        
        _service.SaveSelectedEngine(EngineType.MediaPipeGenAI);
        
        Assert.That(eventEngine, Is.EqualTo(EngineType.MediaPipeGenAI));
    }

    [Test]
    public void LoadSelectedEngine_WithSavedValue_ReturnsIt()
    {
        _mockPreferences.Set("LocalModelEngine", "LLamaSharp");
        
        var engine = _service.LoadSelectedEngine();
        
        Assert.That(engine, Is.EqualTo(EngineType.LLamaSharp));
    }

    [Test]
    public void LoadSelectedEngine_WithInvalidValue_ReturnsDefault()
    {
        _mockPreferences.Set("LocalModelEngine", "InvalidEngine");
        
        var engine = _service.LoadSelectedEngine();
        
        Assert.That(engine, Is.EqualTo(EngineType.OnnxGenAI));
    }

    [Test]
    public void SaveAndLoad_AllEngineTypes_Work()
    {
        foreach (var engineType in Enum.GetValues<EngineType>())
        {
            _service.SaveSelectedEngine(engineType);
            var loaded = _service.LoadSelectedEngine();
            
            Assert.That(loaded, Is.EqualTo(engineType), $"Engine {engineType} should save and load correctly");
        }
    }
}
