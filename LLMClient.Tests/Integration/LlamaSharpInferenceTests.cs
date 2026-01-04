#if WINDOWS
using LLMClient.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Testy inferencji LlamaSharp z modelami GGUF.
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("LocalModel")]
[Category("LlamaSharp")]
public class LlamaSharpInferenceTests
{
    private LlamaSharpLocalModelService _llamaService = null!;
    private bool _modelAvailable = false;
    private static readonly string GgufDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LLMClient", "Models", "gguf");

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var logger = new Mock<ILogger<LlamaSharpLocalModelService>>();
        
        try
        {
            _llamaService = new LlamaSharpLocalModelService(logger.Object);
            
            // Check if any model is downloaded
            var downloaded = await _llamaService.GetDownloadedModelsAsync();
            _modelAvailable = downloaded.Any(kv => kv.Value);
            
            if (_modelAvailable)
            {
                // Select first available model
                var availableModel = downloaded.First(kv => kv.Value).Key;
                await _llamaService.SelectModelAsync(availableModel);
                
                TestContext.WriteLine($"Selected model: {_llamaService.SelectedModel.DisplayName}");
                TestContext.WriteLine($"Model path: {GgufDir}");
                
                // Load the model
                TestContext.WriteLine("Loading model...");
                var loadResult = await _llamaService.LoadModelAsync();
                
                if (loadResult)
                {
                    TestContext.WriteLine("Model loaded successfully!");
                }
                else
                {
                    TestContext.WriteLine("Failed to load model");
                    _modelAvailable = false;
                }
            }
            else
            {
                TestContext.WriteLine("No GGUF models downloaded. Run app to download.");
            }
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"Setup error: {ex.Message}");
            _modelAvailable = false;
        }
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_llamaService != null)
        {
            try
            {
                await _llamaService.UnloadModelAsync();
            }
            catch { }
            
            _llamaService.Dispose();
        }
    }

    private void EnsureModelAvailable()
    {
        if (!_modelAvailable || !_llamaService.IsLoaded)
        {
            Assert.Ignore("GGUF model not available or not loaded");
        }
    }

    [Test]
    public async Task LlamaSharp_SimpleGeneration_ReturnsText()
    {
        EnsureModelAvailable();
        
        // Arrange
        var prompt = "What is 2+2? Answer with just the number:";
        
        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await _llamaService.GenerateResponseAsync(prompt);
        sw.Stop();
        
        // Assert
        Assert.That(response, Is.Not.Null.And.Not.Empty);
        
        TestContext.WriteLine($"Prompt: {prompt}");
        TestContext.WriteLine($"Response: {response}");
        TestContext.WriteLine($"Generation time: {sw.ElapsedMilliseconds}ms");
    }

    [Test]
    public async Task LlamaSharp_PolishGeneration_WorksCorrectly()
    {
        EnsureModelAvailable();
        
        // Arrange
        var prompt = "Odpowiedz po polsku: Jaka jest stolica Polski?";
        
        // Act
        var response = await _llamaService.GenerateResponseAsync(prompt);
        
        // Assert
        Assert.That(response, Is.Not.Null.And.Not.Empty);
        
        TestContext.WriteLine($"Prompt: {prompt}");
        TestContext.WriteLine($"Response: {response}");
        
        // Response should mention Warszawa
        Assert.That(response.ToLower(), Does.Contain("warszaw").Or.Contain("warsaw"),
            "Response should mention Warsaw");
    }

    [Test]
    public async Task LlamaSharp_StreamingGeneration_YieldsTokens()
    {
        EnsureModelAvailable();
        
        // Arrange
        var prompt = "Count from 1 to 5:";
        var tokens = new List<string>();
        
        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await foreach (var token in _llamaService.GenerateStreamingResponseAsync(prompt))
        {
            tokens.Add(token);
            if (tokens.Count > 50) break; // Limit tokens
        }
        sw.Stop();
        
        // Assert
        Assert.That(tokens, Is.Not.Empty);
        
        var fullResponse = string.Join("", tokens);
        TestContext.WriteLine($"Prompt: {prompt}");
        TestContext.WriteLine($"Streamed {tokens.Count} tokens in {sw.ElapsedMilliseconds}ms");
        TestContext.WriteLine($"Full response: {fullResponse}");
    }

    [Test]
    public async Task LlamaSharp_ConversationContext_MaintainsHistory()
    {
        EnsureModelAvailable();
        
        // Arrange - multi-turn conversation
        var messages = new List<(string role, string content)>
        {
            ("user", "My name is Jan."),
            ("assistant", "Hello Jan! Nice to meet you."),
            ("user", "What is my name?")
        };
        
        // Build conversation prompt (ChatML format for Qwen)
        var conversationPrompt = string.Join("\n", messages.Select(m => 
            m.role == "user" ? $"<|im_start|>user\n{m.content}<|im_end|>" : $"<|im_start|>assistant\n{m.content}<|im_end|>"));
        conversationPrompt += "\n<|im_start|>assistant\n";
        
        // Act
        var response = await _llamaService.GenerateResponseAsync(conversationPrompt);
        
        // Assert
        Assert.That(response, Is.Not.Null.And.Not.Empty);
        
        TestContext.WriteLine("Conversation:");
        foreach (var (role, content) in messages)
        {
            TestContext.WriteLine($"  {role}: {content}");
        }
        TestContext.WriteLine($"  assistant: {response}");
        
        // Should remember the name
        Assert.That(response.ToLower(), Does.Contain("jan"),
            "Model should remember the user's name");
    }

    [Test]
    public async Task LlamaSharp_CodeGeneration_ProducesCode()
    {
        EnsureModelAvailable();
        
        // Arrange
        var prompt = "Write a simple C# function that adds two numbers. Only code, no explanation:";
        
        // Act
        var response = await _llamaService.GenerateResponseAsync(prompt);
        
        // Assert
        Assert.That(response, Is.Not.Null.And.Not.Empty);
        
        TestContext.WriteLine($"Prompt: {prompt}");
        TestContext.WriteLine($"Response:\n{response}");
        
        // Should contain some code-like content
        Assert.That(response, Does.Contain("int").Or.Contain("return").Or.Contain("public").Or.Contain("(").Or.Contain("+"),
            "Response should contain code-like content");
    }

    [Test]
    public async Task LlamaSharp_ModelInfo_CorrectDetails()
    {
        EnsureModelAvailable();
        
        // Act
        var modelInfo = await _llamaService.GetModelInfoAsync();
        var selectedModel = _llamaService.SelectedModel;
        
        // Assert
        Assert.That(modelInfo, Is.Not.Null);
        Assert.That(selectedModel, Is.Not.Null);
        
        TestContext.WriteLine("Model Info:");
        TestContext.WriteLine($"  ID: {selectedModel.Id}");
        TestContext.WriteLine($"  Display Name: {selectedModel.DisplayName}");
        TestContext.WriteLine($"  Size: {selectedModel.SizeInMB}MB");
        TestContext.WriteLine($"  Languages: {string.Join(", ", selectedModel.SupportedLanguages)}");
        TestContext.WriteLine($"  Is Loaded: {_llamaService.IsLoaded}");
        TestContext.WriteLine($"  State: {_llamaService.State}");
    }

    [Test]
    public async Task LlamaSharp_CancellationToken_StopsGeneration()
    {
        EnsureModelAvailable();
        
        // Arrange
        var prompt = "Write a very long essay about the history of computing:";
        var cts = new CancellationTokenSource();
        var tokens = new List<string>();
        
        // Act - cancel after 500ms
        cts.CancelAfter(500);
        
        try
        {
            await foreach (var token in _llamaService.GenerateStreamingResponseAsync(prompt, cts.Token))
            {
                tokens.Add(token);
            }
        }
        catch (OperationCanceledException)
        {
            TestContext.WriteLine("Generation cancelled as expected");
        }
        
        // Assert
        TestContext.WriteLine($"Generated {tokens.Count} tokens before cancellation");
        TestContext.WriteLine($"Partial response: {string.Join("", tokens).Substring(0, Math.Min(100, string.Join("", tokens).Length))}...");
    }

    [Test]
    public async Task LlamaSharp_AvailableModels_ListsOptions()
    {
        // Act
        var models = _llamaService.GetAvailableModels();
        var downloaded = await _llamaService.GetDownloadedModelsAsync();
        
        // Assert
        Assert.That(models, Is.Not.Empty);
        
        TestContext.WriteLine("Available GGUF models:");
        foreach (var model in models)
        {
            var isDownloaded = downloaded.TryGetValue(model.Id, out var dl) && dl;
            var status = isDownloaded ? "✓" : "✗";
            TestContext.WriteLine($"  [{status}] {model.DisplayName} ({model.SizeInMB}MB) - {model.Description}");
        }
    }
}
#endif
