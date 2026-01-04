using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WPFLLM.Models;

namespace WPFLLM.Services;

public class ExportService : IExportService
{
    private readonly ILoggingService _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ExportService(ILoggingService logger)
    {
        _logger = logger;
    }

    public Task<string> ExportToMarkdownAsync(Conversation conversation, List<ChatMessage> messages)
    {
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine($"# {conversation.Title}");
        sb.AppendLine();
        sb.AppendLine($"**Exported:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**Created:** {conversation.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**Messages:** {messages.Count}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Messages
        foreach (var msg in messages.OrderBy(m => m.CreatedAt))
        {
            var roleIcon = msg.Role == "user" ? "👤" : "🤖";
            var roleName = msg.Role == "user" ? "User" : "Assistant";
            
            sb.AppendLine($"### {roleIcon} {roleName}");
            sb.AppendLine($"*{msg.CreatedAt:HH:mm:ss}*");
            sb.AppendLine();
            sb.AppendLine(msg.Content);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        _logger.LogInfo($"Exported conversation '{conversation.Title}' to Markdown ({messages.Count} messages)");
        return Task.FromResult(sb.ToString());
    }

    public Task<string> ExportToJsonAsync(Conversation conversation, List<ChatMessage> messages)
    {
        var export = new ConversationExport
        {
            Version = "1.0",
            ExportedAt = DateTime.Now,
            Conversation = new ConversationData
            {
                Title = conversation.Title,
                CreatedAt = conversation.CreatedAt,
                UpdatedAt = conversation.UpdatedAt
            },
            Messages = messages.Select(m => new MessageData
            {
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt
            }).ToList()
        };

        var json = JsonSerializer.Serialize(export, JsonOptions);
        _logger.LogInfo($"Exported conversation '{conversation.Title}' to JSON ({messages.Count} messages)");
        return Task.FromResult(json);
    }

    public async Task ExportToFileAsync(Conversation conversation, List<ChatMessage> messages, string filePath, ExportFormat format)
    {
        var content = format switch
        {
            ExportFormat.Markdown => await ExportToMarkdownAsync(conversation, messages),
            ExportFormat.Json => await ExportToJsonAsync(conversation, messages),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };

        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
        _logger.LogInfo($"Saved conversation export to: {filePath}");
    }

    public async Task<(Conversation conversation, List<ChatMessage> messages)> ImportFromJsonAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return await ImportFromJsonStringAsync(json);
    }

    public Task<(Conversation conversation, List<ChatMessage> messages)> ImportFromJsonStringAsync(string json)
    {
        var export = JsonSerializer.Deserialize<ConversationExport>(json, JsonOptions);
        
        if (export == null)
            throw new InvalidOperationException("Invalid export file format");

        var conversation = new Conversation
        {
            Title = export.Conversation.Title,
            CreatedAt = export.Conversation.CreatedAt,
            UpdatedAt = DateTime.Now
        };

        var messages = export.Messages.Select(m => new ChatMessage
        {
            Role = m.Role,
            Content = m.Content,
            CreatedAt = m.CreatedAt
        }).ToList();

        _logger.LogInfo($"Imported conversation '{conversation.Title}' ({messages.Count} messages)");
        return Task.FromResult((conversation, messages));
    }
}

// Export data models
public class ConversationExport
{
    public string Version { get; set; } = "1.0";
    public DateTime ExportedAt { get; set; }
    public ConversationData Conversation { get; set; } = new();
    public List<MessageData> Messages { get; set; } = [];
}

public class ConversationData
{
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MessageData
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
