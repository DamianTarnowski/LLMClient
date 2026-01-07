using LLMClient.Core.Models;

namespace LLMClient.Tests.Models;

/// <summary>
/// Tests for ModelSettings
/// </summary>
[TestFixture]
public class ModelSettingsTests
{
    [Test]
    public void ModelSettings_CreateNew_HasDefaultValues()
    {
        var settings = new ModelSettings();
        
        Assert.That(settings.Id, Is.EqualTo(0));
        Assert.That(settings.Temperature, Is.EqualTo(0.7f).Within(0.01f));
        Assert.That(settings.MaxTokens, Is.EqualTo(2048));
        Assert.That(settings.EnableStreaming, Is.True);
        Assert.That(settings.EnableMemory, Is.True);
        Assert.That(settings.EnableRag, Is.False);
        Assert.That(settings.RagSearchMode, Is.EqualTo("Hybrid"));
        Assert.That(settings.RagTopK, Is.EqualTo(5));
        Assert.That(settings.RagMinSimilarity, Is.EqualTo(0.3f).Within(0.01f));
    }

    [Test]
    public void ModelSettings_SetTemperature_UpdatesProperty()
    {
        var settings = new ModelSettings { Temperature = 0.9f };
        
        Assert.That(settings.Temperature, Is.EqualTo(0.9f).Within(0.01f));
    }

    [Test]
    public void ModelSettings_SetMaxTokens_UpdatesProperty()
    {
        var settings = new ModelSettings { MaxTokens = 4096 };
        
        Assert.That(settings.MaxTokens, Is.EqualTo(4096));
    }

    [Test]
    public void ModelSettings_SetSystemPrompt_UpdatesProperty()
    {
        var settings = new ModelSettings
        {
            SystemPrompt = "You are a helpful assistant that speaks Polish."
        };
        
        Assert.That(settings.SystemPrompt, Does.Contain("Polish"));
    }

    [Test]
    public void ModelSettings_SetRagSettings_UpdatesProperties()
    {
        var settings = new ModelSettings
        {
            EnableRag = true,
            RagSearchMode = "Vector",
            RagTopK = 10,
            RagMinSimilarity = 0.5f
        };
        
        Assert.That(settings.EnableRag, Is.True);
        Assert.That(settings.RagSearchMode, Is.EqualTo("Vector"));
        Assert.That(settings.RagTopK, Is.EqualTo(10));
        Assert.That(settings.RagMinSimilarity, Is.EqualTo(0.5f).Within(0.01f));
    }

    [Test]
    public void ModelSettings_SelectedModelId_CanBeSet()
    {
        var settings = new ModelSettings
        {
            SelectedModelId = "gpt-4-turbo"
        };
        
        Assert.That(settings.SelectedModelId, Is.EqualTo("gpt-4-turbo"));
    }

    [Test]
    public void ModelSettings_TemperatureRange_ZeroIsValid()
    {
        var settings = new ModelSettings { Temperature = 0.0f };
        Assert.That(settings.Temperature, Is.EqualTo(0.0f));
    }

    [Test]
    public void ModelSettings_TemperatureRange_TwoIsValid()
    {
        var settings = new ModelSettings { Temperature = 2.0f };
        Assert.That(settings.Temperature, Is.EqualTo(2.0f));
    }
}

[TestFixture]
public class MessageWithConversationTitleTests
{
    [Test]
    public void MessageWithConversationTitle_InheritsFromMessage()
    {
        var msg = new MessageWithConversationTitle();
        
        // Should have all Message properties
        Assert.That(msg.Content, Is.Empty);
        Assert.That(msg.IsUser, Is.False);
    }

    [Test]
    public void MessageWithConversationTitle_HasConversationTitle()
    {
        var msg = new MessageWithConversationTitle
        {
            Content = "Hello",
            ConversationTitle = "My Chat"
        };
        
        Assert.That(msg.ConversationTitle, Is.EqualTo("My Chat"));
        Assert.That(msg.Content, Is.EqualTo("Hello"));
    }

    [Test]
    public void MessageWithConversationTitle_CanBeUsedAsMessage()
    {
        Message msg = new MessageWithConversationTitle
        {
            Content = "Test",
            IsUser = true
        };
        
        Assert.That(msg.Content, Is.EqualTo("Test"));
        Assert.That(msg.IsUser, Is.True);
    }
}
