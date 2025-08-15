using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;

namespace LLMClient.Services
{
    public class LocalModelDiagnostic
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public LocalModelState State { get; set; }
        public bool IsAvailable { get; set; }
        public string? LastError { get; set; }
        public int ConsecutiveFailures { get; set; }
        public TimeSpan? DownloadTime { get; set; }
        public long ModelSizeBytes { get; set; }
        public string ModelVersion { get; set; } = string.Empty;
        public Dictionary<string, object> Metrics { get; set; } = new();
    }

    public interface ILocalModelDiagnosticService
    {
        Task<LocalModelDiagnostic> GetCurrentDiagnosticAsync();
        Task<bool> PerformHealthCheckAsync();
        Task<string> GenerateDiagnosticReportAsync();
        Task ClearDiagnosticsAsync();
        event Action<LocalModelDiagnostic> DiagnosticUpdated;
    }

    public class LocalModelDiagnosticService : ILocalModelDiagnosticService
    {
        private readonly ILocalModelService _localModelService;
        private readonly ILogger<LocalModelDiagnosticService> _logger;
        private readonly string _diagnosticsPath;
        private LocalModelDiagnostic _currentDiagnostic = new();
        private readonly Timer _healthCheckTimer;

        public event Action<LocalModelDiagnostic>? DiagnosticUpdated;

        public LocalModelDiagnosticService(ILocalModelService localModelService, ILogger<LocalModelDiagnosticService> logger)
        {
            _localModelService = localModelService;
            _logger = logger;
            _diagnosticsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LLMClient", "diagnostics.json");

            // Subscribe to model service events
            _localModelService.StateChanged += OnModelStateChanged;
            _localModelService.ErrorOccurred += OnModelError;

            // Perform health check every 5 minutes
            _healthCheckTimer = new Timer(async _ => await PerformHealthCheckAsync(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

            // Initial diagnostic
            Task.Run(async () => await UpdateDiagnosticAsync());
        }

        private void OnModelStateChanged(LocalModelState state)
        {
            Task.Run(async () =>
            {
                _currentDiagnostic.State = state;
                _currentDiagnostic.Timestamp = DateTime.UtcNow;
                _currentDiagnostic.IsAvailable = state == LocalModelState.Loaded;
                
                if (state == LocalModelState.Loaded || state == LocalModelState.Downloaded)
                {
                    _currentDiagnostic.ConsecutiveFailures = 0;
                    _currentDiagnostic.LastError = null;
                }

                await SaveDiagnosticAsync();
                DiagnosticUpdated?.Invoke(_currentDiagnostic);
            });
        }

        private void OnModelError(string error)
        {
            Task.Run(async () =>
            {
                _currentDiagnostic.ConsecutiveFailures++;
                _currentDiagnostic.LastError = error;
                _currentDiagnostic.Timestamp = DateTime.UtcNow;
                _currentDiagnostic.IsAvailable = false;

                await SaveDiagnosticAsync();
                DiagnosticUpdated?.Invoke(_currentDiagnostic);
            });
        }

        public async Task<LocalModelDiagnostic> GetCurrentDiagnosticAsync()
        {
            await UpdateDiagnosticAsync();
            return _currentDiagnostic;
        }

        private async Task UpdateDiagnosticAsync()
        {
            try
            {
                _currentDiagnostic.State = _localModelService.State;
                _currentDiagnostic.IsAvailable = _localModelService.IsLoaded;
                _currentDiagnostic.Timestamp = DateTime.UtcNow;

                // Get model info
                var modelInfo = await _localModelService.GetModelInfoAsync();
                _currentDiagnostic.ModelVersion = modelInfo.Version;

                // Calculate model size if downloaded
                if (await _localModelService.IsModelDownloadedAsync())
                {
                    _currentDiagnostic.ModelSizeBytes = await CalculateModelSizeAsync();
                }

                // Update metrics
                _currentDiagnostic.Metrics["LastHealthCheck"] = DateTime.UtcNow;
                _currentDiagnostic.Metrics["IsDownloaded"] = await _localModelService.IsModelDownloadedAsync();
                _currentDiagnostic.Metrics["IsDownloading"] = _localModelService.IsDownloading;
                _currentDiagnostic.Metrics["MemoryUsageMB"] = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating diagnostic");
                _currentDiagnostic.LastError = ex.Message;
                _currentDiagnostic.ConsecutiveFailures++;
            }
        }

        private async Task<long> CalculateModelSizeAsync()
        {
            try
            {
                var modelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LLMClient", "Models", "phi-4-mini-instruct");
                
                if (!Directory.Exists(modelPath))
                    return 0;

                var files = Directory.GetFiles(modelPath, "*", SearchOption.AllDirectories);
                long totalSize = 0;

                foreach (var file in files)
                {
                    totalSize += new FileInfo(file).Length;
                }

                return totalSize;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<bool> PerformHealthCheckAsync()
        {
            try
            {
                _logger.LogDebug("Performing local model health check");

                var isHealthy = true;
                var metrics = new Dictionary<string, object>();

                // Check if model files exist
                var isDownloaded = await _localModelService.IsModelDownloadedAsync();
                metrics["IsDownloaded"] = isDownloaded;

                // Check if model is loaded properly
                var isLoaded = _localModelService.IsLoaded;
                metrics["IsLoaded"] = isLoaded;

                // Check state consistency
                var state = _localModelService.State;
                metrics["State"] = state.ToString();

                // Memory usage check
                var memoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
                metrics["MemoryUsageMB"] = memoryMB;

                if (memoryMB > 8000) // More than 8GB seems excessive
                {
                    _logger.LogWarning($"High memory usage detected: {memoryMB:F1} MB");
                    isHealthy = false;
                }

                // If model should be loaded but isn't, that's unhealthy
                if (isDownloaded && state == LocalModelState.Downloaded && !isLoaded)
                {
                    // This might be normal - model loads on demand
                }

                // Update diagnostic
                _currentDiagnostic.Metrics = metrics;
                _currentDiagnostic.Timestamp = DateTime.UtcNow;
                
                if (isHealthy)
                {
                    _currentDiagnostic.ConsecutiveFailures = Math.Max(0, _currentDiagnostic.ConsecutiveFailures - 1);
                }

                await SaveDiagnosticAsync();
                DiagnosticUpdated?.Invoke(_currentDiagnostic);

                _logger.LogDebug($"Health check completed. Healthy: {isHealthy}");
                return isHealthy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                _currentDiagnostic.LastError = $"Health check failed: {ex.Message}";
                _currentDiagnostic.ConsecutiveFailures++;
                return false;
            }
        }

        public async Task<string> GenerateDiagnosticReportAsync()
        {
            await UpdateDiagnosticAsync();

            var report = new StringBuilder();
            report.AppendLine("=== LLMClient Local Model Diagnostic Report ===");
            report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            report.AppendLine();

            // Current State
            report.AppendLine("Current State:");
            report.AppendLine($"  State: {_currentDiagnostic.State}");
            report.AppendLine($"  Available: {_currentDiagnostic.IsAvailable}");
            report.AppendLine($"  Model Version: {_currentDiagnostic.ModelVersion}");
            report.AppendLine($"  Consecutive Failures: {_currentDiagnostic.ConsecutiveFailures}");
            
            if (!string.IsNullOrEmpty(_currentDiagnostic.LastError))
            {
                report.AppendLine($"  Last Error: {_currentDiagnostic.LastError}");
            }

            report.AppendLine();

            // Model Size
            if (_currentDiagnostic.ModelSizeBytes > 0)
            {
                var sizeMB = _currentDiagnostic.ModelSizeBytes / 1024.0 / 1024.0;
                report.AppendLine($"Model Size: {sizeMB:F1} MB ({_currentDiagnostic.ModelSizeBytes:N0} bytes)");
            }

            report.AppendLine();

            // Metrics
            report.AppendLine("Metrics:");
            foreach (var metric in _currentDiagnostic.Metrics)
            {
                report.AppendLine($"  {metric.Key}: {metric.Value}");
            }

            report.AppendLine();

            // Recommendations
            report.AppendLine("Recommendations:");
            
            if (_currentDiagnostic.ConsecutiveFailures > 0)
            {
                report.AppendLine($"  - {_currentDiagnostic.ConsecutiveFailures} recent failures detected. Consider restarting the model service.");
            }

            if (_currentDiagnostic.Metrics.TryGetValue("MemoryUsageMB", out var memObj) && 
                memObj is double mem && mem > 4000)
            {
                report.AppendLine($"  - High memory usage ({mem:F1} MB). Consider unloading the model when not in use.");
            }

            if (!_currentDiagnostic.IsAvailable && _currentDiagnostic.State == LocalModelState.Downloaded)
            {
                report.AppendLine("  - Model is downloaded but not loaded. Try loading it manually.");
            }

            if (_currentDiagnostic.State == LocalModelState.Error)
            {
                report.AppendLine("  - Model is in error state. Try deleting and re-downloading the model.");
                report.AppendLine("  - Alternatively, use cloud-based models instead.");
            }

            return report.ToString();
        }

        private async Task SaveDiagnosticAsync()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_diagnosticsPath)!);
                var json = JsonSerializer.Serialize(_currentDiagnostic, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_diagnosticsPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save diagnostic");
            }
        }

        public async Task ClearDiagnosticsAsync()
        {
            try
            {
                _currentDiagnostic = new LocalModelDiagnostic
                {
                    State = _localModelService.State,
                    IsAvailable = _localModelService.IsLoaded
                };

                if (File.Exists(_diagnosticsPath))
                {
                    File.Delete(_diagnosticsPath);
                }

                DiagnosticUpdated?.Invoke(_currentDiagnostic);
                _logger.LogInformation("Diagnostics cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing diagnostics");
            }
        }

        public void Dispose()
        {
            _healthCheckTimer?.Dispose();
        }
    }
}