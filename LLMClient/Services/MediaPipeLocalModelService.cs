using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LLMClient.Models;

// MediaPipe GenAI bindings - namespace needs verification on device
// Package: Xamarin.Google.MediaPipe.Tasks.GenAI (Android)
// Java package: com.google.mediapipe.tasks.genai.llminference
// TODO: Verify correct C# namespace after build on Android device
#if ANDROID
using Android.Content;
// Uncomment after namespace verification:
// using Com.Google.Mediapipe.Tasks.Genai.Llminference;
#endif

namespace LLMClient.Services
{
    /// <summary>
    /// MediaPipe GenAI model info for Gemma models
    /// </summary>
    public class MediaPipeModelInfo
    {
        public required string Id { get; init; }
        public required string DisplayName { get; init; }
        public required string HuggingFaceRepo { get; init; }
        public required string FileName { get; init; }
        public required long SizeInMB { get; init; }
        public long RequiredRamMB { get; init; } = 0; // 0 = auto (SizeInMB * 1.5)
        public required string[] SupportedLanguages { get; init; }
        public string Description { get; init; } = "";
        public bool IsRecommended { get; init; } = false;
        public bool SupportsMultimodal { get; init; } = false;
        
        public long EstimatedRamMB => RequiredRamMB > 0 ? RequiredRamMB : (long)(SizeInMB * 1.5);
    }

    /// <summary>
    /// Google AI Edge MediaPipe LLM Inference Service
    /// Supports Gemma-3n (multimodal), Gemma-2 2B, Gemma 2B on Android/iOS
    /// </summary>
    public class MediaPipeLocalModelService : ILocalModelService, IDisposable
    {
        private readonly ILogger<MediaPipeLocalModelService> _logger;
        private LocalModelState _state = LocalModelState.NotDownloaded;
        private readonly string _modelDir;
        private MediaPipeModelInfo _selectedModel;
        private string _modelPath = "";
        private bool _isDisposed = false;

        // TODO: Enable after namespace verification
        // #if ANDROID
        //     private LlmInference? _llmInference;
        // #endif

        /// <summary>
        /// Available MediaPipe models optimized for mobile
        /// </summary>
        public static readonly List<MediaPipeModelInfo> AvailableModels = new()
        {
            new MediaPipeModelInfo
            {
                Id = "gemma-3n-e2b",
                DisplayName = "Gemma 3n E2B (Multimodal)",
                HuggingFaceRepo = "google/gemma-3n-E2B-it-litert-lm",
                FileName = "gemma-3n-e2b.task",
                SizeInMB = 1500,
                RequiredRamMB = 2500,
                SupportedLanguages = new[] { "en", "pl", "de", "es", "fr", "it", "ja", "ko", "zh" },
                Description = "Gemma 3n 2B z obsługą tekstu, obrazów i audio. Wymaga ~2.5 GB RAM.",
                IsRecommended = true,
                SupportsMultimodal = true
            },
            new MediaPipeModelInfo
            {
                Id = "gemma-3n-e4b",
                DisplayName = "Gemma 3n E4B (Multimodal)",
                HuggingFaceRepo = "google/gemma-3n-E4B-it-litert-lm",
                FileName = "gemma-3n-e4b.task",
                SizeInMB = 2800,
                RequiredRamMB = 4500,
                SupportedLanguages = new[] { "en", "pl", "de", "es", "fr", "it", "ja", "ko", "zh" },
                Description = "Gemma 3n 4B - większy model multimodalny. Wymaga ~4.5 GB RAM.",
                SupportsMultimodal = true
            },
            new MediaPipeModelInfo
            {
                Id = "gemma-2-2b",
                DisplayName = "Gemma 2 2B",
                HuggingFaceRepo = "litert-community/Gemma2-2B-it",
                FileName = "gemma2-2b.task",
                SizeInMB = 1400,
                RequiredRamMB = 2500,
                SupportedLanguages = new[] { "en", "pl", "de", "es", "fr", "it", "ja", "ko", "zh" },
                Description = "Gemma 2 2B - szybki model tekstowy. Wymaga ~2.5 GB RAM."
            },
            new MediaPipeModelInfo
            {
                Id = "gemma-3-1b",
                DisplayName = "Gemma 3 1B",
                HuggingFaceRepo = "litert-community/Gemma3-1B-it",
                FileName = "gemma3-1b.task",
                SizeInMB = 800,
                RequiredRamMB = 1500,
                SupportedLanguages = new[] { "en", "pl", "de", "es", "fr", "it", "ja", "ko", "zh" },
                Description = "Gemma 3 1B - najlżejszy model, ultra szybki. Wymaga ~1.5 GB RAM."
            }
        };

