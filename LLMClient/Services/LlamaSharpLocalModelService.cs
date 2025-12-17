// LLamaSharp works on Windows and Android (v0.25.0+)
#if WINDOWS || ANDROID
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Microsoft.Extensions.Logging;
using LLama;
using LLama.Common;
using LLama.Abstractions;
using LLama.Sampling;
using LLama.Native;
using LLMClient.Models;

#if ANDROID
using Android.Util;
#endif

namespace LLMClient.Services
{
    public class GgufModelInfo
    {
        public required string Id { get; init; }
        public required string DisplayName { get; init; }
        public required string HuggingFaceRepo { get; init; }
        public required string FileName { get; init; }
        public required long SizeInMB { get; init; }
        public required string[] SupportedLanguages { get; init; }
        public string? ChatTemplate { get; init; }
        public string Description { get; init; } = "";
        public bool IsRecommended { get; init; } = false;
    }

    public class LlamaSharpLocalModelService : ILocalModelService, IDisposable
    {
        private readonly ILogger<LlamaSharpLocalModelService> _logger;
        private LLamaWeights? _weights;
        private LLamaContext? _context;
        private ILLamaExecutor? _executor;
        private LocalModelState _state = LocalModelState.NotDownloaded;
        private readonly string _modelDir;
        private GgufModelInfo _selectedModel;
        private string _modelPath;
        private string _markerFilePath;
        
        public static readonly List<GgufModelInfo> AvailableModels = new()
        {
            new GgufModelInfo
            {
                Id = "gemma-3-1b-it",
                DisplayName = "Gemma 3 1B Instruct",
                HuggingFaceRepo = "ggml-org/gemma-3-1b-it-GGUF",
                FileName = "gemma-3-1b-it-Q4_K_M.gguf",
                SizeInMB = 700,
                SupportedLanguages = new[] { "en", "pl", "de", "es", "fr", "it", "ja", "ko", "zh", "ru" },
                ChatTemplate = "gemma",
                Description = "Maly, szybki model Gemma 3 od Google. Idealny dla mobile.",
                IsRecommended = true
            },
            new GgufModelInfo
            {
                Id = "qwen2.5-0.5b-it",
                DisplayName = "Qwen2.5 0.5B Instruct",
                HuggingFaceRepo = "Qwen/Qwen2.5-0.5B-Instruct-GGUF",
                FileName = "qwen2.5-0.5b-instruct-q4_k_m.gguf",
                SizeInMB = 491,
                SupportedLanguages = new[] { "en", "pl", "de", "es", "fr", "it", "ja", "ko", "zh", "ru" },
                ChatTemplate = "chatml",
                Description = "Bardzo maly i szybki model do testow na telefonie."
            },
            new GgufModelInfo
            {
                Id = "qwen2-0.5b-it",
                DisplayName = "Qwen2 0.5B Instruct",
                HuggingFaceRepo = "Qwen/Qwen2-0.5B-Instruct-GGUF",
                FileName = "qwen2-0_5b-instruct-q4_k_m.gguf",
                SizeInMB = 398,
                SupportedLanguages = new[] { "en", "pl", "de", "es", "fr", "it", "ja", "ko", "zh", "ru" },
                ChatTemplate = "chatml",
                Description = "Bardzo maly model Qwen2 w formacie GGUF."
            },
            new GgufModelInfo
            {
                Id = "gemma-3-4b-it",
                DisplayName = "Gemma 3 4B Instruct",
                HuggingFaceRepo = "ggml-org/gemma-3-4b-it-GGUF",
                FileName = "gemma-3-4b-it-Q4_K_M.gguf",
                SizeInMB = 2800,
                SupportedLanguages = new[] { "en", "pl", "de", "es", "fr", "it", "ja", "ko", "zh", "ru" },
                ChatTemplate = "gemma",
                Description = "Sredni model Gemma 3."
            },
            new GgufModelInfo
            {
                Id = "smollm2-1.7b-it",
                DisplayName = "SmolLM2 1.7B Instruct",
                HuggingFaceRepo = "HuggingFaceTB/SmolLM2-1.7B-Instruct-GGUF",
                FileName = "smollm2-1.7b-instruct-q4_k_m.gguf",
                SizeInMB = 1060,
                SupportedLanguages = new[] { "en" },
                ChatTemplate = "chatml",
                Description = "Lekki model 1.7B."
            },
            new GgufModelInfo
            {
                Id = "tinyllama-1.1b-chat",
                DisplayName = "TinyLlama 1.1B Chat",
                HuggingFaceRepo = "TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF",
                FileName = "tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf",
                SizeInMB = 669,
                SupportedLanguages = new[] { "en" },
                ChatTemplate = "chatml",
                Description = "Bardzo szybki, maly model do testow."
            },
            new GgufModelInfo
            {
                Id = "qwen3-1.7b-it",
                DisplayName = "Qwen3 1.7B Instruct",
                HuggingFaceRepo = "unsloth/Qwen3-1.7B-Instruct-GGUF",
                FileName = "Qwen3-1.7B-Instruct-Q4_K_M.gguf",
                SizeInMB = 1100,
                SupportedLanguages = new[] { "en", "pl", "de", "es", "fr", "it", "ja", "ko", "zh", "ru" },
                ChatTemplate = "chatml",
                Description = "Kompaktowy Qwen3 z dobra jakoscia."
            },
            new GgufModelInfo
            {
                Id = "qwen3-4b-it",
                DisplayName = "Qwen3 4B Instruct",
                HuggingFaceRepo = "unsloth/Qwen3-4B-Instruct-2507-GGUF",
                FileName = "Qwen3-4B-Instruct-2507-Q4_K_M.gguf",
                SizeInMB = 2700,
                SupportedLanguages = new[] { "en", "pl", "de", "es", "fr", "it", "ja", "ko", "zh", "ru" },
                ChatTemplate = "chatml",
                Description = "Sredni Qwen3."
            },
            new GgufModelInfo
            {
                Id = "phi-3-mini",
                DisplayName = "Phi-3 Mini 3.8B",
                HuggingFaceRepo = "microsoft/Phi-3-mini-4k-instruct-gguf",
                FileName = "Phi-3-mini-4k-instruct-q4.gguf",
                SizeInMB = 2300,
                SupportedLanguages = new[] { "en" },
                ChatTemplate = "chatml",
                Description = "Kompaktowy model od Microsoft."
            }
        };
        
