using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace LLMClient.Services;

public enum EmbeddingProvider
{
    OpenAI,
    Cohere,
    Voyage,
    Jina
}

public class ApiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiEmbeddingService>? _logger;
    
    private EmbeddingProvider _provider = EmbeddingProvider.OpenAI;
    private string _modelName = "text-embedding-3-small";
    private int _dimensions = 1536;
    private bool _isInitialized = false;

    public string ModelVersion => $"API:{_provider}:{_modelName}";
    public bool IsInitialized => _isInitialized;
    public EmbeddingProvider CurrentProvider => _provider;
    public string CurrentModel => _modelName;
    
    public event Action<double>? DownloadProgress;

    public ApiEmbeddingService(ILogger<ApiEmbeddingService>? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient();
    }

    public void Configure(EmbeddingProvider provider, string modelName, int dimensions = 0)
    {
        _provider = provider;
        _modelName = modelName;
        _dimensions = dimensions > 0 ? dimensions : GetDefaultDimensions(provider, modelName);
        _isInitialized = false;
    }

    private static int GetDefaultDimensions(EmbeddingProvider provider, string model) => provider switch
    {
        EmbeddingProvider.OpenAI => model switch
        {
            "text-embedding-3-small" => 1536,
            "text-embedding-3-large" => 3072,
            "text-embedding-ada-002" => 1536,
            _ => 1536
        },
        EmbeddingProvider.Cohere => model switch
        {
            "embed-english-v3.0" => 1024,
            "embed-multilingual-v3.0" => 1024,
            "embed-english-light-v3.0" => 384,
            "embed-multilingual-light-v3.0" => 384,
            _ => 1024
        },
        EmbeddingProvider.Voyage => 1024,
        EmbeddingProvider.Jina => 768,
        _ => 1536
    };

    public async Task InitializeAsync()
    {
        try
        {
            var apiKey = await GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger?.LogWarning("[ApiEmbeddingService] No API key configured for {Provider}", _provider);
                _isInitialized = false;
                return;
            }

            _isInitialized = true;
            _logger?.LogInformation("[ApiEmbeddingService] Initialized with {Provider}:{Model}", _provider, _modelName);
            DownloadProgress?.Invoke(1.0);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[ApiEmbeddingService] Failed to initialize");
            _isInitialized = false;
        }
    }

    public Task<bool> IsModelDownloadedAsync() => Task.FromResult(true);

    public async Task<float[]?> GenerateEmbeddingAsync(string text, bool isQuery = false)
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
            if (!_isInitialized) return null;
        }

        try
        {
            return _provider switch
            {
                EmbeddingProvider.OpenAI => await GenerateOpenAIEmbeddingAsync(text),
                EmbeddingProvider.Cohere => await GenerateCohereEmbeddingAsync(text, isQuery),
                EmbeddingProvider.Voyage => await GenerateVoyageEmbeddingAsync(text, isQuery),
                EmbeddingProvider.Jina => await GenerateJinaEmbeddingAsync(text),
                _ => throw new NotSupportedException($"Provider {_provider} not supported")
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[ApiEmbeddingService] Failed to generate embedding");
            return null;
        }
    }

    private async Task<float[]?> GenerateOpenAIEmbeddingAsync(string text)
    {
        var apiKey = await GetApiKeyAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        
        var payload = new
        {
            input = text,
            model = _modelName,
            dimensions = _dimensions
        };
        
        request.Content = JsonContent.Create(payload);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>();
        return result?.Data?.FirstOrDefault()?.Embedding;
    }

    private async Task<float[]?> GenerateCohereEmbeddingAsync(string text, bool isQuery)
    {
        var apiKey = await GetApiKeyAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.cohere.ai/v1/embed");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        
        var payload = new
        {
            texts = new[] { text },
            model = _modelName,
            input_type = isQuery ? "search_query" : "search_document",
            truncate = "END"
        };
        
        request.Content = JsonContent.Create(payload);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<CohereEmbeddingResponse>();
        return result?.Embeddings?.FirstOrDefault();
    }

    private async Task<float[]?> GenerateVoyageEmbeddingAsync(string text, bool isQuery)
    {
        var apiKey = await GetApiKeyAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.voyageai.com/v1/embeddings");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        
        var payload = new
        {
            input = new[] { text },
            model = _modelName,
            input_type = isQuery ? "query" : "document"
        };
        
        request.Content = JsonContent.Create(payload);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<VoyageEmbeddingResponse>();
        return result?.Data?.FirstOrDefault()?.Embedding;
    }

    private async Task<float[]?> GenerateJinaEmbeddingAsync(string text)
    {
        var apiKey = await GetApiKeyAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.jina.ai/v1/embeddings");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        
        var payload = new
        {
            input = new[] { text },
            model = _modelName
        };
        
        request.Content = JsonContent.Create(payload);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<JinaEmbeddingResponse>();
        return result?.Data?.FirstOrDefault()?.Embedding;
    }

    private async Task<string?> GetApiKeyAsync()
    {
        var keyName = _provider switch
        {
            EmbeddingProvider.OpenAI => "embedding_openai_key",
            EmbeddingProvider.Cohere => "embedding_cohere_key",
            EmbeddingProvider.Voyage => "embedding_voyage_key",
            EmbeddingProvider.Jina => "embedding_jina_key",
            _ => null
        };
        
        if (keyName == null) return null;
        return await SecureStorage.GetAsync(keyName);
    }
    
    public async Task SetApiKeyAsync(string apiKey)
    {
        var keyName = _provider switch
        {
            EmbeddingProvider.OpenAI => "embedding_openai_key",
            EmbeddingProvider.Cohere => "embedding_cohere_key",
            EmbeddingProvider.Voyage => "embedding_voyage_key",
            EmbeddingProvider.Jina => "embedding_jina_key",
            _ => null
        };
        
        if (keyName != null)
        {
            await SecureStorage.SetAsync(keyName, apiKey);
            _isInitialized = false; // Reset to re-initialize with new key
        }
    }

    public byte[] FloatArrayToBytes(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public float[] BytesToFloatArray(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    public float CalculateSimilarity(float[] embedding1, float[] embedding2)
    {
        if (embedding1.Length != embedding2.Length)
            return 0f;

        float dotProduct = 0f;
        float norm1 = 0f;
        float norm2 = 0f;

        for (int i = 0; i < embedding1.Length; i++)
        {
            dotProduct += embedding1[i] * embedding2[i];
            norm1 += embedding1[i] * embedding1[i];
            norm2 += embedding2[i] * embedding2[i];
        }

        var magnitude = MathF.Sqrt(norm1) * MathF.Sqrt(norm2);
        return magnitude > 0 ? dotProduct / magnitude : 0f;
    }

    // Response DTOs
    private class OpenAIEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<OpenAIEmbeddingData>? Data { get; set; }
    }

    private class OpenAIEmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }

    private class CohereEmbeddingResponse
    {
        [JsonPropertyName("embeddings")]
        public List<float[]>? Embeddings { get; set; }
    }

    private class VoyageEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<VoyageEmbeddingData>? Data { get; set; }
    }

    private class VoyageEmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }

    private class JinaEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<JinaEmbeddingData>? Data { get; set; }
    }

    private class JinaEmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
