using System.Text.Json;
using Microsoft.Extensions.Logging;
using LLMClient.Models;
using Microsoft.Maui.Storage;
#if ANDROID
using AndroidApplication = Android.App.Application;
using AndroidEnvironment = Android.OS.Environment;
#endif

namespace LLMClient.Services
{
    /// <summary>
    /// Service for downloading and managing MLC LLM models from HuggingFace.
    /// Handles model discovery, download progress, validation, and cleanup.
    /// </summary>
    public class MlcModelDownloadService : IDisposable
    {
        private readonly ILogger<MlcModelDownloadService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _modelsBasePath;
        private CancellationTokenSource? _downloadCts;
        private bool _disposed;

        private const string CustomModelPathKeyPrefix = "MlcCustomModelPath:";

        // HuggingFace API endpoints
        private const string HF_API_BASE = "https://huggingface.co/api/models";
        private const string HF_RESOLVE_BASE = "https://huggingface.co";

        // Required files for MLC models
        private static readonly string[] RequiredFiles = new[]
        {
            "mlc-chat-config.json",
            "tokenizer.json",
            "tokenizer_config.json"
        };

        // Model weight files (multiple shards possible)
        private const string PARAMS_PATTERN = "params_shard_";
        private const string NDARRAY_CACHE = "ndarray-cache.json";

        public event Action<DownloadProgressInfo>? DownloadProgress;
        public event Action<string>? DownloadCompleted;
        public event Action<string>? DownloadFailed;
        public event Action<string>? StatusChanged;

        public MlcModelDownloadService(ILogger<MlcModelDownloadService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(60)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "LLMClient/1.0");

            _modelsBasePath = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "LLMClient", "Models", "mlc");

