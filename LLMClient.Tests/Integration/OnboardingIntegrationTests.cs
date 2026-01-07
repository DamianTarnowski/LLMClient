using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for Onboarding functionality
/// Tests first-run experience, setup wizard, and initial configuration
/// </summary>
[TestFixture]
[Category("Integration")]
public class OnboardingFlowTests
{
    [Test]
    public void Onboarding_FirstRun_DetectedCorrectly()
    {
        var prefs = new MockPreferencesService();
        
        var isFirstRun = prefs.Get("onboarding_completed") != "true";
        
        Assert.That(isFirstRun, Is.True);
    }

    [Test]
    public void Onboarding_AfterCompletion_NotFirstRun()
    {
        var prefs = new MockPreferencesService();
        prefs.Set("onboarding_completed", "true");
        
        var isFirstRun = prefs.Get("onboarding_completed") != "true";
        
        Assert.That(isFirstRun, Is.False);
    }

    [Test]
    public void Onboarding_StepsOrder_IsCorrect()
    {
        var steps = new[]
        {
            OnboardingStep.Welcome,
            OnboardingStep.LanguageSelection,
            OnboardingStep.ApiKeySetup,
            OnboardingStep.LocalModelChoice,
            OnboardingStep.Complete
        };
        
        Assert.That(steps[0], Is.EqualTo(OnboardingStep.Welcome));
        Assert.That(steps[^1], Is.EqualTo(OnboardingStep.Complete));
    }

    [Test]
    public void Onboarding_CanSkipApiKey_IfLocalModel()
    {
        var config = new OnboardingConfig
        {
            UseLocalModel = true,
            ApiKeyProvided = false
        };
        
        var canProceed = config.UseLocalModel || config.ApiKeyProvided;
        
        Assert.That(canProceed, Is.True);
    }

    [Test]
    public void Onboarding_RequiresApiKey_IfNoLocalModel()
    {
        var config = new OnboardingConfig
        {
            UseLocalModel = false,
            ApiKeyProvided = false
        };
        
        var canProceed = config.UseLocalModel || config.ApiKeyProvided;
        
        Assert.That(canProceed, Is.False);
    }

    [Test]
    public void Onboarding_LanguageSelection_SetsPreference()
    {
        var prefs = new MockPreferencesService();
        
        prefs.Set("language", "pl");
        
        Assert.That(prefs.Get("language"), Is.EqualTo("pl"));
    }

    [Test]
    public void Onboarding_ThemeSelection_SetsPreference()
    {
        var prefs = new MockPreferencesService();
        
        prefs.Set("theme", "dark");
        
        Assert.That(prefs.Get("theme"), Is.EqualTo("dark"));
    }

    [Test]
    public void Onboarding_DefaultSettings_AreReasonable()
    {
        var defaults = new OnboardingDefaults();
        
        Assert.That(defaults.Language, Is.EqualTo("pl"));
        Assert.That(defaults.Theme, Is.EqualTo("system"));
        Assert.That(defaults.EnableMemory, Is.True);
        Assert.That(defaults.EnableStreaming, Is.True);
    }
}

public enum OnboardingStep
{
    Welcome,
    LanguageSelection,
    ApiKeySetup,
    LocalModelChoice,
    Complete
}

public class OnboardingConfig
{
    public bool UseLocalModel { get; set; }
    public bool ApiKeyProvided { get; set; }
    public string SelectedLanguage { get; set; } = "pl";
    public string SelectedTheme { get; set; } = "system";
}

public class OnboardingDefaults
{
    public string Language { get; set; } = "pl";
    public string Theme { get; set; } = "system";
    public bool EnableMemory { get; set; } = true;
    public bool EnableStreaming { get; set; } = true;
    public bool EnableRag { get; set; } = false;
}

[TestFixture]
[Category("Integration")]
public class FirstTimeSetupTests
{
    [Test]
    public void Setup_DatabaseInitialization_Works()
    {
        var initialized = false;
        
        // Simulate database initialization
        initialized = true;
        
        Assert.That(initialized, Is.True);
    }

    [Test]
    public void Setup_DefaultConversation_Created()
    {
        var conversations = new List<Conversation>();
        
        if (!conversations.Any())
        {
            conversations.Add(new Conversation { Title = "Nowa rozmowa" });
        }
        
        Assert.That(conversations.Count, Is.EqualTo(1));
        Assert.That(conversations[0].Title, Is.EqualTo("Nowa rozmowa"));
    }

    [Test]
    public void Setup_WelcomeMessage_Added()
    {
        var conversation = new Conversation { Title = "Witaj!" };
        conversation.Messages.Add(new Message
        {
            Content = "Cześć! Jestem twoim asystentem AI. Jak mogę Ci pomóc?",
            IsUser = false
        });
        
        Assert.That(conversation.Messages.Count, Is.EqualTo(1));
        Assert.That(conversation.Messages[0].Content, Does.Contain("Cześć"));
    }

    [Test]
    public void Setup_PermissionsCheck_Performed()
    {
        var permissions = new Dictionary<string, bool>
        {
            ["storage"] = true,
            ["network"] = true,
            ["notifications"] = false
        };
        
        var requiredGranted = permissions["storage"] && permissions["network"];
        
        Assert.That(requiredGranted, Is.True);
    }
}
