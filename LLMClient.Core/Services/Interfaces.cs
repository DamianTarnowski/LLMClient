using LLMClient.Core.Models;

namespace LLMClient.Core.Services
{
    public enum RetrievalMode
    {
        Vector,
        Keyword,
        Hybrid
    }

    /// <summary>
    /// Interface for AI service
    /// </summary>
    public interface IAiService
    {
        Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default);
        IAsyncEnumerable<string> GenerateStreamingResponseAsync(string prompt, CancellationToken cancellationToken = default);
        Task<string> SummarizeAsync(string text, int maxLength = 500, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for embedding service
    /// </summary>
    public interface IEmbeddingService
    {
        Task<bool> InitializeAsync();
        Task<float[]?> GenerateEmbeddingAsync(string text);
        float CalculateSimilarity(float[] embedding1, float[] embedding2);
        byte[] FloatArrayToBytes(float[] floats);
        float[] BytesToFloatArray(byte[] bytes);
        bool IsInitialized { get; }
    }

    /// <summary>
    /// Interface for memory service
    /// </summary>
    public interface IMemoryService
    {
        Task<List<Memory>> GetAllMemoriesAsync();
        Task<Memory?> GetMemoryByKeyAsync(string key);
        Task<List<Memory>> SearchMemoriesAsync(string searchTerm);
        Task<int> AddMemoryAsync(Memory memory);
        Task<int> UpdateMemoryAsync(Memory memory);
        Task<int> DeleteMemoryAsync(int memoryId);
    }

    /// <summary>
    /// Interface for search service
    /// </summary>
    public interface ISearchService
    {
        List<SearchResult> SearchInConversation(Conversation conversation, string searchTerm);
        Task<List<SearchResult>> SearchInConversationAsync(Conversation conversation, string searchTerm);
    }

    /// <summary>
    /// Search result model
    /// </summary>
    public class SearchResult
    {
        public Message? Message { get; set; }
        public int StartIndex { get; set; }
        public int Length { get; set; }
        public string? HighlightedContent { get; set; }
    }

    /// <summary>
    /// Interface for database service
    /// </summary>
    public interface IDatabaseService
    {
        Task<List<AiModel>> GetModelsAsync();
        Task SaveModelAsync(AiModel model);
        Task DeleteModelAsync(AiModel model);
        Task<List<Conversation>> GetConversationsAsync();
        Task<Conversation?> GetConversationAsync(int id);
        Task<int> SaveConversationAsync(Conversation conversation);
        Task DeleteConversationAsync(int conversationId);
        Task<List<Message>> GetMessagesAsync(int conversationId, int limit = 50, int offset = 0);
        Task<int> SaveMessageAsync(Message message);
        Task DeleteMessageAsync(Message message);
        Task<List<Memory>> GetAllMemoriesAsync();
        Task<int> AddMemoryAsync(Memory memory);
        Task<int> UpdateMemoryAsync(Memory memory);
        Task<int> DeleteMemoryAsync(int memoryId);
    }

    /// <summary>
    /// Interface for RAG service
    /// </summary>
    public interface IRagService
    {
        Task<int> AddDocumentAsync(string filePath, string? customName = null);
        Task<List<RagDocument>> GetDocumentsAsync();
        Task DeleteDocumentAsync(int documentId);
        Task<string> GetRelevantContextAsync(string query, int topK = 5);
    }

    /// <summary>
    /// Interface for localization service
    /// </summary>
    public interface ILocalizationService
    {
        string GetString(string key);
        string CurrentLanguage { get; }
        void SetLanguage(string languageCode);
    }
}
