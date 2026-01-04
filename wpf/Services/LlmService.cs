using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using WPFLLM.Models;

namespace WPFLLM.Services;

public class LlmService : ILlmService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISettingsService _settingsService;
    private readonly ILocalEmbeddingService _localEmbeddingService;
    private readonly IRateLimiter _rateLimiter;
    private readonly ILoggingService _logger;
    
    private const int MaxRetries = 3;
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];

    public LlmService(
        IHttpClientFactory httpClientFactory, 
        ISettingsService settingsService, 
        ILocalEmbeddingService localEmbeddingService,
        IRateLimiter rateLimiter,
        ILoggingService logger)
    {
        _httpClientFactory = httpClientFactory;
        _settingsService = settingsService;
        _localEmbeddingService = localEmbeddingService;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        List<ChatMessage> messages, 
        string? ragContext = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            yield return "[Error: API Key not configured. Please go to Settings.]";
            yield break;
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        var apiMessages = new List<object>();

        var systemPrompt = settings.SystemPrompt;
        if (!string.IsNullOrEmpty(ragContext))
        {
            systemPrompt += $"\n\nContext from knowledge base:\n{ragContext}";
        }

        apiMessages.Add(new { role = "system", content = systemPrompt });

        foreach (var msg in messages)
        {
            apiMessages.Add(new { role = msg.Role, content = msg.Content });
        }

        var requestBody = new
        {
            model = settings.Model,
            messages = apiMessages,
            temperature = settings.Temperature,
            max_tokens = settings.MaxTokens,
            stream = true
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var endpoint = settings.ApiEndpoint.TrimEnd('/') + "/chat/completions";

        // Rate limiting
        await _rateLimiter.WaitAsync(cancellationToken);
        _rateLimiter.RecordRequest();
        
        HttpResponseMessage? response = null;
        string? errorMessage = null;
        var stopwatch = Stopwatch.StartNew();
        
        // Retry logic with exponential backoff
        for (int retry = 0; retry <= MaxRetries; retry++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogApiCall(endpoint, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);
                    break;
                }
                
                // Retry on 429 (rate limit) or 5xx (server error)
                var statusCode = (int)response.StatusCode;
                if ((statusCode == 429 || statusCode >= 500) && retry < MaxRetries)
                {
                    _logger.LogWarning($"API returned {statusCode}, retrying ({retry + 1}/{MaxRetries})...");
                    await Task.Delay(RetryDelays[retry], cancellationToken);
                    continue;
                }
                
                break;
            }
            catch (HttpRequestException ex) when (retry < MaxRetries)
            {
                _logger.LogWarning($"Request failed: {ex.Message}, retrying ({retry + 1}/{MaxRetries})...");
                await Task.Delay(RetryDelays[retry], cancellationToken);
            }
            catch (Exception ex)
            {
                errorMessage = $"[Error: {ex.Message}]";
                _logger.LogError("API request failed", ex);
                break;
            }
        }

        if (errorMessage != null)
        {
            yield return errorMessage;
            yield break;
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            var errorBody = response != null ? await response.Content.ReadAsStringAsync(cancellationToken) : "No response";
            _logger.LogError($"API error: {response?.StatusCode} - {errorBody}");
            yield return $"[Error {response?.StatusCode}: {errorBody}]";
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;
            
            var data = line[6..];
            if (data == "[DONE]") break;

            var parsedContent = TryParseStreamChunk(data);
            if (parsedContent != null)
            {
                yield return parsedContent;
            }
        }
    }

    private static string? TryParseStreamChunk(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() > 0)
            {
                var delta = choices[0].GetProperty("delta");
                if (delta.TryGetProperty("content", out var contentElement))
                {
                    return contentElement.GetString();
                }
            }
        }
        catch
        {
            // Skip malformed JSON
        }
        return null;
    }

    public Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        => GetEmbeddingAsync(text, isQuery: true, cancellationToken);

    public async Task<float[]> GetEmbeddingAsync(string text, bool isQuery, CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync();

        // Use local embeddings if enabled and available
        if (settings.UseLocalEmbeddings)
        {
            try
            {
                if (!await _localEmbeddingService.IsAvailableAsync())
                {
                    await _localEmbeddingService.InitializeAsync(settings.LocalEmbeddingModel);
                }
                return await _localEmbeddingService.GetEmbeddingAsync(text, isQuery, cancellationToken);
            }
            catch
            {
                // Fall back to API if local fails
            }
        }

        // Use API embeddings
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return [];
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        var requestBody = new
        {
            model = "text-embedding-3-small",
            input = text
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var endpoint = settings.ApiEndpoint.TrimEnd('/') + "/embeddings";

        try
        {
            var response = await client.PostAsync(endpoint, content, cancellationToken);
            if (!response.IsSuccessStatusCode) return [];

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            
            var embeddingArray = doc.RootElement
                .GetProperty("data")[0]
                .GetProperty("embedding");

            var embedding = new float[embeddingArray.GetArrayLength()];
            int i = 0;
            foreach (var val in embeddingArray.EnumerateArray())
            {
                embedding[i++] = val.GetSingle();
            }
            return embedding;
        }
        catch
        {
            return [];
        }
    }
}
