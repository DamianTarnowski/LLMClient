using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntimeGenAI;
using LLMClient.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace LLMClient.Services
{
    public enum LocalModelState
    {
        NotDownloaded,
        Downloading,
        Downloaded,
        Loading,
        Loaded,
        Error
    }

    public class LocalModelInfo
    {
        public string ModelId { get; set; } = "phi-4-mini-instruct";
        public string DisplayName { get; set; } = "Phi-4 Mini Instruct (Local)";
        public string Version { get; set; } = "instruct-v1.0";
        public long SizeInMB { get; set; } = 5025; // ~4.86GB data + 52MB model = ~4.91GB total
        public string HuggingFaceRepo { get; set; } = "microsoft/Phi-4-mini-instruct-onnx";
        public string[] SupportedLanguages { get; set; } = { "en", "pl", "de", "es", "fr", "it", "ja", "ko", "zh", "ru", "tr", "nl", "pt" };
        public bool IsOnboardingCapable { get; set; } = true;
        public bool SupportsRealtimeChat { get; set; } = true;
    }

    public interface ILocalModelService
    {
        // Model Management
        Task<bool> IsModelDownloadedAsync();
        Task<LocalModelInfo> GetModelInfoAsync();
        Task<bool> DownloadModelAsync(IProgress<double>? progress = null);
        Task<bool> LoadModelAsync();
        Task UnloadModelAsync();
        Task<bool> DeleteModelAsync();

        // Inference
        Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default);
        Task<string> GenerateResponseAsync(List<Message> conversationHistory, string newMessage, CancellationToken cancellationToken = default);
        IAsyncEnumerable<string> GenerateStreamingResponseAsync(string prompt, CancellationToken cancellationToken = default);

        // Onboarding & Help
        Task<string> GenerateOnboardingResponseAsync(string userLanguage, string topic = "general", CancellationToken cancellationToken = default);
        Task<string> GenerateHelpResponseAsync(string question, string userLanguage, CancellationToken cancellationToken = default);

        // Properties and Events
        LocalModelState State { get; }
        bool IsLoaded { get; }
        bool IsDownloading { get; }
        event Action<LocalModelState> StateChanged;
        event Action<double> DownloadProgress;
        event Action<string> ErrorOccurred;
    }

    public class LocalModelService : ILocalModelService, IDisposable
    {
        private readonly ILogger<LocalModelService> _logger;
        private Model? _model;
        private Tokenizer? _tokenizer;
        private GeneratorParams? _generatorParams;
        private LocalModelState _state = LocalModelState.NotDownloaded;
        private readonly string _modelPath;
        private readonly LocalModelInfo _modelInfo;
        private CancellationTokenSource? _downloadCancellation;

        public LocalModelState State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    _state = value;
                    StateChanged?.Invoke(_state);
                    _logger.LogInformation($"Local model state changed to: {_state}");
                }
            }
        }

        public bool IsLoaded => State == LocalModelState.Loaded;
        public bool IsDownloading => State == LocalModelState.Downloading;

        public event Action<LocalModelState>? StateChanged;
        public event Action<double>? DownloadProgress;
        public event Action<string>? ErrorOccurred;

        public LocalModelService(ILogger<LocalModelService> logger)
        {
            _logger = logger;
            _modelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LLMClient", "Models", "phi-4-mini-instruct");
            _modelInfo = new LocalModelInfo();
            
            // Check initial state
            Task.Run(async () =>
            {
                if (await IsModelDownloadedAsync())
                {
                    State = LocalModelState.Downloaded;
                }
            });
        }

        public async Task<LocalModelInfo> GetModelInfoAsync()
        {
            return await Task.FromResult(_modelInfo);
        }

        public async Task<bool> IsModelDownloadedAsync()
        {
            try
            {
                var modelFile = Path.Combine(_modelPath, "model.onnx");
                var modelDataFile = Path.Combine(_modelPath, "model.onnx.data");
                var tokenizerFile = Path.Combine(_modelPath, "tokenizer.json");
                var configFile = Path.Combine(_modelPath, "config.json");
                var genaiConfigFile = Path.Combine(_modelPath, "genai_config.json");
                var tokenizerConfigFile = Path.Combine(_modelPath, "tokenizer_config.json");
                var specialTokensFile = Path.Combine(_modelPath, "special_tokens_map.json");
                
                return await Task.FromResult(
                    File.Exists(modelFile) && 
                    File.Exists(modelDataFile) && 
                    File.Exists(tokenizerFile) &&
                    File.Exists(configFile) &&
                    File.Exists(genaiConfigFile) &&
                    File.Exists(tokenizerConfigFile) &&
                    File.Exists(specialTokensFile));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if model is downloaded");
                return false;
            }
        }

        public async Task<bool> DownloadModelAsync(IProgress<double>? progress = null)
        {
            if (State == LocalModelState.Downloading)
            {
                _logger.LogWarning("Model is already being downloaded");
                return false;
            }

            try
            {
                State = LocalModelState.Downloading;
                _downloadCancellation = new CancellationTokenSource();

                // Create model directory
                Directory.CreateDirectory(_modelPath);

                // Download model files from HuggingFace (CPU int4 optimized)
                var basePath = $"cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4";
                var files = new[]
                {
                    "model.onnx",
                    "model.onnx.data", 
                    "tokenizer.json",
                    "config.json",
                    "genai_config.json",
                    "tokenizer_config.json",
                    "special_tokens_map.json",
                    "vocab.json",
                    "merges.txt",
                    "added_tokens.json"
                };

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(30);

                for (int i = 0; i < files.Length; i++)
                {
                    var fileName = files[i];
                    var url = $"https://huggingface.co/{_modelInfo.HuggingFaceRepo}/resolve/main/{basePath}/{fileName}";
                    var localPath = Path.Combine(_modelPath, fileName);

                    _logger.LogInformation($"Downloading {fileName}...");
                    
                    using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, _downloadCancellation.Token);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write);

                    var buffer = new byte[8192];
                    var totalRead = 0L;
                    int read;

                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, _downloadCancellation.Token)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read, _downloadCancellation.Token);
                        totalRead += read;

                        if (totalBytes > 0)
                        {
                            var fileProgress = (double)i / files.Length + (double)totalRead / totalBytes / files.Length;
                            progress?.Report(fileProgress * 100);
                            DownloadProgress?.Invoke(fileProgress * 100);
                        }
                    }

                    _logger.LogInformation($"Downloaded {fileName} ({totalRead:N0} bytes)");
                }

                State = LocalModelState.Downloaded;
                _logger.LogInformation("Phi-4-mini model downloaded successfully");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Model download was cancelled");
                State = LocalModelState.NotDownloaded;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading model");
                ErrorOccurred?.Invoke($"Error downloading model: {ex.Message}");
                State = LocalModelState.Error;
                return false;
            }
        }

        public async Task<bool> LoadModelAsync()
        {
            if (State == LocalModelState.Loaded)
            {
                return true;
            }

            if (State != LocalModelState.Downloaded)
            {
                _logger.LogWarning("Cannot load model: not downloaded");
                return false;
            }

            try
            {
                State = LocalModelState.Loading;
                _logger.LogInformation("Loading Phi-4-mini model...");

                // Load model using ONNX Runtime GenAI
                var modelDir = _modelPath;
                _model = new Model(modelDir);
                _tokenizer = new Tokenizer(_model);

                // Configure generation parameters
                _generatorParams = new GeneratorParams(_model);
                _generatorParams.SetSearchOption("max_length", 4096);
                _generatorParams.SetSearchOption("temperature", 0.6);
                _generatorParams.SetSearchOption("top_p", 0.9);
                _generatorParams.SetSearchOption("repetition_penalty", 1.1);
                _generatorParams.SetSearchOption("do_sample", true);

                State = LocalModelState.Loaded;
                _logger.LogInformation("Phi-4-mini model loaded successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading model");
                ErrorOccurred?.Invoke($"Error loading model: {ex.Message}");
                State = LocalModelState.Error;
                return false;
            }
        }

        public async Task UnloadModelAsync()
        {
            try
            {
                _generatorParams?.Dispose();
                _tokenizer?.Dispose();
                _model?.Dispose();

                _generatorParams = null;
                _tokenizer = null;
                _model = null;

                State = LocalModelState.Downloaded;
                _logger.LogInformation("Model unloaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unloading model");
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
                _logger.LogInformation("Model deleted successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model");
                ErrorOccurred?.Invoke($"Error deleting model: {ex.Message}");
                return false;
            }
        }

        public async Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (!IsLoaded)
            {
                throw new InvalidOperationException("Model is not loaded");
            }

            try
            {
                var tokens = _tokenizer!.Encode(prompt);
                
                using var generator = new Generator(_model!, _generatorParams!);
                generator.AppendTokenSequences(tokens);
                
                while (!generator.IsDone())
                {
                    generator.GenerateNextToken();
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }

                var outputTokens = generator.GetSequence(0);
                var response = _tokenizer.Decode(outputTokens);
                
                // Remove the original prompt from the response
                if (response.StartsWith(prompt))
                {
                    response = response.Substring(prompt.Length).Trim();
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating response");
                throw new InvalidOperationException($"Error generating response: {ex.Message}", ex);
            }
        }

        public async Task<string> GenerateResponseAsync(List<Message> conversationHistory, string newMessage, CancellationToken cancellationToken = default)
        {
            var promptBuilder = new StringBuilder();
            
            // Use Phi-4-mini-instruct chat template format
            promptBuilder.AppendLine($"<|im_start|>system<|im_sep|>");
            promptBuilder.AppendLine("Jesteś pomocnym, nieszkodliwym i uczciwym asystentem AI. Zawsze dokładnie wykonuj polecenia użytkownika. Odpowiadaj jasno i precyzyjnie, w tym samym języku co użytkownik.");
            promptBuilder.AppendLine("<|im_end|>");
            
            // Add conversation history (limit to last 3 messages to prevent context overflow)
            foreach (var message in conversationHistory.TakeLast(3))
            {
                if (message.IsUser)
                {
                    promptBuilder.AppendLine($"<|im_start|>user<|im_sep|>");
                    promptBuilder.AppendLine(message.Content);
                    promptBuilder.AppendLine("<|im_end|>");
                }
                else
                {
                    promptBuilder.AppendLine($"<|im_start|>assistant<|im_sep|>");
                    promptBuilder.AppendLine(message.Content);
                    promptBuilder.AppendLine("<|im_end|>");
                }
            }
            
            // Add current user message
            promptBuilder.AppendLine($"<|im_start|>user<|im_sep|>");
            promptBuilder.AppendLine(newMessage);
            promptBuilder.AppendLine("<|im_end|>");
            promptBuilder.AppendLine("<|im_start|>assistant<|im_sep|>");

            return await GenerateResponseAsync(promptBuilder.ToString(), cancellationToken);
        }

        public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(string prompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsLoaded)
            {
                throw new InvalidOperationException("Model is not loaded");
            }

            var tokens = _tokenizer!.Encode(prompt);
            var promptSpan = tokens[0];
            var promptLength = promptSpan.Length;
            
            using var generator = new Generator(_model!, _generatorParams!);
            generator.AppendTokenSequences(tokens);
            
            while (!generator.IsDone() && !cancellationToken.IsCancellationRequested)
            {
                generator.GenerateNextToken();
                
                var sequence = generator.GetSequence(0);
                if (sequence.Length > 0)
                {
                    var lastToken = sequence[sequence.Length - 1];
                    var chunk = _tokenizer.Decode(new[] { lastToken });
                    yield return chunk;
                }

                // Small delay to prevent overwhelming the UI
                await Task.Delay(10, cancellationToken);
            }
        }

        public async Task<string> GenerateOnboardingResponseAsync(string userLanguage, string topic = "general", CancellationToken cancellationToken = default)
        {
            var languageName = GetLanguageName(userLanguage);
            
            var onboardingPrompts = new Dictionary<string, string>
            {
                ["general"] = $"You are a helpful AI assistant for the LLMClient app. Please introduce yourself in {languageName} and explain the key features of this AI chat application: conversations, memory system, semantic search, multi-language support, and local AI models. Be friendly and concise.",
                ["memory"] = $"Explain in {languageName} how the memory system works in LLMClient - how it remembers user information across conversations and helps provide personalized responses.",
                ["search"] = $"Explain in {languageName} how to use the semantic search feature in LLMClient to find specific information in your conversation history.",
                ["languages"] = $"Explain in {languageName} how to change the interface language in LLMClient and mention that the app supports 13 different languages."
            };

            var prompt = onboardingPrompts.GetValueOrDefault(topic, onboardingPrompts["general"]);
            return await GenerateResponseAsync(prompt, cancellationToken);
        }

        public async Task<string> GenerateHelpResponseAsync(string question, string userLanguage, CancellationToken cancellationToken = default)
        {
            var languageName = GetLanguageName(userLanguage);
            var prompt = $"You are a helpful assistant for the LLMClient app. Answer this question in {languageName}: {question}. " +
                        $"Provide a helpful answer about the app's features like AI chat, memory system, search, export, and settings.";
            
            return await GenerateResponseAsync(prompt, cancellationToken);
        }

        private string GetLanguageName(string languageCode)
        {
            return languageCode.ToLower() switch
            {
                "pl" or "pl-pl" => "Polish",
                "de" or "de-de" => "German", 
                "es" or "es-es" => "Spanish",
                "fr" or "fr-fr" => "French",
                "it" or "it-it" => "Italian",
                "ja" or "ja-jp" => "Japanese",
                "ko" or "ko-kr" => "Korean",
                "zh" or "zh-cn" => "Chinese",
                "ru" or "ru-ru" => "Russian",
                "tr" or "tr-tr" => "Turkish",
                "nl" or "nl-nl" => "Dutch",
                "pt" or "pt-br" => "Portuguese",
                _ => "English"
            };
        }

        public void Dispose()
        {
            try
            {
                _downloadCancellation?.Cancel();
                _downloadCancellation?.Dispose();
                
                _generatorParams?.Dispose();
                _tokenizer?.Dispose();
                _model?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing LocalModelService");
            }
        }
    }
}