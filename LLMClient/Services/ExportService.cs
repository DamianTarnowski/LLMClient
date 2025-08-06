using LLMClient.Models;
using System.Text;
using System.Text.Json;

namespace LLMClient.Services
{
    public enum ExportFormat
    {
        Json,
        Markdown,
        PlainText
    }

    public class ExportResult
    {
        public bool Success { get; set; }
        public string? FilePath { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public interface IExportService
    {
        Task<ExportResult> ExportConversationAsync(Conversation conversation, ExportFormat format);
        string GenerateFileName(Conversation conversation, ExportFormat format);
        Task<string> GetExportContentAsync(Conversation conversation, ExportFormat format);
    }

    public class ExportService : IExportService
    {
        public async Task<ExportResult> ExportConversationAsync(Conversation conversation, ExportFormat format)
        {
            try
            {
                if (conversation?.Messages == null || !conversation.Messages.Any())
                {
                    return new ExportResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Konwersacja jest pusta lub nie zawiera wiadomości." 
                    };
                }

                var content = await GetExportContentAsync(conversation, format);
                var fileName = GenerateFileName(conversation, format);
                
                // Save to app's documents folder
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var filePath = Path.Combine(documentsPath, fileName);
                
                await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);

                // Try to open with system default app or show in file explorer
                try
                {
                    await Launcher.Default.OpenAsync(new OpenFileRequest
                    {
                        File = new ReadOnlyFile(filePath)
                    });
                }
                catch
                {
                    // If opening fails, just return the path
                }

                return new ExportResult
                {
                    Success = true,
                    FilePath = filePath
                };
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    ErrorMessage = $"Błąd eksportu: {ex.Message}"
                };
            }
        }

        public string GenerateFileName(Conversation conversation, ExportFormat format)
        {
            var title = string.IsNullOrWhiteSpace(conversation.Title) ? "Konwersacja" : conversation.Title;
            
            // Clean title for filename (remove invalid characters)
            var cleanTitle = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
            if (cleanTitle.Length > 50)
                cleanTitle = cleanTitle.Substring(0, 50);

            var timestamp = conversation.CreatedAt.ToString("yyyy-MM-dd_HH-mm");
            var extension = format switch
            {
                ExportFormat.Json => ".json",
                ExportFormat.Markdown => ".md",
                ExportFormat.PlainText => ".txt",
                _ => ".txt"
            };

            return $"{cleanTitle}_{timestamp}{extension}";
        }

        public async Task<string> GetExportContentAsync(Conversation conversation, ExportFormat format)
        {
            return await Task.Run(() =>
            {
                return format switch
                {
                    ExportFormat.Json => GenerateJsonContent(conversation),
                    ExportFormat.Markdown => GenerateMarkdownContent(conversation),
                    ExportFormat.PlainText => GeneratePlainTextContent(conversation),
                    _ => GeneratePlainTextContent(conversation)
                };
            });
        }

        private string GenerateJsonContent(Conversation conversation)
        {
            var exportData = new
            {
                conversation.Title,
                conversation.CreatedAt,
                ExportedAt = DateTime.Now,
                MessageCount = conversation.Messages.Count,
                Messages = conversation.Messages.Select(m => new
                {
                    m.Id,
                    m.Content,
                    m.IsUser,
                    m.Timestamp,
                    Sender = m.IsUser ? "User" : "AI Assistant"
                }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            return JsonSerializer.Serialize(exportData, options);
        }

        private string GenerateMarkdownContent(Conversation conversation)
        {
            var sb = new StringBuilder();
            
            // Header
            sb.AppendLine($"# {conversation.Title}");
            sb.AppendLine();
            sb.AppendLine($"**Utworzona:** {conversation.CreatedAt:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"**Wyeksportowana:** {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"**Liczba wiadomości:** {conversation.Messages.Count}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            // Messages
            foreach (var message in conversation.Messages.OrderBy(m => m.Timestamp))
            {
                var sender = message.IsUser ? "👤 **Użytkownik**" : "🤖 **AI Asystent**";
                var timestamp = message.Timestamp.ToString("HH:mm:ss");
                
                sb.AppendLine($"## {sender} _{timestamp}_");
                sb.AppendLine();
                sb.AppendLine(message.Content);
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string GeneratePlainTextContent(Conversation conversation)
        {
            var sb = new StringBuilder();
            
            // Header
            sb.AppendLine($"KONWERSACJA: {conversation.Title}");
            sb.AppendLine($"Utworzona: {conversation.CreatedAt:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Wyeksportowana: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Liczba wiadomości: {conversation.Messages.Count}");
            sb.AppendLine();
            sb.AppendLine("=" + new string('=', 60));
            sb.AppendLine();

            // Messages
            foreach (var message in conversation.Messages.OrderBy(m => m.Timestamp))
            {
                var sender = message.IsUser ? "UŻYTKOWNIK" : "AI ASYSTENT";
                var timestamp = message.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                
                sb.AppendLine($"[{timestamp}] {sender}:");
                sb.AppendLine(message.Content);
                sb.AppendLine();
                sb.AppendLine("-" + new string('-', 60));
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
} 