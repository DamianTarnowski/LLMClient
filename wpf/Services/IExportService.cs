using WPFLLM.Models;

namespace WPFLLM.Services;

public interface IExportService
{
    Task<string> ExportToMarkdownAsync(Conversation conversation, List<ChatMessage> messages);
    Task<string> ExportToJsonAsync(Conversation conversation, List<ChatMessage> messages);
    Task ExportToFileAsync(Conversation conversation, List<ChatMessage> messages, string filePath, ExportFormat format);
    Task<(Conversation conversation, List<ChatMessage> messages)> ImportFromJsonAsync(string filePath);
    Task<(Conversation conversation, List<ChatMessage> messages)> ImportFromJsonStringAsync(string json);
}

public enum ExportFormat
{
    Markdown,
    Json
}
