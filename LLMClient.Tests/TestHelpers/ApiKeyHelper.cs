namespace LLMClient.Tests.TestHelpers;

/// <summary>
/// Helper do ładowania API keys z bezpiecznych lokalizacji poza repozytorium Git.
/// Klucze są przechowywane w ~/.llmclient/ 
/// </summary>
public static class ApiKeyHelper
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
        ".llmclient");

    /// <summary>
    /// Pobiera OpenRouter API key z pliku lub zmiennej środowiskowej
    /// </summary>
    public static string? GetOpenRouterApiKey()
    {
        // 1. Sprawdź zmienną środowiskową
        var envKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        if (!string.IsNullOrEmpty(envKey))
            return envKey;

        // 2. Sprawdź plik w katalogu użytkownika
        var keyFile = Path.Combine(ConfigDirectory, "openrouter_api_key.txt");
        if (File.Exists(keyFile))
        {
            return File.ReadAllText(keyFile).Trim();
        }

        return null;
    }

    /// <summary>
    /// Sprawdza czy API key jest dostępny
    /// </summary>
    public static bool HasOpenRouterApiKey()
    {
        return !string.IsNullOrEmpty(GetOpenRouterApiKey());
    }

    /// <summary>
    /// Pobiera dowolny klucz API z pliku
    /// </summary>
    public static string? GetApiKey(string keyName)
    {
        var envKey = Environment.GetEnvironmentVariable(keyName.ToUpperInvariant().Replace("-", "_"));
        if (!string.IsNullOrEmpty(envKey))
            return envKey;

        var keyFile = Path.Combine(ConfigDirectory, $"{keyName}.txt");
        if (File.Exists(keyFile))
        {
            return File.ReadAllText(keyFile).Trim();
        }

        return null;
    }
}