            Directory.CreateDirectory(_modelsBasePath);
        }

        public string? GetCustomModelPath(string modelId)
        {
            var key = CustomModelPathKeyPrefix + modelId;
            var v = Preferences.Get(key, "");
            return string.IsNullOrWhiteSpace(v) ? null : v;
        }

        public void SetCustomModelPath(string modelId, string? path)
        {
            var key = CustomModelPathKeyPrefix + modelId;
            if (string.IsNullOrWhiteSpace(path))
            {
                Preferences.Remove(key);
                return;
            }

            Preferences.Set(key, path.Trim());
        }

        public void ClearCustomModelPath(string modelId)
        {
            Preferences.Remove(CustomModelPathKeyPrefix + modelId);
        }

        private string? ResolveCustomModelPath(string[] candidateModelIds, string modelId)
        {
            var configured = GetCustomModelPath(modelId);
            if (string.IsNullOrWhiteSpace(configured))
                return null;

            try
            {
                if (!Directory.Exists(configured))
                    return null;

                var directConfigFile = Path.Combine(configured, "mlc-chat-config.json");
                if (File.Exists(directConfigFile))
                    return configured;

                foreach (var candidateId in candidateModelIds)
                {
                    var sub = Path.Combine(configured, candidateId);
                    if (!Directory.Exists(sub))
                        continue;

                    var subConfigFile = Path.Combine(sub, "mlc-chat-config.json");
                    if (File.Exists(subConfigFile))
                        return sub;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[MlcDownload] Unable to resolve custom model path for {ModelId}", modelId);
            }

            return null;
        }

        /// <summary>
        /// Get path where model files are stored.
        /// Checks internal storage, app external files, and Downloads folder.
        /// </summary>
        public string GetModelPath(string modelId)
        {
            string[] candidateModelIds;
            try
            {
                var model = MlcModelCatalog.GetModelById(modelId);
                if (model != null && !string.IsNullOrWhiteSpace(model.HuggingFaceId))
                {
                    var hfName = model.HuggingFaceId.Split('/').LastOrDefault();
                    if (!string.IsNullOrWhiteSpace(hfName) && !string.Equals(hfName, modelId, StringComparison.OrdinalIgnoreCase))
                    {
                        candidateModelIds = new[] { modelId, hfName };
                    }
                    else
                    {
                        candidateModelIds = new[] { modelId };
                    }
                }
                else
                {
                    candidateModelIds = new[] { modelId };
                }
            }
            catch
            {
                candidateModelIds = new[] { modelId };
            }

            var customPath = ResolveCustomModelPath(candidateModelIds, modelId);
            if (!string.IsNullOrWhiteSpace(customPath))
            {
                _logger.LogInformation("[MlcDownload] Using custom model path: {Path}", customPath);
                return customPath;
            }

            // First check internal storage
            foreach (var candidateId in candidateModelIds)
            {
                var internalPath = Path.Combine(_modelsBasePath, candidateId);
                if (Directory.Exists(internalPath))
                    return internalPath;
            }

#if ANDROID
            // Check app's external files directory (accessible without special permissions)
            var externalFilesDir = AndroidApplication.Context.GetExternalFilesDir(null)?.AbsolutePath;
            var appExternalRoot = string.IsNullOrWhiteSpace(externalFilesDir)
                ? null
                : Path.Combine(externalFilesDir, "LLMClient", "Models", "mlc");

            if (!string.IsNullOrWhiteSpace(appExternalRoot))
            {
                foreach (var candidateId in candidateModelIds)
                {
                    var appExternalPath = Path.Combine(appExternalRoot, candidateId);
                    if (Directory.Exists(appExternalPath))
                    {
                        _logger.LogInformation("[MlcDownload] Found model in app external storage: {Path}", appExternalPath);
                        return appExternalPath;
                    }
                }
            }

            // Check Downloads folder (requires storage permission)
            var downloadsDir = AndroidEnvironment.GetExternalStoragePublicDirectory(AndroidEnvironment.DirectoryDownloads)?.AbsolutePath;
            var downloadsRoot = string.IsNullOrWhiteSpace(downloadsDir)
                ? null
                : Path.Combine(downloadsDir, "LLMClient", "Models", "mlc");

            if (!string.IsNullOrWhiteSpace(downloadsRoot))
            {
                foreach (var candidateId in candidateModelIds)
                {
                    var downloadsPath = Path.Combine(downloadsRoot, candidateId);
                    if (Directory.Exists(downloadsPath))
                    {
                        _logger.LogInformation("[MlcDownload] Found model in Downloads: {Path}", downloadsPath);
                        return downloadsPath;
                    }
                }
            }
#endif

            // Return internal path as default (for new downloads)
            return Path.Combine(_modelsBasePath, modelId);
        }

        /// <summary>
        /// Check if a model is fully downloaded.
        /// </summary>
        public Task<bool> IsModelDownloadedAsync(string modelId)
        {
            var modelPath = GetModelPath(modelId);
            if (!Directory.Exists(modelPath))
                return Task.FromResult(false);

            // Check required files
            foreach (var file in RequiredFiles)
            {
                if (!File.Exists(Path.Combine(modelPath, file)))
                    return Task.FromResult(false);
            }

            // Check for at least one params shard
            var paramsFiles = Directory.GetFiles(modelPath, "params_shard_*.bin");
            return Task.FromResult(paramsFiles.Length > 0);
        }

        /// <summary>
        /// Get download status for a model.
        /// </summary>
        public async Task<ModelDownloadStatus> GetModelStatusAsync(string modelId)
        {
            var modelPath = GetModelPath(modelId);

            if (!Directory.Exists(modelPath))
                return new ModelDownloadStatus { State = MlcDownloadState.NotDownloaded };

            var files = Directory.GetFiles(modelPath);
            if (files.Length == 0)
                return new ModelDownloadStatus { State = MlcDownloadState.NotDownloaded };

            // Check completeness
            var isComplete = await IsModelDownloadedAsync(modelId);
            if (isComplete)
            {
                var totalSize = files.Sum(f => new FileInfo(f).Length);
                return new ModelDownloadStatus
                {
                    State = MlcDownloadState.Downloaded,
                    DownloadedBytes = totalSize,
                    TotalBytes = totalSize,
                    Progress = 1.0
                };
            }

            // Partial download
            var downloadedSize = files.Sum(f => new FileInfo(f).Length);
            return new ModelDownloadStatus
            {
                State = MlcDownloadState.Partial,
                DownloadedBytes = downloadedSize
            };
        }

        /// <summary>
        /// Get list of files to download from HuggingFace.
        /// </summary>
        public async Task<List<HfFileInfo>> GetModelFilesAsync(string huggingFaceId)
        {
            try
            {
                // Extract org/model from full ID (e.g., "mlc-ai/Qwen2.5-1.5B-Instruct-q4f16_1-MLC")
                var url = $"{HF_API_BASE}/{huggingFaceId}";
                var response = await _httpClient.GetStringAsync(url);
                var modelInfo = JsonSerializer.Deserialize<HfModelResponse>(response);

                if (modelInfo?.Siblings == null)
                    return new List<HfFileInfo>();

                // Filter relevant files
                var files = modelInfo.Siblings
                    .Where(s => IsRelevantFile(s.Rfilename))
                    .Select(s => new HfFileInfo
                    {
                        Filename = s.Rfilename,
                        Size = s.Size ?? 0,
                        DownloadUrl = $"{HF_RESOLVE_BASE}/{huggingFaceId}/resolve/main/{s.Rfilename}"
                    })
                    .ToList();

                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MlcDownload] Failed to get model files for {Model}", huggingFaceId);
                return new List<HfFileInfo>();
            }
        }

        private bool IsRelevantFile(string filename)
        {
            // Include config, tokenizer, params, and cache files
            return filename.EndsWith(".json") ||
                   filename.EndsWith(".bin") ||
                   filename.EndsWith(".model") ||
                   filename == "tokenizer.model";
        }

        /// <summary>
        /// Download a model from HuggingFace.
        /// </summary>
        public async Task<bool> DownloadModelAsync(
            MlcModelInfo modelInfo,
            IProgress<DownloadProgressInfo>? progress = null,
            CancellationToken cancellationToken = default)
        {
            _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _downloadCts.Token;

            try
            {
                StatusChanged?.Invoke($"Preparing download: {modelInfo.DisplayName}");
                _logger.LogInformation("[MlcDownload] Starting download of {Model}", modelInfo.HuggingFaceId);

                var modelPath = GetModelPath(modelInfo.Id);
                Directory.CreateDirectory(modelPath);

                // Get file list
                var files = await GetModelFilesAsync(modelInfo.HuggingFaceId);
                if (files.Count == 0)
                {
                    DownloadFailed?.Invoke("Failed to get model file list");
                    return false;
                }

                var totalSize = files.Sum(f => f.Size);
                
                // Fallback: use model's known size if file sizes are unknown (HF API sometimes doesn't return sizes)
                if (totalSize == 0 && modelInfo.SizeMB > 0)
                {
                    totalSize = modelInfo.SizeMB * 1024L * 1024L;
                    _logger.LogWarning("[MlcDownload] File sizes unknown, using model size estimate: {Size} MB", modelInfo.SizeMB);
                }
                
                var downloadedSize = 0L;
                var completedFiles = 0;

                foreach (var file in files)
                {
                    token.ThrowIfCancellationRequested();

                    var filePath = Path.Combine(modelPath, file.Filename);

                    // Skip if already downloaded
                    if (File.Exists(filePath))
                    {
                        var existingSize = new FileInfo(filePath).Length;
                        if (existingSize == file.Size || file.Size == 0)
                        {
                            downloadedSize += existingSize;
                            completedFiles++;
                            _logger.LogDebug("[MlcDownload] Skipping existing file: {File}", file.Filename);
                            continue;
                        }
                    }

                    // Create directory if needed (for nested files)
                    var fileDir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(fileDir))
                        Directory.CreateDirectory(fileDir);

                    StatusChanged?.Invoke($"Downloading: {file.Filename}");
                    _logger.LogInformation("[MlcDownload] Downloading: {File} ({Size:F1} MB)",
                        file.Filename, file.Size / 1024.0 / 1024.0);

                    // Download file with progress and speed tracking
                    var fileStartTime = DateTime.UtcNow;
                    var lastSpeedCheckTime = fileStartTime;
                    var lastSpeedCheckBytes = 0L;
                    var currentSpeedMBps = 0.0;
                    
                    await DownloadFileAsync(
                        file.DownloadUrl,
                        filePath,
                        file.Size,
                        (bytesReceived, fileTotal) =>
                        {
                            var totalDownloaded = downloadedSize + bytesReceived;
                            var overallProgress = totalSize > 0 ? (double)totalDownloaded / totalSize : 0;
                            
                            // Calculate speed every 500ms
                            var now = DateTime.UtcNow;
                            var timeSinceLastCheck = (now - lastSpeedCheckTime).TotalSeconds;
                            if (timeSinceLastCheck >= 0.5)
                            {
                                var bytesDelta = bytesReceived - lastSpeedCheckBytes;
                                currentSpeedMBps = (bytesDelta / (1024.0 * 1024.0)) / timeSinceLastCheck;
                                lastSpeedCheckTime = now;
                                lastSpeedCheckBytes = bytesReceived;
                            }

                            var progressInfo = new DownloadProgressInfo
                            {
                                ModelId = modelInfo.Id,
                                CurrentFile = file.Filename,
                                CompletedFiles = completedFiles,
                                TotalFiles = files.Count,
                                DownloadedBytes = totalDownloaded,
                                TotalBytes = totalSize,
                                Progress = overallProgress,
                                SpeedMBps = currentSpeedMBps
                            };

                            progress?.Report(progressInfo);
                            DownloadProgress?.Invoke(progressInfo);
                        },
                        token);

                    downloadedSize += file.Size > 0 ? file.Size : new FileInfo(filePath).Length;
                    completedFiles++;
                }

                // Verify download
                if (await IsModelDownloadedAsync(modelInfo.Id))
                {
                    _logger.LogInformation("[MlcDownload] Download completed: {Model}", modelInfo.Id);
                    DownloadCompleted?.Invoke(modelInfo.Id);
                    return true;
                }
                else
                {
                    DownloadFailed?.Invoke("Download incomplete - some files missing");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[MlcDownload] Download cancelled: {Model}", modelInfo.Id);
                DownloadFailed?.Invoke("Download cancelled");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MlcDownload] Download failed: {Model}", modelInfo.Id);
                DownloadFailed?.Invoke($"Download failed: {ex.Message}");
                return false;
            }
            finally
            {
                _downloadCts?.Dispose();
                _downloadCts = null;
            }
        }

        private async Task DownloadFileAsync(
            string url,
            string destPath,
            long expectedSize,
            Action<long, long> onProgress,
            CancellationToken token)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? expectedSize;
            var buffer = new byte[81920]; // 80KB buffer
            var bytesReceived = 0L;

            await using var contentStream = await response.Content.ReadAsStreamAsync(token);
            await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true);

            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                bytesReceived += bytesRead;
                onProgress(bytesReceived, totalBytes);
            }
        }

        /// <summary>
        /// Cancel ongoing download.
        /// </summary>
        public void CancelDownload()
        {
            _downloadCts?.Cancel();
        }

        /// <summary>
        /// Delete a downloaded model.
        /// </summary>
        public Task<bool> DeleteModelAsync(string modelId)
        {
            try
            {
                var modelPath = GetModelPath(modelId);
                if (Directory.Exists(modelPath))
                {
                    Directory.Delete(modelPath, true);
                    _logger.LogInformation("[MlcDownload] Deleted model: {Model}", modelId);
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MlcDownload] Failed to delete model: {Model}", modelId);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Get total disk space used by downloaded models.
        /// </summary>
        public long GetTotalDownloadedSize()
        {
            if (!Directory.Exists(_modelsBasePath))
                return 0;

            return Directory.GetFiles(_modelsBasePath, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }

        /// <summary>
        /// Get list of downloaded models.
        /// </summary>
        public async Task<List<(string ModelId, long SizeBytes)>> GetDownloadedModelsAsync()
        {
            var result = new List<(string, long)>();

            if (!Directory.Exists(_modelsBasePath))
                return result;

            foreach (var dir in Directory.GetDirectories(_modelsBasePath))
            {
                var modelId = Path.GetFileName(dir);
                if (await IsModelDownloadedAsync(modelId))
                {
                    var size = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                        .Sum(f => new FileInfo(f).Length);
                    result.Add((modelId, size));
                }
            }

            return result;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _downloadCts?.Cancel();
                _downloadCts?.Dispose();
                _httpClient.Dispose();
                _disposed = true;
            }
        }
    }

    #region Models

    public class DownloadProgressInfo
    {
        public string ModelId { get; set; } = "";
        public string CurrentFile { get; set; } = "";
        public int CompletedFiles { get; set; }
        public int TotalFiles { get; set; }
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
        public double Progress { get; set; }
        public double SpeedMBps { get; set; }

        public string ProgressText => $"{Progress * 100:F1}%";
        public string SizeText => $"{DownloadedBytes / 1024.0 / 1024.0:F1} / {TotalBytes / 1024.0 / 1024.0:F1} MB";
        public string SpeedText => SpeedMBps > 0 ? $"{SpeedMBps:F1} MB/s" : "";
        public string FileText => $"{CompletedFiles}/{TotalFiles} files";
    }

    public enum MlcDownloadState
    {
        NotDownloaded,
        Partial,
        Downloading,
        Downloaded,
        Error
    }

    public class ModelDownloadStatus
    {
        public MlcDownloadState State { get; set; }
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
        public double Progress { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class HfFileInfo
    {
        public string Filename { get; set; } = "";
        public long Size { get; set; }
        public string DownloadUrl { get; set; } = "";
    }

    // HuggingFace API response models
    internal class HfModelResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("siblings")]
        public List<HfSibling>? Siblings { get; set; }
    }

    internal class HfSibling
    {
        [System.Text.Json.Serialization.JsonPropertyName("rfilename")]
        public string Rfilename { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("size")]
        public long? Size { get; set; }
    }

    #endregion
}
