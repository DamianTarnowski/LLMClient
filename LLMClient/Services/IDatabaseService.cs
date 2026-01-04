using LLMClient.Models;
using LLMClient.ViewModels;

namespace LLMClient.Services;

public interface IDatabaseService
{
    // AiModel operations
    Task<List<AiModel>> GetModelsAsync();
    Task SaveModelAsync(AiModel model);
    Task DeleteModelAsync(AiModel model);
    
    // Conversation operations
    Task<List<Conversation>> GetConversationsAsync();
    Task<Conversation?> GetConversationAsync(int id);
    Task<int> SaveConversationAsync(Conversation conversation);
    Task DeleteConversationAsync(int conversationId);
    
    // Message operations
    Task<List<Message>> GetMessagesAsync(int conversationId, int limit = 50, int offset = 0);
    Task<int> SaveMessageAsync(Message message);
    Task DeleteMessageAsync(Message message);
    
    // Embedding operations
    Task<bool> GenerateAndSaveEmbeddingAsync(Message message);
    Task<List<Message>> GetMessagesNeedingEmbeddingsAsync();
    Task<List<(Message message, float similarity)>> SemanticSearchInConversationAsync(int conversationId, float[] queryEmbedding, float minSimilarity = 0.3f, int maxResults = 10);
    Task<List<(Message message, float similarity, string conversationTitle)>> SemanticSearchAcrossConversationsAsync(float[] queryEmbedding, float minSimilarity = 0.3f, int maxResults = 20);
    Task<List<(Message message, float matchScore, string conversationTitle)>> TextSearchAcrossConversationsAsync(string searchQuery, int maxResults = 20);
    Task<(int withEmbeddings, int total)> GetEmbeddingStatsAsync();
    Task<int> ClearAllEmbeddingsAsync();
    
    // Memory operations
    Task<List<Memory>> GetAllMemoriesAsync();
    Task<Memory?> GetMemoryByKeyAsync(string key);
    Task<List<Memory>> SearchMemoriesAsync(string searchTerm);
    Task<List<Memory>> GetMemoriesByCategoryAsync(string category);
    Task<int> AddMemoryAsync(Memory memory);
    Task<int> UpdateMemoryAsync(Memory memory);
    Task<int> DeleteMemoryAsync(int memoryId);
    Task<int> UpsertMemoryAsync(string key, string value, string category = "", string tags = "", bool isImportant = false);
    Task<List<string>> GetMemoryCategoriesAsync();
    Task<List<string>> GetMemoryTagsAsync();
    
    // Model Settings
    Task<ModelSettings?> GetModelSettingsAsync();
    Task<bool> SaveModelSettingsAsync(ModelSettings settings);
    
    // RAG Document operations
    Task SaveRagDocumentAsync(RagDocument document);
    Task<List<RagDocument>> GetRagDocumentsAsync();
    Task DeleteRagDocumentAsync(int documentId);
    Task SaveRagChunksAsync(int documentId, List<string> chunks);
    Task<List<RagChunk>> GetAllRagChunksAsync();
    Task<List<RagChunk>> GetRagChunksByDocumentAsync(int documentId);
    Task UpdateRagChunkEmbeddingAsync(RagChunk chunk);
    
    // Encryption/Security info
    Task<bool> IsDatabaseEncryptedAsync();
    Task<string> GetEncryptionInfoAsync();
    Task<string> GetApplicationIdAsync();
}
