using WPFLLM.Models;

namespace WPFLLM.Services;

public record RagContextInfo(
    bool IsEnabled, 
    int ChunksFound, 
    string Context, 
    RetrievalResult? Result = null, 
    RagTrace? Trace = null);

public interface IChatService
{
    event EventHandler<RagContextInfo>? RagContextRetrieved;
    Task<List<Conversation>> GetConversationsAsync();
    Task<Conversation> CreateConversationAsync(string title);
    Task UpdateConversationAsync(Conversation conversation);
    Task DeleteConversationAsync(long id);
    Task<List<ChatMessage>> GetMessagesAsync(long conversationId);
    Task<ChatMessage> AddMessageAsync(long conversationId, string role, string content);
    Task UpdateMessageAsync(ChatMessage message);
    Task DeleteMessageAsync(long id);
    IAsyncEnumerable<string> SendMessageAsync(long conversationId, string userMessage, CancellationToken cancellationToken = default);
    
    Task<List<MessageSearchResult>> SemanticSearchAsync(string query, int topK = 10, CancellationToken cancellationToken = default);
    Task GenerateMessageEmbeddingsAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
    Task<int> GetMessagesWithoutEmbeddingsCountAsync();
    Task<string> GenerateConversationTitleAsync(string userMessage, string assistantResponse, CancellationToken cancellationToken = default);
}
