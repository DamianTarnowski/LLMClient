using System.Net.Http;
using System.Text.Json;
using WPFLLM.Models;

namespace WPFLLM.Services;

public class OpenRouterService : IOpenRouterService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private List<OpenRouterModel>? _cachedModels;
    private DateTime _cacheTime;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public OpenRouterService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<OpenRouterModel>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedModels != null && DateTime.UtcNow - _cacheTime < CacheDuration)
        {
            return _cachedModels;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync("https://openrouter.ai/api/v1/models", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return _cachedModels ?? [];
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<OpenRouterModelsResponse>(json);

            if (result?.Data != null)
            {
                _cachedModels = result.Data
                    .Where(m => m.Architecture?.Modality?.Contains("text") == true)
                    .OrderBy(m => m.Provider)
                    .ThenBy(m => m.Name)
                    .ToList();
                _cacheTime = DateTime.UtcNow;
            }

            return _cachedModels ?? [];
        }
        catch
        {
            return _cachedModels ?? [];
        }
    }
}
