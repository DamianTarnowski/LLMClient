using LLMClient.Services;
using Moq;
using NUnit.Framework;

namespace LLMClient.Tests.Services;

[TestFixture]
public class OnboardingServiceTests
{
    private Mock<ILocalizationService> _mockLocalizationService = null!;
    private Mock<ILocalModelService> _mockLocalModelService = null!;
    private OnboardingService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockLocalizationService = new Mock<ILocalizationService>();
        _mockLocalModelService = new Mock<ILocalModelService>();
        
        // Setup default localization behavior
        _mockLocalizationService.Setup(x => x.GetString(It.IsAny<string>()))
            .Returns<string>(key => key);
        _mockLocalizationService.Setup(x => x.CurrentCulture).Returns("en-US");
        
        _service = new OnboardingService(_mockLocalizationService.Object, _mockLocalModelService.Object);
    }

    [TearDown]
    public void TearDown()
    {
        // Reset preferences after each test
        Preferences.Remove("onboarding_completed");
        Preferences.Remove("onboarding_steps");
    }

    #region GetOnboardingStepsAsync Tests

    [Test]
    public async Task GetOnboardingStepsAsync_ReturnsSteps()
    {
        // Act
        var steps = await _service.GetOnboardingStepsAsync();

        // Assert
        Assert.That(steps, Is.Not.Null);
        Assert.That(steps.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task GetOnboardingStepsAsync_ContainsWelcomeStep()
    {
        // Act
        var steps = await _service.GetOnboardingStepsAsync();

        // Assert
        Assert.That(steps.Any(s => s.Id == "welcome"), Is.True);
    }

    [Test]
    public async Task GetOnboardingStepsAsync_ContainsAiSetupStep()
    {
        // Act
        var steps = await _service.GetOnboardingStepsAsync();

        // Assert
        Assert.That(steps.Any(s => s.Id == "ai_setup"), Is.True);
    }

    [Test]
    public async Task GetOnboardingStepsAsync_ContainsMemoryStep()
    {
        // Act
        var steps = await _service.GetOnboardingStepsAsync();

        // Assert
        Assert.That(steps.Any(s => s.Id == "memory_system"), Is.True);
    }

    [Test]
    public async Task GetOnboardingStepsAsync_StepsHaveRequiredProperties()
    {
        // Act
        var steps = await _service.GetOnboardingStepsAsync();

        // Assert
        foreach (var step in steps)
        {
            Assert.That(step.Id, Is.Not.Null.And.Not.Empty, "Id is required");
            Assert.That(step.TitleKey, Is.Not.Null.And.Not.Empty, $"TitleKey is required for {step.Id}");
            Assert.That(step.DescriptionKey, Is.Not.Null.And.Not.Empty, $"DescriptionKey is required for {step.Id}");
            Assert.That(step.IconCode, Is.Not.Null.And.Not.Empty, $"IconCode is required for {step.Id}");
        }
    }

    #endregion

    #region IsOnboardingCompletedAsync Tests

    [Test]
    public async Task IsOnboardingCompletedAsync_WhenNotCompleted_ReturnsFalse()
    {
        // Arrange
        Preferences.Remove("onboarding_completed");

        // Act
        var result = await _service.IsOnboardingCompletedAsync();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task IsOnboardingCompletedAsync_WhenCompleted_ReturnsTrue()
    {
        // Arrange
        await _service.CompleteOnboardingAsync();

        // Act
        var result = await _service.IsOnboardingCompletedAsync();

        // Assert
        Assert.That(result, Is.True);
    }

    #endregion

    #region MarkStepAsCompletedAsync Tests

    [Test]
    public async Task MarkStepAsCompletedAsync_MarksStepAsCompleted()
    {
        // Act
        await _service.MarkStepAsCompletedAsync("welcome");
        var steps = await _service.GetOnboardingStepsAsync();

        // Assert
        var welcomeStep = steps.FirstOrDefault(s => s.Id == "welcome");
        Assert.That(welcomeStep?.IsCompleted, Is.True);
    }

    [Test]
    public async Task MarkStepAsCompletedAsync_DoesNotAffectOtherSteps()
    {
        // Act
        await _service.MarkStepAsCompletedAsync("welcome");
        var steps = await _service.GetOnboardingStepsAsync();

        // Assert
        var otherSteps = steps.Where(s => s.Id != "welcome");
        Assert.That(otherSteps.All(s => !s.IsCompleted), Is.True);
    }

    [Test]
    public async Task MarkStepAsCompletedAsync_WithInvalidStepId_DoesNotThrow()
    {
        // Act & Assert
        Assert.DoesNotThrowAsync(async () => 
            await _service.MarkStepAsCompletedAsync("non_existent_step"));
    }

    #endregion

    #region CompleteOnboardingAsync Tests

    [Test]
    public async Task CompleteOnboardingAsync_SetsOnboardingAsCompleted()
    {
        // Act
        await _service.CompleteOnboardingAsync();

        // Assert
        Assert.That(_service.ShouldShowOnboarding, Is.False);
    }

    #endregion

    #region ResetOnboardingAsync Tests

    [Test]
    public async Task ResetOnboardingAsync_ResetsOnboardingState()
    {
        // Arrange
        await _service.CompleteOnboardingAsync();
        Assert.That(_service.ShouldShowOnboarding, Is.False);

        // Act
        await _service.ResetOnboardingAsync();

        // Assert
        Assert.That(_service.ShouldShowOnboarding, Is.True);
    }

    [Test]
    public async Task ResetOnboardingAsync_ResetsAllSteps()
    {
        // Arrange
        await _service.MarkStepAsCompletedAsync("welcome");
        await _service.MarkStepAsCompletedAsync("ai_setup");

        // Act
        await _service.ResetOnboardingAsync();
        var steps = await _service.GetOnboardingStepsAsync();

        // Assert
        Assert.That(steps.All(s => !s.IsCompleted), Is.True);
    }

    #endregion

    #region ShouldShowOnboarding Tests

    [Test]
    public void ShouldShowOnboarding_WhenNotCompleted_ReturnsTrue()
    {
        // Arrange
        Preferences.Remove("onboarding_completed");

        // Assert
        Assert.That(_service.ShouldShowOnboarding, Is.True);
    }

    [Test]
    public async Task ShouldShowOnboarding_WhenCompleted_ReturnsFalse()
    {
        // Arrange
        await _service.CompleteOnboardingAsync();

        // Assert
        Assert.That(_service.ShouldShowOnboarding, Is.False);
    }

    #endregion

    #region OnboardingStep Tests

    [Test]
    public void OnboardingStep_DefaultValues()
    {
        // Arrange & Act
        var step = new OnboardingStep();

        // Assert
        Assert.That(step.Id, Is.EqualTo(string.Empty));
        Assert.That(step.TitleKey, Is.EqualTo(string.Empty));
        Assert.That(step.DescriptionKey, Is.EqualTo(string.Empty));
        Assert.That(step.IconCode, Is.EqualTo(string.Empty));
        Assert.That(step.IsCompleted, Is.False);
        Assert.That(step.RequiresLocalModel, Is.False);
        Assert.That(step.OnboardingTopic, Is.EqualTo("general"));
    }

    [Test]
    public void OnboardingStep_CanSetProperties()
    {
        // Arrange & Act
        var step = new OnboardingStep
        {
            Id = "test_id",
            TitleKey = "test_title",
            DescriptionKey = "test_desc",
            IconCode = "&#xe001;",
            IsCompleted = true,
            RequiresLocalModel = true,
            OnboardingTopic = "custom_topic"
        };

        // Assert
        Assert.That(step.Id, Is.EqualTo("test_id"));
        Assert.That(step.TitleKey, Is.EqualTo("test_title"));
        Assert.That(step.IsCompleted, Is.True);
        Assert.That(step.RequiresLocalModel, Is.True);
        Assert.That(step.OnboardingTopic, Is.EqualTo("custom_topic"));
    }

    #endregion

    #region Constructor Tests

    [Test]
    public void Constructor_WithNullLocalModelService_DoesNotThrow()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => 
            new OnboardingService(_mockLocalizationService.Object, null));
    }

    [Test]
    public void Constructor_WithLocalModelService_SetsService()
    {
        // Act
        var service = new OnboardingService(_mockLocalizationService.Object, _mockLocalModelService.Object);

        // Assert
        Assert.That(service, Is.Not.Null);
    }

    #endregion
}
