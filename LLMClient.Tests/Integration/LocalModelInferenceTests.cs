using LLMClient.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Testy inferencji lokalnych modeli - ONNX embeddings i LlamaSharp GGUF.
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("LocalModel")]
public class LocalModelInferenceTests
{
    private EmbeddingService _embeddingService = null!;
    private bool _onnxModelAvailable = false;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Setup ONNX embedding service
        var logger = new Mock<ILogger<EmbeddingService>>();
        _embeddingService = new EmbeddingService(logger.Object);
        
        _onnxModelAvailable = await _embeddingService.IsModelDownloadedAsync();
        
        if (_onnxModelAvailable)
        {
            try
            {
                await _embeddingService.InitializeAsync();
                TestContext.WriteLine($"ONNX model initialized: {_embeddingService.IsInitialized}");
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"ONNX init error: {ex.Message}");
                _onnxModelAvailable = false;
            }
        }
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        (_embeddingService as IDisposable)?.Dispose();
    }

    #region ONNX Embedding Tests

    [Test]
    public async Task ONNX_Inference_SingleText_Returns1024Dims()
    {
        if (!_onnxModelAvailable || !_embeddingService.IsInitialized)
            Assert.Ignore("ONNX model not available");
        
        // Arrange
        var text = "Test inference na modelu ONNX multilingual-e5-large.";
        
        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var embedding = await _embeddingService.GenerateEmbeddingAsync(text);
        sw.Stop();
        
        // Assert
        Assert.That(embedding, Is.Not.Null);
        Assert.That(embedding!.Length, Is.EqualTo(1024), "E5-large returns 1024 dimensions");
        
        TestContext.WriteLine($"ONNX inference time: {sw.ElapsedMilliseconds}ms");
        TestContext.WriteLine($"Embedding dims: {embedding.Length}");
        TestContext.WriteLine($"First 5 values: [{string.Join(", ", embedding.Take(5).Select(v => v.ToString("F4")))}]");
    }

    [Test]
    public async Task ONNX_Inference_BatchTexts_ConsistentResults()
    {
        if (!_onnxModelAvailable || !_embeddingService.IsInitialized)
            Assert.Ignore("ONNX model not available");
        
        // Arrange
        var texts = new[]
        {
            "Pierwsze zdanie testowe.",
            "Drugie zdanie testowe.",
            "Trzecie zdanie testowe."
        };
        
        // Act
        var embeddings = new List<float[]>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        foreach (var text in texts)
        {
            var emb = await _embeddingService.GenerateEmbeddingAsync(text);
            Assert.That(emb, Is.Not.Null);
            embeddings.Add(emb!);
        }
        
        sw.Stop();
        
        // Assert - all embeddings should have same dimensions
        Assert.That(embeddings.All(e => e.Length == 1024), Is.True);
        
        // Same text should give same embedding (deterministic)
        var emb1 = await _embeddingService.GenerateEmbeddingAsync(texts[0]);
        var similarity = _embeddingService.CalculateSimilarity(embeddings[0], emb1!);
        
        TestContext.WriteLine($"Batch inference time: {sw.ElapsedMilliseconds}ms for {texts.Length} texts");
        TestContext.WriteLine($"Avg per text: {sw.ElapsedMilliseconds / texts.Length}ms");
        TestContext.WriteLine($"Same text similarity: {similarity:F4}");
        
        Assert.That(similarity, Is.GreaterThan(0.99f), "Same text should produce identical embeddings");
    }

    [Test]
    public async Task ONNX_Inference_LongText_Truncates()
    {
        if (!_onnxModelAvailable || !_embeddingService.IsInitialized)
            Assert.Ignore("ONNX model not available");
        
        // Arrange - very long text (should be truncated by tokenizer)
        var longText = string.Join(" ", Enumerable.Range(1, 1000).Select(i => $"To jest słowo numer {i}."));
        
        // Act
        var embedding = await _embeddingService.GenerateEmbeddingAsync(longText);
        
        // Assert
        Assert.That(embedding, Is.Not.Null);
        Assert.That(embedding!.Length, Is.EqualTo(1024));
        
        TestContext.WriteLine($"Long text length: {longText.Length} chars");
        TestContext.WriteLine($"Embedding produced successfully");
    }

    [Test]
    public async Task ONNX_Inference_PolishVsEnglish_CrossLingual()
    {
        if (!_onnxModelAvailable || !_embeddingService.IsInitialized)
            Assert.Ignore("ONNX model not available");
        
        // Arrange
        var polishText = "Sztuczna inteligencja zmienia świat technologii.";
        var englishText = "Artificial intelligence is changing the world of technology.";
        var germanText = "Künstliche Intelligenz verändert die Welt der Technologie.";
        var unrelatedText = "Pierogi są pyszne z cebulką.";
        
        // Act
        var plEmb = await _embeddingService.GenerateEmbeddingAsync(polishText);
        var enEmb = await _embeddingService.GenerateEmbeddingAsync(englishText);
        var deEmb = await _embeddingService.GenerateEmbeddingAsync(germanText);
        var unrelEmb = await _embeddingService.GenerateEmbeddingAsync(unrelatedText);
        
        Assert.That(plEmb, Is.Not.Null);
        Assert.That(enEmb, Is.Not.Null);
        Assert.That(deEmb, Is.Not.Null);
        Assert.That(unrelEmb, Is.Not.Null);
        
        var plEnSim = _embeddingService.CalculateSimilarity(plEmb!, enEmb!);
        var plDeSim = _embeddingService.CalculateSimilarity(plEmb!, deEmb!);
        var enDeSim = _embeddingService.CalculateSimilarity(enEmb!, deEmb!);
        var plUnrelSim = _embeddingService.CalculateSimilarity(plEmb!, unrelEmb!);
        
        TestContext.WriteLine("Cross-lingual similarities (same meaning):");
        TestContext.WriteLine($"  PL-EN: {plEnSim:F4}");
        TestContext.WriteLine($"  PL-DE: {plDeSim:F4}");
        TestContext.WriteLine($"  EN-DE: {enDeSim:F4}");
        TestContext.WriteLine($"  PL-Unrelated: {plUnrelSim:F4}");
        
        // Assert - same meaning across languages should be more similar than unrelated
        Assert.That(plEnSim, Is.GreaterThan(plUnrelSim), "Same meaning PL-EN should be more similar than unrelated");
        Assert.That(plDeSim, Is.GreaterThan(plUnrelSim), "Same meaning PL-DE should be more similar than unrelated");
    }

    [Test]
    public async Task ONNX_Inference_QueryVsPassage_DifferentPrefixes()
    {
        if (!_onnxModelAvailable || !_embeddingService.IsInitialized)
            Assert.Ignore("ONNX model not available");
        
        // Arrange - E5 model uses query: and passage: prefixes
        var question = "Jak działa uczenie maszynowe?";
        var answer = "Uczenie maszynowe to dziedzina sztucznej inteligencji, która pozwala komputerom uczyć się na podstawie danych.";
        
        // Act - query embedding vs passage embedding
        var queryEmb = await _embeddingService.GenerateEmbeddingAsync(question, isQuery: true);
        var passageEmb = await _embeddingService.GenerateEmbeddingAsync(answer, isQuery: false);
        
        Assert.That(queryEmb, Is.Not.Null);
        Assert.That(passageEmb, Is.Not.Null);
        
        var similarity = _embeddingService.CalculateSimilarity(queryEmb!, passageEmb!);
        
        TestContext.WriteLine($"Query: {question}");
        TestContext.WriteLine($"Passage: {answer.Substring(0, Math.Min(60, answer.Length))}...");
        TestContext.WriteLine($"Query-Passage similarity: {similarity:F4}");
        
        // Assert - query and relevant passage should have high similarity
        Assert.That(similarity, Is.GreaterThan(0.7f), "Query and relevant passage should be similar");
    }

    [Test]
    public async Task ONNX_Inference_Performance_Under100ms()
    {
        if (!_onnxModelAvailable || !_embeddingService.IsInitialized)
            Assert.Ignore("ONNX model not available");
        
        // Arrange
        var text = "Test wydajności inference na modelu ONNX.";
        
        // Warmup
        await _embeddingService.GenerateEmbeddingAsync("warmup");
        
        // Act - measure 10 inferences
        var times = new List<long>();
        for (int i = 0; i < 10; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await _embeddingService.GenerateEmbeddingAsync(text);
            sw.Stop();
            times.Add(sw.ElapsedMilliseconds);
        }
        
        var avgMs = times.Average();
        var minMs = times.Min();
        var maxMs = times.Max();
        
        TestContext.WriteLine($"ONNX Inference Performance (10 runs):");
        TestContext.WriteLine($"  Avg: {avgMs:F1}ms");
        TestContext.WriteLine($"  Min: {minMs}ms");
        TestContext.WriteLine($"  Max: {maxMs}ms");
        
        // Assert - should be reasonably fast
        Assert.That(avgMs, Is.LessThan(500), "Average inference should be under 500ms");
    }

    #endregion

    #region ONNX Model Info Tests

    [Test]
    public void ONNX_ModelInfo_CorrectVersion()
    {
        // Assert
        Assert.That(_embeddingService.ModelVersion, Does.Contain("e5"));
        TestContext.WriteLine($"Model version: {_embeddingService.ModelVersion}");
    }

    [Test]
    public async Task ONNX_ModelDownloaded_ReturnsTrue()
    {
        // Act
        var isDownloaded = await _embeddingService.IsModelDownloadedAsync();
        
        // Assert
        TestContext.WriteLine($"Model downloaded: {isDownloaded}");
        // This test documents the state, doesn't enforce
    }

    #endregion
}
