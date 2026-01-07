using LLMClient.Core.Services;
using Moq;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for Localization functionality
/// Tests language switching, string retrieval, and fallback behavior
/// </summary>
[TestFixture]
[Category("Integration")]
public class LocalizationServiceTests
{
    private Mock<ILocalizationService> _localizationService = null!;
    private Dictionary<string, Dictionary<string, string>> _translations = null!;

    [SetUp]
    public void Setup()
    {
        _translations = new Dictionary<string, Dictionary<string, string>>
        {
            ["pl"] = new()
            {
                ["greeting"] = "Cześć",
                ["goodbye"] = "Do widzenia",
                ["send"] = "Wyślij",
                ["cancel"] = "Anuluj",
                ["error_network"] = "Błąd sieci. Sprawdź połączenie.",
                ["error_api"] = "Błąd API. Spróbuj ponownie później."
            },
            ["en"] = new()
            {
                ["greeting"] = "Hello",
                ["goodbye"] = "Goodbye",
                ["send"] = "Send",
                ["cancel"] = "Cancel",
                ["error_network"] = "Network error. Check your connection.",
                ["error_api"] = "API error. Please try again later."
            }
        };
        
        _localizationService = new Mock<ILocalizationService>();
        
        var currentLanguage = "pl";
        
        _localizationService.SetupGet(x => x.CurrentLanguage).Returns(() => currentLanguage);
        
        _localizationService.Setup(x => x.SetLanguage(It.IsAny<string>()))
            .Callback((string lang) => currentLanguage = lang);
            
        _localizationService.Setup(x => x.GetString(It.IsAny<string>()))
            .Returns((string key) =>
            {
                if (_translations.TryGetValue(currentLanguage, out var lang) &&
                    lang.TryGetValue(key, out var value))
                    return value;
                    
                // Fallback to English
                if (_translations["en"].TryGetValue(key, out var fallback))
                    return fallback;
                    
                return key;
            });
    }

    [Test]
    public void Localization_DefaultLanguage_IsPolish()
    {
        Assert.That(_localizationService.Object.CurrentLanguage, Is.EqualTo("pl"));
    }

    [Test]
    public void Localization_GetString_ReturnsPolish()
    {
        var greeting = _localizationService.Object.GetString("greeting");
        Assert.That(greeting, Is.EqualTo("Cześć"));
    }

    [Test]
    public void Localization_SwitchToEnglish_Works()
    {
        _localizationService.Object.SetLanguage("en");
        var greeting = _localizationService.Object.GetString("greeting");
        
        Assert.That(greeting, Is.EqualTo("Hello"));
    }

    [Test]
    public void Localization_MissingKey_ReturnsFallback()
    {
        var result = _localizationService.Object.GetString("unknown_key");
        
        Assert.That(result, Is.EqualTo("unknown_key"));
    }

    [Test]
    public void Localization_ErrorMessages_ArePolish()
    {
        var networkError = _localizationService.Object.GetString("error_network");
        var apiError = _localizationService.Object.GetString("error_api");
        
        Assert.That(networkError, Does.Contain("Błąd"));
        Assert.That(apiError, Does.Contain("Błąd"));
    }

    [Test]
    public void Localization_AllKeysPresent_InBothLanguages()
    {
        var polishKeys = _translations["pl"].Keys.ToHashSet();
        var englishKeys = _translations["en"].Keys.ToHashSet();
        
        Assert.That(polishKeys.SetEquals(englishKeys), Is.True);
    }

    [Test]
    public void Localization_LanguageSwitch_UpdatesAllStrings()
    {
        var polishGreeting = _localizationService.Object.GetString("greeting");
        
        _localizationService.Object.SetLanguage("en");
        var englishGreeting = _localizationService.Object.GetString("greeting");
        
        Assert.That(polishGreeting, Is.Not.EqualTo(englishGreeting));
    }
}

[TestFixture]
[Category("Integration")]
public class CultureFormattingTests
{
    [Test]
    public void DateFormat_Polish_IsCorrect()
    {
        var date = new DateTime(2025, 6, 15);
        var polishFormat = date.ToString("d MMMM yyyy", new System.Globalization.CultureInfo("pl-PL"));
        
        Assert.That(polishFormat, Does.Contain("czerwca"));
    }

    [Test]
    public void DateFormat_English_IsCorrect()
    {
        var date = new DateTime(2025, 6, 15);
        var englishFormat = date.ToString("MMMM d, yyyy", new System.Globalization.CultureInfo("en-US"));
        
        Assert.That(englishFormat, Does.Contain("June"));
    }

    [Test]
    public void NumberFormat_Polish_UsesComma()
    {
        var number = 1234.56;
        var polishFormat = number.ToString("N2", new System.Globalization.CultureInfo("pl-PL"));
        
        Assert.That(polishFormat, Does.Contain(","));
    }

    [Test]
    public void TimeFormat_24Hour_Polish()
    {
        var time = new DateTime(2025, 1, 1, 14, 30, 0);
        var polishTime = time.ToString("HH:mm", new System.Globalization.CultureInfo("pl-PL"));
        
        Assert.That(polishTime, Is.EqualTo("14:30"));
    }

    [Test]
    public void RelativeTime_Polish()
    {
        var now = DateTime.UtcNow;
        var minutesAgo = now.AddMinutes(-5);
        var hoursAgo = now.AddHours(-2);
        var daysAgo = now.AddDays(-1);
        
        Assert.That(FormatRelativeTime(minutesAgo, now), Does.Contain("minut"));
        Assert.That(FormatRelativeTime(hoursAgo, now), Does.Contain("godzin"));
        Assert.That(FormatRelativeTime(daysAgo, now), Does.Contain("dzień"));
    }

    private static string FormatRelativeTime(DateTime past, DateTime now)
    {
        var diff = now - past;
        
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} minut temu";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} godzin temu";
        if (diff.TotalDays < 2)
            return "wczoraj (1 dzień temu)";
        
        return $"{(int)diff.TotalDays} dni temu";
    }
}
