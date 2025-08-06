using System.Collections.Concurrent;
using LLMClient.Models;

namespace LLMClient.Services
{
    public interface IStreamingBatchService
    {
        void StartBatching(Message message, Action onUpdate);
        void AddChunk(string chunk);
        Task FlushAsync();
        void StopBatching();
    }

    public class StreamingBatchService : IStreamingBatchService
    {
        private readonly DatabaseService _databaseService;
        private readonly object _lock = new object();
        
        private Message? _currentMessage;
        private Action? _onUpdate;
        private readonly List<string> _pendingChunks = new();
        private bool _isBatching = false;
        private DateTime _lastFlush = DateTime.Now;

        private const int BATCH_INTERVAL_MS = 100; // Flush co 100ms
        private const int MAX_CHUNKS_BEFORE_FLUSH = 10; // Lub po 10 chunkach

        public StreamingBatchService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public void StartBatching(Message message, Action onUpdate)
        {
            lock (_lock)
            {
                _currentMessage = message;
                _onUpdate = onUpdate;
                _isBatching = true;
                _pendingChunks.Clear();
                _lastFlush = DateTime.Now;
            }
        }

        public void AddChunk(string chunk)
        {
            if (!_isBatching || _currentMessage == null) return;

            lock (_lock)
            {
                _pendingChunks.Add(chunk);
                
                // Update UI immediately for responsiveness
                _currentMessage.Content += chunk;
                _onUpdate?.Invoke();

                // Check if we should flush
                var shouldFlush = _pendingChunks.Count >= MAX_CHUNKS_BEFORE_FLUSH ||
                                 (DateTime.Now - _lastFlush).TotalMilliseconds >= BATCH_INTERVAL_MS;

                if (shouldFlush)
                {
                    _ = Task.Run(async () => await FlushAsync());
                }
            }
        }

        public async Task FlushAsync()
        {
            Message? messageToUpdate = null;
            List<string> chunksToFlush;

            lock (_lock)
            {
                if (!_isBatching || _currentMessage == null || _pendingChunks.Count == 0)
                    return;

                messageToUpdate = _currentMessage;
                chunksToFlush = new List<string>(_pendingChunks);
                _pendingChunks.Clear();
                _lastFlush = DateTime.Now;
            }

            if (messageToUpdate != null)
            {
                try
                {
                    // Single database update for all pending chunks
                    await _databaseService.SaveMessageAsync(messageToUpdate);
                    System.Diagnostics.Debug.WriteLine($"Batched {chunksToFlush.Count} chunks to database");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error flushing batch: {ex.Message}");
                }
            }
        }

        public void StopBatching()
        {
            lock (_lock)
            {
                _isBatching = false;
            }

            // Final flush
            _ = Task.Run(async () => await FlushAsync());
        }
    }
} 