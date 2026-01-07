using LLMClient.Core.Models;

namespace LLMClient.Tests.Models;

/// <summary>
/// Tests for EmbeddingModelInfo and related classes
/// </summary>
[TestFixture]
public class EmbeddingModelInfoTests
{
    [Test]
    public void EmbeddingModelInfo_CreateNew_HasDefaultValues()
    {
        var model = new EmbeddingModelInfo();
        
        Assert.That(model.Id, Is.Empty);
        Assert.That(model.DisplayName, Is.Empty);
        Assert.That(model.Dimensions, Is.EqualTo(0));
        Assert.That(model.MinRAMGB, Is.EqualTo(4));
        Assert.That(model.QualityScore, Is.EqualTo(70));
        Assert.That(model.SpeedScore, Is.EqualTo(50));
    }

    [Test]
    public void EmbeddingModelInfo_SetValues_UpdatesProperties()
    {
        var model = new EmbeddingModelInfo
        {
            Id = "custom-model",
            DisplayName = "Custom Model",
            Description = "A custom embedding model",
            Dimensions = 512,
            SizeInMB = 500,
            HuggingFaceRepo = "custom/model",
            SupportedLanguages = new[] { "en", "pl" },
            IsRecommended = true,
            IsDefault = false,
            RequiresQueryPrefix = true,
            QueryPrefix = "query: ",
            PassagePrefix = "passage: ",
            MinRAMGB = 6,
            QualityScore = 85,
            SpeedScore = 75
        };
        
        Assert.That(model.Id, Is.EqualTo("custom-model"));
        Assert.That(model.Dimensions, Is.EqualTo(512));
        Assert.That(model.SupportedLanguages.Length, Is.EqualTo(2));
        Assert.That(model.RequiresQueryPrefix, Is.True);
        Assert.That(model.QualityScore, Is.EqualTo(85));
    }
}

[TestFixture]
public class EmbeddingModelsStaticTests
{
    [Test]
    public void EmbeddingModels_EmbeddingGemma_HasCorrectValues()
    {
        var model = EmbeddingModels.EmbeddingGemma;
        
        Assert.That(model.Id, Is.EqualTo("embeddinggemma-300m"));
        Assert.That(model.Dimensions, Is.EqualTo(768));
        Assert.That(model.RequiresQueryPrefix, Is.False);
        Assert.That(model.MinRAMGB, Is.EqualTo(4));
        Assert.That(model.SpeedScore, Is.EqualTo(90));
    }

    [Test]
    public void EmbeddingModels_E5LargeMultilingual_HasCorrectValues()
    {
        var model = EmbeddingModels.E5LargeMultilingual;
        
        Assert.That(model.Id, Is.EqualTo("intfloat-e5-large-multilingual-v1"));
        Assert.That(model.Dimensions, Is.EqualTo(1024));
        Assert.That(model.RequiresQueryPrefix, Is.True);
        Assert.That(model.QueryPrefix, Is.EqualTo("query: "));
        Assert.That(model.PassagePrefix, Is.EqualTo("passage: "));
        Assert.That(model.IsDefault, Is.True);
        Assert.That(model.IsRecommended, Is.True);
        Assert.That(model.MinRAMGB, Is.EqualTo(8));
    }

    [Test]
    public void EmbeddingModels_All_ContainsBothModels()
    {
        var all = EmbeddingModels.All;
        
        Assert.That(all.Count, Is.EqualTo(2));
        Assert.That(all.Any(m => m.Id == "embeddinggemma-300m"), Is.True);
        Assert.That(all.Any(m => m.Id == "intfloat-e5-large-multilingual-v1"), Is.True);
    }

    [Test]
    public void EmbeddingModels_GetById_ReturnsCorrectModel()
    {
        var model = EmbeddingModels.GetById("embeddinggemma-300m");
        
        Assert.That(model.Id, Is.EqualTo("embeddinggemma-300m"));
        Assert.That(model.DisplayName, Does.Contain("Gemma"));
    }

    [Test]
    public void EmbeddingModels_GetById_UnknownId_ReturnsFallback()
    {
        var model = EmbeddingModels.GetById("unknown-model");
        
        // Falls back to EmbeddingGemma
        Assert.That(model.Id, Is.EqualTo("embeddinggemma-300m"));
    }

    [Test]
    public void EmbeddingModels_GetDefault_ReturnsE5Large()
    {
        var model = EmbeddingModels.GetDefault();
        
        Assert.That(model.IsDefault, Is.True);
        Assert.That(model.Id, Is.EqualTo("intfloat-e5-large-multilingual-v1"));
    }

    [Test]
    public void EmbeddingModels_GetRecommendedForRAM_LowRAM_ReturnsGemma()
    {
        // 4 GB RAM
        var model = EmbeddingModels.GetRecommendedForRAM(4L * 1024 * 1024 * 1024);
        
        Assert.That(model.Id, Is.EqualTo("embeddinggemma-300m"));
    }

    [Test]
    public void EmbeddingModels_GetRecommendedForRAM_HighRAM_ReturnsE5()
    {
        // 16 GB RAM
        var model = EmbeddingModels.GetRecommendedForRAM(16L * 1024 * 1024 * 1024);
        
        Assert.That(model.Id, Is.EqualTo("intfloat-e5-large-multilingual-v1"));
    }

    [Test]
    public void EmbeddingModels_GetRecommendedForRAM_Boundary_8GB_ReturnsE5()
    {
        // Exactly 8 GB RAM - should use E5
        var model = EmbeddingModels.GetRecommendedForRAM(8L * 1024 * 1024 * 1024);
        
        Assert.That(model.Id, Is.EqualTo("intfloat-e5-large-multilingual-v1"));
    }

    [Test]
    public void EmbeddingModels_SupportedLanguages_IncludesPolish()
    {
        var gemma = EmbeddingModels.EmbeddingGemma;
        var e5 = EmbeddingModels.E5LargeMultilingual;
        
        Assert.That(gemma.SupportedLanguages, Does.Contain("pl"));
        Assert.That(e5.SupportedLanguages, Does.Contain("pl"));
    }

    [Test]
    public void EmbeddingModels_QualityVsSpeed_Tradeoff()
    {
        var gemma = EmbeddingModels.EmbeddingGemma;
        var e5 = EmbeddingModels.E5LargeMultilingual;
        
        // E5 has higher quality
        Assert.That(e5.QualityScore, Is.GreaterThan(gemma.QualityScore));
        
        // Gemma is faster
        Assert.That(gemma.SpeedScore, Is.GreaterThan(e5.SpeedScore));
    }
}
