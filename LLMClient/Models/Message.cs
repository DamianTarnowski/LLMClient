using SQLite;
using System.ComponentModel;
using SQLiteNetExtensions.Attributes;

namespace LLMClient.Models
{
    public class Message : INotifyPropertyChanged
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        private string _content = string.Empty;
        private bool _isUser;
        private DateTime _timestamp;
        private string? _imagePath;
        private string? _imageBase64;
        private byte[]? _embedding;
        private string? _embeddingVersion;

        [ForeignKey(typeof(Conversation))] 
        public int ConversationId { get; set; }

        public string Content
        {
            get => _content;
            set
            {
                _content = value;
                OnPropertyChanged();
            }
        }

        public bool IsUser
        {
            get => _isUser;
            set
            {
                _isUser = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBot));
            }
        }

        public bool IsBot => !IsUser;

        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                _timestamp = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Ścieżka do obrazka (dla wyświetlania w UI)
        /// </summary>
        public string? ImagePath
        {
            get => _imagePath;
            set
            {
                _imagePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasImage));
            }
        }

        /// <summary>
        /// Obrazek w formacie Base64 (dla wysyłania do API)
        /// </summary>
        public string? ImageBase64
        {
            get => _imageBase64;
            set
            {
                _imageBase64 = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasImage));
            }
        }

        /// <summary>
        /// Czy wiadomość zawiera obrazek
        /// </summary>
        [Ignore]
        public bool HasImage => !string.IsNullOrEmpty(ImagePath) || !string.IsNullOrEmpty(ImageBase64);

        /// <summary>
        /// Embedding wektorowy wiadomości (384 float32 dla all-MiniLM-L6-v2)
        /// </summary>
        public byte[]? Embedding
        {
            get => _embedding;
            set
            {
                _embedding = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasEmbedding));
            }
        }

        /// <summary>
        /// Wersja modelu użytego do wygenerowania embeddingu
        /// </summary>
        public string? EmbeddingVersion
        {
            get => _embeddingVersion;
            set
            {
                _embeddingVersion = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Czy wiadomość ma wygenerowany embedding
        /// </summary>
        [Ignore]
        public bool HasEmbedding => _embedding != null && _embedding.Length > 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
