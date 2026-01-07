using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Extensions.Logging;
using LLMClient.Models;

namespace LLMClient.Services
{
    /// <summary>
    /// Serwis embeddingowy z obsługą wielu modeli (Gemma, E5-Large).
    /// Domyślnie używa EmbeddingGemma jako szybszego i lżejszego modelu.
    /// </summary>
    public class MultiModelEmbeddingService : IEmbeddingService, IDisposable
    {
        private readonly ILogger<MultiModelEmbeddingService> _logger;
        private InferenceSession? _session;
        private EmbeddingModelInfo _currentModel;
        private bool _isInitialized;
        private bool _tokenizerReady;
        private string? _tokenizerPath;
        private string _tokenizerName = "";

        private const string PREF_SELECTED_MODEL = "EmbeddingService_SelectedModelId";

        public string ModelVersion => _currentModel.Id;
        public bool IsInitialized => _isInitialized;
        public EmbeddingModelInfo CurrentModel => _currentModel;
        public IReadOnlyList<EmbeddingModelInfo> AvailableModels => EmbeddingModels.All;

        public event Action<double>? DownloadProgress;
        public event Action<string>? ModelChanged;

        public MultiModelEmbeddingService(ILogger<MultiModelEmbeddingService> logger)
        {
            _logger = logger;
            _currentModel = LoadSelectedModel();
            _logger.LogInformation($"[MultiModelEmbedding] Initialized with model: {_currentModel.DisplayName}");
        }

        private EmbeddingModelInfo LoadSelectedModel()
        {
            try
            {
                var savedId = Microsoft.Maui.Storage.Preferences.Get(PREF_SELECTED_MODEL, "");
                if (!string.IsNullOrEmpty(savedId))
                {
                    return EmbeddingModels.GetById(savedId);
                }
                
                // Pierwszy raz - wybierz model na podstawie dostępnego RAM
                var recommended = GetRecommendedModelForDevice();
                _logger.LogInformation($"[MultiModelEmbedding] First run - recommending {recommended.DisplayName} based on device RAM");
                SaveSelectedModel(recommended.Id);
                return recommended;
            }
            catch
            {
                return EmbeddingModels.GetDefault();
            }
        }
        
        /// <summary>
        /// Zwraca rekomendowany model na podstawie RAM urządzenia
        /// </summary>
        public static EmbeddingModelInfo GetRecommendedModelForDevice()
        {
            try
            {
                var memInfo = GC.GetGCMemoryInfo();
                var totalRAM = memInfo.TotalAvailableMemoryBytes;
                return EmbeddingModels.GetRecommendedForRAM(totalRAM);
            }
            catch
            {
                // Fallback - zakładamy mniej RAM, wybieramy Gemma
                return EmbeddingModels.EmbeddingGemma;
            }
        }
        
        /// <summary>
        /// Zwraca ilość RAM urządzenia w GB
        /// </summary>
        public static int GetDeviceRAMInGB()
        {
            try
            {
                var memInfo = GC.GetGCMemoryInfo();
                return (int)(memInfo.TotalAvailableMemoryBytes / (1024L * 1024L * 1024L));
            }
            catch { return 4; }
        }

        private void SaveSelectedModel(string modelId)
        {
            try { Microsoft.Maui.Storage.Preferences.Set(PREF_SELECTED_MODEL, modelId); }
            catch { }
        }

        public async Task<bool> SelectModelAsync(string modelId)
        {
            var model = EmbeddingModels.All.FirstOrDefault(m => m.Id == modelId);
            if (model == null)
            {
                _logger.LogWarning($"[MultiModelEmbedding] Model {modelId} not found");
                return false;
            }

            if (_currentModel.Id == modelId && _isInitialized)
                return true;

            _logger.LogInformation($"[MultiModelEmbedding] Switching from {_currentModel.DisplayName} to {model.DisplayName}");

            // Cleanup current session
            _session?.Dispose();
            _session = null;
            _isInitialized = false;
            _tokenizerReady = false;

            _currentModel = model;
            SaveSelectedModel(modelId);

            // Re-initialize with new model
            await InitializeAsync();

            ModelChanged?.Invoke(modelId);
            return true;
        }

