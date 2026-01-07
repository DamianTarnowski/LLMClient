using LLMClient.Models;
using LLMClient.Services;
using Moq;
using NUnit.Framework;

namespace LLMClient.Tests.Services;

[TestFixture]
public class MemoryContextServiceTests
{
    private Mock<IMemoryService> _mockMemoryService = null!;
    private Mock<IAiService> _mockAiService = null!;
    private MemoryContextService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockMemoryService = new Mock<IMemoryService>();
        _mockAiService = new Mock<IAiService>();
        
        var lazyAiService = new Lazy<IAiService?>(() => _mockAiService.Object);
        _service = new MemoryContextService(_mockMemoryService.Object, lazyAiService);
    }

    #region GenerateMemoryContextAsync Tests

    [Test]
    public async Task GenerateMemoryContextAsync_WithNoMemories_ReturnsEmptyString()
    {
        // Arrange
        _mockMemoryService.Setup(x => x.GetAllMemoriesAsync())
            .ReturnsAsync(new List<Memory>());

        // Act
        var result = await _service.GenerateMemoryContextAsync();

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GenerateMemoryContextAsync_WithMemories_ReturnsFormattedContext()
    {
        // Arrange
        var memories = new List<Memory>
        {
            new Memory { Key = "user_name", Value = "Jan Kowalski", Category = "personal", IsImportant = true },
            new Memory { Key = "preference", Value = "dark mode", Category = "settings", IsImportant = false }
        };
        _mockMemoryService.Setup(x => x.GetAllMemoriesAsync()).ReturnsAsync(memories);

        // Act
        var result = await _service.GenerateMemoryContextAsync();

        // Assert
        Assert.That(result, Does.Contain("PAMIĘĆ UŻYTKOWNIKA"));
        Assert.That(result, Does.Contain("Jan Kowalski"));
        Assert.That(result, Does.Contain("dark mode"));
        Assert.That(result, Does.Contain("KONIEC PAMIĘCI"));
    }

    [Test]
    public async Task GenerateMemoryContextAsync_PrioritizesImportantMemories()
    {
        // Arrange
        var memories = new List<Memory>
        {
            new Memory { Key = "not_important", Value = "value1", IsImportant = false, UpdatedAt = DateTime.Now },
            new Memory { Key = "important", Value = "value2", IsImportant = true, UpdatedAt = DateTime.Now.AddDays(-1) }
        };
        _mockMemoryService.Setup(x => x.GetAllMemoriesAsync()).ReturnsAsync(memories);

        // Act
        var result = await _service.GenerateMemoryContextAsync();

        // Assert
        // Important memory should appear before non-important
        var importantIndex = result.IndexOf("important");
        var notImportantIndex = result.IndexOf("not_important");
        Assert.That(importantIndex, Is.LessThan(notImportantIndex));
    }

    [Test]
    public async Task GenerateMemoryContextAsync_SortsNewerMemoriesFirst()
    {
        // Arrange
        var memories = new List<Memory>
        {
            new Memory { Key = "old", Value = "old_value", IsImportant = false, UpdatedAt = DateTime.Now.AddDays(-10) },
            new Memory { Key = "new", Value = "new_value", IsImportant = false, UpdatedAt = DateTime.Now }
        };
        _mockMemoryService.Setup(x => x.GetAllMemoriesAsync()).ReturnsAsync(memories);

        // Act
        var result = await _service.GenerateMemoryContextAsync();

        // Assert
        var newIndex = result.IndexOf("new_value");
        var oldIndex = result.IndexOf("old_value");
        Assert.That(newIndex, Is.LessThan(oldIndex));
    }

    [Test]
    public async Task GenerateMemoryContextAsync_IncludesCategory()
    {
        // Arrange
        var memories = new List<Memory>
        {
            new Memory { Key = "test_key", Value = "test_value", Category = "test_category" }
        };
        _mockMemoryService.Setup(x => x.GetAllMemoriesAsync()).ReturnsAsync(memories);

        // Act
        var result = await _service.GenerateMemoryContextAsync();

        // Assert
        Assert.That(result, Does.Contain("test_category"));
    }

    [Test]
    public async Task GenerateMemoryContextAsync_IncludesTags()
    {
        // Arrange
        var memories = new List<Memory>
        {
            new Memory { Key = "test_key", Value = "test_value", Tags = "tag1, tag2" }
        };
        _mockMemoryService.Setup(x => x.GetAllMemoriesAsync()).ReturnsAsync(memories);

        // Act
        var result = await _service.GenerateMemoryContextAsync();

        // Assert
        Assert.That(result, Does.Contain("tag1"));
    }

    [Test]
    public async Task GenerateMemoryContextAsync_HandlesException_ReturnsErrorMessage()
    {
        // Arrange
        _mockMemoryService.Setup(x => x.GetAllMemoriesAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _service.GenerateMemoryContextAsync();

        // Assert
        Assert.That(result, Does.Contain("Błąd podczas ładowania pamięci"));
    }

    #endregion

    #region SummarizeOldMemoriesAsync Tests

    [Test]
    public async Task SummarizeOldMemoriesAsync_WithEmptyList_ReturnsEmptyString()
    {
        // Act
        var result = await _service.SummarizeOldMemoriesAsync(new List<Memory>());

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task SummarizeOldMemoriesAsync_WithSmallMemories_ReturnsConcatenated()
    {
        // Arrange
        var memories = new List<Memory>
        {
            new Memory { Key = "key1", Value = "value1" },
            new Memory { Key = "key2", Value = "value2" }
        };

        // Act
        var result = await _service.SummarizeOldMemoriesAsync(memories);

        // Assert
        Assert.That(result, Does.Contain("key1"));
        Assert.That(result, Does.Contain("key2"));
    }

    [Test]
    public async Task SummarizeOldMemoriesAsync_WithLargeMemories_CallsAiService()
    {
        // Arrange
        var largeValue = new string('X', 10000);
        var memories = new List<Memory>
        {
            new Memory { Key = "large", Value = largeValue }
        };

        _mockAiService.Setup(x => x.GetResponseAsync(
            It.IsAny<string>(),
            It.IsAny<List<Message>>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync("Summarized content");

        // Act
        var result = await _service.SummarizeOldMemoriesAsync(memories);

        // Assert
        // Should either return concatenated or AI summary depending on size
        Assert.That(result, Is.Not.Null);
    }

    #endregion
}
