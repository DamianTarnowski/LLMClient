using SQLite;
using LLMClient.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using SQLiteNetExtensionsAsync.Extensions;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace LLMClient.Services
{
    public class DatabaseService : IDatabaseService
    {
        private SQLiteAsyncConnection? _database;
        private readonly ISecureApiKeyService _secureApiKeyService;
        private readonly IEmbeddingService? _embeddingService;
        private bool _migrationCompleted = false;
        private const string DB_ENCRYPTION_KEY_NAME = "llmclient_db_key";
        private const string APPLICATION_ID_KEY_NAME = "llmclient_app_id";
        private const string DB_CUSTOM_KEY_NAME = "llmclient_db_custom_key";
        private const int PBKDF2_ITERATIONS = 10000;
        private const int PBKDF2_KEY_SIZE = 32;

        public DatabaseService(ISecureApiKeyService secureApiKeyService, IEmbeddingService? embeddingService = null)
        {
            _secureApiKeyService = secureApiKeyService;
            _embeddingService = embeddingService;
            System.Diagnostics.Debug.WriteLine("DatabaseService: Constructor completed - lazy initialization will be used");
        }

        private async Task EnsureDatabaseInitializedAsync()
        {
            if (_migrationCompleted) return;

            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "llmclient.db3");
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"DatabaseService: Initializing database at {dbPath}");
                System.Diagnostics.Debug.WriteLine($"DatabaseService: Database file exists: {File.Exists(dbPath)}");
                
                // Pobierz lub wygeneruj klucz szyfrowania
                var encryptionKey = await GetOrGenerateEncryptionKeyAsync();
                System.Diagnostics.Debug.WriteLine("DatabaseService: Encryption key obtained");
                
                // Pobierz lub wygeneruj unikalny identyfikator aplikacji
                var applicationId = await GetOrGenerateApplicationIdAsync();
                System.Diagnostics.Debug.WriteLine($"DatabaseService: Application ID obtained: {applicationId?.Substring(0, Math.Min(8, applicationId?.Length ?? 0))}...");
                
                // Utwórz zaszyfrowane połączenie z bazą danych
                var connectionString = new SQLiteConnectionString(dbPath, true, key: encryptionKey);
                _database = new SQLiteAsyncConnection(connectionString);
                System.Diagnostics.Debug.WriteLine("DatabaseService: Database connection established");
                
                await _database.CreateTableAsync<AiModel>();
                System.Diagnostics.Debug.WriteLine("DatabaseService: AiModel table created/verified");
                
                await _database.CreateTableAsync<LLMClient.Models.Conversation>();
                System.Diagnostics.Debug.WriteLine("DatabaseService: Conversation table created/verified");
                
                await _database.CreateTableAsync<LLMClient.Models.Message>();
                System.Diagnostics.Debug.WriteLine("DatabaseService: Message table created/verified");
                
                await _database.CreateTableAsync<LLMClient.Models.Memory>();
                System.Diagnostics.Debug.WriteLine("DatabaseService: Memory table created/verified");
                await _database.CreateTableAsync<LLMClient.ViewModels.ModelSettings>();
                System.Diagnostics.Debug.WriteLine("DatabaseService: ModelSettings table created/verified");
                
                await _database.CreateTableAsync<LLMClient.Models.RagDocument>();
                System.Diagnostics.Debug.WriteLine("DatabaseService: RagDocument table created/verified");
                
                await _database.CreateTableAsync<LLMClient.Models.RagChunk>();
                System.Diagnostics.Debug.WriteLine("DatabaseService: RagChunk table created/verified");
                
                _migrationCompleted = true;
                System.Diagnostics.Debug.WriteLine("DatabaseService: Initialization completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DatabaseService: CRITICAL ERROR during initialization: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"DatabaseService: Stack trace: {ex.StackTrace}");
                
                // Jeśli baza danych jest uszkodzona, spróbuj ją usunąć i utworzyć ponownie
                if (ex.Message.Contains("file is not a database") || ex.Message.Contains("database disk image is malformed"))
                {
                    System.Diagnostics.Debug.WriteLine("DatabaseService: Database appears corrupted, attempting to recreate...");
                    try
                    {
                        // Zamknij istniejące połączenie jeśli istnieje
                        _database?.CloseAsync()?.Wait();
                        _database = null;
                        
                        // Usuń uszkodzoną bazę danych
                        if (File.Exists(dbPath))
                        {
                            File.Delete(dbPath);
                            System.Diagnostics.Debug.WriteLine($"DatabaseService: Corrupted database deleted: {dbPath}");
                        }
                        
                        // Opóźnienie żeby upewnić się że plik jest zwolniony
                        await Task.Delay(100);
                        
                        // Sprawdź czy plik rzeczywiście został usunięty
                        if (File.Exists(dbPath))
                        {
                            System.Diagnostics.Debug.WriteLine("DatabaseService: Failed to delete corrupted database file");
                            throw new Exception("Cannot delete corrupted database file");
                        }
                        
                        // Spróbuj ponownie utworzyć bazę danych z nowym kluczem
                        var newEncryptionKey = await GetOrGenerateEncryptionKeyAsync();
                        var connectionString = new SQLiteConnectionString(dbPath, true, key: newEncryptionKey);
                        _database = new SQLiteAsyncConnection(connectionString);
                        
                        await _database.CreateTableAsync<AiModel>();
                        await _database.CreateTableAsync<LLMClient.Models.Conversation>();
                        await _database.CreateTableAsync<LLMClient.Models.Message>();
                        await _database.CreateTableAsync<LLMClient.Models.Memory>();
                        await _database.CreateTableAsync<LLMClient.ViewModels.ModelSettings>();
                        
                        _migrationCompleted = true;
                        System.Diagnostics.Debug.WriteLine("DatabaseService: Database successfully recreated after corruption");
                        return;
                    }
                    catch (Exception recreateEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"DatabaseService: Failed to recreate database: {recreateEx.Message}");
                        System.Diagnostics.Debug.WriteLine($"DatabaseService: Recreation error stack trace: {recreateEx.StackTrace}");
                        
                        // Jako ostatnia deska ratunku, spróbuj bez szyfrowania
                        try
                        {
                            System.Diagnostics.Debug.WriteLine("DatabaseService: Attempting to create unencrypted database as fallback...");
                            _database?.CloseAsync()?.Wait();
                            _database = null;
                            
                            if (File.Exists(dbPath))
                                File.Delete(dbPath);
                            
                            await Task.Delay(100);
                            
                            var unencryptedConnectionString = new SQLiteConnectionString(dbPath, false);
                            _database = new SQLiteAsyncConnection(unencryptedConnectionString);
                            
                            await _database.CreateTableAsync<AiModel>();
                            await _database.CreateTableAsync<LLMClient.Models.Conversation>();
                            await _database.CreateTableAsync<LLMClient.Models.Message>();
                            await _database.CreateTableAsync<LLMClient.Models.Memory>();
                            await _database.CreateTableAsync<LLMClient.ViewModels.ModelSettings>();
                            
                            _migrationCompleted = true;
                            System.Diagnostics.Debug.WriteLine("DatabaseService: Unencrypted fallback database created successfully");
                            return;
                        }
                        catch (Exception fallbackEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"DatabaseService: Fallback unencrypted database also failed: {fallbackEx.Message}");
                        }
                    }
                }
                
                throw; 
            }
        }

        /// <summary>
        /// Pobiera istniejący klucz szyfrowania z SecureStorage lub generuje nowy jeśli nie istnieje
        /// </summary>
        private async Task<string> GetOrGenerateEncryptionKeyAsync()
        {
            try
            {
                // Sprawdź custom key najpierw
                var customKey = await SecureStorage.GetAsync(DB_CUSTOM_KEY_NAME);
                if (!string.IsNullOrEmpty(customKey))
                    return customKey;

                // Potem legacy auto-generated
                var existingKey = await SecureStorage.GetAsync(DB_ENCRYPTION_KEY_NAME);
                if (!string.IsNullOrEmpty(existingKey))
                    return existingKey;

                // Generate new auto-key
                var newKey = GenerateEncryptionKey();
                await SecureStorage.SetAsync(DB_ENCRYPTION_KEY_NAME, newKey);
                return newKey;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Nie udało się pobrać/wygenerować klucza szyfrowania: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generuje bezpieczny klucz szyfrowania
        /// </summary>
        private string GenerateEncryptionKey()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var keyBytes = new byte[32]; // 256-bit klucz
                rng.GetBytes(keyBytes);
                return Convert.ToBase64String(keyBytes);
            }
        }

        /// <summary>
        /// Pobiera istniejący identyfikator aplikacji z SecureStorage lub generuje nowy jeśli nie istnieje
        /// </summary>
        private async Task<string> GetOrGenerateApplicationIdAsync()
        {
            try
            {
                // Sprawdź czy Application ID już istnieje
                var existingId = await SecureStorage.GetAsync(APPLICATION_ID_KEY_NAME);
                
                if (!string.IsNullOrEmpty(existingId))
                {
                    return existingId;
                }

                // Wygeneruj nowy unikalny identyfikator
                var newId = GenerateApplicationId();
                
                // Zapisz w SecureStorage
                await SecureStorage.SetAsync(APPLICATION_ID_KEY_NAME, newId);
                
                return newId;
            }
            catch (Exception ex)
            {
                // W przypadku błędu, wygeneruj tymczasowy ID
                throw new InvalidOperationException($"Nie udało się pobrać/wygenerować Application ID: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generuje unikalny identyfikator aplikacji
        /// </summary>
        private string GenerateApplicationId()
        {
            // Generuj GUID + timestamp dla dodatkowej unikalności
            var guid = Guid.NewGuid();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return $"{guid:N}-{timestamp:X}";
        }

        /// <summary>
        /// Sprawdza czy baza danych jest zaszyfrowana
        /// </summary>
        public async Task<bool> IsDatabaseEncryptedAsync()
        {
            try
            {
                // Próba wykonania prostego zapytania na zaszyfrowanej bazie
                await _database.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master");
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Regeneruje klucz szyfrowania bazy danych (wymaga ponownego uruchomienia aplikacji)
        /// </summary>
        public async Task<bool> RegenerateEncryptionKeyAsync()
        {
            try
            {
                // Wygeneruj nowy klucz
                var newKey = GenerateEncryptionKey();
                
                // Zapisz nowy klucz w SecureStorage
                await SecureStorage.SetAsync(DB_ENCRYPTION_KEY_NAME, newKey);
                
                // Uwaga: Zmiana klucza wymaga ponownego uruchomienia aplikacji
                // Alternatywnie można by użyć PRAGMA rekey, ale to jest bardziej skomplikowane
                return true;
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"Błąd podczas regeneracji klucza: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pobiera informacje o szyfrowaniu bazy danych
        /// </summary>
        public async Task<string> GetEncryptionInfoAsync()
        {
            try
            {
                var isEncrypted = await IsDatabaseEncryptedAsync();
                var keyExists = !string.IsNullOrEmpty(await SecureStorage.GetAsync(DB_ENCRYPTION_KEY_NAME));
                
                if (isEncrypted && keyExists)
                {
                    return "Baza danych jest zaszyfrowana i bezpieczna 🔒";
                }
                else if (keyExists)
                {
                    return "Klucz szyfrowania istnieje, ale status niejasny ⚠️";
                }
                else
                {
                    return "Baza danych nie jest zaszyfrowana ❌";
                }
            }
            catch (Exception ex)
            {
                return $"Błąd sprawdzania szyfrowania: {ex.Message}";
            }
        }

        /// <summary>
        /// Pobiera unikalny identyfikator aplikacji
        /// </summary>
        public async Task<string> GetApplicationIdAsync()
        {
            try
            {
                var applicationId = await SecureStorage.GetAsync(APPLICATION_ID_KEY_NAME);
                return applicationId ?? "ID nie został jeszcze wygenerowany";
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Nie udało się pobrać Application ID: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Pobiera informacje o identyfikatorze aplikacji
        /// </summary>
        public async Task<string> GetApplicationInfoAsync()
        {
            try
            {
                var applicationId = await SecureStorage.GetAsync(APPLICATION_ID_KEY_NAME);
                
                if (!string.IsNullOrEmpty(applicationId))
                {
                    // Pokaż tylko pierwsze 8 znaków dla bezpieczeństwa
                    var shortId = applicationId.Length > 8 ? applicationId.Substring(0, 8) + "..." : applicationId;
                    return $"ID: {shortId} ✅";
                }
                else
                {
                    return "ID aplikacji nie został wygenerowany ❌";
                }
            }
            catch (Exception ex)
            {
                return $"Błąd pobierania ID: {ex.Message}";
            }
        }

        /// <summary>
        /// Regeneruje identyfikator aplikacji (ostrożnie - zmieni to sposób identyfikacji użytkownika!)
        /// </summary>
        public async Task<bool> RegenerateApplicationIdAsync()
        {
            try
            {
                // Usuń stary ID
                SecureStorage.Remove(APPLICATION_ID_KEY_NAME);
                
                // Wygeneruj nowy
                var newId = GenerateApplicationId();
                await SecureStorage.SetAsync(APPLICATION_ID_KEY_NAME, newId);
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd podczas regeneracji Application ID: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetCustomPassphraseAsync(string passphrase)
        {
            if (string.IsNullOrWhiteSpace(passphrase) || passphrase.Length < 8)
                return false; // Walidacja

            try
            {
                // Generate salt
                var salt = new byte[16];
                RandomNumberGenerator.Fill(salt);

                // Derive key z passphrase using static Pbkdf2 method (.NET 10+)
                var keyBytes = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, PBKDF2_ITERATIONS, HashAlgorithmName.SHA256, PBKDF2_KEY_SIZE);
                var customKey = Convert.ToBase64String(keyBytes) + ":" + Convert.ToBase64String(salt); // Store key:salt

                await SecureStorage.SetAsync(DB_CUSTOM_KEY_NAME, customKey);
                // Usuń stary auto-key jeśli istnieje
                SecureStorage.Remove(DB_ENCRYPTION_KEY_NAME);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Metody dla AiModel
        public async Task<List<AiModel>> GetModelsAsync()
        {
            await EnsureDatabaseInitializedAsync();
            var models = await _database.GetAllWithChildrenAsync<AiModel>();
            
            // Migrate API keys from SQLite to SecureStorage (one-time operation)
            if (!_migrationCompleted)
            {
                await MigrateApiKeysAsync(models);
                _migrationCompleted = true;
            }
            
            // Load API keys from SecureStorage for UI binding
            foreach (var model in models)
            {
                model.ApiKey = await _secureApiKeyService.GetApiKeyAsync(model.Id) ?? string.Empty;
            }
            
            return models;
        }

        public async Task SaveModelAsync(AiModel model)
        {
            await EnsureDatabaseInitializedAsync();
            // Save API key to SecureStorage
            if (!string.IsNullOrWhiteSpace(model.ApiKey))
            {
                await _secureApiKeyService.SetApiKeyAsync(model.Id, model.ApiKey);
            }
            
            // Clear API key before saving to database
            var originalApiKey = model.ApiKey;
            model.ApiKey = string.Empty;
            
            try
            {
                await _database.InsertOrReplaceWithChildrenAsync(model);
            }
            finally
            {
                // Restore API key for UI binding
                model.ApiKey = originalApiKey;
            }
        }

        public async Task DeleteModelAsync(AiModel model)
        {
            await EnsureDatabaseInitializedAsync();
            await _secureApiKeyService.DeleteApiKeyAsync(model.Id);
            await _database.DeleteAsync(model);
        }

        private async Task MigrateApiKeysAsync(List<AiModel> models)
        {
            try
            {
                // Check if any model has API key stored in SQLite (old format)
                var modelsWithApiKeys = models.Where(m => !string.IsNullOrWhiteSpace(m.ApiKey)).ToList();
                
                if (modelsWithApiKeys.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"Migrating {modelsWithApiKeys.Count} API keys to SecureStorage");
                    
                    foreach (var model in modelsWithApiKeys)
                    {
                        await _secureApiKeyService.SetApiKeyAsync(model.Id, model.ApiKey);
                        
                        // Clear API key from SQLite
                        model.ApiKey = string.Empty;
                        await _database.UpdateAsync(model);
                    }
                    
                    System.Diagnostics.Debug.WriteLine("API key migration completed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during API key migration: {ex.Message}");
            }
        }

        // Metody dla Conversation - POPRAWIONE
        public async Task<List<Conversation>> GetConversationsAsync()
        {
            await EnsureDatabaseInitializedAsync();
            var conversations = await _database.Table<Conversation>().ToListAsync();

            foreach (var conversation in conversations)
            {
                var messages = await _database.Table<Message>()
                    .Where(m => m.ConversationId == conversation.Id)
                    .OrderBy(m => m.Timestamp)
                    .ThenBy(m => m.Id)
                    .ToListAsync();

                conversation.Messages = new ObservableCollection<Message>(messages);
            }

            return conversations;
        }

        public async Task<int> SaveConversationAsync(Conversation conversation)
        {
            await EnsureDatabaseInitializedAsync();
            if (conversation.Id != 0)
            {
                await _database.UpdateAsync(conversation);
                return conversation.Id;
            }
            else
            {
                await _database.InsertAsync(conversation);
                return conversation.Id; // SQLite automatycznie ustawia ID
            }
        }

        public async Task DeleteConversationAsync(int conversationId)
        {
            await EnsureDatabaseInitializedAsync();
            // Usu� wszystkie wiadomo�ci z konwersacji
            await _database.Table<Message>()
                .Where(m => m.ConversationId == conversationId)
                .DeleteAsync();

            // Usu� konwersacj�
            await _database.DeleteAsync<Conversation>(conversationId);
        }

        // Przeci��enie dla obiektu Conversation
        public Task DeleteConversationAsync(Conversation conversation) => DeleteConversationAsync(conversation.Id);

        // Metody dla Message - POPRAWIONE
        public Task<List<Message>> GetMessagesAsync(int conversationId, int limit = 50, int offset = 0) =>
            _database.Table<Message>()
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.Timestamp)
                .ThenBy(m => m.Id)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

        public async Task<int> SaveMessageAsync(Message message)
        {
            await EnsureDatabaseInitializedAsync();
            if (message.Id != 0)
            {
                await _database.UpdateAsync(message);
                return message.Id;
            }
            else
            {
                await _database.InsertAsync(message);
                return message.Id; // SQLite automatycznie ustawia ID
            }
        }

        public Task DeleteMessageAsync(Message message) => _database.DeleteAsync(message);

        // Dodatkowa metoda do pobierania konkretnej konwersacji
        public async Task<Conversation?> GetConversationAsync(int id)
        {
            await EnsureDatabaseInitializedAsync();
            var conversation = await _database.FindAsync<Conversation>(id);

            if (conversation != null)
            {
                var messages = await _database.Table<Message>()
                    .Where(m => m.ConversationId == conversation.Id)
                    .OrderBy(m => m.Timestamp)
                    .ThenBy(m => m.Id)
                    .ToListAsync();

                conversation.Messages = new ObservableCollection<Message>(messages);
            }

            return conversation;
        }

        // ===== EMBEDDING METHODS =====
        // TODO: Uncomment when IEmbeddingService interface is properly resolved

        /// <summary>
        /// Generuje i zapisuje embedding dla wiadomości
        /// </summary>
        public async Task<bool> GenerateAndSaveEmbeddingAsync(Message message)
        {
            if (_embeddingService == null || !_embeddingService.IsInitialized || string.IsNullOrWhiteSpace(message.Content))
                return false;

            try
            {
                var embedding = await _embeddingService.GenerateEmbeddingAsync(message.Content, false);
                if (embedding == null) return false;

                message.Embedding = _embeddingService.FloatArrayToBytes(embedding);
                message.EmbeddingVersion = _embeddingService.ModelVersion;

                await SaveMessageAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd generowania embeddingu: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Znajduje wiadomości bez embeddingów lub z przestarzałą wersją
        /// </summary>
        public async Task<List<Message>> GetMessagesNeedingEmbeddingsAsync()
        {
            await EnsureDatabaseInitializedAsync();
            
            if (_embeddingService == null) return new List<Message>();
            
            var currentVersion = _embeddingService.ModelVersion;
            
            return await _database.Table<Message>()
                .Where(m => m.Embedding == null || m.EmbeddingVersion != currentVersion)
                .Where(m => !string.IsNullOrEmpty(m.Content))
                .ToListAsync();
        }

        /// <summary>
        /// Wyszukiwanie semantyczne w konkretnej konwersacji
        /// </summary>
        public async Task<List<(Message message, float similarity)>> SemanticSearchInConversationAsync(
            int conversationId, 
            float[] queryEmbedding, 
            float minSimilarity = 0.3f, 
            int maxResults = 10)
        {
            await EnsureDatabaseInitializedAsync();
            
            if (_embeddingService == null) return new List<(Message, float)>();
            
            var messages = await _database.Table<Message>()
                .Where(m => m.ConversationId == conversationId && m.Embedding != null)
                .ToListAsync();

            var results = new List<(Message message, float similarity)>();

            foreach (var message in messages)
            {
                if (message.Embedding == null) continue;
                
                var messageEmbedding = _embeddingService.BytesToFloatArray(message.Embedding);
                if (messageEmbedding == null || queryEmbedding == null || messageEmbedding.Length == 0 || queryEmbedding.Length == 0 || messageEmbedding.Length != queryEmbedding.Length)
                {
                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] Ostrzeżenie: Nieprawidłowy embedding (messageId={message.Id}, queryEmbedding null={queryEmbedding==null}, messageEmbedding null={messageEmbedding==null}, messageEmbedding.Length={messageEmbedding?.Length}, queryEmbedding.Length={queryEmbedding?.Length})");
                    continue;
                }
                var similarity = _embeddingService.CalculateSimilarity(queryEmbedding, messageEmbedding);
                
                if (similarity >= minSimilarity)
                {
                    results.Add((message, similarity));
                }
            }

            return results
                .OrderByDescending(r => r.similarity)
                .Take(maxResults)
                .ToList();
        }

        // Klasa pomocnicza do mapowania wyników SQL
        public class MessageWithConversationTitle : Message
        {
            public string ConversationTitle { get; set; } = string.Empty;
        }

        /// <summary>
        /// Wyszukiwanie semantyczne we wszystkich konwersacjach
        /// </summary>
        public async Task<List<(Message message, float similarity, string conversationTitle)>> SemanticSearchAcrossConversationsAsync(
            float[] queryEmbedding, 
            float minSimilarity = 0.3f, 
            int maxResults = 20)
        {
            await EnsureDatabaseInitializedAsync();
            
            if (_embeddingService == null) return new List<(Message, float, string)>();
            
            var query = @"
                SELECT m.Id, m.Content, m.IsUser, m.Timestamp, m.ConversationId, m.Embedding, m.EmbeddingVersion, m.ImagePath, m.ImageBase64, c.Title as ConversationTitle 
                FROM Message m 
                INNER JOIN Conversation c ON m.ConversationId = c.Id 
                WHERE m.Embedding IS NOT NULL
            ";
            
            var messagesWithConversations = await _database.QueryAsync<MessageWithConversationTitle>(query);
            var results = new List<(Message message, float similarity, string conversationTitle)>();
            int scanned = 0;
            int invalid = 0;

            foreach (var row in messagesWithConversations)
            {
                var message = (Message)row;
                var conversationTitle = row.ConversationTitle ?? "Bez tytułu";
                
                if (message.Embedding == null) continue;
                
                var messageEmbedding = _embeddingService.BytesToFloatArray(message.Embedding);
                if (messageEmbedding == null || queryEmbedding == null || messageEmbedding.Length == 0 || queryEmbedding.Length == 0 || messageEmbedding.Length != queryEmbedding.Length)
                {
                    invalid++;
                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] Ostrzeżenie: Nieprawidłowy embedding (messageId={message.Id}, msgLen={messageEmbedding?.Length}, queryLen={queryEmbedding?.Length})");
                    continue;
                }
                scanned++;
                var similarity = _embeddingService.CalculateSimilarity(queryEmbedding, messageEmbedding);
                
                if (similarity >= minSimilarity)
                {
                    results.Add((message, similarity, conversationTitle));
                }
            }

                if (results.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] SemanticSearch scanned={scanned}, invalidDim={invalid}, passedThreshold=0");
                }
                else
                {
                    var maxSim = results.Max(r => r.similarity);
                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] SemanticSearch scanned={scanned}, invalidDim={invalid}, passedThreshold={results.Count}, maxSim={maxSim:F3}");
                }

            return results
                .OrderByDescending(r => r.similarity)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Wyszukiwanie tekstowe we wszystkich konwersacjach (case-insensitive LIKE)
        /// </summary>
        public async Task<List<(Message message, float matchScore, string conversationTitle)>> TextSearchAcrossConversationsAsync(
            string searchQuery,
            int maxResults = 20)
        {
            await EnsureDatabaseInitializedAsync();
            
            if (string.IsNullOrWhiteSpace(searchQuery))
                return new List<(Message, float, string)>();

            var escapedQuery = searchQuery.Replace("'", "''");
            
            var query = $@"
                SELECT m.Id, m.Content, m.IsUser, m.Timestamp, m.ConversationId, m.Embedding, m.EmbeddingVersion, m.ImagePath, m.ImageBase64, c.Title as ConversationTitle 
                FROM Message m 
                INNER JOIN Conversation c ON m.ConversationId = c.Id 
                WHERE m.Content LIKE '%{escapedQuery}%' COLLATE NOCASE
                ORDER BY m.Timestamp DESC
                LIMIT {maxResults * 2}
            ";
            
            var messagesWithConversations = await _database.QueryAsync<MessageWithConversationTitle>(query);
            var results = new List<(Message message, float matchScore, string conversationTitle)>();

            foreach (var row in messagesWithConversations)
            {
                var message = (Message)row;
                var conversationTitle = row.ConversationTitle ?? "Bez tytułu";
                
                var content = message.Content?.ToLowerInvariant() ?? "";
                var queryLower = searchQuery.ToLowerInvariant();
                
                int occurrences = 0;
                int index = 0;
                while ((index = content.IndexOf(queryLower, index, StringComparison.Ordinal)) != -1)
                {
                    occurrences++;
                    index += queryLower.Length;
                }
                
                float matchScore = Math.Min(1f, occurrences * 0.3f + 0.4f);
                
                if (content.Contains($" {queryLower} ") || content.StartsWith($"{queryLower} ") || content.EndsWith($" {queryLower}"))
                    matchScore = Math.Min(1f, matchScore + 0.2f);
                
                results.Add((message, matchScore, conversationTitle));
            }

            System.Diagnostics.Debug.WriteLine($"[DatabaseService] TextSearch query='{searchQuery}', found={results.Count}");

            return results
                .OrderByDescending(r => r.matchScore)
                .ThenByDescending(r => r.message.Timestamp)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Zlicza wiadomości z embeddingami
        /// </summary>
        public async Task<(int withEmbeddings, int total)> GetEmbeddingStatsAsync()
        {
            await EnsureDatabaseInitializedAsync();
            
            var total = await _database.Table<Message>().CountAsync();
            var withEmbeddings = await _database.Table<Message>()
                .Where(m => m.Embedding != null)
                .CountAsync();

            return (withEmbeddings, total);
        }

        /// <summary>
        /// Usuwa wszystkie embeddingi (przydatne przy zmianie modelu)
        /// </summary>
        public async Task<int> ClearAllEmbeddingsAsync()
        {
            await EnsureDatabaseInitializedAsync();
            
            return await _database.ExecuteAsync(@"
                UPDATE Message 
                SET Embedding = NULL, EmbeddingVersion = NULL 
                WHERE Embedding IS NOT NULL
            ");
        }

        /// <summary>
        /// Aktualizuje embeddingi do nowej wersji modelu
        /// </summary>
        public async Task<int> UpdateEmbeddingsToCurrentVersionAsync()
        {
            if (_embeddingService == null || !_embeddingService.IsInitialized) return 0;
            
            var messagesNeedingUpdate = await GetMessagesNeedingEmbeddingsAsync();
            var updateCount = 0;
            
            foreach (var message in messagesNeedingUpdate)
            {
                if (await GenerateAndSaveEmbeddingAsync(message))
                {
                    updateCount++;
                }
            }
            
            return updateCount;
        }

        // Memory operations
        public async Task<List<Memory>> GetAllMemoriesAsync()
        {
            System.Diagnostics.Debug.WriteLine("[DatabaseService] GetAllMemoriesAsync called");
            await EnsureDatabaseInitializedAsync();
            System.Diagnostics.Debug.WriteLine($"[DatabaseService] Database initialized, connection: {_database != null}");
            
            var memories = await _database!.Table<Memory>()
                .OrderByDescending(m => m.UpdatedAt)
                .ToListAsync();
            System.Diagnostics.Debug.WriteLine($"[DatabaseService] Retrieved {memories.Count} memories from database");
            
            foreach (var memory in memories)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Memory: {memory.Key} = {memory.Value} (ID: {memory.Id})");
            }
            
            return memories;
        }

        public async Task<Memory?> GetMemoryByKeyAsync(string key)
        {
            await EnsureDatabaseInitializedAsync();
            return await _database!.Table<Memory>()
                .Where(m => m.Key == key)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Memory>> SearchMemoriesAsync(string searchTerm)
        {
            await EnsureDatabaseInitializedAsync();
            var lowerSearchTerm = searchTerm.ToLower();
            return await _database!.Table<Memory>()
                .Where(m => m.Key.ToLower().Contains(lowerSearchTerm) || 
                           m.Value.ToLower().Contains(lowerSearchTerm) ||
                           m.Category.ToLower().Contains(lowerSearchTerm) ||
                           m.Tags.ToLower().Contains(lowerSearchTerm))
                .OrderByDescending(m => m.UpdatedAt)
                .ToListAsync();
        }

        public async Task<List<Memory>> GetMemoriesByCategoryAsync(string category)
        {
            await EnsureDatabaseInitializedAsync();
            return await _database!.Table<Memory>()
                .Where(m => m.Category == category)
                .OrderByDescending(m => m.UpdatedAt)
                .ToListAsync();
        }

        public async Task<int> AddMemoryAsync(Memory memory)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseService] AddMemoryAsync called for key: {memory.Key}");
            await EnsureDatabaseInitializedAsync();
            memory.CreatedAt = DateTime.Now;
            memory.UpdatedAt = DateTime.Now;
            
            var result = await _database!.InsertAsync(memory);
            System.Diagnostics.Debug.WriteLine($"[DatabaseService] Memory inserted with result: {result}, memory ID now: {memory.Id}");
            return result;
        }

        public async Task<int> UpdateMemoryAsync(Memory memory)
        {
            await EnsureDatabaseInitializedAsync();
            memory.UpdatedAt = DateTime.Now;
            return await _database!.UpdateAsync(memory);
        }

        public async Task<int> DeleteMemoryAsync(int memoryId)
        {
            await EnsureDatabaseInitializedAsync();
            return await _database!.DeleteAsync<Memory>(memoryId);
        }

        public async Task<int> UpsertMemoryAsync(string key, string value, string category = "", string tags = "", bool isImportant = false)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseService] UpsertMemoryAsync called - Key: {key}, Value: {value}");
            await EnsureDatabaseInitializedAsync();
            System.Diagnostics.Debug.WriteLine($"[DatabaseService] Database initialized for upsert");
            
            var existingMemory = await GetMemoryByKeyAsync(key);
            System.Diagnostics.Debug.WriteLine($"[DatabaseService] Existing memory found: {existingMemory != null}");
            
            int result;
            if (existingMemory != null)
            {
                existingMemory.Value = value;
                existingMemory.Category = category;
                existingMemory.Tags = tags;
                existingMemory.IsImportant = isImportant;
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Updating existing memory ID: {existingMemory.Id}");
                result = await UpdateMemoryAsync(existingMemory);
            }
            else
            {
                var newMemory = new Memory
                {
                    Key = key,
                    Value = value,
                    Category = category,
                    Tags = tags,
                    IsImportant = isImportant
                };
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Creating new memory");
                result = await AddMemoryAsync(newMemory);
            }
            
            System.Diagnostics.Debug.WriteLine($"[DatabaseService] UpsertMemoryAsync completed with result: {result}");
            return result;
        }

        public async Task<List<string>> GetMemoryCategoriesAsync()
        {
            await EnsureDatabaseInitializedAsync();
            var memories = await _database.Table<Memory>().ToListAsync();
            return memories.Where(m => !string.IsNullOrEmpty(m.Category))
                          .Select(m => m.Category)
                          .Distinct()
                          .OrderBy(c => c)
                          .ToList();
        }

        public async Task<List<string>> GetMemoryTagsAsync()
        {
            await EnsureDatabaseInitializedAsync();
            var memories = await _database.Table<Memory>().ToListAsync();
            var allTags = new List<string>();
            
            foreach (var memory in memories.Where(m => !string.IsNullOrEmpty(m.Tags)))
            {
                var tags = memory.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(tag => tag.Trim())
                                    .Where(tag => !string.IsNullOrEmpty(tag));
                allTags.AddRange(tags);
            }
            
            return allTags.Distinct().OrderBy(t => t).ToList();
        }
        
        // Model Settings methods
        public async Task<LLMClient.ViewModels.ModelSettings?> GetModelSettingsAsync()
        {
            try
            {
                await EnsureDatabaseInitializedAsync();
                var settings = await _database.Table<LLMClient.ViewModels.ModelSettings>().FirstOrDefaultAsync();
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Retrieved model settings: {settings?.SystemPrompt?.Substring(0, Math.Min(50, settings?.SystemPrompt?.Length ?? 0))}...");
                return settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Error getting model settings: {ex.Message}");
                return null;
            }
        }
        
        public async Task<bool> SaveModelSettingsAsync(LLMClient.ViewModels.ModelSettings settings)
        {
            try
            {
                await EnsureDatabaseInitializedAsync();
                
                // Check if settings already exist
                var existingSettings = await _database.Table<LLMClient.ViewModels.ModelSettings>().FirstOrDefaultAsync();
                
                if (existingSettings != null)
                {
                    // Update existing settings
                    existingSettings.SystemPrompt = settings.SystemPrompt;
                    existingSettings.Temperature = settings.Temperature;
                    existingSettings.MaxLength = settings.MaxLength;
                    existingSettings.RepetitionPenalty = settings.RepetitionPenalty;
                    existingSettings.TopP = settings.TopP;
                    existingSettings.UpdatedAt = DateTime.UtcNow;
                    
                    var result = await _database.UpdateAsync(existingSettings);
                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] Updated model settings, result: {result}");
                    return result > 0;
                }
                else
                {
                    // Insert new settings
                    settings.UpdatedAt = DateTime.UtcNow;
                    var result = await _database.InsertAsync(settings);
                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] Inserted new model settings, result: {result}");
                    return result > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Error saving model settings: {ex.Message}");
                return false;
            }
        }

        // ==================== RAG Document Methods ====================

        public async Task SaveRagDocumentAsync(RagDocument document)
        {
            await EnsureDatabaseInitializedAsync();
            await _database!.InsertAsync(document);
            System.Diagnostics.Debug.WriteLine($"[DatabaseService] Saved RAG document: {document.FileName}, Id={document.Id}");
        }

        public async Task<List<RagDocument>> GetRagDocumentsAsync()
        {
            await EnsureDatabaseInitializedAsync();
            return await _database!.Table<RagDocument>().ToListAsync();
        }

        public async Task DeleteRagDocumentAsync(int documentId)
        {
            await EnsureDatabaseInitializedAsync();
            await _database!.Table<RagChunk>().DeleteAsync(c => c.DocumentId == documentId);
            await _database!.DeleteAsync<RagDocument>(documentId);
            System.Diagnostics.Debug.WriteLine($"[DatabaseService] Deleted RAG document and chunks: DocumentId={documentId}");
        }

        public async Task SaveRagChunksAsync(int documentId, List<string> chunks)
        {
            await EnsureDatabaseInitializedAsync();
            var ragChunks = chunks.Select((content, index) => new RagChunk
            {
                DocumentId = documentId,
                ChunkIndex = index,
                Content = content
            }).ToList();

            await _database!.InsertAllAsync(ragChunks);
            System.Diagnostics.Debug.WriteLine($"[DatabaseService] Saved {ragChunks.Count} RAG chunks for DocumentId={documentId}");
        }

        public async Task<List<RagChunk>> GetAllRagChunksAsync()
        {
            await EnsureDatabaseInitializedAsync();
            return await _database!.Table<RagChunk>().ToListAsync();
        }

        public async Task<List<RagChunk>> GetRagChunksByDocumentAsync(int documentId)
        {
            await EnsureDatabaseInitializedAsync();
            return await _database!.Table<RagChunk>().Where(c => c.DocumentId == documentId).ToListAsync();
        }

        public async Task UpdateRagChunkEmbeddingAsync(RagChunk chunk)
        {
            await EnsureDatabaseInitializedAsync();
            await _database!.UpdateAsync(chunk);
        }
    }
}