        public async Task<bool> IsModelDownloadedAsync()
        {
            var modelDir = GetModelDirectory(_currentModel);
            var modelPath = GetModelPath(_currentModel);
            return await Task.FromResult(File.Exists(modelPath));
        }

        public async Task<bool> IsModelDownloadedAsync(string modelId)
        {
            var model = EmbeddingModels.GetById(modelId);
            var modelPath = GetModelPath(model);
            return await Task.FromResult(File.Exists(modelPath));
        }

        private string GetModelDirectory(EmbeddingModelInfo model)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "LLMClient", "Data", "models", model.Id);
        }

        private string GetModelPath(EmbeddingModelInfo model)
        {
            var dir = GetModelDirectory(model);
            return model.Id == "embeddinggemma-300m"
                ? Path.Combine(dir, "onnx", "model.onnx")
                : Path.Combine(dir, "model.onnx");
        }

        private string GetTokenizerPath(EmbeddingModelInfo model)
        {
            var dir = GetModelDirectory(model);
            return Path.Combine(dir, "tokenizer.json");
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation($"[MultiModelEmbedding] Initializing {_currentModel.DisplayName}...");

            try
            {
                var modelPath = await DownloadModelIfNeededAsync();

                if (File.Exists(modelPath))
                {
                    var options = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
                    _session = new InferenceSession(modelPath, options);
                    _logger.LogInformation($"[MultiModelEmbedding] Model loaded: {_currentModel.DisplayName}");

                    // Initialize tokenizer for both models
                    await InitializeTokenizerAsync();

                    _isInitialized = true;
                }
                else
                {
                    _logger.LogWarning("[MultiModelEmbedding] Model not found, using demo mode");
                    _isInitialized = true; // Demo mode
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MultiModelEmbedding] Initialization failed");
                _isInitialized = true; // Demo mode fallback
            }
        }

        private async Task InitializeTokenizerAsync()
        {
            var tokenizerPath = GetTokenizerPath(_currentModel);
            if (!File.Exists(tokenizerPath))
            {
                _logger.LogWarning($"[MultiModelEmbedding] Tokenizer not found: {tokenizerPath}");
                return;
            }

            try
            {
                // Use named tokenizer API for multi-model support
                _tokenizerName = _currentModel.Id;
                var result = await TokenizerNative.InitNamedAsync(_tokenizerName, tokenizerPath);
                _tokenizerReady = result == 0;
                _tokenizerPath = tokenizerPath;
                _logger.LogInformation($"[MultiModelEmbedding] Tokenizer '{_tokenizerName}' initialized: {(_tokenizerReady ? "OK" : "FAILED")}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MultiModelEmbedding] Tokenizer init failed");
                _tokenizerReady = false;
            }
        }

        private async Task<string> DownloadModelIfNeededAsync()
        {
            var modelDir = GetModelDirectory(_currentModel);
            var modelPath = GetModelPath(_currentModel);

            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);

            if (File.Exists(modelPath))
            {
                _logger.LogInformation($"[MultiModelEmbedding] Model exists: {modelPath}");
                return modelPath;
            }

            _logger.LogInformation($"[MultiModelEmbedding] Downloading {_currentModel.DisplayName}...");

            try
            {
                if (_currentModel.Id == "embeddinggemma-300m")
                {
                    await DownloadGemmaModelAsync(modelDir);
                }
                else
                {
                    await DownloadE5ModelAsync(modelDir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MultiModelEmbedding] Download failed");
            }

            return modelPath;
        }

        private async Task DownloadGemmaModelAsync(string modelDir)
        {
            var onnxDir = Path.Combine(modelDir, "onnx");
            Directory.CreateDirectory(onnxDir);

            // Files from onnx-community repo which has tokenizer.json
            var files = new[]
            {
                ("onnx/model.onnx", "model.onnx"),
                ("tokenizer.json", "../tokenizer.json")
            };

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            var baseUrl = "https://huggingface.co/onnx-community/embeddinggemma-300m-ONNX/resolve/main/";

            foreach (var (remotePath, localPath) in files)
            {
                var fullLocalPath = Path.Combine(onnxDir, localPath);
                if (File.Exists(fullLocalPath)) continue;

                _logger.LogInformation($"[MultiModelEmbedding] Downloading {remotePath}...");
                var url = baseUrl + remotePath;

                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                await using var contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(fullLocalPath, FileMode.Create);

                var buffer = new byte[81920];
                long downloaded = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));
                    downloaded += read;
                    if (totalBytes > 0)
                        DownloadProgress?.Invoke((double)downloaded / totalBytes);
                }
            }
        }

        private async Task DownloadE5ModelAsync(string modelDir)
        {
            var files = new[]
            {
                ("onnx/model.onnx", "model.onnx"),
                ("onnx/model.onnx_data", "model.onnx_data"),
                ("tokenizer.json", "tokenizer.json")
            };

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(60) };
            var baseUrl = "https://huggingface.co/intfloat/multilingual-e5-large/resolve/main/";

            foreach (var (remotePath, localPath) in files)
            {
                var fullLocalPath = Path.Combine(modelDir, localPath);
                if (File.Exists(fullLocalPath)) continue;

                _logger.LogInformation($"[MultiModelEmbedding] Downloading {remotePath}...");
                var url = baseUrl + remotePath;

                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                await using var contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(fullLocalPath, FileMode.Create);

                var buffer = new byte[81920];
                long downloaded = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));
                    downloaded += read;
                    if (totalBytes > 0)
                        DownloadProgress?.Invoke((double)downloaded / totalBytes);
                }
            }
        }

        public async Task<float[]?> GenerateEmbeddingAsync(string text, bool isQuery = false)
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("[MultiModelEmbedding] Not initialized");
                return null;
            }

            try
            {
                // Add prefix if required
                var processedText = PrepareText(text, isQuery);

                if (_session != null)
                {
                    return _currentModel.Id == "embeddinggemma-300m"
                        ? await GenerateGemmaEmbeddingAsync(processedText)
                        : await GenerateE5EmbeddingAsync(processedText);
                }

                return GenerateDemoEmbedding(text);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MultiModelEmbedding] Embedding generation failed");
                return GenerateDemoEmbedding(text);
            }
        }

        private string PrepareText(string text, bool isQuery)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            if (!_currentModel.RequiresQueryPrefix) return text;

            var prefix = isQuery ? _currentModel.QueryPrefix : _currentModel.PassagePrefix;
            if (text.StartsWith("query:") || text.StartsWith("passage:")) return text;
            return prefix + text;
        }

        private async Task<float[]> GenerateGemmaEmbeddingAsync(string text)
        {
            int[] tokenIds;
            
            if (_tokenizerReady && !string.IsNullOrEmpty(_tokenizerName))
            {
                // Use real tokenizer
                var ids = new int[512];
                var len = await TokenizerNative.EncodeNamedAsync(_tokenizerName, text, ids, 512);
                if (len > 0)
                {
                    tokenIds = ids.Take(len).ToArray();
                    _logger.LogDebug($"[Gemma] Tokenized {text.Length} chars -> {len} tokens");
                }
                else
                {
                    _logger.LogWarning($"[Gemma] Tokenizer returned {len}, using fallback");
                    tokenIds = FallbackTokenize(text);
                }
            }
            else
            {
                _logger.LogWarning("[Gemma] Tokenizer not ready, using fallback");
                tokenIds = FallbackTokenize(text);
            }

            // Pad/truncate to fixed length
            var seqLen = Math.Min(tokenIds.Length, 512);
            var inputIds = new long[seqLen];
            var attentionMask = new long[seqLen];
            
            for (int i = 0; i < seqLen; i++)
            {
                inputIds[i] = tokenIds[i];
                attentionMask[i] = tokenIds[i] != 0 ? 1L : 0L;
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(inputIds, new[] { 1, seqLen })),
                NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(attentionMask, new[] { 1, seqLen }))
            };

            using var results = _session!.Run(inputs);
            var embedding = results.Last().AsTensor<float>().ToArray();
            return NormalizeVector(embedding);
        }
        
        private int[] FallbackTokenize(string text)
        {
            // Hash-based fallback tokenization
            var tokens = new List<int> { 2 }; // BOS
            foreach (var c in text.Take(510))
                tokens.Add(((int)c * 31 + 17) % 250000 + 100);
            tokens.Add(1); // EOS
            return tokens.ToArray();
        }

        private async Task<float[]> GenerateE5EmbeddingAsync(string text)
        {
            int[] tokenIds;

            if (_tokenizerReady && !string.IsNullOrEmpty(_tokenizerName))
            {
                var ids = new int[256];
                var len = await TokenizerNative.EncodeNamedAsync(_tokenizerName, text, ids, 256);
                tokenIds = ids.Take(Math.Max(len, 1)).ToArray();
            }
            else
            {
                // Fallback hash tokenization
                tokenIds = text.Take(254).Select((c, i) => ((int)c * 31 + 17) % 30000 + 100).Prepend(101).Append(102).ToArray();
            }

            var inputIdsLong = tokenIds.Select(id => (long)id).ToArray();
            var attentionMask = inputIdsLong.Select(id => id != 0 ? 1L : 0L).ToArray();

            var padLen = 256;
            Array.Resize(ref inputIdsLong, padLen);
            Array.Resize(ref attentionMask, padLen);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(inputIdsLong, new[] { 1, padLen })),
                NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(attentionMask, new[] { 1, padLen }))
            };

            using var results = _session!.Run(inputs);
            var hiddenStates = results.First().AsTensor<float>();

            // Mean pooling
            var hiddenSize = hiddenStates.Dimensions[2];
            var seqLen = tokenIds.Length;
            var embedding = new float[hiddenSize];

            for (int i = 0; i < hiddenSize; i++)
            {
                float sum = 0;
                for (int j = 0; j < seqLen; j++)
                    sum += hiddenStates[0, j, i];
                embedding[i] = sum / seqLen;
            }

            return NormalizeVector(embedding);
        }

        private float[] GenerateDemoEmbedding(string text)
        {
            var dims = _currentModel.Dimensions;
            var embedding = new float[dims];
            var random = new Random(text.GetHashCode());

            for (int i = 0; i < dims; i++)
                embedding[i] = (float)(random.NextDouble() * 2 - 1);

            return NormalizeVector(embedding);
        }

        private float[] NormalizeVector(float[] vector)
        {
            var norm = (float)Math.Sqrt(vector.Sum(x => x * x));
            if (norm > 0)
                for (int i = 0; i < vector.Length; i++)
                    vector[i] /= norm;
            return vector;
        }

        public byte[] FloatArrayToBytes(float[] embedding)
        {
            if (embedding == null || embedding.Length == 0) return Array.Empty<byte>();
            var bytes = new byte[embedding.Length * sizeof(float)];
            Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public float[] BytesToFloatArray(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return Array.Empty<float>();
            var floatCount = bytes.Length / sizeof(float);
            var floats = new float[floatCount];
            Buffer.BlockCopy(bytes, 0, floats, 0, floatCount * sizeof(float));
            return floats;
        }

        public float CalculateSimilarity(float[] embedding1, float[] embedding2)
        {
            if (embedding1 == null || embedding2 == null || embedding1.Length != embedding2.Length)
                return 0f;

            float dot = 0;
            for (int i = 0; i < embedding1.Length; i++)
                dot += embedding1[i] * embedding2[i];

            return Math.Max(-1f, Math.Min(1f, dot));
        }

        public void Dispose()
        {
            _session?.Dispose();
            if (_tokenizerReady)
            {
                try { TokenizerNative.Cleanup(); } catch { }
            }
            _isInitialized = false;
            _logger.LogInformation("[MultiModelEmbedding] Disposed");
        }
    }
}
