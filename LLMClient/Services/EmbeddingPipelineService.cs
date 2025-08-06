using Microsoft.Extensions.Logging;

namespace LLMClient.Services
{
    public interface IEmbeddingPipelineService
    {
        Task<EmbeddingPipelineResult> GenerateEmbeddingsForAllMessagesAsync(IProgress<EmbeddingPipelineProgress>? progress = null);
        Task<EmbeddingPipelineResult> UpdateEmbeddingsToLatestVersionAsync(IProgress<EmbeddingPipelineProgress>? progress = null);
        Task<EmbeddingStats> GetEmbeddingStatsAsync();
    }

    public class EmbeddingPipelineProgress
    {
        public int TotalMessages { get; set; }
        public int ProcessedMessages { get; set; }
        public int SuccessfulEmbeddings { get; set; }
        public int FailedEmbeddings { get; set; }
        public double ProgressPercentage => TotalMessages > 0 ? (double)ProcessedMessages / TotalMessages * 100 : 0;
        public string CurrentMessage { get; set; } = string.Empty;
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
    }

    public class EmbeddingPipelineResult
    {
        public bool Success { get; set; }
        public int TotalProcessed { get; set; }
        public int SuccessfulEmbeddings { get; set; }
        public int FailedEmbeddings { get; set; }
        public TimeSpan TotalTime { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new();
    }

    public class EmbeddingStats
    {
        public int TotalMessages { get; set; }
        public int MessagesWithEmbeddings { get; set; }
        public int MessagesWithoutEmbeddings { get; set; }
        public int MessagesWithOutdatedEmbeddings { get; set; }
        public string CurrentModelVersion { get; set; } = string.Empty;
        public double EmbeddingCoverage => TotalMessages > 0 ? (double)MessagesWithEmbeddings / TotalMessages * 100 : 0;
    }

    public class EmbeddingPipelineService : IEmbeddingPipelineService
    {
        private readonly DatabaseService _databaseService;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<EmbeddingPipelineService> _logger;
        private const int BatchSize = 50; // Przetwarzaj po 50 wiadomości na raz

        public EmbeddingPipelineService(
            DatabaseService databaseService,
            IEmbeddingService embeddingService,
            ILogger<EmbeddingPipelineService> logger)
        {
            _databaseService = databaseService;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        public async Task<EmbeddingPipelineResult> GenerateEmbeddingsForAllMessagesAsync(IProgress<EmbeddingPipelineProgress>? progress = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = new EmbeddingPipelineResult();

            try
            {
                _logger.LogInformation("Starting embedding generation pipeline for all messages");

                if (!_embeddingService.IsInitialized)
                {
                    await _embeddingService.InitializeAsync();
                    if (!_embeddingService.IsInitialized)
                    {
                        result.ErrorMessage = "Failed to initialize embedding service";
                        _logger.LogError(result.ErrorMessage);
                        return result;
                    }
                }

                // Pobierz wszystkie wiadomości bez embeddingów
                var messagesNeedingEmbeddings = await _databaseService.GetMessagesNeedingEmbeddingsAsync();
                var totalMessages = messagesNeedingEmbeddings.Count;

                if (totalMessages == 0)
                {
                    _logger.LogInformation("No messages need embeddings");
                    result.Success = true;
                    return result;
                }

                _logger.LogInformation($"Found {totalMessages} messages that need embeddings");

                var progressData = new EmbeddingPipelineProgress
                {
                    TotalMessages = totalMessages
                };

                // Przetwarzaj wiadomości w batch'ach
                for (int i = 0; i < messagesNeedingEmbeddings.Count; i += BatchSize)
                {
                    var batch = messagesNeedingEmbeddings.Skip(i).Take(BatchSize).ToList();
                    
                    foreach (var message in batch)
                    {
                        try
                        {
                            progressData.CurrentMessage = $"Message ID: {message.Id}";
                            progressData.ProcessedMessages++;
                            progressData.ElapsedTime = stopwatch.Elapsed;
                            
                            if (progressData.ProcessedMessages > 1)
                            {
                                var avgTimePerMessage = progressData.ElapsedTime.TotalMilliseconds / progressData.ProcessedMessages;
                                var remainingMessages = progressData.TotalMessages - progressData.ProcessedMessages;
                                progressData.EstimatedTimeRemaining = TimeSpan.FromMilliseconds(avgTimePerMessage * remainingMessages);
                            }

                            progress?.Report(progressData);

                            var success = await _databaseService.GenerateAndSaveEmbeddingAsync(message);
                            
                            if (success)
                            {
                                progressData.SuccessfulEmbeddings++;
                                result.SuccessfulEmbeddings++;
                                _logger.LogDebug($"Generated embedding for message {message.Id}");
                            }
                            else
                            {
                                progressData.FailedEmbeddings++;
                                result.FailedEmbeddings++;
                                result.Warnings.Add($"Failed to generate embedding for message {message.Id}");
                                _logger.LogWarning($"Failed to generate embedding for message {message.Id}");
                            }
                        }
                        catch (Exception ex)
                        {
                            progressData.FailedEmbeddings++;
                            result.FailedEmbeddings++;
                            result.Warnings.Add($"Error processing message {message.Id}: {ex.Message}");
                            _logger.LogError(ex, $"Error processing message {message.Id}");
                        }
                    }

                    // Krótka pauza między batch'ami żeby nie obciążać systemu
                    await Task.Delay(100);
                }

                result.TotalProcessed = totalMessages;
                result.Success = result.FailedEmbeddings < totalMessages; // Sukces jeśli przynajmniej niektóre się powiodły
                
                _logger.LogInformation($"Embedding pipeline completed: {result.SuccessfulEmbeddings}/{totalMessages} successful");
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Pipeline failed: {ex.Message}";
                _logger.LogError(ex, "Embedding pipeline failed");
            }
            finally
            {
                stopwatch.Stop();
                result.TotalTime = stopwatch.Elapsed;
            }

            return result;
        }

        public async Task<EmbeddingPipelineResult> UpdateEmbeddingsToLatestVersionAsync(IProgress<EmbeddingPipelineProgress>? progress = null)
        {
            _logger.LogInformation("Starting embedding update pipeline for outdated versions");
            return await GenerateEmbeddingsForAllMessagesAsync(progress);
        }

        public async Task<EmbeddingStats> GetEmbeddingStatsAsync()
        {
            try
            {
                var stats = new EmbeddingStats
                {
                    CurrentModelVersion = _embeddingService.ModelVersion
                };

                var embeddingStats = await _databaseService.GetEmbeddingStatsAsync();
                stats.TotalMessages = embeddingStats.total;
                stats.MessagesWithEmbeddings = embeddingStats.withEmbeddings;
                stats.MessagesWithoutEmbeddings = stats.TotalMessages - stats.MessagesWithEmbeddings;

                // Sprawdź ile wiadomości ma przestarzałe embeddingi
                var messagesNeedingUpdate = await _databaseService.GetMessagesNeedingEmbeddingsAsync();
                stats.MessagesWithOutdatedEmbeddings = messagesNeedingUpdate.Count - stats.MessagesWithoutEmbeddings;

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get embedding stats");
                return new EmbeddingStats();
            }
        }
    }
} 