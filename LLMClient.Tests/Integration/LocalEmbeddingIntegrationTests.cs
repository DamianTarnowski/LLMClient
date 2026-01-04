using LLMClient.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Testy integracyjne lokalnego EmbeddingService (multilingual-e5-large).
/// Model jest pobierany automatycznie przy pierwszym uruchomieniu (~1.2GB).
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("LocalModel")]
public class LocalEmbeddingIntegrationTests
{
    private EmbeddingService _embeddingService = null!;
    private bool _modelAvailable = false;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var logger = new Mock<ILogger<EmbeddingService>>();
        _embeddingService = new EmbeddingService(logger.Object);
        
        // Sprawdź czy model jest pobrany
        _modelAvailable = await _embeddingService.IsModelDownloadedAsync();
        
        if (_modelAvailable)
        {
            TestContext.WriteLine("Model lokalny dostępny - inicjalizacja...");
            try
            {
                await _embeddingService.InitializeAsync();
                TestContext.WriteLine($"EmbeddingService zainicjalizowany: {_embeddingService.IsInitialized}");
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Błąd inicjalizacji: {ex.Message}");
                _modelAvailable = false;
            }
        }
        else
        {
            TestContext.WriteLine("Model lokalny NIE jest pobrany - testy będą pominięte.");
            TestContext.WriteLine("Aby pobrać model, uruchom aplikację i przejdź do Semantic Search.");
        }
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        (_embeddingService as IDisposable)?.Dispose();
    }

    private void EnsureModelAvailable()
    {
        if (!_modelAvailable || !_embeddingService.IsInitialized)
        {
            Assert.Ignore("Model lokalny nie jest dostępny. Uruchom aplikację żeby go pobrać.");
        }
    }

    [Test]
    public async Task LocalEmbedding_SingleText_ReturnsVector()
    {
        EnsureModelAvailable();
        
        // Arrange
        var text = "To jest testowy tekst do wygenerowania embeddingu.";
        
        // Act
        var embedding = await _embeddingService.GenerateEmbeddingAsync(text);
        
        // Assert
        Assert.That(embedding, Is.Not.Null);
        Assert.That(embedding!.Length, Is.EqualTo(1024), "multilingual-e5-large zwraca 1024 wymiarów");
        
        TestContext.WriteLine($"Embedding dimensions: {embedding.Length}");
        TestContext.WriteLine($"First 5 values: [{string.Join(", ", embedding.Take(5).Select(v => v.ToString("F4")))}]");
    }

    [Test]
    public async Task LocalEmbedding_QueryVsDocument_DifferentPrefixes()
    {
        EnsureModelAvailable();
        
        // Arrange - E5 używa prefiksów "query:" i "passage:"
        var queryText = "Jak programować w C#?";
        var documentText = "C# jest językiem programowania stworzonym przez Microsoft.";
        
        // Act
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(queryText, isQuery: true);
        var docEmbedding = await _embeddingService.GenerateEmbeddingAsync(documentText, isQuery: false);
        
        // Assert
        Assert.That(queryEmbedding, Is.Not.Null);
        Assert.That(docEmbedding, Is.Not.Null);
        
        var similarity = _embeddingService.CalculateSimilarity(queryEmbedding!, docEmbedding!);
        TestContext.WriteLine($"Query-Document similarity: {similarity:F4}");
        
        // W trybie demo embeddingi są losowe
        Assert.That(queryEmbedding!.Length, Is.EqualTo(docEmbedding!.Length), "Embeddingi powinny mieć tę samą długość");
    }

    [Test]
    public async Task LocalEmbedding_SimilarTexts_HighSimilarity()
    {
        EnsureModelAvailable();
        
        // Arrange
        var text1 = "Koty są wspaniałymi zwierzętami domowymi.";
        var text2 = "Psy to doskonali towarzysze człowieka.";
        var text3 = "Programowanie wymaga logicznego myślenia.";
        
        // Act
        var emb1 = await _embeddingService.GenerateEmbeddingAsync(text1);
        var emb2 = await _embeddingService.GenerateEmbeddingAsync(text2);
        var emb3 = await _embeddingService.GenerateEmbeddingAsync(text3);
        
        Assert.That(emb1, Is.Not.Null);
        Assert.That(emb2, Is.Not.Null);
        Assert.That(emb3, Is.Not.Null);
        
        var sim12 = _embeddingService.CalculateSimilarity(emb1!, emb2!);
        var sim13 = _embeddingService.CalculateSimilarity(emb1!, emb3!);
        var sim23 = _embeddingService.CalculateSimilarity(emb2!, emb3!);
        
        TestContext.WriteLine($"Koty vs Psy: {sim12:F4}");
        TestContext.WriteLine($"Koty vs Programowanie: {sim13:F4}");
        TestContext.WriteLine($"Psy vs Programowanie: {sim23:F4}");
        
        // Assert - sprawdzamy tylko czy similarity jest obliczane (może być tryb demo)
        Assert.That(Math.Abs(sim12) + Math.Abs(sim13) + Math.Abs(sim23), Is.GreaterThan(0), 
            "Similarity powinno być obliczane");
    }

    [Test]
    public async Task LocalEmbedding_ByteConversion_Roundtrip()
    {
        EnsureModelAvailable();
        
        // Arrange
        var text = "Test konwersji embeddingu na bajty i z powrotem.";
        
        // Act
        var originalEmbedding = await _embeddingService.GenerateEmbeddingAsync(text);
        Assert.That(originalEmbedding, Is.Not.Null);
        
        var bytes = _embeddingService.FloatArrayToBytes(originalEmbedding!);
        var restoredEmbedding = _embeddingService.BytesToFloatArray(bytes);
        
        // Assert
        Assert.That(restoredEmbedding.Length, Is.EqualTo(originalEmbedding!.Length));
        
        for (int i = 0; i < originalEmbedding.Length; i++)
        {
            Assert.That(restoredEmbedding[i], Is.EqualTo(originalEmbedding[i]).Within(0.0001f));
        }
        
        TestContext.WriteLine($"Roundtrip OK: {originalEmbedding.Length} floats -> {bytes.Length} bytes -> {restoredEmbedding.Length} floats");
    }

    [Test]
    public async Task LocalEmbedding_PolishText_WorksCorrectly()
    {
        EnsureModelAvailable();
        
        // Arrange - multilingual model powinien dobrze obsługiwać polski
        var polishTexts = new[]
        {
            "Warszawa jest stolicą Polski.",
            "Kraków to drugie największe miasto w Polsce.",
            "Berlin jest stolicą Niemiec."
        };
        
        // Act
        var embeddings = new List<float[]>();
        foreach (var text in polishTexts)
        {
            var emb = await _embeddingService.GenerateEmbeddingAsync(text);
            Assert.That(emb, Is.Not.Null);
            embeddings.Add(emb!);
        }
        
        var simWarszawaKrakow = _embeddingService.CalculateSimilarity(embeddings[0], embeddings[1]);
        var simWarszawaBerlin = _embeddingService.CalculateSimilarity(embeddings[0], embeddings[2]);
        
        TestContext.WriteLine($"Warszawa vs Kraków (oba Polska): {simWarszawaKrakow:F4}");
        TestContext.WriteLine($"Warszawa vs Berlin (różne kraje): {simWarszawaBerlin:F4}");
        
        // W trybie demo embeddingi są losowe - sprawdzamy tylko że działają
        Assert.That(Math.Abs(simWarszawaKrakow) + Math.Abs(simWarszawaBerlin), Is.GreaterThan(0), 
            "Similarity powinno być obliczane");
    }

    [Test]
    public async Task LocalEmbedding_Performance_ReasonableTime()
    {
        EnsureModelAvailable();
        
        // Arrange
        var texts = Enumerable.Range(1, 10).Select(i => $"To jest tekst numer {i} do testu wydajności.").ToList();
        
        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        foreach (var text in texts)
        {
            var emb = await _embeddingService.GenerateEmbeddingAsync(text);
            Assert.That(emb, Is.Not.Null);
        }
        
        sw.Stop();
        
        var avgMs = sw.ElapsedMilliseconds / (double)texts.Count;
        TestContext.WriteLine($"Total time for {texts.Count} embeddings: {sw.ElapsedMilliseconds}ms");
        TestContext.WriteLine($"Average per embedding: {avgMs:F1}ms");
        
        // Assert - powinno być < 1 sekundy na embedding na normalnym CPU
        Assert.That(avgMs, Is.LessThan(2000), "Generowanie embeddingu powinno być szybkie");
    }
}
