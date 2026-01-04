using Microsoft.Data.Sqlite;
using System.IO;
using System.Text.Json;
using WPFLLM.Models;

namespace WPFLLM.Services;

public class DatabaseService : IDatabaseService
{
    private readonly string _connectionString;
    private readonly string _dbPath;

    public DatabaseService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "WPFLLM");
        Directory.CreateDirectory(appFolder);
        _dbPath = Path.Combine(appFolder, "wpfllm.db");
        _connectionString = $"Data Source={_dbPath}";
    }

    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public async Task InitializeAsync()
    {
        using var connection = CreateConnection();
        
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Conversations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Messages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ConversationId INTEGER NOT NULL,
                Role TEXT NOT NULL,
                Content TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                Embedding TEXT,
                FOREIGN KEY (ConversationId) REFERENCES Conversations(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Documents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FileName TEXT NOT NULL,
                Content TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Chunks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DocumentId INTEGER NOT NULL,
                Content TEXT NOT NULL,
                ChunkIndex INTEGER NOT NULL,
                Embedding TEXT,
                FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Settings (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                Data TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS SavedModels (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ModelId TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL,
                Provider TEXT NOT NULL,
                Description TEXT,
                ContextLength INTEGER,
                PricingInfo TEXT,
                IsFavorite INTEGER DEFAULT 0,
                LastUsed TEXT,
                UseCount INTEGER DEFAULT 0,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ApiKeys (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Provider TEXT NOT NULL UNIQUE,
                ApiKey TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            -- FTS5 virtual table for full-text search on chunks
            CREATE VIRTUAL TABLE IF NOT EXISTS ChunksFts USING fts5(
                Content,
                content='Chunks',
                content_rowid='Id'
            );
            """;
        
        await command.ExecuteNonQueryAsync();

        // Migration: Add Embedding column if not exists
        var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = "ALTER TABLE Messages ADD COLUMN Embedding TEXT";
        try { await alterCommand.ExecuteNonQueryAsync(); } catch { /* Column already exists */ }

        // Rebuild FTS index from existing chunks
        await RebuildFtsIndexAsync(connection);
    }

    private static async Task RebuildFtsIndexAsync(SqliteConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO ChunksFts(ChunksFts) VALUES('rebuild')";
        try { await cmd.ExecuteNonQueryAsync(); } catch { /* FTS rebuild might fail if empty */ }
    }

    public async Task<List<Conversation>> GetConversationsAsync()
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Title, CreatedAt, UpdatedAt FROM Conversations ORDER BY UpdatedAt DESC";
        
        var conversations = new List<Conversation>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            conversations.Add(new Conversation
            {
                Id = reader.GetInt64(0),
                Title = reader.GetString(1),
                CreatedAt = DateTime.Parse(reader.GetString(2)),
                UpdatedAt = DateTime.Parse(reader.GetString(3))
            });
        }
        return conversations;
    }

    public async Task<Conversation> CreateConversationAsync(string title)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        var now = DateTime.UtcNow.ToString("o");
        command.CommandText = "INSERT INTO Conversations (Title, CreatedAt, UpdatedAt) VALUES (@title, @now, @now); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("@title", title);
        command.Parameters.AddWithValue("@now", now);
        
        var id = (long)(await command.ExecuteScalarAsync())!;
        return new Conversation { Id = id, Title = title, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    }

    public async Task UpdateConversationAsync(Conversation conversation)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Conversations SET Title = @title, UpdatedAt = @updatedAt WHERE Id = @id";
        command.Parameters.AddWithValue("@title", conversation.Title);
        command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("o"));
        command.Parameters.AddWithValue("@id", conversation.Id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteConversationAsync(long id)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Messages WHERE ConversationId = @id; DELETE FROM Conversations WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<ChatMessage>> GetMessagesAsync(long conversationId)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, ConversationId, Role, Content, CreatedAt FROM Messages WHERE ConversationId = @convId ORDER BY CreatedAt";
        command.Parameters.AddWithValue("@convId", conversationId);
        
        var messages = new List<ChatMessage>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            messages.Add(new ChatMessage
            {
                Id = reader.GetInt64(0),
                ConversationId = reader.GetInt64(1),
                Role = reader.GetString(2),
                Content = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4))
            });
        }
        return messages;
    }

    public async Task<ChatMessage> AddMessageAsync(long conversationId, string role, string content)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        var now = DateTime.UtcNow.ToString("o");
        command.CommandText = "INSERT INTO Messages (ConversationId, Role, Content, CreatedAt) VALUES (@convId, @role, @content, @now); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("@convId", conversationId);
        command.Parameters.AddWithValue("@role", role);
        command.Parameters.AddWithValue("@content", content);
        command.Parameters.AddWithValue("@now", now);
        
        var id = (long)(await command.ExecuteScalarAsync())!;
        
        var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = "UPDATE Conversations SET UpdatedAt = @now WHERE Id = @convId";
        updateCmd.Parameters.AddWithValue("@now", now);
        updateCmd.Parameters.AddWithValue("@convId", conversationId);
        await updateCmd.ExecuteNonQueryAsync();
        
        return new ChatMessage { Id = id, ConversationId = conversationId, Role = role, Content = content, CreatedAt = DateTime.UtcNow };
    }

    public async Task UpdateMessageAsync(ChatMessage message)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Messages SET Content = @content WHERE Id = @id";
        command.Parameters.AddWithValue("@content", message.Content);
        command.Parameters.AddWithValue("@id", message.Id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteMessageAsync(long id)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Messages WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateMessageEmbeddingAsync(long messageId, string embedding)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Messages SET Embedding = @embedding WHERE Id = @id";
        command.Parameters.AddWithValue("@embedding", embedding);
        command.Parameters.AddWithValue("@id", messageId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<ChatMessage>> GetAllMessagesWithEmbeddingsAsync()
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, ConversationId, Role, Content, CreatedAt, Embedding FROM Messages WHERE Embedding IS NOT NULL ORDER BY CreatedAt DESC";
        
        var messages = new List<ChatMessage>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            messages.Add(new ChatMessage
            {
                Id = reader.GetInt64(0),
                ConversationId = reader.GetInt64(1),
                Role = reader.GetString(2),
                Content = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                Embedding = reader.GetString(5)
            });
        }
        return messages;
    }

    public async Task<List<ChatMessage>> GetMessagesWithoutEmbeddingsAsync()
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, ConversationId, Role, Content, CreatedAt FROM Messages WHERE Embedding IS NULL ORDER BY CreatedAt";
        
        var messages = new List<ChatMessage>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            messages.Add(new ChatMessage
            {
                Id = reader.GetInt64(0),
                ConversationId = reader.GetInt64(1),
                Role = reader.GetString(2),
                Content = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4))
            });
        }
        return messages;
    }

    public async Task<List<RagDocument>> GetDocumentsAsync()
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, FileName, Content, CreatedAt FROM Documents ORDER BY CreatedAt DESC";
        
        var documents = new List<RagDocument>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            documents.Add(new RagDocument
            {
                Id = reader.GetInt64(0),
                FileName = reader.GetString(1),
                Content = reader.GetString(2),
                CreatedAt = DateTime.Parse(reader.GetString(3))
            });
        }
        return documents;
    }

    public async Task<RagDocument> AddDocumentAsync(string fileName, string content)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        var now = DateTime.UtcNow.ToString("o");
        command.CommandText = "INSERT INTO Documents (FileName, Content, CreatedAt) VALUES (@fileName, @content, @now); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("@fileName", fileName);
        command.Parameters.AddWithValue("@content", content);
        command.Parameters.AddWithValue("@now", now);
        
        var id = (long)(await command.ExecuteScalarAsync())!;
        return new RagDocument { Id = id, FileName = fileName, Content = content, CreatedAt = DateTime.UtcNow };
    }

    public async Task DeleteDocumentAsync(long id)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Chunks WHERE DocumentId = @id; DELETE FROM Documents WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<RagChunk>> GetChunksAsync(long documentId)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, DocumentId, Content, ChunkIndex, Embedding FROM Chunks WHERE DocumentId = @docId ORDER BY ChunkIndex";
        command.Parameters.AddWithValue("@docId", documentId);
        
        var chunks = new List<RagChunk>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            chunks.Add(new RagChunk
            {
                Id = reader.GetInt64(0),
                DocumentId = reader.GetInt64(1),
                Content = reader.GetString(2),
                ChunkIndex = reader.GetInt32(3),
                Embedding = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        return chunks;
    }

    public async Task<List<RagChunk>> GetAllChunksAsync()
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, DocumentId, Content, ChunkIndex, Embedding FROM Chunks ORDER BY DocumentId, ChunkIndex";
        
        var chunks = new List<RagChunk>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            chunks.Add(new RagChunk
            {
                Id = reader.GetInt64(0),
                DocumentId = reader.GetInt64(1),
                Content = reader.GetString(2),
                ChunkIndex = reader.GetInt32(3),
                Embedding = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        return chunks;
    }

    public async Task AddChunksAsync(long documentId, List<string> chunks)
    {
        using var connection = CreateConnection();
        for (int i = 0; i < chunks.Count; i++)
        {
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Chunks (DocumentId, Content, ChunkIndex) VALUES (@docId, @content, @index)";
            command.Parameters.AddWithValue("@docId", documentId);
            command.Parameters.AddWithValue("@content", chunks[i]);
            command.Parameters.AddWithValue("@index", i);
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task UpdateChunkEmbeddingAsync(long chunkId, string embedding)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Chunks SET Embedding = @embedding WHERE Id = @id";
        command.Parameters.AddWithValue("@embedding", embedding);
        command.Parameters.AddWithValue("@id", chunkId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<(RagChunk Chunk, double Score)>> SearchChunksFtsAsync(string query, int limit = 20)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        
        // Escape FTS5 special characters and prepare query
        var ftsQuery = PrepareFtsQuery(query);
        
        command.CommandText = """
            SELECT c.Id, c.DocumentId, c.Content, c.ChunkIndex, c.Embedding, bm25(ChunksFts) as score
            FROM ChunksFts fts
            JOIN Chunks c ON c.Id = fts.rowid
            WHERE ChunksFts MATCH @query
            ORDER BY score
            LIMIT @limit
            """;
        command.Parameters.AddWithValue("@query", ftsQuery);
        command.Parameters.AddWithValue("@limit", limit);

        var results = new List<(RagChunk, double)>();
        try
        {
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var chunk = new RagChunk
                {
                    Id = reader.GetInt64(0),
                    DocumentId = reader.GetInt64(1),
                    Content = reader.GetString(2),
                    ChunkIndex = reader.GetInt32(3),
                    Embedding = reader.IsDBNull(4) ? null : reader.GetString(4)
                };
                var score = Math.Abs(reader.GetDouble(5)); // BM25 returns negative scores
                results.Add((chunk, score));
            }
        }
        catch
        {
            // FTS might not have data yet
        }
        return results;
    }

    private static string PrepareFtsQuery(string query)
    {
        // Split into terms and join with OR for more flexible matching
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 1)
            .Select(t => $"\"{t}\"*"); // Prefix matching with quotes for safety
        
        return string.Join(" OR ", terms);
    }

    public async Task<string?> GetDocumentNameAsync(long documentId)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT FileName FROM Documents WHERE Id = @id";
        command.Parameters.AddWithValue("@id", documentId);
        
        var result = await command.ExecuteScalarAsync();
        return result as string;
    }

    public async Task<AppSettings> GetSettingsAsync()
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Data FROM Settings WHERE Id = 1";
        
        var result = await command.ExecuteScalarAsync();
        if (result is string json)
        {
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        return new AppSettings();
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        using var connection = CreateConnection();
        var json = JsonSerializer.Serialize(settings);
        var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO Settings (Id, Data) VALUES (1, @data)";
        command.Parameters.AddWithValue("@data", json);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<SavedModel>> GetSavedModelsAsync()
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, ModelId, DisplayName, Provider, Description, ContextLength, PricingInfo, IsFavorite, LastUsed, UseCount, CreatedAt FROM SavedModels ORDER BY IsFavorite DESC, UseCount DESC, LastUsed DESC";
        
        var models = new List<SavedModel>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            models.Add(new SavedModel
            {
                Id = reader.GetInt64(0),
                ModelId = reader.GetString(1),
                DisplayName = reader.GetString(2),
                Provider = reader.GetString(3),
                Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                ContextLength = reader.GetInt32(5),
                PricingInfo = reader.IsDBNull(6) ? null : reader.GetString(6),
                IsFavorite = reader.GetInt32(7) == 1,
                LastUsed = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                UseCount = reader.GetInt32(9),
                CreatedAt = DateTime.Parse(reader.GetString(10))
            });
        }
        return models;
    }

    public async Task<SavedModel?> GetSavedModelAsync(string modelId)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, ModelId, DisplayName, Provider, Description, ContextLength, PricingInfo, IsFavorite, LastUsed, UseCount, CreatedAt FROM SavedModels WHERE ModelId = @modelId";
        command.Parameters.AddWithValue("@modelId", modelId);
        
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SavedModel
            {
                Id = reader.GetInt64(0),
                ModelId = reader.GetString(1),
                DisplayName = reader.GetString(2),
                Provider = reader.GetString(3),
                Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                ContextLength = reader.GetInt32(5),
                PricingInfo = reader.IsDBNull(6) ? null : reader.GetString(6),
                IsFavorite = reader.GetInt32(7) == 1,
                LastUsed = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                UseCount = reader.GetInt32(9),
                CreatedAt = DateTime.Parse(reader.GetString(10))
            };
        }
        return null;
    }

    public async Task SaveModelAsync(SavedModel model)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        var now = DateTime.UtcNow.ToString("o");
        command.CommandText = """
            INSERT OR REPLACE INTO SavedModels (ModelId, DisplayName, Provider, Description, ContextLength, PricingInfo, IsFavorite, LastUsed, UseCount, CreatedAt)
            VALUES (@modelId, @displayName, @provider, @description, @contextLength, @pricingInfo, @isFavorite, @lastUsed, @useCount, @createdAt)
            """;
        command.Parameters.AddWithValue("@modelId", model.ModelId);
        command.Parameters.AddWithValue("@displayName", model.DisplayName);
        command.Parameters.AddWithValue("@provider", model.Provider);
        command.Parameters.AddWithValue("@description", model.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@contextLength", model.ContextLength);
        command.Parameters.AddWithValue("@pricingInfo", model.PricingInfo ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@isFavorite", model.IsFavorite ? 1 : 0);
        command.Parameters.AddWithValue("@lastUsed", model.LastUsed?.ToString("o") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@useCount", model.UseCount);
        command.Parameters.AddWithValue("@createdAt", model.CreatedAt == default ? now : model.CreatedAt.ToString("o"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteSavedModelAsync(string modelId)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SavedModels WHERE ModelId = @modelId";
        command.Parameters.AddWithValue("@modelId", modelId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateModelUsageAsync(string modelId)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        var now = DateTime.UtcNow.ToString("o");
        command.CommandText = "UPDATE SavedModels SET LastUsed = @now, UseCount = UseCount + 1 WHERE ModelId = @modelId";
        command.Parameters.AddWithValue("@now", now);
        command.Parameters.AddWithValue("@modelId", modelId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task ToggleFavoriteModelAsync(string modelId)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE SavedModels SET IsFavorite = CASE WHEN IsFavorite = 1 THEN 0 ELSE 1 END WHERE ModelId = @modelId";
        command.Parameters.AddWithValue("@modelId", modelId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<SavedApiKey>> GetApiKeysAsync()
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Provider, ApiKey, CreatedAt, UpdatedAt FROM ApiKeys ORDER BY Provider";
        
        var keys = new List<SavedApiKey>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            keys.Add(new SavedApiKey
            {
                Id = reader.GetInt64(0),
                Provider = reader.GetString(1),
                ApiKey = reader.GetString(2),
                CreatedAt = DateTime.Parse(reader.GetString(3)),
                UpdatedAt = DateTime.Parse(reader.GetString(4))
            });
        }
        return keys;
    }

    public async Task<string?> GetApiKeyAsync(string provider)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT ApiKey FROM ApiKeys WHERE Provider = @provider";
        command.Parameters.AddWithValue("@provider", provider);
        
        var result = await command.ExecuteScalarAsync();
        return result as string;
    }

    public async Task SaveApiKeyAsync(string provider, string apiKey)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        var now = DateTime.UtcNow.ToString("o");
        command.CommandText = """
            INSERT INTO ApiKeys (Provider, ApiKey, CreatedAt, UpdatedAt) VALUES (@provider, @apiKey, @now, @now)
            ON CONFLICT(Provider) DO UPDATE SET ApiKey = @apiKey, UpdatedAt = @now
            """;
        command.Parameters.AddWithValue("@provider", provider);
        command.Parameters.AddWithValue("@apiKey", apiKey);
        command.Parameters.AddWithValue("@now", now);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteApiKeyAsync(string provider)
    {
        using var connection = CreateConnection();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ApiKeys WHERE Provider = @provider";
        command.Parameters.AddWithValue("@provider", provider);
        await command.ExecuteNonQueryAsync();
    }
}
