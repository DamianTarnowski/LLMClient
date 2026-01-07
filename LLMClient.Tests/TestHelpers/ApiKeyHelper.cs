namespace LLMClient.Tests.TestHelpers;

/// <summary>
/// Helper class for loading API keys securely from user profile
/// </summary>
public static class ApiKeyHelper
{
    private static readonly string ApiKeyDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".llmclient");

    /// <summary>
    /// Gets the OpenRouter API key from secure storage
    /// </summary>
    public static string? GetOpenRouterApiKey()
    {
        var keyPath = Path.Combine(ApiKeyDirectory, "openrouter_api_key.txt");
        if (File.Exists(keyPath))
        {
            return File.ReadAllText(keyPath).Trim();
        }
        return Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
    }

    /// <summary>
    /// Checks if OpenRouter API key is available
    /// </summary>
    public static bool HasOpenRouterApiKey()
    {
        var key = GetOpenRouterApiKey();
        return !string.IsNullOrEmpty(key) && key.StartsWith("sk-or-");
    }

    /// <summary>
    /// Gets any API key by provider name
    /// </summary>
    public static string? GetApiKey(string provider)
    {
        var keyPath = Path.Combine(ApiKeyDirectory, $"{provider.ToLower()}_api_key.txt");
        if (File.Exists(keyPath))
        {
            return File.ReadAllText(keyPath).Trim();
        }
        return Environment.GetEnvironmentVariable($"{provider.ToUpper()}_API_KEY");
    }
}