        public MediaPipeLocalModelService(ILogger<MediaPipeLocalModelService> logger)
        {
            _logger = logger;
            
#if ANDROID
            _modelDir = Path.Combine(Android.App.Application.Context.FilesDir?.AbsolutePath ?? "/data/local/tmp", "mediapipe_models");
#else
            _modelDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mediapipe_models");
#endif
            
            if (!Directory.Exists(_modelDir))
            {
                Directory.CreateDirectory(_modelDir);
            }

            // Load selected model from preferences
            var selectedModelId = Preferences.Get("MediaPipe_SelectedModelId", AvailableModels[0].Id);
            _selectedModel = AvailableModels.Find(m => m.Id == selectedModelId) ?? AvailableModels[0];
            _modelPath = Path.Combine(_modelDir, _selectedModel.FileName);

            // Check if model exists
            if (File.Exists(_modelPath))
            {
                _state = LocalModelState.Downloaded;
                _logger.LogInformation($"[MediaPipe] Model found: {_selectedModel.DisplayName}");
            }
            else
            {
                _state = LocalModelState.NotDownloaded;
                _logger.LogInformation($"[MediaPipe] Model not downloaded: {_selectedModel.DisplayName}");
            }
        }

        public LocalModelState State => _state;
        public bool IsLoaded => _state == LocalModelState.Loaded;
        public bool IsDownloading => _state == LocalModelState.Downloading;

        public event Action<LocalModelState>? StateChanged;
        public event Action<double>? DownloadProgress;
        public event Action<string>? ErrorOccurred;

        /// <summary>
        /// Select a different model
        /// </summary>
        public void SelectModel(string modelId)
        {
            var model = AvailableModels.Find(m => m.Id == modelId);
            if (model != null)
            {
                _selectedModel = model;
                _modelPath = Path.Combine(_modelDir, _selectedModel.FileName);
                Preferences.Set("MediaPipe_SelectedModelId", modelId);

                if (File.Exists(_modelPath))
                {
                    _state = LocalModelState.Downloaded;
                }
                else
                {
                    _state = LocalModelState.NotDownloaded;
                }
                StateChanged?.Invoke(_state);
                _logger.LogInformation($"[MediaPipe] Selected model: {_selectedModel.DisplayName}");
            }
        }

        public Task<LocalModelInfo> GetModelInfoAsync()
        {
            return Task.FromResult(new LocalModelInfo
            {
                ModelId = _selectedModel.Id,
                DisplayName = _selectedModel.DisplayName,
                SizeInMB = _selectedModel.SizeInMB,
                RequiredRamMB = _selectedModel.RequiredRamMB > 0 ? _selectedModel.RequiredRamMB : (long)(_selectedModel.SizeInMB * 1.5),
                HuggingFaceRepo = _selectedModel.HuggingFaceRepo,
                SupportedLanguages = _selectedModel.SupportedLanguages
            });
        }

        public Task<(bool CanRun, string? WarningMessage)> CheckDeviceCompatibilityAsync()
        {
            // MediaPipe handles device compatibility internally
            var totalRam = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024 * 1024.0);
            
            if (totalRam < 3)
            {
                return Task.FromResult((false, "Urządzenie ma mniej niż 3 GB RAM. MediaPipe może nie działać."));
            }
            
            if (totalRam < 4)
            {
                return Task.FromResult((true, "Urządzenie ma mniej niż 4 GB RAM. Zalecany mniejszy model (Gemma 3 1B)."));
            }
            
            return Task.FromResult<(bool, string?)>((true, null));
        }

        public async Task<bool> DownloadModelAsync(IProgress<double>? progress = null)
        {
            if (_state == LocalModelState.Downloading)
            {
                _logger.LogWarning("[MediaPipe] Download already in progress");
                return false;
            }

            try
            {
                _state = LocalModelState.Downloading;
                StateChanged?.Invoke(_state);

                _logger.LogInformation($"[MediaPipe] Starting download: {_selectedModel.DisplayName} from {_selectedModel.HuggingFaceRepo}");

                // Construct HuggingFace download URL
                var downloadUrl = $"https://huggingface.co/{_selectedModel.HuggingFaceRepo}/resolve/main/{_selectedModel.FileName}";
                
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromHours(2);

                using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? _selectedModel.SizeInMB * 1024 * 1024;
                var downloadedBytes = 0L;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(_modelPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[81920];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;

                    var progressPercent = (double)downloadedBytes / totalBytes * 100;
                    progress?.Report(progressPercent);
                    DownloadProgress?.Invoke(progressPercent);
                }

                _state = LocalModelState.Downloaded;
                StateChanged?.Invoke(_state);
                _logger.LogInformation($"[MediaPipe] Download complete: {_selectedModel.DisplayName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaPipe] Download failed");
                _state = LocalModelState.Error;
                StateChanged?.Invoke(_state);
                ErrorOccurred?.Invoke($"Błąd pobierania: {ex.Message}");
                return false;
            }
        }

