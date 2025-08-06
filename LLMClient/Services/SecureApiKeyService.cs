namespace LLMClient.Services
{
    public interface ISecureApiKeyService
    {
        Task<string?> GetApiKeyAsync(int modelId);
        Task SetApiKeyAsync(int modelId, string apiKey);
        Task DeleteApiKeyAsync(int modelId);
        Task<bool> HasApiKeyAsync(int modelId);
    }

    public class SecureApiKeyService : ISecureApiKeyService
    {
        private const string API_KEY_PREFIX = "api_key_";

        public async Task<string?> GetApiKeyAsync(int modelId)
        {
            try
            {
                var key = $"{API_KEY_PREFIX}{modelId}";
                return await SecureStorage.GetAsync(key);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting API key for model {modelId}: {ex.Message}");
                return null;
            }
        }

        public async Task SetApiKeyAsync(int modelId, string apiKey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    await DeleteApiKeyAsync(modelId);
                    return;
                }

                var key = $"{API_KEY_PREFIX}{modelId}";
                await SecureStorage.SetAsync(key, apiKey);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting API key for model {modelId}: {ex.Message}");
                throw new InvalidOperationException($"Nie udało się zapisać API Key: {ex.Message}");
            }
        }

        public async Task DeleteApiKeyAsync(int modelId)
        {
            try
            {
                var key = $"{API_KEY_PREFIX}{modelId}";
                SecureStorage.Remove(key);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting API key for model {modelId}: {ex.Message}");
            }
        }

        public async Task<bool> HasApiKeyAsync(int modelId)
        {
            var apiKey = await GetApiKeyAsync(modelId);
            return !string.IsNullOrWhiteSpace(apiKey);
        }

        /// <summary>
        /// Migruje API keys z SQLite do SecureStorage
        /// </summary>
        public async Task MigrateApiKeysFromDatabase(List<LLMClient.Models.AiModel> models)
        {
            foreach (var model in models)
            {
                if (!string.IsNullOrWhiteSpace(model.ApiKey))
                {
                    await SetApiKeyAsync(model.Id, model.ApiKey);
                    // Wyczyść API key z modelu po migracji
                    model.ApiKey = string.Empty;
                }
            }
        }
    }
} 