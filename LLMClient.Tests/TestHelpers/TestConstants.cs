namespace LLMClient.Tests.TestHelpers;

/// <summary>
/// Constants used across integration tests
/// </summary>
public static class TestConstants
{
    public const string OpenRouterBaseUrl = "https://openrouter.ai/api/v1";
    public const string TestModel = "meta-llama/llama-3.2-3b-instruct:free";
    public const string TestModelFast = "meta-llama/llama-3.2-1b-instruct:free";
    
    public const int DefaultTimeoutSeconds = 60;
    public const int ShortTimeoutSeconds = 30;
    
    public static readonly string[] TestPrompts = new[]
    {
        "Powiedz 'OK' jednym słowem.",
        "Ile to jest 2+2? Odpowiedz tylko liczbą.",
        "Jaki jest dzień tygodnia po poniedziałku? Odpowiedz jednym słowem."
    };
}
