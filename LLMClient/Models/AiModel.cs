using SQLite;
using System.ComponentModel;

namespace LLMClient.Models
{
    public enum AiProvider
    {
        OpenAI,
        Gemini,
        OpenAICompatible,
        LocalModel
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