using System.Runtime.CompilerServices;
using System.Text.Json;
using WPFLLM.Models;

namespace WPFLLM.Services;

public class ChatService : IChatService
{
    private readonly IDatabaseService _database;
    private readonly ILlmService _llmService;
    private readonly IRagService _ragService;
    private readonly ISettingsService _settingsService;

    public event EventHandler<RagContextInfo>? RagContextRetrieved;

    public ChatService(
        IDatabaseService database, 
        ILlmService llmService, 
        IRagService ragService,
        ISettingsService settingsService)
    {
        _database = database;
        _llmService = llmService;
        _ragService = ragService;
        _settingsService = settingsService;
    }

    public Task<List<Conversation>> GetConversationsAsync() => _database.GetConversationsAsync();

    public Task<Conversation> CreateConversationAsync(string title) => _database.CreateConversationAsync(title);

    public Task UpdateConversationAsync(Conversation conversation) => _database.UpdateConversationAsync(conversation);

    public Task DeleteConversationAsync(long id) => _database.DeleteConversationAsync(id);

    public Task<List<ChatMessage>> GetMessagesAsync(long conversationId) => _database.GetMessagesAsync(conversationId);

    public Task<ChatMessage> AddMessageAsync(long conversationId, string role, string content) 
        => _database.AddMessageAsync(conversationId, role, content);

    public Task UpdateMessageAsync(ChatMessage message) => _database.UpdateMessageAsync(message);

    public Task DeleteMessageAsync(long id) => _database.DeleteMessageAsync(id);

    public async IAsyncEnumerable<string> SendMessageAsync(
        long conversationId, 
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await _database.AddMessageAsync(conversationId, "user", userMessage);

        var settings = await _settingsService.GetSettingsAsync();
        string? ragContext = null;
        int chunksFound = 0;

        if (settings.UseRag)
        {
            var (retrievalResult, trace) = await _ragService.RetrieveWithTraceAsync(
                userMessage, 
                settings.RagTopK, 
                settings.RagMinSimilarity, 
                settings.RagRetrievalMode,
                settings.RrfK,
                settings.HybridBalance);
            
            ragContext = retrievalResult.CombinedContext;
            chunksFound = retrievalResult.Chunks.Count;
            RagContextRetrieved?.Invoke(this, new RagContextInfo(true, chunksFound, ragContext, retrievalResult, trace));
        }
        else
        {
            RagContextRetrieved?.Invoke(this, new RagContextInfo(false, 0, string.Empty));
        }

        var messages = await _database.GetMessagesAsync(conversationId);

        await foreach (var chunk in _llmService.StreamChatAsync(messages, ragContext, cancellationToken))
        {
            yield return chunk;
        }
    }

    public async Task<List<MessageSearchResult>> SemanticSearchAsync(string query, int topK = 10, CancellationToken cancellationToken = default)
    {
        var queryEmbedding = await _llmService.GetEmbeddingAsync(query, cancellationToken);
        if (queryEmbedding.Length == 0) return [];

        var messagesWithEmbeddings = await _database.GetAllMessagesWithEmbeddingsAsync();
        if (messagesWithEmbeddings.Count == 0) return [];

        var conversations = await _database.GetConversationsAsync();
        var convDict = conversations.ToDictionary(c => c.Id);

        var scored = new List<MessageSearchResult>();
        foreach (var msg in messagesWithEmbeddings)
        {
            var embedding = JsonSerializer.Deserialize<float[]>(msg.Embedding!);
            if (embedding != null)
            {
                var score = CosineSimilarity(queryEmbedding, embedding);
                if (convDict.TryGetValue(msg.ConversationId, out var conv))
                {
                    scored.Add(new MessageSearchResult
                    {
                        Message = msg,
                        Conversation = conv,
                        Score = score
                    });
                }
            }
        }

        return scored
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();
    }

    public async Task GenerateMessageEmbeddingsAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var messages = await _database.GetMessagesWithoutEmbeddingsAsync();
        progress?.Report($"Processing {messages.Count} messages...");

        for (int i = 0; i < messages.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var msg = messages[i];
            progress?.Report($"Embedding message {i + 1}/{messages.Count}");

            var embedding = await _llmService.GetEmbeddingAsync(msg.Content, cancellationToken);
            if (embedding.Length > 0)
            {
                var embeddingJson = JsonSerializer.Serialize(embedding);
                await _database.UpdateMessageEmbeddingAsync(msg.Id, embeddingJson);
            }

            await Task.Delay(50, cancellationToken); // Rate limiting
        }

        progress?.Report("Done!");
    }

    public async Task<int> GetMessagesWithoutEmbeddingsCountAsync()
    {
        var messages = await _database.GetMessagesWithoutEmbeddingsAsync();
        return messages.Count;
    }

    public async Task<string> GenerateConversationTitleAsync(string userMessage, string assistantResponse, CancellationToken cancellationToken = default)
    {
        var titlePrompt = new List<ChatMessage>
        {
            new() { Role = "system", Content = "Generate a very short title (3-6 words max) for this conversation. Reply with ONLY the title, no quotes, no punctuation at the end." },
            new() { Role = "user", Content = $"User: {userMessage}\n\nAssistant: {assistantResponse[..Math.Min(200, assistantResponse.Length)]}" }
        };

        var titleBuilder = new System.Text.StringBuilder();
        await foreach (var chunk in _llmService.StreamChatAsync(titlePrompt, null, cancellationToken))
        {
            titleBuilder.Append(chunk);
        }

        var title = titleBuilder.ToString().Trim().Trim('"').Trim('\'');
        return title.Length > 50 ? title[..50] : title;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom == 0 ? 0 : dot / denom;
    }
}
