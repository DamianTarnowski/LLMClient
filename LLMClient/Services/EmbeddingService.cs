using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Numerics.Tensors;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
// using Microsoft.ML.Tokenizers; // usunięto – nieużywane po przejściu na Rust FFI
using System.IO;
// UWAGA: Wymaga natywnego tokenizera Rust (TokenizerNative)

namespace LLMClient.Services
{
    public interface IEmbeddingService
    {
        Task InitializeAsync();
        Task<float[]?> GenerateEmbeddingAsync(string text, bool isQuery = false);
        byte[] FloatArrayToBytes(float[] embedding);
        float[] BytesToFloatArray(byte[] bytes);
        float CalculateSimilarity(float[] embedding1, float[] embedding2);
        string ModelVersion { get; }
        bool IsInitialized { get; }
        event Action<double> DownloadProgress;
    }

    public class EmbeddingService : IEmbeddingService, IDisposable
    {
        // Model configuration constants
        private const string MODEL_BASE_URL = "https://huggingface.co/intfloat/multilingual-e5-large/resolve/main/";
        private const string MODEL_ONNX_FILE = "onnx/model.onnx";
        private const string MODEL_ONNX_DATA_FILE = "onnx/model.onnx_data";
        private const string TOKENIZER_FILE = "tokenizer.json";
        private const string SPECIAL_TOKENS_FILE = "special_tokens_map.json";
        private const string CONFIG_FILE = "config.json";
        // MaxSequenceLength: 256 is a balanced value for BERT-like models to avoid OOM on mobile devices while handling most sentences
        private const int MaxSequenceLength = 256;
        private const int EmbeddingDimensions = 1024;
        private InferenceSession? _session;
        private readonly ILogger<EmbeddingService> _logger;
        private bool _isInitialized = false;
        private Dictionary<string, int> _vocabulary = new();
        private readonly Dictionary<string, int> _specialTokens = new()
        {
            { "[PAD]", 0 },
            { "[UNK]", 100 },
            { "[CLS]", 101 },
            { "[SEP]", 102 }
        };
        private string? _tokenizerPath;
        private bool _tokenizerReady = false; // flag set po udanym TokenizerNative.InitAsync

        // E5-base zwraca 768-wymiarowy wektor – wersja v3 wymusza ponowną generację.
        public string ModelVersion => "intfloat-e5-large-multilingual-v1";
        public bool IsInitialized => _isInitialized;
        public event Action<double> DownloadProgress;

        public EmbeddingService(ILogger<EmbeddingService> logger)
        {
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Inicjalizacja EmbeddingService...");
                // Load ONNX model
                await LoadModelAsync();
                _logger.LogInformation($"Tryb modelu: {( _session != null ? "REAL (ONNX)" : "DEMO (brak modelu)" )}");
                // Initialize tokenizer
                if (!string.IsNullOrEmpty(_tokenizerPath))
                {
                    await InitializeTokenizerAsync(_tokenizerPath);
                }
                else
                {
                    _logger.LogWarning("Brak ścieżki do tokenizer.json ‑ pomijam inicjalizację tokenizera, przechodzę w tryb demo.");
                }
                _isInitialized = true;
                _logger.LogInformation("EmbeddingService zainicjalizowany pomyślnie");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas inicjalizacji EmbeddingService");
                throw;
            }
        }

