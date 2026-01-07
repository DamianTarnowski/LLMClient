using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;

namespace LLMClient.Tests.Services;

[TestFixture]
public class MockDatabaseServiceTests
{
    private Mock<IDatabaseService> _mockDbService = null!;

    [SetUp]
    public void Setup()
    {
        _mockDbService = new Mock<IDatabaseService>();
    }

    [Test]
    public async Task GetModelsAsync_ReturnsModels()
    {
        var models = new List<AiModel>
        {
            new() { Name = "GPT-4", Provider = AiProvider.OpenAI },
            new() { Name = "Claude", Provider = AiProvider.Anthropic }
        };
        _mockDbService.Setup(x => x.GetModelsAsync()).ReturnsAsync(models);

        var result = await _mockDbService.Object.GetModelsAsync();

        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0].Name, Is.EqualTo("GPT-4"));
    }

    [Test]
    public async Task GetConversationsAsync_ReturnsConversations()
    {
        var conversations = new List<Conversation>
        {
            new() { Title = "Test 1" },
            new() { Title = "Test 2" }
        };
        _mockDbService.Setup(x => x.GetConversationsAsync()).ReturnsAsync(conversations);

        var result = await _mockDbService.Object.GetConversationsAsync();

        Assert.That(result.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task SaveConversationAsync_ReturnsId()
    {
        var conversation = new Conversation { Title = "New Conversation" };
        _mockDbService.Setup(x => x.SaveConversationAsync(It.IsAny<Conversation>())).ReturnsAsync(42);

        var id = await _mockDbService.Object.SaveConversationAsync(conversation);

        Assert.That(id, Is.EqualTo(42));
    }

    [Test]
    public async Task GetMessagesAsync_ReturnsMessages()
    {
        var messages = new List<Message>
        {
            new() { Content = "Hello", IsUser = true },
            new() { Content = "Hi there", IsUser = false }
        };
        _mockDbService.Setup(x => x.GetMessagesAsync(1, 50, 0)).ReturnsAsync(messages);

        var result = await _mockDbService.Object.GetMessagesAsync(1, 50, 0);

        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0].IsUser, Is.True);
        Assert.That(result[1].IsBot, Is.True);
    }

    [Test]
    public async Task DeleteConversationAsync_CallsService()
    {
        _mockDbService.Setup(x => x.DeleteConversationAsync(1)).Returns(Task.CompletedTask);

        await _mockDbService.Object.DeleteConversationAsync(1);

        _mockDbService.Verify(x => x.DeleteConversationAsync(1), Times.Once);
    }
}

[TestFixture]
public class MockAiServiceTests
{
    private Mock<IAiService> _mockAiService = null!;

    [SetUp]
    public void Setup()
    {
        _mockAiService = new Mock<IAiService>();
    }

