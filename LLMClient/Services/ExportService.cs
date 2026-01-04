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

    public class ImportResult
    {
        public bool Success { get; set; }
        public Conversation? Conversation { get; set; }
        public List<Message>? Messages { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public interface IExportService
    {
        Task<ExportResult> ExportConversationAsync(Conversation conversation, ExportFormat format);
        string GenerateFileName(Conversation conversation, ExportFormat format);
        Task<string> GetExportContentAsync(Conversation conversation, ExportFormat format);
        Task<ImportResult> ImportConversationAsync(string json);
        Task<ImportResult> ImportConversationFromFileAsync(string filePath);
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

        public async Task<ImportResult> ImportConversationFromFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new ImportResult
                    {
                        Success = false,
                        ErrorMessage = "Plik nie istnieje."
                    };
                }

                var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                return await ImportConversationAsync(json);
            }
            catch (Exception ex)
            {
                return new ImportResult
                {
                    Success = false,
                    ErrorMessage = $"Błąd importu: {ex.Message}"
                };
            }
        }

        public async Task<ImportResult> ImportConversationAsync(string json)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var exportData = JsonSerializer.Deserialize<ConversationExportData>(json, options);
                
                if (exportData == null)
                {
                    return new ImportResult
                    {
                        Success = false,
                        ErrorMessage = "Nieprawidłowy format pliku eksportu."
                    };
                }

                var conversation = new Conversation
                {
                    Title = exportData.Title ?? "Zaimportowana konwersacja",
                    CreatedAt = exportData.CreatedAt != default ? exportData.CreatedAt : DateTime.Now
                };

                var messages = new List<Message>();
                if (exportData.Messages != null)
                {
                    foreach (var msgData in exportData.Messages)
                    {
                        messages.Add(new Message
                        {
                            Content = msgData.Content ?? "",
                            IsUser = msgData.IsUser,
                            Timestamp = msgData.Timestamp != default ? msgData.Timestamp : DateTime.Now
                        });
                    }
                }

                return await Task.FromResult(new ImportResult
                {
                    Success = true,
                    Conversation = conversation,
                    Messages = messages
                });
            }
            catch (JsonException ex)
            {
                return new ImportResult
                {
                    Success = false,
                    ErrorMessage = $"Błąd parsowania JSON: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new ImportResult
                {
                    Success = false,
                    ErrorMessage = $"Błąd importu: {ex.Message}"
                };
            }
        }
    }

    public class ConversationExportData
    {
        public string? Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExportedAt { get; set; }
        public int MessageCount { get; set; }
        public List<MessageExportData>? Messages { get; set; }
    }

    public class MessageExportData
    {
        public int Id { get; set; }
        public string? Content { get; set; }
        public bool IsUser { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Sender { get; set; }
    }
}