        public void CancelDownload()
        {
            if (_state == LocalModelState.Downloading)
            {
                _logger.LogInformation("[MediaPipe] Cancelling download...");
                _state = LocalModelState.NotDownloaded;
                StateChanged?.Invoke(_state);
            }
        }

        public async Task<bool> LoadModelAsync()
        {
            if (_state == LocalModelState.Loaded)
            {
                _logger.LogInformation("[MediaPipe] Model already loaded");
                return true;
            }

            if (!File.Exists(_modelPath))
            {
                _logger.LogError("[MediaPipe] Model file not found");
                ErrorOccurred?.Invoke("Plik modelu nie istnieje. Pobierz model najpierw.");
                return false;
            }

            try
            {
                _state = LocalModelState.Loading;
                StateChanged?.Invoke(_state);

                // TODO: Enable native MediaPipe after namespace verification
                // Placeholder - mark as loaded for testing UI flow
                await Task.Delay(100);
                _state = LocalModelState.Loaded;
                StateChanged?.Invoke(_state);
                _logger.LogInformation($"[MediaPipe] Model loaded (placeholder): {_selectedModel.DisplayName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaPipe] Failed to load model");
                _state = LocalModelState.Error;
                StateChanged?.Invoke(_state);
                ErrorOccurred?.Invoke($"Błąd ładowania modelu: {ex.Message}");
                return false;
            }
        }

        public async Task UnloadModelAsync()
        {
            try
            {
                _state = LocalModelState.Downloaded;
                StateChanged?.Invoke(_state);
                _logger.LogInformation("[MediaPipe] Model unloaded");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaPipe] Error unloading model");
            }
        }

        public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(
            string prompt,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_state != LocalModelState.Loaded)
            {
                yield return "Model nie jest załadowany. Załaduj model przed generowaniem odpowiedzi.";
                yield break;
            }

            // TODO: Enable native MediaPipe inference after namespace verification
            // Placeholder response
            yield return "[MediaPipe] Inferencja natywna wymaga weryfikacji namespace. ";
            yield return "Sprawdź dokumentację w MediaPipeLocalModelService.cs. ";
            yield return "Użyj LLamaSharp lub ONNX GenAI do czasu weryfikacji.";
            await Task.CompletedTask;
        }

        // ILocalModelService interface implementations
        public Task<bool> IsModelDownloadedAsync()
        {
            return Task.FromResult(File.Exists(_modelPath));
        }

        public Task<bool> DeleteModelAsync()
        {
            try
            {
                if (File.Exists(_modelPath))
                {
                    File.Delete(_modelPath);
                    _state = LocalModelState.NotDownloaded;
                    StateChanged?.Invoke(_state);
                    _logger.LogInformation("[MediaPipe] Model deleted");
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaPipe] Error deleting model");
                return Task.FromResult(false);
            }
        }

        public async Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var result = new System.Text.StringBuilder();
            await foreach (var chunk in GenerateStreamingResponseAsync(prompt, cancellationToken))
            {
                result.Append(chunk);
            }
            return result.ToString();
        }

        public async Task<string> GenerateResponseAsync(List<Message> conversationHistory, string newMessage, CancellationToken cancellationToken = default)
        {
            // Build prompt from conversation history
            var promptBuilder = new System.Text.StringBuilder();
            
            foreach (var msg in conversationHistory.TakeLast(10))
            {
                var role = msg.IsUser ? "User" : "Assistant";
                promptBuilder.AppendLine($"{role}: {msg.Content}");
            }
            promptBuilder.AppendLine($"User: {newMessage}");
            promptBuilder.AppendLine("Assistant:");

            return await GenerateResponseAsync(promptBuilder.ToString(), cancellationToken);
        }

        public async Task<string> GenerateOnboardingResponseAsync(string userLanguage, string topic = "general", CancellationToken cancellationToken = default)
        {
            var prompt = userLanguage == "pl" 
                ? $"Witaj! Jestem asystentem AI. Jak mogę Ci dzisiaj pomóc? Temat: {topic}"
                : $"Hello! I'm an AI assistant. How can I help you today? Topic: {topic}";
            
            return await GenerateResponseAsync(prompt, cancellationToken);
        }

        public async Task<string> GenerateHelpResponseAsync(string question, string userLanguage, CancellationToken cancellationToken = default)
        {
            var prompt = userLanguage == "pl"
                ? $"Odpowiedz na pytanie pomocowe: {question}"
                : $"Answer this help question: {question}";
            
            return await GenerateResponseAsync(prompt, cancellationToken);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _logger.LogInformation("[MediaPipe] Service disposed");
        }
    }
}