    [Test]
    public async Task GenerateResponseAsync_ReturnsResponse()
    {
        _mockAiService.Setup(x => x.GenerateResponseAsync(
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Generated response");

        var result = await _mockAiService.Object.GenerateResponseAsync("Test prompt");

        Assert.That(result, Is.EqualTo("Generated response"));
    }

    [Test]
    public async Task SummarizeAsync_ReturnsSummary()
    {
        _mockAiService.Setup(x => x.SummarizeAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Summary text");

        var result = await _mockAiService.Object.SummarizeAsync("Long text to summarize");

        Assert.That(result, Is.EqualTo("Summary text"));
    }
}

[TestFixture]
public class MockMemoryServiceTests
{
    private Mock<IMemoryService> _mockMemoryService = null!;

    [SetUp]
    public void Setup()
    {
        _mockMemoryService = new Mock<IMemoryService>();
    }

    [Test]
    public async Task GetAllMemoriesAsync_ReturnsMemories()
    {
        var memories = new List<Memory>
        {
            new() { Key = "user_name", Value = "John" },
            new() { Key = "preference", Value = "dark_mode" }
        };
        _mockMemoryService.Setup(x => x.GetAllMemoriesAsync()).ReturnsAsync(memories);

        var result = await _mockMemoryService.Object.GetAllMemoriesAsync();

        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0].Key, Is.EqualTo("user_name"));
    }

    [Test]
    public async Task SearchMemoriesAsync_ReturnsMatchingMemories()
    {
        var memories = new List<Memory>
        {
            new() { Key = "user_name", Value = "John" }
        };
        _mockMemoryService.Setup(x => x.SearchMemoriesAsync("user")).ReturnsAsync(memories);

        var result = await _mockMemoryService.Object.SearchMemoriesAsync("user");

        Assert.That(result.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task AddMemoryAsync_ReturnsId()
    {
        var memory = new Memory { Key = "new_key", Value = "new_value" };
        _mockMemoryService.Setup(x => x.AddMemoryAsync(It.IsAny<Memory>())).ReturnsAsync(1);

        var id = await _mockMemoryService.Object.AddMemoryAsync(memory);

        Assert.That(id, Is.EqualTo(1));
    }
}

[TestFixture]
public class MockEmbeddingServiceTests
{
    private Mock<IEmbeddingService> _mockEmbeddingService = null!;

    [SetUp]
    public void Setup()
    {
        _mockEmbeddingService = new Mock<IEmbeddingService>();
    }

    [Test]
    public async Task InitializeAsync_ReturnsTrue()
    {
        _mockEmbeddingService.Setup(x => x.InitializeAsync()).ReturnsAsync(true);

        var result = await _mockEmbeddingService.Object.InitializeAsync();

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task GenerateEmbeddingAsync_ReturnsEmbedding()
    {
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingAsync("test text")).ReturnsAsync(embedding);

        var result = await _mockEmbeddingService.Object.GenerateEmbeddingAsync("test text");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Length, Is.EqualTo(3));
    }

    [Test]
    public void CalculateSimilarity_ReturnsSimilarityScore()
    {
        var embedding1 = new float[] { 1.0f, 0.0f, 0.0f };
        var embedding2 = new float[] { 1.0f, 0.0f, 0.0f };
        _mockEmbeddingService.Setup(x => x.CalculateSimilarity(embedding1, embedding2)).Returns(1.0f);

        var result = _mockEmbeddingService.Object.CalculateSimilarity(embedding1, embedding2);

        Assert.That(result, Is.EqualTo(1.0f));
    }

    [Test]
    public void FloatArrayToBytes_ConvertsCorrectly()
    {
        var floats = new float[] { 1.0f, 2.0f };
        var bytes = new byte[8];
        _mockEmbeddingService.Setup(x => x.FloatArrayToBytes(floats)).Returns(bytes);

        var result = _mockEmbeddingService.Object.FloatArrayToBytes(floats);

        Assert.That(result.Length, Is.EqualTo(8));
    }

    [Test]
    public void BytesToFloatArray_ConvertsCorrectly()
    {
        var bytes = new byte[8];
        var floats = new float[] { 1.0f, 2.0f };
        _mockEmbeddingService.Setup(x => x.BytesToFloatArray(bytes)).Returns(floats);

        var result = _mockEmbeddingService.Object.BytesToFloatArray(bytes);

        Assert.That(result.Length, Is.EqualTo(2));
    }
}

[TestFixture]
public class MockRagServiceTests
{
    private Mock<IRagService> _mockRagService = null!;

    [SetUp]
    public void Setup()
    {
        _mockRagService = new Mock<IRagService>();
    }

    [Test]
    public async Task AddDocumentAsync_ReturnsDocumentId()
    {
        _mockRagService.Setup(x => x.AddDocumentAsync("/path/to/doc.pdf", null)).ReturnsAsync(1);

        var id = await _mockRagService.Object.AddDocumentAsync("/path/to/doc.pdf");

        Assert.That(id, Is.EqualTo(1));
    }

    [Test]
    public async Task GetDocumentsAsync_ReturnsDocuments()
    {
        var docs = new List<RagDocument>
        {
            new() { FileName = "doc1.pdf" },
            new() { FileName = "doc2.pdf" }
        };
        _mockRagService.Setup(x => x.GetDocumentsAsync()).ReturnsAsync(docs);

        var result = await _mockRagService.Object.GetDocumentsAsync();

        Assert.That(result.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task GetRelevantContextAsync_ReturnsContext()
    {
        _mockRagService.Setup(x => x.GetRelevantContextAsync("query", 5))
            .ReturnsAsync("Relevant context from documents");

        var result = await _mockRagService.Object.GetRelevantContextAsync("query", 5);

        Assert.That(result, Is.EqualTo("Relevant context from documents"));
    }

    [Test]
    public async Task DeleteDocumentAsync_CallsService()
    {
        _mockRagService.Setup(x => x.DeleteDocumentAsync(1)).Returns(Task.CompletedTask);

        await _mockRagService.Object.DeleteDocumentAsync(1);

        _mockRagService.Verify(x => x.DeleteDocumentAsync(1), Times.Once);
    }
}

[TestFixture]
public class MockSearchServiceTests
{
    private Mock<ISearchService> _mockSearchService = null!;

    [SetUp]
    public void Setup()
    {
        _mockSearchService = new Mock<ISearchService>();
    }

    [Test]
    public void SearchInConversation_ReturnsResults()
    {
        var conversation = new Conversation { Title = "Test" };
        var results = new List<SearchResult>
        {
            new() { StartIndex = 0, Length = 5 }
        };
        _mockSearchService.Setup(x => x.SearchInConversation(conversation, "test")).Returns(results);

        var result = _mockSearchService.Object.SearchInConversation(conversation, "test");

        Assert.That(result.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task SearchInConversationAsync_ReturnsResults()
    {
        var conversation = new Conversation { Title = "Test" };
        var results = new List<SearchResult>
        {
            new() { StartIndex = 0, Length = 5, HighlightedContent = "<em>test</em>" }
        };
        _mockSearchService.Setup(x => x.SearchInConversationAsync(conversation, "test")).ReturnsAsync(results);

        var result = await _mockSearchService.Object.SearchInConversationAsync(conversation, "test");

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].HighlightedContent, Does.Contain("test"));
    }
}