        private LocalModelInfo _modelInfo;
        private int _maxNewTokens = 512;
        private double _temperature = 0.7;
        private double _topP = 0.95;
        private double _repetitionPenalty = 1.1;
        private readonly SemaphoreSlim _genLock = new(1, 1);

        public LlamaSharpLocalModelService(ILogger<LlamaSharpLocalModelService> logger)
        {
            _logger = logger;
#if ANDROID
            try
            {
                var nativeLibDir = Android.App.Application.Context.ApplicationInfo?.NativeLibraryDir;
                if (!string.IsNullOrEmpty(nativeLibDir))
                {
                    var llamaPath = Path.Combine(nativeLibDir, "libllama.so");
                    if (File.Exists(llamaPath))
                    {
                        NativeLibraryConfig.LLama.WithLibrary(llamaPath);
                        _logger.LogInformation("[LLamaSharp] Android: Using native library from {Path}", llamaPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LLamaSharp] Android: Failed to configure native library path");
            }
            _modelDir = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "Models", "gguf");
#else
            _modelDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LLMClient", "Models", "gguf");
#endif
            try { Directory.CreateDirectory(_modelDir); }
            catch (Exception ex) { _logger.LogError(ex, "[LLamaSharp] Cannot create model dir: {Dir}", _modelDir); throw; }
            
            var savedModelId = LoadSelectedModelId();
            _selectedModel = AvailableModels.FirstOrDefault(m => m.Id == savedModelId) ?? AvailableModels.First(m => m.IsRecommended);
            UpdateModelPaths();
            UpdateModelInfo();
            _ = InitializeStateAsync();
        }
        
        public IReadOnlyList<GgufModelInfo> GetAvailableModels() => AvailableModels;
        public GgufModelInfo SelectedModel => _selectedModel;
        
        public async Task<bool> SelectModelAsync(string modelId)
        {
            var model = AvailableModels.FirstOrDefault(m => m.Id == modelId);
            if (model == null) { _logger.LogWarning("[LLamaSharp] Model {ModelId} not found", modelId); return false; }
            if (_selectedModel.Id == modelId) return true;
            if (IsLoaded) await UnloadModelAsync();
            _selectedModel = model;
            SaveSelectedModelId(modelId);
            UpdateModelPaths();
            UpdateModelInfo();
            SetState(await IsModelDownloadedAsync() ? LocalModelState.Downloaded : LocalModelState.NotDownloaded);
            _logger.LogInformation("[LLamaSharp] Selected model: {Model}", model.DisplayName);
            return true;
        }
        
        public async Task<Dictionary<string, bool>> GetDownloadedModelsAsync()
        {
            var result = new Dictionary<string, bool>();
            foreach (var model in AvailableModels)
            {
                var path = Path.Combine(_modelDir, model.FileName);
                var marker = path + ".complete";
                result[model.Id] = File.Exists(path) && File.Exists(marker);
            }
            return await Task.FromResult(result);
        }
        
        private void UpdateModelPaths()
        {
            _modelPath = Path.Combine(_modelDir, _selectedModel.FileName);
            _markerFilePath = _modelPath + ".complete";
        }
        
        private void UpdateModelInfo()
        {
            _modelInfo = new LocalModelInfo
            {
                ModelId = _selectedModel.Id,
                DisplayName = _selectedModel.DisplayName,
                Version = "1.0",
                SizeInMB = _selectedModel.SizeInMB,
                HuggingFaceRepo = _selectedModel.HuggingFaceRepo,
                SupportedLanguages = _selectedModel.SupportedLanguages,
                IsOnboardingCapable = true,
                SupportsRealtimeChat = true
            };
        }
        
        private string LoadSelectedModelId()
        {
            // Default to Qwen 2.5 - more stable than Gemma 3 on llama.cpp Android
            try { return Microsoft.Maui.Storage.Preferences.Get("LlamaSharp_SelectedModelId", "qwen2.5-0.5b-it"); }
            catch { return "qwen2.5-0.5b-it"; }
        }
        
        private void SaveSelectedModelId(string modelId)
        {
            try { Microsoft.Maui.Storage.Preferences.Set("LlamaSharp_SelectedModelId", modelId); } catch { }
        }

        private async Task InitializeStateAsync()
        {
            if (await IsModelDownloadedAsync()) SetState(LocalModelState.Downloaded);
        }

        private void SetState(LocalModelState s)
        {
            if (_state != s) { _state = s; StateChanged?.Invoke(_state); _logger.LogInformation("[LLamaSharp] State: {State}", _state); }
        }

        public LocalModelState State => _state;
        public bool IsLoaded => State == LocalModelState.Loaded;
        public bool IsDownloading => State == LocalModelState.Downloading;
        public event Action<LocalModelState>? StateChanged;
        public event Action<double>? DownloadProgress;
        public event Action<string>? ErrorOccurred;
        public Task<LocalModelInfo> GetModelInfoAsync() => Task.FromResult(_modelInfo);

        public Task<bool> IsModelDownloadedAsync()
        {
            try
            {
                if (File.Exists(_modelPath))
                {
                    if (File.Exists(_markerFilePath)) return Task.FromResult(true);
                    try
                    {
                        var sizeMb = new FileInfo(_modelPath).Length / (1024.0 * 1024.0);
                        if (sizeMb >= Math.Max(50, _modelInfo.SizeInMB * 0.6)) return Task.FromResult(true);
                    }
                    catch { }
                }
                return Task.FromResult(false);
            }
            catch { return Task.FromResult(false); }
        }

        public async Task<bool> DownloadModelAsync(IProgress<double>? progress = null)
        {
            if (IsDownloading) return false;
            try
            {
                Directory.CreateDirectory(_modelDir);
                SetState(LocalModelState.Downloading);
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("LLMClient/1.0");
                var hfToken = Environment.GetEnvironmentVariable("HF_TOKEN") ?? Environment.GetEnvironmentVariable("HUGGINGFACE_TOKEN");
                if (!string.IsNullOrWhiteSpace(hfToken))
                    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hfToken);

                var url = $"https://huggingface.co/{_selectedModel.HuggingFaceRepo}/resolve/main/{_selectedModel.FileName}?download=true";
                var dest = _modelPath;
                var temp = dest + ".partial";
                _logger.LogInformation("[LLamaSharp] Downloading: {Url}", url);

                using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                resp.EnsureSuccessStatusCode();

                var total = resp.Content.Headers.ContentLength ?? 0;
                await using var content = await resp.Content.ReadAsStreamAsync();
                await using var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None);

                var buf = new byte[1 << 20];
                long readTotal = 0;
                int read;
                while ((read = await content.ReadAsync(buf, 0, buf.Length)) > 0)
                {
                    await fs.WriteAsync(buf, 0, read);
                    readTotal += read;
                    if (total > 0) { var p = (double)readTotal / total * 100.0; progress?.Report(p); DownloadProgress?.Invoke(p); }
                }

                try { if (File.Exists(dest)) File.Delete(dest); File.Move(temp, dest); }
                catch { File.Copy(temp, dest, true); File.Delete(temp); }

                try { File.WriteAllText(_markerFilePath, $"ok\nsize={readTotal}\nwhen={DateTime.UtcNow:o}"); } catch { }

                SetState(LocalModelState.Downloaded);
                _logger.LogInformation("[LLamaSharp] Model downloaded: {Path}", dest);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LLamaSharp] Download error");
                ErrorOccurred?.Invoke($"Download error: {ex.Message}");
                SetState(LocalModelState.Error);
                return false;
            }
        }

        public Task<bool> LoadModelAsync()
        {
            try
            {
                if (!File.Exists(_modelPath)) { SetState(LocalModelState.Error); return Task.FromResult(false); }
                SetState(LocalModelState.Loading);
                _logger.LogInformation("[LLamaSharp] Loading model: {Path}", _modelPath);

                return Task.Run(() =>
                {
                    try
                    {
                        // Android backend is CPU-only in this app; avoid GPU offload settings.
#if ANDROID
                        var pCpu = BuildModelParams(_modelPath, false);
                        _weights = LLamaWeights.LoadFromFile(pCpu);
                        _context = _weights.CreateContext(pCpu);
                        _executor = new StatelessExecutor(_weights, _context.Params);
#else
                        try
                        {
                            var pGpu = BuildModelParams(_modelPath, true);
                            _weights = LLamaWeights.LoadFromFile(pGpu);
                            _context = _weights.CreateContext(pGpu);
                            _executor = new StatelessExecutor(_weights, _context.Params);
                        }
                        catch
                        {
                            var pCpuFallback = BuildModelParams(_modelPath, false);
                            _weights = LLamaWeights.LoadFromFile(pCpuFallback);
                            _context = _weights.CreateContext(pCpuFallback);
                            _executor = new StatelessExecutor(_weights, _context.Params);
                        }
#endif
                        SetState(LocalModelState.Loaded);
                        _logger.LogInformation("[LLamaSharp] Model loaded");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[LLamaSharp] Load error");
                        ErrorOccurred?.Invoke($"Load error: {ex.Message}");
                        SetState(LocalModelState.Error);
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LLamaSharp] Load error");
                SetState(LocalModelState.Error);
                return Task.FromResult(false);
            }
        }

        public Task UnloadModelAsync()
        {
            try
            {
                if (_executor is IDisposable de) de.Dispose();
                _context?.Dispose();
                _weights?.Dispose();
            }
            catch { }
            finally
            {
                _executor = null; _context = null; _weights = null;
                SetState(File.Exists(_modelPath) ? LocalModelState.Downloaded : LocalModelState.NotDownloaded);
            }
            return Task.CompletedTask;
        }

        public async Task<bool> DeleteModelAsync()
        {
            try
            {
                await UnloadModelAsync();
                if (Directory.Exists(_modelDir)) Directory.Delete(_modelDir, true);
                SetState(LocalModelState.NotDownloaded);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LLamaSharp] Delete error");
                ErrorOccurred?.Invoke($"Delete error: {ex.Message}");
                return false;
            }
        }

        private bool IsGemmaModel() => _selectedModel.ChatTemplate == "gemma" || _selectedModel.Id.Contains("gemma", StringComparison.OrdinalIgnoreCase);
        private bool IsChatMLModel() => _selectedModel.ChatTemplate == "chatml" || _selectedModel.Id.Contains("qwen", StringComparison.OrdinalIgnoreCase) || _selectedModel.Id.Contains("tinyllama", StringComparison.OrdinalIgnoreCase);

        private List<string> GetAntiPrompts()
        {
            var stops = new List<string> { "</s>" };
            if (IsGemmaModel())
            {
                stops.Add("<end_of_turn>");
            }
            else if (IsChatMLModel())
            {
                stops.Add("<" + "|im_end|" + ">");
                stops.Add("<" + "|endoftext|" + ">");
            }
            return stops;
        }

        public async Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var sb = new StringBuilder();
            await foreach (var chunk in GenerateStreamingResponseAsync(prompt, cancellationToken))
                sb.Append(chunk);
            return sb.ToString();
        }

        public Task<string> GenerateResponseAsync(List<Message> conversationHistory, string newMessage, CancellationToken cancellationToken = default)
        {
            var prompt = BuildPrompt(conversationHistory, newMessage);
            return GenerateResponseAsync(prompt, cancellationToken);
        }

        private string BuildPrompt(List<Message> conversationHistory, string newMessage)
        {
            var sb = new StringBuilder();

            var historyEndsWithSameUserMessage = conversationHistory.Count > 0
                && conversationHistory[^1].IsUser
                && string.Equals(conversationHistory[^1].Content?.Trim(), newMessage?.Trim(), StringComparison.Ordinal);
            
            if (IsGemmaModel())
            {
                // Gemma 3 format: <bos><start_of_turn>user\n...<end_of_turn>\n<start_of_turn>model\n
                sb.Append("<bos>");
                foreach (var msg in conversationHistory.TakeLast(3))
                {
                    sb.Append("<start_of_turn>");
                    sb.Append(msg.IsUser ? "user" : "model");
                    sb.Append('\n');
                    sb.Append(msg.Content);
                    sb.Append("<end_of_turn>\n");
                }
                if (historyEndsWithSameUserMessage)
                {
                    sb.Append("<start_of_turn>model\n");
                }
                else
                {
                    sb.Append("<start_of_turn>user\n");
                    sb.Append(newMessage);
                    sb.Append("<end_of_turn>\n");
                    sb.Append("<start_of_turn>model\n");
                }
            }
            else if (IsChatMLModel())
            {
                // ChatML format for Qwen, TinyLlama, etc.
                var imStart = "<" + "|im_start|" + ">";
                var imEnd = "<" + "|im_end|" + ">";
                
                sb.Append(imStart + "system\n");
                sb.Append("You are a helpful AI assistant. Answer in the user's language." + imEnd + "\n");
                
                foreach (var msg in conversationHistory.TakeLast(3))
                {
                    sb.Append(imStart);
                    sb.Append(msg.IsUser ? "user" : "assistant");
                    sb.Append('\n');
                    sb.Append(msg.Content);
                    sb.Append(imEnd + "\n");
                }
                if (historyEndsWithSameUserMessage)
                {
                    sb.Append(imStart + "assistant\n");
                }
                else
                {
                    sb.Append(imStart + "user\n");
                    sb.Append(newMessage);
                    sb.Append(imEnd + "\n");
                    sb.Append(imStart + "assistant\n");
                }
            }
            else
            {
                // Plain format (fallback)
                sb.Append("System: You are a helpful AI assistant. Answer in the user's language.\n\n");
                foreach (var msg in conversationHistory.TakeLast(3))
                {
                    sb.Append(msg.IsUser ? "User: " : "Assistant: ");
                    sb.Append(msg.Content);
                    sb.Append('\n');
                }
                if (historyEndsWithSameUserMessage)
                {
                    sb.Append("Assistant: ");
                }
                else
                {
                    sb.Append("User: ");
                    sb.Append(newMessage);
                    sb.Append("\nAssistant: ");
                }
            }
            
            return sb.ToString();
        }

        public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(string prompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsLoaded || _executor is null)
                throw new InvalidOperationException("Model not loaded");

            await _genLock.WaitAsync(cancellationToken);
            try
            {
                var preview = prompt.Length > 800 ? prompt[..800] : prompt;
                var modelFileName = _selectedModel?.FileName ?? Path.GetFileName(_modelPath);
                var modelSizeMb = 0.0;
                try { modelSizeMb = File.Exists(_modelPath) ? new FileInfo(_modelPath).Length / (1024.0 * 1024.0) : 0.0; } catch { }
                _logger.LogInformation("[LLamaSharp] Infer start. Model={ModelId}. PromptPreview=\n{PromptPreview}", _selectedModel.Id, preview);

#if ANDROID
                Log.Info("LLMCLIENT", "[LLamaSharp] Infer start. Model=" + _selectedModel.Id + " File=" + modelFileName + " SizeMB=" + modelSizeMb.ToString("F0") + " PromptPreview=\n" + preview);
#endif

                var samplingPipeline = new DefaultSamplingPipeline()
                {
                    Temperature = (float)_temperature,
                    TopP = (float)_topP,
                    TopK = 40,
                    RepeatPenalty = (float)_repetitionPenalty,
                };

                _logger.LogInformation("[LLamaSharp] Sampling: temp={Temp}, topP={TopP}, rep={Rep}, maxTokens={MaxTokens}", _temperature, _topP, _repetitionPenalty, _maxNewTokens);

#if ANDROID
                Log.Info("LLMCLIENT", "[LLamaSharp] Sampling: temp=" + _temperature + ", topP=" + _topP + ", rep=" + _repetitionPenalty + ", maxTokens=" + _maxNewTokens);
#endif

                var inferenceParams = new InferenceParams()
                {
                    MaxTokens = _maxNewTokens,
                    SamplingPipeline = samplingPipeline,
                    AntiPrompts = GetAntiPrompts()
                };

                IAsyncEnumerable<string> enumerable;
                try
                {
                    enumerable = _executor.InferAsync(prompt, inferenceParams, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[LLamaSharp] Infer error");
                    ErrorOccurred?.Invoke($"Infer error: {ex.Message}");
                    yield break;
                }

                await foreach (var text in enumerable.WithCancellation(cancellationToken))
                {
                    yield return text;
                }
            }
            finally
            {
                _genLock.Release();
            }
        }

        public Task<string> GenerateOnboardingResponseAsync(string userLanguage, string topic = "general", CancellationToken cancellationToken = default)
        {
            var languageName = GetLanguageName(userLanguage);
            var prompts = new Dictionary<string, string>
            {
                ["general"] = $"You are a helpful AI assistant for the LLMClient app. Please introduce yourself in {languageName} and explain the key features of this AI chat application: conversations, memory system, semantic search, multi-language support, and local AI models. Be friendly and concise.",
                ["memory"] = $"Explain in {languageName} how the memory system works in LLMClient.",
                ["search"] = $"Explain in {languageName} how to use the semantic search feature in LLMClient.",
                ["languages"] = $"Explain in {languageName} how to change the interface language in LLMClient."
            };
            var prompt = prompts.GetValueOrDefault(topic, prompts["general"]);
            return GenerateResponseAsync(prompt, cancellationToken);
        }

        public Task<string> GenerateHelpResponseAsync(string question, string userLanguage, CancellationToken cancellationToken = default)
        {
            var languageName = GetLanguageName(userLanguage);
            var prompt = $"You are a helpful assistant for the LLMClient app. Answer this question in {languageName}: {question}. Provide a helpful answer about the app's features.";
            return GenerateResponseAsync(prompt, cancellationToken);
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

        private int GetOptimalContextSize()
        {
#if ANDROID
            return 2048;
#else
            return 4096;
#endif
        }

        private ModelParams BuildModelParams(string modelPath, bool useGpu)
        {
            var parameters = new ModelParams(modelPath)
            {
                ContextSize = (uint)GetOptimalContextSize(),
                BatchSize = 256,
                Threads = Math.Max(1, Environment.ProcessorCount - 1),
                UseMemorymap = true,
                GpuLayerCount = useGpu ? int.MaxValue : 0,
            };
#if ANDROID
            parameters.UseMemoryLock = false;
            parameters.Threads = Math.Max(1, Math.Min(4, Environment.ProcessorCount - 2));
#endif
            // Fix Gemma 3 gibberish: override sliding window attention metadata
            // See: https://huggingface.co/google/gemma-3-1b-it-qat-q4_0-gguf/discussions/1
            if (IsGemmaModel())
            {
                parameters.MetadataOverrides.Add(new MetadataOverride("gemma3.attention.sliding_window", 512));
            }
            return parameters;
        }

        public void Dispose()
        {
            try
            {
                _genLock?.Dispose();
                if (_executor is IDisposable de) de.Dispose();
                _context?.Dispose();
                _weights?.Dispose();
            }
            catch { }
        }
    }
}
#endif
