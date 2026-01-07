using SQLite;

namespace LLMClient.Core.Models
{
    /// <summary>
    /// Stores user preferences for model settings
    /// </summary>
    [Table("ModelSettings")]
    public class ModelSettings
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        public string? SelectedModelId { get; set; }
        public float Temperature { get; set; } = 0.7f;
        public int MaxTokens { get; set; } = 2048;
        public string? SystemPrompt { get; set; }
        public bool EnableStreaming { get; set; } = true;
        public bool EnableMemory { get; set; } = true;
        public bool EnableRag { get; set; } = false;
        public string? RagSearchMode { get; set; } = "Hybrid";
        public int RagTopK { get; set; } = 5;
        public float RagMinSimilarity { get; set; } = 0.3f;
    }

    /// <summary>
    /// Helper class for message search results with conversation title
    /// </summary>
    public class MessageWithConversationTitle : Message
    {
        public string? ConversationTitle { get; set; }
    }
}
