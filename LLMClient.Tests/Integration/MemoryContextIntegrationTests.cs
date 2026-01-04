using LLMClient.Models;
using LLMClient.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Testy integracyjne MemoryContextService - ekstrakcja i wyszukiwanie wspomnień.
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("LocalModel")]
public class MemoryContextIntegrationTests
{
    private EmbeddingService _embeddingService = null!;
    private bool _modelAvailable = false;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var logger = new Mock<ILogger<EmbeddingService>>();
        _embeddingService = new EmbeddingService(logger.Object);
        
        _modelAvailable = await _embeddingService.IsModelDownloadedAsync();
        
        if (_modelAvailable)
        {
            try
            {
                await _embeddingService.InitializeAsync();
            }
            catch
            {
                _modelAvailable = false;
            }
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
            Assert.Ignore("Model lokalny nie jest dostępny.");
        }
    }

    [Test]
    public async Task MemoryEmbedding_SimilarMemories_HighSimilarity()
    {
        EnsureModelAvailable();
        
        // Arrange - wspomnienia użytkownika
        var memories = new[]
        {
            new Memory { Id = 1, Key = "name", Value = "Użytkownik ma na imię Michał i mieszka w Warszawie.", Category = "personal" },
            new Memory { Id = 2, Key = "job", Value = "Michał pracuje jako programista .NET.", Category = "work" },
            new Memory { Id = 3, Key = "lang", Value = "Ulubiony język programowania użytkownika to C#.", Category = "preferences" },
            new Memory { Id = 4, Key = "hobby", Value = "Użytkownik lubi gotować włoskie jedzenie.", Category = "hobbies" }
        };
        
        var query = "Gdzie pracuje użytkownik?";
        
        // Act - wygeneruj embeddingi
        var queryEmb = await _embeddingService.GenerateEmbeddingAsync(query, isQuery: true);
        Assert.That(queryEmb, Is.Not.Null);
        
        var results = new List<(Memory mem, float similarity)>();
        foreach (var mem in memories)
        {
            var memEmb = await _embeddingService.GenerateEmbeddingAsync(mem.Value, isQuery: false);
            if (memEmb != null)
            {
                var similarity = _embeddingService.CalculateSimilarity(queryEmb!, memEmb);
                results.Add((mem, similarity));
            }
        }
        
        var ranked = results.OrderByDescending(x => x.similarity).ToList();
        
        TestContext.WriteLine($"Query: {query}");
        TestContext.WriteLine("Ranked memories:");
        foreach (var (mem, sim) in ranked)
        {
            TestContext.WriteLine($"  [{sim:F4}] [{mem.Category}] {mem.Value}");
        }
        
        // Assert - pamięć o pracy powinna być w top 3
        var top3 = ranked.Take(3).Select(r => r.mem.Value.ToLower()).ToList();
        Assert.That(top3.Any(v => v.Contains("pracuje") || v.Contains("programist")), Is.True,
            "Pamięć o pracy powinna być w top 3 wyników");
    }

    [Test]
    public async Task MemoryEmbedding_DifferentCategories_ProperlyRanked()
    {
        EnsureModelAvailable();
        
        // Arrange
        var memories = new[]
        {
            "Użytkownik preferuje ciemny motyw interfejsu.",
            "Użytkownik ma alergię na orzechy.",
            "Użytkownik urodził się w 1990 roku.",
            "Użytkownik zna języki: polski, angielski, niemiecki."
        };
        
        var query = "Jakie języki zna użytkownik?";
        
        // Act
        var queryEmb = await _embeddingService.GenerateEmbeddingAsync(query, isQuery: true);
        Assert.That(queryEmb, Is.Not.Null);
        
        var results = new List<(string mem, float similarity)>();
        foreach (var mem in memories)
        {
            var memEmb = await _embeddingService.GenerateEmbeddingAsync(mem, isQuery: false);
            if (memEmb != null)
            {
                var similarity = _embeddingService.CalculateSimilarity(queryEmb!, memEmb);
                results.Add((mem, similarity));
            }
        }
        
        var ranked = results.OrderByDescending(x => x.similarity).ToList();
        
        TestContext.WriteLine($"Query: {query}");
        foreach (var (mem, sim) in ranked)
        {
            TestContext.WriteLine($"  [{sim:F4}] {mem}");
        }
        
        // Assert - pamięć o językach powinna być najbardziej relewantna
        Assert.That(ranked[0].mem.ToLower(), Does.Contain("język"));
    }

    [Test]
    public async Task MemoryEmbedding_RelevantContextBuilding_Works()
    {
        EnsureModelAvailable();
        
        // Arrange - symulacja budowania kontekstu dla AI
        var userMemories = new Dictionary<string, string>
        {
            ["name"] = "Użytkownik nazywa się Anna Kowalska.",
            ["location"] = "Anna mieszka w Krakowie.",
            ["job"] = "Anna jest nauczycielką matematyki.",
            ["hobby"] = "Anna lubi grać w szachy i czytać książki.",
            ["food"] = "Anna jest wegetarianką."
        };
        
        var currentMessage = "Zaplanuj mi zdrowy obiad.";
        
        // Act - znajdź najbardziej relewantne wspomnienia
        var queryEmb = await _embeddingService.GenerateEmbeddingAsync(currentMessage, isQuery: true);
        Assert.That(queryEmb, Is.Not.Null);
        
        var relevantMemories = new List<(string key, string content, float score)>();
        foreach (var (key, content) in userMemories)
        {
            var memEmb = await _embeddingService.GenerateEmbeddingAsync(content, isQuery: false);
            if (memEmb != null)
            {
                var score = _embeddingService.CalculateSimilarity(queryEmb!, memEmb);
                relevantMemories.Add((key, content, score));
            }
        }
        
        var topMemories = relevantMemories
            .OrderByDescending(x => x.score)
            .Take(3)
            .ToList();
        
        TestContext.WriteLine($"Message: {currentMessage}");
        TestContext.WriteLine("Top 3 relevant memories for context:");
        foreach (var (key, content, score) in topMemories)
        {
            TestContext.WriteLine($"  [{score:F4}] [{key}] {content}");
        }
        
        // Assert - pamięć o jedzeniu powinna być wysoko
        Assert.That(topMemories.Any(m => m.key == "food" || m.content.ToLower().Contains("wegeta")), Is.True,
            "Pamięć o diecie powinna być relewantna dla planowania obiadu");
    }

    [Test]
    public async Task MemoryEmbedding_TemporalQueries_FindsRelevant()
    {
        EnsureModelAvailable();
        
        // Arrange - wspomnienia z różnych okresów
        var memories = new[]
        {
            "Wczoraj użytkownik rozmawiał o projekcie XYZ.",
            "W zeszłym tygodniu użytkownik miał spotkanie z klientem ABC.",
            "Użytkownik planuje urlop w lipcu.",
            "Użytkownik ma deadline projektu na koniec miesiąca."
        };
        
        var query = "Co mam zaplanowane?";
        
        // Act
        var queryEmb = await _embeddingService.GenerateEmbeddingAsync(query, isQuery: true);
        Assert.That(queryEmb, Is.Not.Null);
        
        var results = new List<(string mem, float similarity)>();
        foreach (var mem in memories)
        {
            var memEmb = await _embeddingService.GenerateEmbeddingAsync(mem, isQuery: false);
            if (memEmb != null)
            {
                var similarity = _embeddingService.CalculateSimilarity(queryEmb!, memEmb);
                results.Add((mem, similarity));
            }
        }
        
        var ranked = results.OrderByDescending(x => x.similarity).ToList();
        
        TestContext.WriteLine($"Query: {query}");
        foreach (var (mem, sim) in ranked)
        {
            TestContext.WriteLine($"  [{sim:F4}] {mem}");
        }
        
        // Assert - wspomnienia o planach powinny być wyżej
        var topTwo = ranked.Take(2).Select(r => r.mem.ToLower()).ToList();
        Assert.That(topTwo.Any(m => m.Contains("planuje") || m.Contains("deadline")), Is.True);
    }

    [Test]
    public async Task MemoryEmbedding_EmptyQuery_HandlesGracefully()
    {
        EnsureModelAvailable();
        
        // Arrange
        var emptyQuery = "";
        
        // Act
        var emb = await _embeddingService.GenerateEmbeddingAsync(emptyQuery, isQuery: true);
        
        // Assert - powinien obsłużyć pusty string
        // Może zwrócić null lub embedding - ważne że nie rzuca wyjątku
        TestContext.WriteLine($"Empty query embedding: {(emb != null ? $"{emb.Length} dims" : "null")}");
    }
}
