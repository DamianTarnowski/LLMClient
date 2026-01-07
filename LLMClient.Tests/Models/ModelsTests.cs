using LLMClient.Core.Models;
using LLMClient.Core.Services;

namespace LLMClient.Tests.Models;

[TestFixture]
public class ModelsTests
{
    [Test]
    public void Conversation_CreateNew_HasDefaultValues()
    {
        var conversation = new Conversation();
        
        Assert.That(conversation.Id, Is.EqualTo(0));
        Assert.That(conversation.Title, Is.Null.Or.Empty);
        Assert.That(conversation.Messages, Is.Not.Null);
    }

    [Test]
    public void Message_CreateNew_HasDefaultValues()
    {
        var message = new Message();
        
        Assert.That(message.Id, Is.EqualTo(0));
        Assert.That(message.Content, Is.Null.Or.Empty);
        Assert.That(message.IsUser, Is.False);
    }

    [Test]
    public void Memory_CreateNew_HasDefaultValues()
    {
        var memory = new Memory();
        
        Assert.That(memory.Id, Is.EqualTo(0));
        Assert.That(memory.Key, Is.Null.Or.Empty);
    }

    [Test]
    public void AiModel_CreateNew_HasDefaultValues()
    {
        var model = new AiModel();
        
        Assert.That(model.Name, Is.Null.Or.Empty);
        Assert.That(model.Provider, Is.EqualTo(AiProvider.OpenAI)); // default enum value
    }

    [Test]
    public void RagDocument_CreateNew_HasDefaultValues()
    {
        var doc = new RagDocument();
        
        Assert.That(doc.Id, Is.EqualTo(0));
        Assert.That(doc.FileName, Is.Null.Or.Empty);
    }

    [Test]
    public void RetrievalMode_HasAllValues()
    {
        var values = Enum.GetValues<RetrievalMode>();
        Assert.That(values.Length, Is.EqualTo(3));
    }

    [Test]
    public void SearchResult_CreateNew_HasDefaultValues()
    {
        var result = new SearchResult();
        
        Assert.That(result.Message, Is.Null);
        Assert.That(result.StartIndex, Is.EqualTo(0));
        Assert.That(result.Length, Is.EqualTo(0));
    }
}
