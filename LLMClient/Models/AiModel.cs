using SQLite;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.Frozen;

namespace LLMClient.Models
{
    public enum AiProvider
    {
        OpenAI,
        Gemini,
        OpenAICompatible,
        LocalModel,
        Anthropic,
        Mistral,
        Groq,
        Together,
        Fireworks,
        DeepSeek,
        xAI,
        SambaNova,
        Perplexity,
        Cohere,
        OpenRouter
    }

    public record ApiProviderInfo(string Name, string Endpoint, string Description, string KeyUrl);

    public static class ApiProviders
    {
        /// <summary>
        /// FrozenDictionary (.NET 8+) - niezmienne, zoptymalizowane pod kątem odczytu
        /// Idealne dla statycznych danych konfiguracyjnych
        /// </summary>
        public static readonly FrozenDictionary<AiProvider, ApiProviderInfo> ProviderInfo = new Dictionary<AiProvider, ApiProviderInfo>
        {
            [AiProvider.OpenAI] = new("OpenAI", "https://api.openai.com/v1", "GPT-4o, GPT-4, o1, o3", "https://platform.openai.com/api-keys"),
            [AiProvider.Anthropic] = new("Anthropic", "https://api.anthropic.com/v1", "Claude 3.5/4 Sonnet, Opus, Haiku", "https://console.anthropic.com/"),
            [AiProvider.Gemini] = new("Google AI", "https://generativelanguage.googleapis.com/v1beta", "Gemini 2.0/1.5 Pro, Flash", "https://aistudio.google.com/apikey"),
            [AiProvider.Mistral] = new("Mistral", "https://api.mistral.ai/v1", "Mistral Large, Medium, Small", "https://console.mistral.ai/api-keys"),
            [AiProvider.Groq] = new("Groq", "https://api.groq.com/openai/v1", "Llama 3, Mixtral (ultra-szybkie)", "https://console.groq.com/keys"),
            [AiProvider.Together] = new("Together AI", "https://api.together.xyz/v1", "Llama, Qwen, DeepSeek", "https://api.together.ai/settings/api-keys"),
            [AiProvider.Fireworks] = new("Fireworks AI", "https://api.fireworks.ai/inference/v1", "Llama, Mixtral, DeepSeek", "https://fireworks.ai/account/api-keys"),
            [AiProvider.DeepSeek] = new("DeepSeek", "https://api.deepseek.com/v1", "DeepSeek V3, R1 Reasoner", "https://platform.deepseek.com/api_keys"),
            [AiProvider.xAI] = new("xAI (Grok)", "https://api.x.ai/v1", "Grok 2, Grok 3", "https://console.x.ai/"),
            [AiProvider.SambaNova] = new("SambaNova", "https://api.sambanova.ai/v1", "Llama (ultra-szybkie)", "https://cloud.sambanova.ai/apis"),
            [AiProvider.Perplexity] = new("Perplexity", "https://api.perplexity.ai", "Sonar (z dostępem do internetu)", "https://www.perplexity.ai/settings/api"),
            [AiProvider.Cohere] = new("Cohere", "https://api.cohere.ai/v1", "Command R+", "https://dashboard.cohere.com/api-keys"),
            [AiProvider.OpenRouter] = new("OpenRouter", "https://openrouter.ai/api/v1", "Agregator 200+ modeli", "https://openrouter.ai/keys"),
        }.ToFrozenDictionary();

        public static string GetEndpoint(AiProvider provider)
        {
            return ProviderInfo.TryGetValue(provider, out var info) ? info.Endpoint : "";
        }

        public static string GetKeyUrl(AiProvider provider)
        {
            return ProviderInfo.TryGetValue(provider, out var info) ? info.KeyUrl : "";
        }

        public static string GetDescription(AiProvider provider)
        {
            return ProviderInfo.TryGetValue(provider, out var info) ? info.Description : "";
        }

        public static List<AiProvider> GetCloudProviders() => ProviderInfo.Keys.ToList();
    }

    public class AiModel : INotifyPropertyChanged
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; } 

        private string _name = string.Empty;
        private AiProvider _provider;
        private string _modelId = string.Empty;
        private string _apiKey = string.Empty; // Only for UI binding, not stored in DB
        private string _endpoint = string.Empty;
        private bool _isActive;
        private bool _supportsStreaming = true;
        private bool _supportsImages = false;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public AiProvider Provider
        {
            get => _provider;
            set
            {
                _provider = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProviderName));
                OnPropertyChanged(nameof(RequiresEndpoint));
            }
        }

        public string ProviderName => Provider.ToString();

        public bool RequiresEndpoint => Provider == AiProvider.OpenAICompatible;
        
        public bool IsLocalModel => Provider == AiProvider.LocalModel;
        
        public bool RequiresApiKey => Provider != AiProvider.LocalModel;

        public string ModelId
        {
            get => _modelId;
            set
            {
                _modelId = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// API Key for UI binding only - actual key is stored securely via SecureApiKeyService
        /// </summary>
        [Ignore] // SQLite should ignore this property
        public string ApiKey
        {
            get => _apiKey;
            set
            {
                _apiKey = value;
                OnPropertyChanged();
            }
        }

        public string Endpoint
        {
            get => _endpoint;
            set
            {
                _endpoint = value;
                OnPropertyChanged();
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }

        public bool SupportsStreaming
        {
            get => _supportsStreaming;
            set
            {
                _supportsStreaming = value;
                OnPropertyChanged();
            }
        }

        public bool SupportsImages
        {
            get => _supportsImages;
            set
            {
                _supportsImages = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    
}