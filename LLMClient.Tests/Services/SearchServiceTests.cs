using LLMClient.Models;
using LLMClient.Services;
using Moq;
using NUnit.Framework;
using System.Collections.ObjectModel;

namespace LLMClient.Tests.Services;

[TestFixture]
public class SearchServiceTests
{
    private Mock<DatabaseService> _mockDatabaseService = null!;
    private Mock<IEmbeddingService> _mockEmbeddingService = null!;

    [SetUp]
    public void SetUp()
    {
        _mockEmbeddingService = new Mock<IEmbeddingService>();
    }

    private Conversation CreateTestConversation(params string[] messageContents)
    {
        var conversation = new Conversation
        {
            Id = 1,
            Title = "Test Conversation",
            Messages = new ObservableCollection<Message>()
        };

        for (int i = 0; i < messageContents.Length; i++)
        {
            conversation.Messages.Add(new Message
            {
                Id = i + 1,
                ConversationId = 1,
                Content = messageContents[i],
                IsUser = i % 2 == 0,
                Timestamp = DateTime.Now.AddMinutes(-messageContents.Length + i)
            });
        }

        return conversation;
    }

    #region SearchInConversation Tests

    [Test]
    public void SearchInConversation_WithNullSearchTerm_ReturnsEmptyList()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = CreateTestConversation("Hello world", "Test message");

        // Act
        var results = service.SearchInConversation(conversation, null!);