        private async Task LoadModelAsync()
        {
            try
            {
                // Try to download and load the actual model
                var modelPath = await DownloadModelIfNeededAsync();
                
                if (File.Exists(modelPath))
                {
                    _logger.LogInformation($"Ładowanie modelu ONNX z: {modelPath}");
                    _session = new InferenceSession(modelPath);
                    _logger.LogInformation("Model ONNX załadowany pomyślnie");
                }
                else
                {
                    _logger.LogWarning("Model ONNX nie został znaleziony, używam trybu demo");
                    // W trybie demo nie tworzymy sesji, będziemy generować dummy embeddings
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania modelu ONNX, przechodzę na tryb demo");
                // W przypadku błędu, będziemy działać w trybie demo
                _session = null;
            }
        }

        private async Task<string> DownloadModelIfNeededAsync()
        {
            _logger.LogInformation("DownloadModelIfNeededAsync rozpoczęty");
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var modelsDir = Path.Combine(appData, "User Name", "com.companyname.llmclient", "Data", "models", ModelVersion);
            Directory.CreateDirectory(modelsDir);
            
            var modelPath = Path.Combine(modelsDir, "model.onnx");
            // Dodatkowy plik z wagami dla dużych modeli (>2GB) – ONNX zewnętrzne dane
            var modelExternalDataPath = Path.Combine(modelsDir, "model.onnx_data");
            var tokenizerJsonPath = Path.Combine(modelsDir, "tokenizer.json");
            var specialTokensPath = Path.Combine(modelsDir, "special_tokens_map.json");
            var configPath = Path.Combine(modelsDir, "config.json");
            
            // Pobierz model ONNX
            if (!File.Exists(modelPath))
            {
                _logger.LogInformation("Pobieranie modelu e5-base-multilingual...");
                try
                {
                    var modelUrl = "https://huggingface.co/intfloat/multilingual-e5-large/resolve/main/onnx/model.onnx";
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromMinutes(10);
                    _logger.LogInformation("Rozpoczynam pobieranie modelu (może potrwać kilka minut)...");
                    var modelBytes = await httpClient.GetByteArrayAsync(modelUrl);
                    await File.WriteAllBytesAsync(modelPath, modelBytes);
                    _logger.LogInformation($"Model pobrany i zapisany w: {modelPath} ({modelBytes.Length / 1024 / 1024} MB)");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się pobrać modelu");
                    throw;
                }
            }
            else
            {
                _logger.LogInformation($"Model już istnieje: {modelPath}");
            }

            // Pobierz model.onnx_data – wymagany przy dużych modelach zewnętrznych wag
            if (!File.Exists(modelExternalDataPath))
            {
                _logger.LogInformation("Pobieranie model.onnx_data (zewnętrzne wagi)...");
                try
                {
                    var externalUrl = "https://huggingface.co/intfloat/multilingual-e5-large/resolve/main/onnx/model.onnx_data";
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromMinutes(60); // większy timeout – plik ~1,5-2 GB
                    using var response = await httpClient.GetAsync(externalUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    await using var fs = new FileStream(modelExternalDataPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                    await using var httpStream = await response.Content.ReadAsStreamAsync();
                    var buffer = new byte[81920];
                    long downloaded = 0;
                    int read;
                    while ((read = await httpStream.ReadAsync(buffer)) > 0)
                    {
                        await fs.WriteAsync(buffer.AsMemory(0, read));
                        downloaded += read;
                        if (totalBytes > 0 && downloaded % (100 * 1024 * 1024) < 81920) // co ~100 MB
                        {
                            _logger.LogInformation($"Pobrano {downloaded / 1024 / 1024} / {totalBytes / 1024 / 1024} MB model.onnx_data...");
                            DownloadProgress?.Invoke((double)downloaded / totalBytes);
                        }
                    }
                    _logger.LogInformation($"model.onnx_data pobrany i zapisany w: {modelExternalDataPath} ({downloaded / 1024 / 1024} MB)");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się pobrać model.onnx_data – model może nie zostać załadowany");
                }
            }
            // Pobierz tokenizer.json
            if (!File.Exists(tokenizerJsonPath))
            {
                _logger.LogInformation("Pobieranie tokenizer.json...");
                try
                {
                    var tokenizerUrl = "https://huggingface.co/intfloat/multilingual-e5-large/resolve/main/tokenizer.json";
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromMinutes(5);
                    var tokenizerBytes = await httpClient.GetByteArrayAsync(tokenizerUrl);
                    await File.WriteAllBytesAsync(tokenizerJsonPath, tokenizerBytes);
                    _logger.LogInformation($"tokenizer.json pobrany i zapisany w: {tokenizerJsonPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się pobrać tokenizer.json");
                }
            }
            // Pobierz special_tokens_map.json
            if (!File.Exists(specialTokensPath))
            {
                _logger.LogInformation("Pobieranie special_tokens_map.json...");
                try
                {
                    var url = "https://huggingface.co/intfloat/multilingual-e5-large/resolve/main/special_tokens_map.json";
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromMinutes(2);
                    var bytes = await httpClient.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(specialTokensPath, bytes);
                    _logger.LogInformation($"special_tokens_map.json pobrany i zapisany w: {specialTokensPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się pobrać special_tokens_map.json");
                }
            }
            // Pobierz config.json
            if (!File.Exists(configPath))
            {
                _logger.LogInformation("Pobieranie config.json...");
                try
                {
                    var url = "https://huggingface.co/intfloat/multilingual-e5-large/resolve/main/config.json";
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromMinutes(2);
                    var bytes = await httpClient.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(configPath, bytes);
                    _logger.LogInformation($"config.json pobrany i zapisany w: {configPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się pobrać config.json");
                }
            }
            // Ustaw ścieżkę do tokenizer-a
            if (File.Exists(tokenizerJsonPath))
                _tokenizerPath = tokenizerJsonPath;
            else
                _tokenizerPath = null;
            return modelPath;
        }

        private void InitializeVocabulary()
        {
            // Niepotrzebne przy prawdziwym tokenizerze
            _vocabulary.Clear();
        }

        private async Task InitializeTokenizerAsync(string tokenizerPath)
        {
            if (string.IsNullOrEmpty(tokenizerPath))
            {
                _logger.LogWarning("InitializeTokenizerAsync wywołane z pustą ścieżką – pomijam.");
                return;
            }

            var result = await TokenizerNative.InitAsync(tokenizerPath);
            if (result != 0)
                _logger.LogError($"Błąd inicjalizacji tokenizera Rust: kod {result}");
            else
            {
                _tokenizerReady = true;
                _logger.LogInformation($"Załadowano natywny tokenizer Rust z: {tokenizerPath}");
            }
        }

        public async Task<float[]?> GenerateEmbeddingAsync(string text, bool isQuery = false)
        {
            text = PrepareE5Text(text, isQuery);
            _logger.LogDebug($"GenerateEmbeddingAsync start | text='{text}' | mode={( _session!=null?"REAL":"DEMO" )}");
            if (!_isInitialized)
            {
                _logger.LogWarning("EmbeddingService nie jest zainicjalizowany");
                return null;
            }

            try
            {
                _logger.LogDebug($"Generowanie embeddingu dla tekstu: {text[..Math.Min(50, text.Length)]}...");
                
                if (_session != null)
                {
                    // Use real ONNX model
                    return await GenerateRealEmbeddingAsync(text);
                }
                else
                {
                    // Use demo embedding (deterministic dummy based on text)
                    return GenerateDemoEmbedding(text);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Błąd podczas generowania embeddingu dla tekstu: {text[..Math.Min(50, text.Length)]}...");
                return null;
            }
        }

        private async Task<float[]?> GenerateRealEmbeddingAsync(string text)
        {
            try
            {
                // Tokenizuj tekst przez natywny Rust FFI
                if (!_tokenizerReady)
                {
                    _logger.LogWarning("Tokenizer Rust nie jest gotowy – fallback do demo embeddingu");
                    return GenerateDemoEmbedding(text);
                }
                var ids = await TokenizeTextAsync(text, MaxSequenceLength);
                var tokenIds = ids.Select(id => (long)id).ToArray();
                // Przygotuj input_ids i attention_mask
                var inputIds = new DenseTensor<long>(tokenIds, new[] { 1, tokenIds.Length });
                var attentionMask = new DenseTensor<long>(
                    tokenIds.Select(id => id != 0 ? 1L : 0L).ToArray(), // 0 to zwykle [PAD]
                    new[] { 1, tokenIds.Length }
                );
                // token_type_ids nie wszystkie modele przewidują – sprawdź metadane
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
                    NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
                };

                if (_session!.InputMetadata.ContainsKey("token_type_ids"))
                {
                    var tokenTypeIds = new DenseTensor<long>(
                        new long[tokenIds.Length],
                        new[] { 1, tokenIds.Length }
                    );
                    inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds));
                }
                using var results = _session!.Run(inputs);
                var lastHiddenStates = results.First().AsTensor<float>();
                var embedding = MeanPooling(lastHiddenStates, attentionMask);
                var normalizedEmbedding = NormalizeVector(embedding);
                await Task.Delay(1);
                return normalizedEmbedding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas przetwarzania z modelem ONNX");
                return GenerateDemoEmbedding(text);
            }
        }

        private float[] GenerateDemoEmbedding(string text)
        {
            // Create a deterministic dummy embedding based on text content
            _logger.LogDebug("Generowanie demo embeddingu");
            
            var embedding = new float[EmbeddingDimensions];
            var random = new Random(text.GetHashCode()); // Deterministic based on text
            
            // Generate based on text characteristics
            var textLength = text.Length;
            var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var upperCount = text.Count(char.IsUpper);
            
            for (int i = 0; i < EmbeddingDimensions; i++)
            {
                // Mix random values with text characteristics
                var baseValue = (float)(random.NextDouble() * 2.0 - 1.0);
                var textInfluence = (float)Math.Sin(i * textLength * 0.01) * 0.1f;
                var wordInfluence = (float)Math.Cos(i * wordCount * 0.02) * 0.1f;
                var caseInfluence = (float)Math.Sin(i * upperCount * 0.03) * 0.05f;
                
                embedding[i] = baseValue + textInfluence + wordInfluence + caseInfluence;
            }
            
            // Normalize the embedding
            return NormalizeVector(embedding);
        }

        private string PrepareE5Text(string text, bool isQuery)
        {
            // Dla modeli E5 należy dodać prefix "query: " lub "passage: "
            if (string.IsNullOrWhiteSpace(text)) return text;
            var prefix = isQuery ? "query: " : "passage: ";
            // Unikaj podwójnego dodawania prefiksu
            if (text.StartsWith("query:") || text.StartsWith("passage:")) return text;
            return prefix + text;
        }

        // Tokenizacja tekstu przez natywny Rust FFI
        private async Task<int[]> TokenizeTextAsync(string text, int maxLen)
        {
            _logger.LogDebug($"TokenizeTextAsync | text='{text}' (len={text.Length}) | maxLen={maxLen}");
            int[] ids = new int[maxLen];
            int len = await TokenizerNative.EncodeAsync(text, ids, maxLen);
            if (len < 0)
            {
                _logger.LogError($"Błąd tokenizacji przez Rust FFI: kod {len}");
                return Array.Empty<int>();
            }
            _logger.LogDebug($"TokenizeTextAsync | encodedLen={len} | firstIds={string.Join(',', ids.Take(Math.Min(10, len)))}");
            return ids.Take(len).ToArray();
        }

        private float[] MeanPooling(Microsoft.ML.OnnxRuntime.Tensors.Tensor<float> hiddenStates, DenseTensor<long> attentionMask)
        {
            var batchSize = hiddenStates.Dimensions[0];
            var seqLength = hiddenStates.Dimensions[1];
            var hiddenSize = hiddenStates.Dimensions[2];
            
            var embedding = new float[hiddenSize];
            var sumMask = 0f;

            for (int i = 0; i < seqLength; i++)
            {
                if (attentionMask[0, i] == 1)
                {
                    for (int j = 0; j < hiddenSize; j++)
                    {
                        embedding[j] += hiddenStates[0, i, j];
                    }
                    sumMask += 1f;
                }
            }

            // Average
            if (sumMask > 0)
            {
                for (int i = 0; i < hiddenSize; i++)
                {
                    embedding[i] /= sumMask;
                }
            }

            return embedding;
        }

        private float[] NormalizeVector(float[] vector)
        {
            var norm = (float)Math.Sqrt(vector.Sum(x => x * x));
            if (norm > 0)
            {
                for (int i = 0; i < vector.Length; i++)
                {
                    vector[i] /= norm;
                }
            }
            return vector;
        }

            public byte[] FloatArrayToBytes(float[] embedding)
    {
        if (embedding == null || embedding.Length == 0)
            return new byte[0];
            
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

            public float[] BytesToFloatArray(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return new float[0];
            
        // Handle invalid byte length gracefully
        var floatCount = bytes.Length / sizeof(float);
        if (floatCount == 0)
            return new float[0];
            
        var floats = new float[floatCount];
        var bytesToCopy = floatCount * sizeof(float);
        Buffer.BlockCopy(bytes, 0, floats, 0, bytesToCopy);
        return floats;
    }

            public float CalculateSimilarity(float[] embedding1, float[] embedding2)
    {
        if (embedding1 == null || embedding2 == null)
            return 0.0f;
            
        if (embedding1.Length == 0 || embedding2.Length == 0)
            return 0.0f;
            
        if (embedding1.Length != embedding2.Length)
            return 0.0f;

            // Cosine similarity (vectors should already be normalized)
            var dotProduct = 0f;
            for (int i = 0; i < embedding1.Length; i++)
            {
                dotProduct += embedding1[i] * embedding2[i];
            }
            
            return Math.Max(-1f, Math.Min(1f, dotProduct)); // Clamp to [-1, 1]
        }

        public void Dispose()
        {
            _session?.Dispose();
            TokenizerNative.Cleanup();
            _isInitialized = false;
            _logger.LogInformation("EmbeddingService disposed, Rust tokenizer cleaned up.");
        }
    }
} 
