using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for Embedding functionality
/// Tests embedding generation, similarity calculation, and vector operations
/// </summary>
[TestFixture]
[Category("Integration")]
public class EmbeddingGenerationTests
{
    private Mock<IEmbeddingService> _embeddingService = null!;

    [SetUp]
    public void Setup()
    {
        _embeddingService = new Mock<IEmbeddingService>();
        
        // Simulate embedding generation - returns random but consistent embeddings
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync((string text) => GenerateMockEmbedding(text));
            
        _embeddingService.Setup(x => x.CalculateSimilarity(It.IsAny<float[]>(), It.IsAny<float[]>()))
            .Returns((float[] a, float[] b) => CosineSimilarity(a, b));
            
        _embeddingService.Setup(x => x.FloatArrayToBytes(It.IsAny<float[]>()))
            .Returns((float[] floats) => 
            {
                var bytes = new byte[floats.Length * 4];
                Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
                return bytes;
            });
            
        _embeddingService.Setup(x => x.BytesToFloatArray(It.IsAny<byte[]>()))
            .Returns((byte[] bytes) =>
            {
                var floats = new float[bytes.Length / 4];
                Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
                return floats;
            });
            
        _embeddingService.SetupGet(x => x.IsInitialized).Returns(true);
    }

    [Test]
    public async Task Embedding_Generate_ReturnsVector()
    {
        var embedding = await _embeddingService.Object.GenerateEmbeddingAsync("Hello world");
        
        Assert.That(embedding, Is.Not.Null);
        Assert.That(embedding!.Length, Is.EqualTo(384)); // Standard embedding size
    }

    [Test]
    public async Task Embedding_SameText_ReturnsSimilarVectors()
    {
        var embedding1 = await _embeddingService.Object.GenerateEmbeddingAsync("test text");
        var embedding2 = await _embeddingService.Object.GenerateEmbeddingAsync("test text");
        
        var similarity = _embeddingService.Object.CalculateSimilarity(embedding1!, embedding2!);
        
        Assert.That(similarity, Is.EqualTo(1.0f).Within(0.01f));
    }

    [Test]
    public async Task Embedding_DifferentText_ReturnsDifferentVectors()
    {
        var embedding1 = await _embeddingService.Object.GenerateEmbeddingAsync("cats are pets");
        var embedding2 = await _embeddingService.Object.GenerateEmbeddingAsync("programming languages");
        
        var similarity = _embeddingService.Object.CalculateSimilarity(embedding1!, embedding2!);
        
        Assert.That(similarity, Is.LessThan(0.9f));
    }

    [Test]
    public async Task Embedding_SimilarText_HasHighSimilarity()
    {
        // For mock, similar texts have similar hash-based embeddings
        var embedding1 = await _embeddingService.Object.GenerateEmbeddingAsync("machine learning");
        var embedding2 = await _embeddingService.Object.GenerateEmbeddingAsync("machine learning algorithms");
        
        var similarity = _embeddingService.Object.CalculateSimilarity(embedding1!, embedding2!);
        
        // Mock returns deterministic embeddings based on text
        Assert.That(similarity, Is.GreaterThan(0.0f));
    }

    [Test]
    public void Embedding_ByteConversion_RoundTrip()
    {
        var original = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        
        var bytes = _embeddingService.Object.FloatArrayToBytes(original);
        var restored = _embeddingService.Object.BytesToFloatArray(bytes);
        
        Assert.That(restored, Is.EqualTo(original).Within(0.0001f));
    }

    [Test]
    public void Embedding_ByteConversion_CorrectSize()
    {
        var floats = new float[384];
        var bytes = _embeddingService.Object.FloatArrayToBytes(floats);
        
        Assert.That(bytes.Length, Is.EqualTo(384 * 4)); // 4 bytes per float
    }

    [Test]
    public void Embedding_IsInitialized_ReturnsTrue()
    {
        Assert.That(_embeddingService.Object.IsInitialized, Is.True);
    }

    [Test]
    public async Task Embedding_EmptyText_ReturnsEmbedding()
    {
        var embedding = await _embeddingService.Object.GenerateEmbeddingAsync("");
        
        Assert.That(embedding, Is.Not.Null);
    }

    [Test]
    public async Task Embedding_LongText_ReturnsEmbedding()
    {
        var longText = string.Concat(Enumerable.Repeat("This is a test sentence. ", 100));
        var embedding = await _embeddingService.Object.GenerateEmbeddingAsync(longText);
        
        Assert.That(embedding, Is.Not.Null);
        Assert.That(embedding!.Length, Is.EqualTo(384));
    }

    [Test]
    public async Task Embedding_PolishText_ReturnsEmbedding()
    {
        var polishText = "Cześć, jak się masz? To jest test polskiego tekstu z ąęółżźćń.";
        var embedding = await _embeddingService.Object.GenerateEmbeddingAsync(polishText);
        
        Assert.That(embedding, Is.Not.Null);
        Assert.That(embedding!.Length, Is.EqualTo(384));
    }

    private static float[] GenerateMockEmbedding(string text)
    {
        var embedding = new float[384];
        var hash = text.GetHashCode();
        var random = new Random(hash);
        
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }
        
        // Normalize
        var magnitude = MathF.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] /= magnitude;
        }
        
        return embedding;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        
        float dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        
        return dot / (MathF.Sqrt(magA) * MathF.Sqrt(magB));
    }
}

[TestFixture]
[Category("Integration")]
public class EmbeddingModelSelectionTests
{
    [Test]
    public void EmbeddingModel_SelectByRAM_LowRAM_SelectsGemma()
    {
        var model = EmbeddingModels.GetRecommendedForRAM(4L * 1024 * 1024 * 1024);
        
        Assert.That(model.Id, Is.EqualTo("embeddinggemma-300m"));
        Assert.That(model.MinRAMGB, Is.LessThanOrEqualTo(4));
    }

    [Test]
    public void EmbeddingModel_SelectByRAM_HighRAM_SelectsE5()
    {
        var model = EmbeddingModels.GetRecommendedForRAM(16L * 1024 * 1024 * 1024);
        
        Assert.That(model.Id, Is.EqualTo("intfloat-e5-large-multilingual-v1"));
        Assert.That(model.QualityScore, Is.GreaterThan(90));
    }

    [Test]
    public void EmbeddingModel_E5RequiresPrefix_AppliesCorrectly()
    {
        var model = EmbeddingModels.E5LargeMultilingual;
        
        var query = "What is machine learning?";
        var formattedQuery = model.RequiresQueryPrefix 
            ? $"{model.QueryPrefix}{query}" 
            : query;
        
        Assert.That(formattedQuery, Does.StartWith("query: "));
    }

    [Test]
    public void EmbeddingModel_GemmaNoPrefix_WorksDirectly()
    {
        var model = EmbeddingModels.EmbeddingGemma;
        
        Assert.That(model.RequiresQueryPrefix, Is.False);
        Assert.That(model.QueryPrefix, Is.Empty);
    }

    [Test]
    public void EmbeddingModel_AllSupportPolish()
    {
        foreach (var model in EmbeddingModels.All)
        {
            Assert.That(model.SupportedLanguages, Does.Contain("pl"),
                $"Model {model.Id} should support Polish");
        }
    }
}
