using LLMClient.Services;
using NUnit.Framework;
using System.ComponentModel;

namespace LLMClient.Tests.Services;

[TestFixture]
public class LocalizationServiceTests
{
    #region AvailableLanguages Tests

    [Test]
    public void AvailableLanguages_ContainsEnglish()
    {
        // Arrange
        var service = new LocalizationService();

        // Act
        var hasEnglish = service.AvailableLanguages.Any(l => l.Code == "en-US");

        // Assert
        Assert.That(hasEnglish, Is.True);
    }

    [Test]
    public void AvailableLanguages_ContainsPolish()
    {
        // Arrange
        var service = new LocalizationService();

        // Act
        var hasPolish = service.AvailableLanguages.Any(l => l.Code == "pl-PL");

        // Assert
        Assert.That(hasPolish, Is.True);
    }

    [Test]
    public void AvailableLanguages_HasAtLeast30Languages()
    {
        // Arrange
        var service = new LocalizationService();

        // Assert
        Assert.That(service.AvailableLanguages.Count, Is.GreaterThanOrEqualTo(30));
    }

    [Test]
    public void AvailableLanguages_AllHaveRequiredProperties()
    {
        // Arrange
        var service = new LocalizationService();

        // Assert
        foreach (var lang in service.AvailableLanguages)
        {
            Assert.That(lang.Code, Is.Not.Null.And.Not.Empty, $"Code is required");
            Assert.That(lang.DisplayName, Is.Not.Null.And.Not.Empty, $"DisplayName is required for {lang.Code}");
            Assert.That(lang.NativeName, Is.Not.Null.And.Not.Empty, $"NativeName is required for {lang.Code}");
        }
    }

    [Test]
    public void AvailableLanguages_CodesAreUnique()
    {
        // Arrange
        var service = new LocalizationService();

        // Act
        var codes = service.AvailableLanguages.Select(l => l.Code).ToList();
        var uniqueCodes = codes.Distinct().ToList();

        // Assert
        Assert.That(uniqueCodes.Count, Is.EqualTo(codes.Count), "All language codes should be unique");
    }

    #endregion

    #region GetString Tests

    [Test]
    public void GetString_WithUnknownKey_ReturnsKey()
    {
        // Arrange
        var service = new LocalizationService();

        // Act
        var result = service.GetString("NonExistentKey_12345");

        // Assert
        Assert.That(result, Is.EqualTo("NonExistentKey_12345"));
    }

    [Test]
    public void Indexer_ReturnsStringFromGetString()
    {
        // Arrange
        var service = new LocalizationService();
        var key = "TestKey";

        // Act
        var indexerResult = service[key];
        var getStringResult = service.GetString(key);

        // Assert
        Assert.That(indexerResult, Is.EqualTo(getStringResult));
    }

    [Test]
    public void GetString_WithNullKey_ReturnsNull()
    {
        // Arrange
        var service = new LocalizationService();

        // Act
        var result = service.GetString(null!);

        // Assert
        Assert.That(result, Is.Null);
    }

    #endregion

    #region SetCulture Tests

    [Test]
    public void SetCulture_UpdatesCurrentCulture()
    {
        // Arrange
        var service = new LocalizationService();
        var initialCulture = service.CurrentCulture;

        // Act
        var newCulture = initialCulture == "pl-PL" ? "en-US" : "pl-PL";
        service.SetCulture(newCulture);

        // Assert
        Assert.That(service.CurrentCulture, Is.EqualTo(newCulture));
    }

    [Test]
    public void SetCulture_RaisesPropertyChanged()
    {
        // Arrange
        var service = new LocalizationService();
        var changedProperties = new List<string>();
        service.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        var newCulture = service.CurrentCulture == "pl-PL" ? "en-US" : "pl-PL";
        service.SetCulture(newCulture);

        // Assert
        Assert.That(changedProperties, Does.Contain("Item[]"));
        Assert.That(changedProperties, Does.Contain("CurrentCulture"));
    }

    [Test]
    public void SetCulture_WithSameCulture_DoesNotRaisePropertyChanged()
    {
        // Arrange
        var service = new LocalizationService();
        service.SetCulture("en-US"); // Ensure we start with en-US
        
        var changedProperties = new List<string>();
        service.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        service.SetCulture("en-US"); // Set same culture again

        // Assert
        Assert.That(changedProperties, Is.Empty, "Should not raise PropertyChanged for same culture");
    }

    [Test]
    public void SetCulture_WithInvalidCulture_ThrowsException()
    {
        // Arrange
        var service = new LocalizationService();

        // Act & Assert
        Assert.Throws<System.Globalization.CultureNotFoundException>(() => 
            service.SetCulture("invalid-culture-code-xyz"));
    }

    #endregion

    #region CurrentCulture Tests

    [Test]
    public void CurrentCulture_ReturnsValidCultureName()
    {
        // Arrange
        var service = new LocalizationService();

        // Act
        var culture = service.CurrentCulture;

        // Assert
        Assert.That(culture, Is.Not.Null.And.Not.Empty);
        Assert.DoesNotThrow(() => new System.Globalization.CultureInfo(culture));
    }

    #endregion

    #region LanguageOption Tests

    [Test]
    public void LanguageOption_CanBeCreated()
    {
        // Arrange & Act
        var option = new LanguageOption
        {
            Code = "test-CODE",
            DisplayName = "Test Language",
            NativeName = "Test Native"
        };

        // Assert
        Assert.That(option.Code, Is.EqualTo("test-CODE"));
        Assert.That(option.DisplayName, Is.EqualTo("Test Language"));
        Assert.That(option.NativeName, Is.EqualTo("Test Native"));
    }

    [Test]
    public void LanguageOption_DefaultsToEmptyStrings()
    {
        // Arrange & Act
        var option = new LanguageOption();

        // Assert
        Assert.That(option.Code, Is.EqualTo(string.Empty));
        Assert.That(option.DisplayName, Is.EqualTo(string.Empty));
        Assert.That(option.NativeName, Is.EqualTo(string.Empty));
    }

    #endregion
}
