using LLMClient.Core.Models;
using LLMClient.Core.Services;
using Moq;

namespace LLMClient.Tests.Services;

/// <summary>
/// Contract tests for service interfaces - verify that interfaces define expected methods
/// </summary>
[TestFixture]
public class IAiServiceContractTests
{
    [Test]
    public void IAiService_HasGenerateResponseAsync()
    {
        var mock = new Mock<IAiService>();
        mock.Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("response");
        
        Assert.That(mock.Object, Is.Not.Null);
    }

    [Test]
    public void IAiService_HasGenerateStreamingResponseAsync()
    {
        var mock = new Mock<IAiService>();
        
        // Interface should have streaming method
        var method = typeof(IAiService).GetMethod("GenerateStreamingResponseAsync");
        Assert.That(method, Is.Not.Null);
    }

    [Test]
    public void IAiService_HasSummarizeAsync()
    {
        var mock = new Mock<IAiService>();
        mock.Setup(x => x.SummarizeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("summary");
        
        Assert.That(mock.Object, Is.Not.Null);
    }
}

[TestFixture]
public class IEmbeddingServiceContractTests
{
    [Test]
    public void IEmbeddingService_HasInitializeAsync()
    {
        var mock = new Mock<IEmbeddingService>();
        mock.Setup(x => x.InitializeAsync()).ReturnsAsync(true);
        
        Assert.That(mock.Object, Is.Not.Null);
    }

    [Test]
    public void IEmbeddingService_HasGenerateEmbeddingAsync()
    {
        var mock = new Mock<IEmbeddingService>();
        mock.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f });
        
        Assert.That(mock.Object, Is.Not.Null);
    }

    [Test]
    public void IEmbeddingService_HasCalculateSimilarity()
    {
        var mock = new Mock<IEmbeddingService>();
        mock.Setup(x => x.CalculateSimilarity(It.IsAny<float[]>(), It.IsAny<float[]>()))
            .Returns(0.95f);
        
        Assert.That(mock.Object, Is.Not.Null);
    }

    [Test]
    public void IEmbeddingService_HasIsInitializedProperty()
    {
        var mock = new Mock<IEmbeddingService>();
        mock.SetupGet(x => x.IsInitialized).Returns(true);
        
        Assert.That(mock.Object.IsInitialized, Is.True);
    }

    [Test]
    public void IEmbeddingService_HasByteConversionMethods()
    {
        var mock = new Mock<IEmbeddingService>();
        
        // FloatArrayToBytes
        mock.Setup(x => x.FloatArrayToBytes(It.IsAny<float[]>()))
            .Returns(new byte[] { 1, 2, 3, 4 });
        
        // BytesToFloatArray
        mock.Setup(x => x.BytesToFloatArray(It.IsAny<byte[]>()))
            .Returns(new float[] { 1.0f });
        
        Assert.That(mock.Object, Is.Not.Null);
    }
}

[TestFixture]
public class IDatabaseServiceContractTests
{
    [Test]
    public void IDatabaseService_HasModelsMethods()
    {
        var mock = new Mock<IDatabaseService>();
        mock.Setup(x => x.GetModelsAsync()).ReturnsAsync(new List<AiModel>());
        mock.Setup(x => x.SaveModelAsync(It.IsAny<AiModel>())).Returns(Task.CompletedTask);
        mock.Setup(x => x.DeleteModelAsync(It.IsAny<AiModel>())).Returns(Task.CompletedTask);
        
        Assert.That(mock.Object, Is.Not.Null);
    }

