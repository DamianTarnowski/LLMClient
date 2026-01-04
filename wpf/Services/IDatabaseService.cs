using WPFLLM.Models;

namespace WPFLLM.Services;

public interface IDatabaseService
{
    Task InitializeAsync();
    
    Task<List<Conversation>> GetConversationsAsync();
    Task<Conversation> CreateConversationAsync(string title);
    Task UpdateConversationAsync(Conversation conversation);
    Task DeleteConversationAsync(long id);
    
    Task<List<ChatMessage>> GetMessagesAsync(long conversationId);
    Task<ChatMessage> AddMessageAsync(long conversationId, string role, string content);
    Task UpdateMessageAsync(ChatMessage message);
    Task DeleteMessageAsync(long id);
    Task UpdateMessageEmbeddingAsync(long messageId, string embedding);
    Task<List<ChatMessage>> GetAllMessagesWithEmbeddingsAsync();
    Task<List<ChatMessage>> GetMessagesWithoutEmbeddingsAsync();
    
    Task<List<RagDocument>> GetDocumentsAsync();
    Task<RagDocument> AddDocumentAsync(string fileName, string content);
    Task DeleteDocumentAsync(long id);
    
    Task<List<RagChunk>> GetChunksAsync(long documentId);
    Task<List<RagChunk>> GetAllChunksAsync();
    Task AddChunksAsync(long documentId, List<string> chunks);
    Task UpdateChunkEmbeddingAsync(long chunkId, string embedding);
    Task<List<(RagChunk Chunk, double Score)>> SearchChunksFtsAsync(string query, int limit = 20);
    Task<string?> GetDocumentNameAsync(long documentId);
    
    Task<AppSettings> GetSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    
    Task<List<SavedModel>> GetSavedModelsAsync();
    Task<SavedModel?> GetSavedModelAsync(string modelId);
    Task SaveModelAsync(SavedModel model);
    Task DeleteSavedModelAsync(string modelId);
    Task UpdateModelUsageAsync(string modelId);
    Task ToggleFavoriteModelAsync(string modelId);
    
    Task<List<SavedApiKey>> GetApiKeysAsync();
    Task<string?> GetApiKeyAsync(string provider);
    Task SaveApiKeyAsync(string provider, string apiKey);
    Task DeleteApiKeyAsync(string provider);
}
