using System.Globalization;
using System.Text.Json.Serialization;

namespace WPFLLM.Models;

public class OpenRouterModelsResponse
{
    [JsonPropertyName("data")]
    public List<OpenRouterModel> Data { get; set; } = [];
}

public class OpenRouterModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("context_length")]
    public int ContextLength { get; set; }

    [JsonPropertyName("pricing")]
    public OpenRouterPricing? Pricing { get; set; }

    [JsonPropertyName("architecture")]
    public OpenRouterArchitecture? Architecture { get; set; }

    [JsonPropertyName("top_provider")]
    public OpenRouterTopProvider? TopProvider { get; set; }

    public string Provider => Id.Contains('/') ? Id.Split('/')[0] : "unknown";
    
    public string DisplayName => Name;
    
    public string ContextInfo => $"{ContextLength:N0} tokens";

    public string PricingInfo
    {
        get
        {
            if (Pricing == null) return "Free";
            var promptPrice = decimal.Parse(Pricing.Prompt, CultureInfo.InvariantCulture) * 1000000;
            var completionPrice = decimal.Parse(Pricing.Completion, CultureInfo.InvariantCulture) * 1000000;
            if (promptPrice == 0 && completionPrice == 0) return "Free";
            return $"${promptPrice:F2} / ${completionPrice:F2} per 1M tokens";
        }
    }

    public string DirectApiId
    {
        get
        {
            // Convert OpenRouter ID to direct API model ID
            // e.g., "openai/gpt-4o" -> "gpt-4o"
            // e.g., "anthropic/claude-3-opus" -> "claude-3-opus"
            // e.g., "google/gemini-pro" -> "gemini-pro"
            if (Id.Contains('/'))
            {
                var parts = Id.Split('/', 2);
                return parts.Length > 1 ? parts[1] : Id;
            }
            return Id;
        }
    }
}

public class OpenRouterTopProvider
{
    [JsonPropertyName("context_length")]
    public int? ContextLength { get; set; }

    [JsonPropertyName("max_completion_tokens")]
    public int? MaxCompletionTokens { get; set; }

    [JsonPropertyName("is_moderated")]
    public bool? IsModerated { get; set; }
}

public class OpenRouterPricing
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "0";

    [JsonPropertyName("completion")]
    public string Completion { get; set; } = "0";

    [JsonPropertyName("request")]
    public string Request { get; set; } = "0";

    [JsonPropertyName("image")]
    public string Image { get; set; } = "0";
}

public class OpenRouterArchitecture
{
    [JsonPropertyName("modality")]
    public string Modality { get; set; } = string.Empty;

    [JsonPropertyName("tokenizer")]
    public string Tokenizer { get; set; } = string.Empty;

    [JsonPropertyName("instruct_type")]
    public string? InstructType { get; set; }
}
