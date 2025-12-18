using Microsoft.Extensions.Logging;
using LLMClient.Models;
using System.Text;
using System.Text.Json;

namespace LLMClient.Services
{
    /// <summary>
    /// MLC LLM Local Model Service - High-performance GPU-accelerated inference
    /// Uses OpenCL on Android, Metal on iOS for maximum performance.
    /// Models are selected via MlcModelCatalog and downloaded via MlcModelDownloadService.
    /// </summary>
    public class MlcLlmLocalModelService : ILocalModelService, IDisposable
    {
        private readonly ILogger<MlcLlmLocalModelService> _logger;
        private readonly DatabaseService? _databaseService;
        private readonly MlcModelDownloadService? _downloadService;
        private LocalModelState _state = LocalModelState.NotDownloaded;
        private string _modelPath;
        private string? _currentModelLib;
        private LocalModelInfo _modelInfo;
        private MlcModelInfo _selectedModel;
        private bool _isModelLoaded = false;

#if ANDROID
        private Platforms.Android.MlcLlm.MlcLlmBridge? _androidBridge;
#elif IOS
        private Platforms.iOS.MlcLlm.MlcLlmBridge? _iosBridge;
#endif

        public LocalModelState State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    _state = value;
                    StateChanged?.Invoke(_state);
                    _logger.LogInformation($"[MLC LLM] State changed to: {_state}");
                }
            }
        }

        public bool IsLoaded => _isModelLoaded && State == LocalModelState.Loaded;
        public bool IsDownloading => State == LocalModelState.Downloading;

        public event Action<LocalModelState>? StateChanged;
        public event Action<double>? DownloadProgress;
        public event Action<string>? ErrorOccurred;

        public MlcLlmLocalModelService(
            ILogger<MlcLlmLocalModelService> logger,
            DatabaseService? databaseService = null,
            MlcModelDownloadService? downloadService = null)
        {
            _logger = logger;
            _databaseService = databaseService;
            _downloadService = downloadService;

            // Load selected model from preferences or use default
            var selectedModelId = Preferences.Get("MlcSelectedModelId", MlcModelCatalog.GetDefaultModel().Id);
            _selectedModel = MlcModelCatalog.GetModelById(selectedModelId) ?? MlcModelCatalog.GetDefaultModel();

            _modelPath = _downloadService?.GetModelPath(_selectedModel.Id)
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LLMClient", "Models", "mlc", _selectedModel.Id);
            _currentModelLib = TryLoadModelLibFromConfig(_modelPath);

            _modelInfo = new LocalModelInfo
            {
                ModelId = _selectedModel.Id,
                DisplayName = _selectedModel.DisplayName,
                SizeInMB = _selectedModel.SizeMB,
                HuggingFaceRepo = _selectedModel.HuggingFaceId,
                SupportedLanguages = _selectedModel.Languages
            };

            // Subscribe to download service events if available
            if (_downloadService != null)
            {
                _downloadService.DownloadProgress += OnDownloadProgress;
                _downloadService.DownloadCompleted += OnDownloadCompleted;
                _downloadService.DownloadFailed += OnDownloadFailed;
            }

            // Initialize platform bridge
            InitializePlatformBridge();

            // Check if model exists
            Task.Run(CheckModelStatusAsync);
        }

        private void OnDownloadProgress(DownloadProgressInfo progress)
        {
            if (progress.ModelId == _selectedModel.Id)
            {
                DownloadProgress?.Invoke(progress.Progress * 100);
            }
        }

        private void OnDownloadCompleted(string modelId)
        {
            if (modelId == _selectedModel.Id)
            {
                State = LocalModelState.Downloaded;
            }
        }

        private void OnDownloadFailed(string error)
        {
            State = LocalModelState.Error;
            ErrorOccurred?.Invoke(error);
        }

        /// <summary>
        /// Change the selected model (requires reload).
        /// </summary>
        public async Task SwitchModelAsync(string modelId)
        {
            var newModel = MlcModelCatalog.GetModelById(modelId);
            if (newModel == null)
            {
                _logger.LogWarning("[MLC LLM] Model not found: {ModelId}", modelId);
                return;
            }

            // Unload current model if loaded
            if (_isModelLoaded)
            {
                await UnloadModelAsync();
            }

            // Update selection
            _selectedModel = newModel;
            Preferences.Set("MlcSelectedModelId", modelId);

            _modelPath = _downloadService?.GetModelPath(_selectedModel.Id)
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LLMClient", "Models", "mlc", _selectedModel.Id);
            _currentModelLib = TryLoadModelLibFromConfig(_modelPath);

            _modelInfo = new LocalModelInfo
            {
                ModelId = _selectedModel.Id,
                DisplayName = _selectedModel.DisplayName,
                SizeInMB = _selectedModel.SizeMB,
                HuggingFaceRepo = _selectedModel.HuggingFaceId,
                SupportedLanguages = _selectedModel.Languages
            };

            // Check status
            await CheckModelStatusAsync();

            _logger.LogInformation("[MLC LLM] Switched to model: {Model}", _selectedModel.DisplayName);
        }

        /// <summary>
        /// Get the currently selected model info.
        /// </summary>
        public MlcModelInfo GetSelectedModelInfo() => _selectedModel;

        private void InitializePlatformBridge()
        {
#if ANDROID
            _androidBridge = new Platforms.Android.MlcLlm.MlcLlmBridge(_logger);
            _androidBridge.OnToken += token => { /* Stream handling */ };
            _androidBridge.OnError += error => ErrorOccurred?.Invoke(error);
#elif IOS
            _iosBridge = new Platforms.iOS.MlcLlm.MlcLlmBridge(_logger);
            _iosBridge.OnToken += token => { /* Stream handling */ };
            _iosBridge.OnError += error => ErrorOccurred?.Invoke(error);
            _logger.LogInformation("[MLC LLM] iOS bridge initialized (Metal GPU)");
#else
            _logger.LogWarning("[MLC LLM] Platform not supported for MLC LLM");
#endif
        }

        private async Task CheckModelStatusAsync()
        {
            try
            {
                if (_downloadService != null)
                {
                    _modelPath = _downloadService.GetModelPath(_selectedModel.Id);
                    _currentModelLib = TryLoadModelLibFromConfig(_modelPath);
                }

                if (await IsModelDownloadedAsync())
                {
                    State = LocalModelState.Downloaded;
                    _logger.LogInformation("[MLC LLM] Model files found, ready to load");
                }
                else
                {
                    State = LocalModelState.NotDownloaded;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MLC LLM] Error checking model status");
                State = LocalModelState.Error;
            }
        }

        public async Task<LocalModelInfo> GetModelInfoAsync()
        {
            return await Task.FromResult(_modelInfo);
        }

        public async Task<bool> IsModelDownloadedAsync()
        {
            // Use download service if available
            if (_downloadService != null)
            {
                return await _downloadService.IsModelDownloadedAsync(_selectedModel.Id);
            }

            // Fallback: Check for essential MLC model files
            var requiredFiles = new[] { "mlc-chat-config.json", "tokenizer.json" };
            foreach (var file in requiredFiles)
            {
                var path = Path.Combine(_modelPath, file);
                if (!File.Exists(path))
                    return false;
            }

            // Check for at least one params shard
            var paramsFiles = Directory.Exists(_modelPath)
                ? Directory.GetFiles(_modelPath, "params_shard_*.bin")
                : Array.Empty<string>();
            return paramsFiles.Length > 0;
        }

        public async Task<bool> DownloadModelAsync(IProgress<double>? progress = null)
        {
            try
            {
                State = LocalModelState.Downloading;
                _logger.LogInformation($"[MLC LLM] Starting download of {_selectedModel.DisplayName}");

                // Use download service if available (preferred)
                if (_downloadService != null)
                {
                    var downloadProgress = new Progress<DownloadProgressInfo>(p =>
                    {
                        var percent = p.Progress * 100;
                        progress?.Report(percent);
                        DownloadProgress?.Invoke(percent);
                    });

                    var success = await _downloadService.DownloadModelAsync(_selectedModel, downloadProgress);

                    if (success)
                    {
                        State = LocalModelState.Downloaded;
                        progress?.Report(100);
                        DownloadProgress?.Invoke(100);
                        _logger.LogInformation("[MLC LLM] Model download completed via download service");
                        return true;
                    }
                    else
                    {
                        State = LocalModelState.Error;
                        return false;
                    }
                }

                // Fallback: Direct download (basic implementation)
                Directory.CreateDirectory(_modelPath);

                var files = new[]
                {
                    "mlc-chat-config.json",
                    "tokenizer.json",
                    "tokenizer_config.json",
                    "ndarray-cache.json"
                };

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(60);

                var baseUrl = $"https://huggingface.co/{_selectedModel.HuggingFaceId}/resolve/main/";
                var totalFiles = files.Length;
                var completedFiles = 0;

                foreach (var file in files)
                {
                    var url = baseUrl + file;
                    var filePath = Path.Combine(_modelPath, file);

                    if (File.Exists(filePath))
                    {
                        completedFiles++;
                        continue;
                    }

                    try
                    {
                        _logger.LogInformation($"[MLC LLM] Downloading {file}...");

                        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();

                        await using var contentStream = await response.Content.ReadAsStreamAsync();
                        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);

                        var buffer = new byte[65536];
                        int bytesRead;
                        var totalBytesRead = 0L;
                        var contentLength = response.Content.Headers.ContentLength ?? 0;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;

                            if (contentLength > 0)
                            {
                                var fileProgress = (double)totalBytesRead / contentLength;
                                var overallProgress = (completedFiles + fileProgress) / totalFiles * 100;
                                progress?.Report(overallProgress);
                                DownloadProgress?.Invoke(overallProgress);
                            }
                        }

                        completedFiles++;
                        _logger.LogInformation($"[MLC LLM] Downloaded {file} ({completedFiles}/{totalFiles})");
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogWarning($"[MLC LLM] Failed to download {file}: {ex.Message}");
                    }
                }

                if (await IsModelDownloadedAsync())
                {
                    State = LocalModelState.Downloaded;
                    progress?.Report(100);
                    DownloadProgress?.Invoke(100);
                    _logger.LogInformation("[MLC LLM] Model download completed");
                    return true;
                }
                else
                {
                    State = LocalModelState.Error;
                    ErrorOccurred?.Invoke("Some model files failed to download");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MLC LLM] Download failed");
                State = LocalModelState.Error;
                ErrorOccurred?.Invoke($"Download failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> LoadModelAsync()
        {
            if (State == LocalModelState.Loaded)
                return true;

            if (_downloadService != null)
            {
                _modelPath = _downloadService.GetModelPath(_selectedModel.Id);
                _currentModelLib = TryLoadModelLibFromConfig(_modelPath);
            }

            if (!await IsModelDownloadedAsync())
            {
                _logger.LogWarning("[MLC LLM] Cannot load: model not downloaded");
                return false;
            }

            try
            {
                State = LocalModelState.Loading;
                _logger.LogInformation($"[MLC LLM] Loading model from {_modelPath}");

#if ANDROID
                if (_androidBridge != null)
                {
                    var modelLib = _currentModelLib ?? TryLoadModelLibFromConfig(_modelPath);
                    _currentModelLib = modelLib;

                    var success = await _androidBridge.InitializeAsync(_modelPath, modelLib);
                    if (success)
                    {
                        _isModelLoaded = true;
                        State = LocalModelState.Loaded;
                        _logger.LogInformation("[MLC LLM] Model loaded successfully (Android GPU via OpenCL)");
                        return true;
                    }
                }
#elif IOS
                if (_iosBridge != null)
                {
                    var success = await _iosBridge.InitializeAsync(_modelPath);
                    if (success)
                    {
                        _isModelLoaded = true;
                        State = LocalModelState.Loaded;
                        _logger.LogInformation("[MLC LLM] Model loaded successfully (iOS GPU via Metal)");
                        return true;
                    }
                }
#endif

                State = LocalModelState.Error;
                ErrorOccurred?.Invoke("Failed to load model on this platform");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MLC LLM] Failed to load model");
                State = LocalModelState.Error;
                ErrorOccurred?.Invoke($"Load failed: {ex.Message}");
                return false;
            }
        }

        public async Task UnloadModelAsync()
        {
            try
            {
#if ANDROID
                _androidBridge?.Unload();
#elif IOS
                _iosBridge?.Unload();
#endif
                _isModelLoaded = false;
                State = LocalModelState.Downloaded;
                _logger.LogInformation("[MLC LLM] Model unloaded");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MLC LLM] Error unloading model");
            }

            await Task.CompletedTask;
        }

        public async Task<bool> DeleteModelAsync()
        {
            try
            {
                await UnloadModelAsync();

                if (Directory.Exists(_modelPath))
                {
                    Directory.Delete(_modelPath, true);
                }

                State = LocalModelState.NotDownloaded;
                _logger.LogInformation("[MLC LLM] Model deleted");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MLC LLM] Error deleting model");
                ErrorOccurred?.Invoke($"Delete failed: {ex.Message}");
                return false;
            }
        }

        public async Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (!IsLoaded)
                throw new InvalidOperationException("Model not loaded");

            try
            {
                var settings = await LoadModelSettingsAsync();

#if ANDROID
                if (_androidBridge != null)
                {
                    return await _androidBridge.GenerateAsync(prompt,
                        (int)settings.MaxLength,
                        settings.Temperature);
                }
#elif IOS
                if (_iosBridge != null)
                {
                    return await _iosBridge.GenerateAsync(prompt,
                        (int)settings.MaxLength,
                        settings.Temperature);
                }
#endif

                return "Platform not supported";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MLC LLM] Generation error");
                throw;
            }
        }

        public async Task<string> GenerateResponseAsync(List<Message> conversationHistory, string newMessage, CancellationToken cancellationToken = default)
        {
            var settings = await LoadModelSettingsAsync();
            var systemPrompt = string.IsNullOrWhiteSpace(settings.SystemPrompt)
                ? "You are a helpful AI assistant. Respond in the same language the user writes in."
                : settings.SystemPrompt;

            // Build chat format prompt
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine($"<|im_start|>system\n{systemPrompt}<|im_end|>");

            foreach (var msg in conversationHistory.TakeLast(5))
            {
                var role = msg.IsUser ? "user" : "assistant";
                promptBuilder.AppendLine($"<|im_start|>{role}\n{msg.Content}<|im_end|>");
            }

            promptBuilder.AppendLine($"<|im_start|>user\n{newMessage}<|im_end|>");
            promptBuilder.AppendLine("<|im_start|>assistant");

            return await GenerateResponseAsync(promptBuilder.ToString(), cancellationToken);
        }

        public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(
            string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsLoaded)
                yield break;

            var settings = await LoadModelSettingsAsync();
            var tokenQueue = new System.Collections.Concurrent.ConcurrentQueue<string>();
            var isComplete = false;
            var hasError = false;

            // Start streaming generation in background
            var streamingTask = Task.Run(async () =>
            {
#if ANDROID
                if (_androidBridge != null)
                {
                    _androidBridge.OnComplete += _ => isComplete = true;
                    _androidBridge.OnError += _ => { isComplete = true; hasError = true; };

                    await _androidBridge.GenerateStreamingAsync(prompt,
                        (int)settings.MaxLength,
                        settings.Temperature,
                        token => tokenQueue.Enqueue(token),
                        cancellationToken);

                    isComplete = true;
                }
#elif IOS
                if (_iosBridge != null)
                {
                    _iosBridge.OnComplete += _ => isComplete = true;
                    _iosBridge.OnError += _ => { isComplete = true; hasError = true; };

                    await _iosBridge.GenerateStreamingAsync(prompt,
                        (int)settings.MaxLength,
                        settings.Temperature,
                        token => tokenQueue.Enqueue(token),
                        cancellationToken);

                    isComplete = true;
                }
#else
                isComplete = true;
#endif
            }, cancellationToken);

            // Yield tokens as they arrive
            while (!isComplete && !cancellationToken.IsCancellationRequested)
            {
                if (tokenQueue.TryDequeue(out var token))
                {
                    yield return token;
                }
                else
                {
                    await Task.Delay(10, cancellationToken);
                }
            }

            // Yield any remaining tokens
            while (tokenQueue.TryDequeue(out var token))
            {
                yield return token;
            }

            // Ensure streaming task completes
            try
            {
                await streamingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
        }

        public async Task<string> GenerateOnboardingResponseAsync(string userLanguage, string topic = "general", CancellationToken cancellationToken = default)
        {
            var languageName = GetLanguageName(userLanguage);
            var prompt = $"You are a helpful AI assistant. Introduce yourself briefly in {languageName} and explain that you're running locally on this device using MLC LLM with GPU acceleration.";
            return await GenerateResponseAsync(prompt, cancellationToken);
        }

        public async Task<string> GenerateHelpResponseAsync(string question, string userLanguage, CancellationToken cancellationToken = default)
        {
            var languageName = GetLanguageName(userLanguage);
            var prompt = $"Answer this question in {languageName}: {question}";
            return await GenerateResponseAsync(prompt, cancellationToken);
        }

        private async Task<ViewModels.ModelSettings> LoadModelSettingsAsync()
        {
            try
            {
                if (_databaseService != null)
                {
                    var settings = await _databaseService.GetModelSettingsAsync();
                    if (settings != null) return settings;
                }
            }
            catch { }

            return new ViewModels.ModelSettings
            {
                Temperature = 0.7,
                MaxLength = 512,
                TopP = 0.95,
                RepetitionPenalty = 1.1
            };
        }

        private string GetLanguageName(string code) => code?.ToLower() switch
        {
            "pl" or "pl-pl" => "Polish",
            "de" or "de-de" => "German",
            "es" or "es-es" => "Spanish",
            "fr" or "fr-fr" => "French",
            "ja" or "ja-jp" => "Japanese",
            "ko" or "ko-kr" => "Korean",
            "zh" or "zh-cn" => "Chinese",
            _ => "English"
        };

        /// <summary>
        /// Read model_lib from mlc-chat-config.json if present.
        /// </summary>
        private string? TryLoadModelLibFromConfig(string modelPath)
        {
            try
            {
                var configPath = Path.Combine(modelPath, "mlc-chat-config.json");
                if (!File.Exists(configPath))
                    return null;

                using var stream = File.OpenRead(configPath);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("model_lib", out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    var lib = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(lib))
                    {
                        _logger.LogInformation($"[MLC LLM] model_lib detected: {lib}");
                        return lib;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[MLC LLM] Unable to read model_lib from config: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get list of available MLC models from catalog.
        /// </summary>
        public static MlcModelInfo[] GetAvailableModels() => MlcModelCatalog.AllModels;

        /// <summary>
        /// Check if GPU is available on this device
        /// </summary>
        public bool IsGpuAvailable()
        {
#if ANDROID
            return true; // OpenCL available on most Android devices
#elif IOS
            return true; // Metal available on all iOS devices
#else
            return false;
#endif
        }

        public void Dispose()
        {
            // Unsubscribe from download service
            if (_downloadService != null)
            {
                _downloadService.DownloadProgress -= OnDownloadProgress;
                _downloadService.DownloadCompleted -= OnDownloadCompleted;
                _downloadService.DownloadFailed -= OnDownloadFailed;
            }

#if ANDROID
            _androidBridge?.Dispose();
#elif IOS
            _iosBridge?.Dispose();
#endif
        }
    }
}