    [Test]
    public void IDatabaseService_HasConversationMethods()
    {
        var mock = new Mock<IDatabaseService>();
        mock.Setup(x => x.GetConversationsAsync()).ReturnsAsync(new List<Conversation>());
        mock.Setup(x => x.GetConversationAsync(It.IsAny<int>())).ReturnsAsync((Conversation?)null);
        mock.Setup(x => x.SaveConversationAsync(It.IsAny<Conversation>())).ReturnsAsync(1);
        mock.Setup(x => x.DeleteConversationAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
        
        Assert.That(mock.Object, Is.Not.Null);
    }

    [Test]
    public void IDatabaseService_HasMessageMethods()
    {
        var mock = new Mock<IDatabaseService>();
        mock.Setup(x => x.GetMessagesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Message>());
        mock.Setup(x => x.SaveMessageAsync(It.IsAny<Message>())).ReturnsAsync(1);
        mock.Setup(x => x.DeleteMessageAsync(It.IsAny<Message>())).Returns(Task.CompletedTask);
        
        Assert.That(mock.Object, Is.Not.Null);
    }

    [Test]
    public void IDatabaseService_HasMemoryMethods()
    {
        var mock = new Mock<IDatabaseService>();
        mock.Setup(x => x.GetAllMemoriesAsync()).ReturnsAsync(new List<Memory>());
        mock.Setup(x => x.AddMemoryAsync(It.IsAny<Memory>())).ReturnsAsync(1);
        mock.Setup(x => x.UpdateMemoryAsync(It.IsAny<Memory>())).ReturnsAsync(1);
        mock.Setup(x => x.DeleteMemoryAsync(It.IsAny<int>())).ReturnsAsync(1);
        
        Assert.That(mock.Object, Is.Not.Null);
    }
}

[TestFixture]
public class IMemoryServiceContractTests
{
    [Test]
    public void IMemoryService_HasAllMethods()
    {
        var mock = new Mock<IMemoryService>();
        mock.Setup(x => x.GetAllMemoriesAsync()).ReturnsAsync(new List<Memory>());
        mock.Setup(x => x.GetMemoryByKeyAsync(It.IsAny<string>())).ReturnsAsync((Memory?)null);
        mock.Setup(x => x.SearchMemoriesAsync(It.IsAny<string>())).ReturnsAsync(new List<Memory>());
        mock.Setup(x => x.AddMemoryAsync(It.IsAny<Memory>())).ReturnsAsync(1);
        mock.Setup(x => x.UpdateMemoryAsync(It.IsAny<Memory>())).ReturnsAsync(1);
        mock.Setup(x => x.DeleteMemoryAsync(It.IsAny<int>())).ReturnsAsync(1);
        
        Assert.That(mock.Object, Is.Not.Null);
    }
}

[TestFixture]
public class IRagServiceContractTests
{
    [Test]
    public void IRagService_HasDocumentMethods()
    {
        var mock = new Mock<IRagService>();
        mock.Setup(x => x.AddDocumentAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(1);
        mock.Setup(x => x.GetDocumentsAsync()).ReturnsAsync(new List<RagDocument>());
        mock.Setup(x => x.DeleteDocumentAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
        
        Assert.That(mock.Object, Is.Not.Null);
    }

    [Test]
    public void IRagService_HasRetrievalMethod()
    {
        var mock = new Mock<IRagService>();
        mock.Setup(x => x.GetRelevantContextAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync("context");
        
        Assert.That(mock.Object, Is.Not.Null);
    }
}

[TestFixture]
public class ISearchServiceContractTests
{
    [Test]
    public void ISearchService_HasSyncSearchMethod()
    {
        var mock = new Mock<ISearchService>();
        mock.Setup(x => x.SearchInConversation(It.IsAny<Conversation>(), It.IsAny<string>()))
            .Returns(new List<SearchResult>());
        
        Assert.That(mock.Object, Is.Not.Null);
    }

    [Test]
    public void ISearchService_HasAsyncSearchMethod()
    {
        var mock = new Mock<ISearchService>();
        mock.Setup(x => x.SearchInConversationAsync(It.IsAny<Conversation>(), It.IsAny<string>()))
            .ReturnsAsync(new List<SearchResult>());
        
        Assert.That(mock.Object, Is.Not.Null);
    }
}

[TestFixture]
public class ILocalizationServiceContractTests
{
    [Test]
    public void ILocalizationService_HasGetStringMethod()
    {
        var mock = new Mock<ILocalizationService>();
        mock.Setup(x => x.GetString(It.IsAny<string>())).Returns("translated");
        
        Assert.That(mock.Object, Is.Not.Null);
    }

    [Test]
    public void ILocalizationService_HasCurrentLanguageProperty()
    {
        var mock = new Mock<ILocalizationService>();
        mock.SetupGet(x => x.CurrentLanguage).Returns("pl");
        
        Assert.That(mock.Object.CurrentLanguage, Is.EqualTo("pl"));
    }

    [Test]
    public void ILocalizationService_HasSetLanguageMethod()
    {
        var mock = new Mock<ILocalizationService>();
        mock.Setup(x => x.SetLanguage(It.IsAny<string>()));
        
        mock.Object.SetLanguage("en");
        mock.Verify(x => x.SetLanguage("en"), Times.Once);
    }
}