        // Assert
        Assert.That(results, Is.Empty);
        Assert.That(service.HasResults, Is.False);
    }

    [Test]
    public void SearchInConversation_WithEmptySearchTerm_ReturnsEmptyList()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = CreateTestConversation("Hello world", "Test message");

        // Act
        var results = service.SearchInConversation(conversation, "");

        // Assert
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void SearchInConversation_WithWhitespaceSearchTerm_ReturnsEmptyList()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = CreateTestConversation("Hello world", "Test message");

        // Act
        var results = service.SearchInConversation(conversation, "   ");

        // Assert
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void SearchInConversation_WithNullConversation_ReturnsEmptyList()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);

        // Act
        var results = service.SearchInConversation(null!, "test");

        // Assert
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void SearchInConversation_WithMatchingTerm_ReturnsResults()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = CreateTestConversation(
            "Hello world, this is a test",
            "Another message here",
            "This test is important"
        );

        // Act
        var results = service.SearchInConversation(conversation, "test");

        // Assert
        Assert.That(results.Count, Is.EqualTo(2));
        Assert.That(service.HasResults, Is.True);
        Assert.That(service.CurrentResultIndex, Is.EqualTo(0));
    }

    [Test]
    public void SearchInConversation_IsCaseInsensitive()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = CreateTestConversation(
            "Hello WORLD",
            "hello world",
            "HeLLo WoRLD"
        );

        // Act
        var results = service.SearchInConversation(conversation, "hello");

        // Assert
        Assert.That(results.Count, Is.EqualTo(3));
    }

    [Test]
    public void SearchInConversation_FindsMultipleMatchesInSameMessage()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = CreateTestConversation(
            "test test test"
        );

        // Act
        var results = service.SearchInConversation(conversation, "test");

        // Assert
        Assert.That(results.Count, Is.EqualTo(3));
    }

    [Test]
    public void SearchInConversation_SetsCorrectStartIndexAndLength()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = CreateTestConversation("Hello world");

        // Act
        var results = service.SearchInConversation(conversation, "world");

        // Assert
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].StartIndex, Is.EqualTo(6));
        Assert.That(results[0].Length, Is.EqualTo(5));
    }

    [Test]
    public void SearchInConversation_SkipsMessagesWithNullContent()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = new Conversation
        {
            Id = 1,
            Messages = new ObservableCollection<Message>
            {
                new Message { Id = 1, Content = null },
                new Message { Id = 2, Content = "test message" },
                new Message { Id = 3, Content = "" }
            }
        };

        // Act
        var results = service.SearchInConversation(conversation, "test");

        // Assert
        Assert.That(results.Count, Is.EqualTo(1));
    }

    [Test]
    public void SearchInConversation_HandlesSpecialRegexCharacters()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = CreateTestConversation(
            "This has [special] characters",
            "And (parentheses) too",
            "Plus some.*regex+stuff?"
        );

        // Act
        var results1 = service.SearchInConversation(conversation, "[special]");
        var results2 = service.SearchInConversation(conversation, "(parentheses)");
        var results3 = service.SearchInConversation(conversation, ".*regex+");

        // Assert
        Assert.That(results1.Count, Is.EqualTo(1));
        Assert.That(results2.Count, Is.EqualTo(1));
        Assert.That(results3.Count, Is.EqualTo(1));
    }

    #endregion

    #region SearchInConversationAsync Tests

    [Test]
    public async Task SearchInConversationAsync_WithNullSearchTerm_ReturnsEmptyList()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = CreateTestConversation("Hello world");

        // Act
        var results = await service.SearchInConversationAsync(conversation, null!);

        // Assert
        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task SearchInConversationAsync_WithMatchingTerm_ReturnsResults()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = CreateTestConversation(
            "Hello world, this is a test",
            "Another test message"
        );

        // Act
        var results = await service.SearchInConversationAsync(conversation, "test");

        // Assert
        Assert.That(results.Count, Is.EqualTo(2));
        Assert.That(service.HasResults, Is.True);
    }

    [Test]
    public async Task SearchInConversationAsync_IsCaseInsensitive()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = CreateTestConversation("HELLO World", "hello WORLD");

        // Act
        var results = await service.SearchInConversationAsync(conversation, "hello");

        // Assert
        Assert.That(results.Count, Is.EqualTo(2));
    }

    #endregion

    #region Navigation Tests

    [Test]
    public void GetCurrentResult_WithNoResults_ReturnsNull()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);

        // Act
        var result = service.GetCurrentResult();

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetCurrentResult_WithResults_ReturnsFirstResult()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = CreateTestConversation("test one", "test two");
        service.SearchInConversation(conversation, "test");

        // Act
        var result = service.GetCurrentResult();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Message.Content, Is.EqualTo("test one"));
    }

    [Test]
    public void GetNextResult_CyclesThroughResults()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = CreateTestConversation("test one", "test two", "test three");
        service.SearchInConversation(conversation, "test");

        // Act & Assert
        Assert.That(service.CurrentResultIndex, Is.EqualTo(0));
        
        var next1 = service.GetNextResult();
        Assert.That(service.CurrentResultIndex, Is.EqualTo(1));
        
        var next2 = service.GetNextResult();
        Assert.That(service.CurrentResultIndex, Is.EqualTo(2));
        
        var next3 = service.GetNextResult(); // Should cycle back to 0
        Assert.That(service.CurrentResultIndex, Is.EqualTo(0));
    }

    [Test]
    public void GetPreviousResult_CyclesThroughResults()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = CreateTestConversation("test one", "test two", "test three");
        service.SearchInConversation(conversation, "test");

        // Act & Assert
        var prev1 = service.GetPreviousResult(); // Should go to last
        Assert.That(service.CurrentResultIndex, Is.EqualTo(2));
        
        var prev2 = service.GetPreviousResult();
        Assert.That(service.CurrentResultIndex, Is.EqualTo(1));
    }

    [Test]
    public void ClearResults_ResetsState()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = CreateTestConversation("test message");
        service.SearchInConversation(conversation, "test");
        Assert.That(service.HasResults, Is.True);

        // Act
        service.ClearResults();

        // Assert
        Assert.That(service.HasResults, Is.False);
        Assert.That(service.CurrentResultIndex, Is.EqualTo(-1));
        Assert.That(service.CurrentResults, Is.Empty);
    }

    [Test]
    public void CurrentResultIndex_Setter_ValidatesRange()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);
        var conversation = CreateTestConversation("test one", "test two");
        service.SearchInConversation(conversation, "test");

        // Act & Assert - valid index
        service.CurrentResultIndex = 1;
        Assert.That(service.CurrentResultIndex, Is.EqualTo(1));

        // Invalid index - should not change
        service.CurrentResultIndex = 100;
        Assert.That(service.CurrentResultIndex, Is.EqualTo(1));

        service.CurrentResultIndex = -5;
        Assert.That(service.CurrentResultIndex, Is.EqualTo(1));
    }

    #endregion

    #region HighlightText Tests

    [Test]
    public void HighlightText_WithNullText_ReturnsNull()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);

        // Act
        var result = service.HighlightText(null!, "test");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void HighlightText_WithNullSearchTerm_ReturnsOriginalText()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);

        // Act
        var result = service.HighlightText("Hello world", null!);

        // Assert
        Assert.That(result, Is.EqualTo("Hello world"));
    }

    [Test]
    public void HighlightText_HighlightsMatchingText()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);

        // Act
        var result = service.HighlightText("Hello world", "world");

        // Assert
        Assert.That(result, Does.Contain("**world**"));
    }

    [Test]
    public void HighlightText_IsCaseInsensitive()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);

        // Act
        var result = service.HighlightText("Hello WORLD", "world");

        // Assert
        Assert.That(result, Does.Contain("**world**"));
    }

    [Test]
    public void HighlightText_HighlightsMultipleOccurrences()
    {
        // Arrange
        var service = new SearchService(null!, _mockEmbeddingService.Object);

        // Act
        var result = service.HighlightText("test one test two test", "test");

        // Assert
        var highlightCount = result.Split("**test**").Length - 1;
        Assert.That(highlightCount, Is.EqualTo(3));
    }

    #endregion
}
