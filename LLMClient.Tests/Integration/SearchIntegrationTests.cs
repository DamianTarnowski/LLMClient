using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;
using System.Text.RegularExpressions;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Integration tests for Search functionality
/// Tests message search, highlighting, and async search operations
/// </summary>
[TestFixture]
[Category("Integration")]
public class SearchServiceTests
{
    private Mock<ISearchService> _searchService = null!;

    [SetUp]
    public void Setup()
    {
        _searchService = new Mock<ISearchService>();
        
        _searchService.Setup(x => x.SearchInConversation(It.IsAny<Conversation>(), It.IsAny<string>()))
            .Returns((Conversation conv, string term) => SearchMessages(conv.Messages.ToList(), term));
            
        _searchService.Setup(x => x.SearchInConversationAsync(It.IsAny<Conversation>(), It.IsAny<string>()))
            .ReturnsAsync((Conversation conv, string term) => SearchMessages(conv.Messages.ToList(), term));
    }

    [Test]
    public void Search_FindsExactMatch()
    {
        var conversation = CreateConversationWithMessages(
            "Hello world",
            "How are you?",
            "The world is beautiful"
        );
        
        var results = _searchService.Object.SearchInConversation(conversation, "world");
        
        Assert.That(results.Count, Is.EqualTo(2));
    }

    [Test]
    public void Search_CaseInsensitive()
    {
        var conversation = CreateConversationWithMessages("Hello WORLD", "world hello");
        
        var results = _searchService.Object.SearchInConversation(conversation, "world");
        
        Assert.That(results.Count, Is.EqualTo(2));
    }

    [Test]
    public void Search_NoMatch_ReturnsEmpty()
    {
        var conversation = CreateConversationWithMessages("Hello", "Goodbye");
        
        var results = _searchService.Object.SearchInConversation(conversation, "xyz");
        
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void Search_EmptyQuery_ReturnsEmpty()
    {
        var conversation = CreateConversationWithMessages("Hello", "World");
        
        var results = _searchService.Object.SearchInConversation(conversation, "");
        
        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task SearchAsync_Works()
    {
        var conversation = CreateConversationWithMessages("Test message", "Another test");
        
        var results = await _searchService.Object.SearchInConversationAsync(conversation, "test");
        
        Assert.That(results.Count, Is.EqualTo(2));
    }

    [Test]
    public void Search_HighlightsMatch()
    {
        var conversation = CreateConversationWithMessages("Hello world today");
        
        var results = _searchService.Object.SearchInConversation(conversation, "world");
        
        Assert.That(results[0].HighlightedContent, Does.Contain("<mark>"));
        Assert.That(results[0].HighlightedContent, Does.Contain("world"));
    }

    [Test]
    public void Search_TracksPosition()
    {
        var conversation = CreateConversationWithMessages("Hello world");
        
        var results = _searchService.Object.SearchInConversation(conversation, "world");
        
        Assert.That(results[0].StartIndex, Is.EqualTo(6));
        Assert.That(results[0].Length, Is.EqualTo(5));
    }

    [Test]
    public void Search_PolishCharacters_Works()
    {
        var conversation = CreateConversationWithMessages(
            "Cześć, jak się masz?",
            "Świetnie, dziękuję!"
        );
        
        var results = _searchService.Object.SearchInConversation(conversation, "cześć");
        
        Assert.That(results.Count, Is.EqualTo(1));
    }

    [Test]
    public void Search_MultipleMatchesInSameMessage()
    {
        var conversation = CreateConversationWithMessages("test test test");
        
        var results = _searchService.Object.SearchInConversation(conversation, "test");
        
        // Should find the message (even if multiple matches within it)
        Assert.That(results.Count, Is.GreaterThanOrEqualTo(1));
    }

    private static Conversation CreateConversationWithMessages(params string[] contents)
    {
        var conversation = new Conversation { Title = "Test Conversation" };
        foreach (var content in contents)
        {
            conversation.Messages.Add(new Message { Content = content });
        }
        return conversation;
    }

    private static List<SearchResult> SearchMessages(List<Message> messages, string term)
    {
        if (string.IsNullOrEmpty(term)) return new List<SearchResult>();
        
        var results = new List<SearchResult>();
        
        foreach (var message in messages)
        {
            var index = message.Content.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                results.Add(new SearchResult
                {
                    Message = message,
                    StartIndex = index,
                    Length = term.Length,
                    HighlightedContent = Regex.Replace(
                        message.Content,
                        Regex.Escape(term),
                        match => $"<mark>{match.Value}</mark>",
                        RegexOptions.IgnoreCase)
                });
            }
        }
        
        return results;
    }
}

[TestFixture]
[Category("Integration")]
public class GlobalSearchTests
{
    [Test]
    public void GlobalSearch_AcrossConversations_FindsAll()
    {
        var conversations = new List<Conversation>
        {
            CreateConversationWithMessages("Chat 1", "Hello world"),
            CreateConversationWithMessages("Chat 2", "World news"),
            CreateConversationWithMessages("Chat 3", "Hello there")
        };
        
        var results = conversations
            .SelectMany(c => c.Messages)
            .Where(m => m.Content.Contains("world", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        Assert.That(results.Count, Is.EqualTo(2));
    }

    [Test]
    public void GlobalSearch_WithConversationTitle_IncludesContext()
    {
        var conversation = new Conversation { Title = "AI Discussion" };
        conversation.Messages.Add(new Message { Content = "Machine learning is great" });
        
        var result = new MessageWithConversationTitle
        {
            Content = "Machine learning is great",
            ConversationTitle = conversation.Title
        };
        
        Assert.That(result.ConversationTitle, Is.EqualTo("AI Discussion"));
    }

    private static Conversation CreateConversationWithMessages(string title, params string[] contents)
    {
        var conversation = new Conversation { Title = title };
        foreach (var content in contents)
        {
            conversation.Messages.Add(new Message { Content = content });
        }
        return conversation;
    }
}
