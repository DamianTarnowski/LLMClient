using SQLite;
using System.Collections.ObjectModel;
using System.ComponentModel;
using SQLiteNetExtensions.Attributes;


namespace LLMClient.Models
{
    public class Conversation : INotifyPropertyChanged
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        private string _title = string.Empty;
        private DateTime _createdAt;
        private ObservableCollection<Message> _messages = new();

        [ForeignKey(typeof(AiModel))]
        public int AiModelId { get; set; }

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged();
            }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set
            {
                _createdAt = value;
                OnPropertyChanged();
            }
        }
        [Ignore]
        public ObservableCollection<Message> Messages
        {
            get => _messages;
            set
            {
                _messages = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastMessage));
                OnPropertyChanged(nameof(LastMessageTime));
            }
        }

        public string LastMessage => Messages.LastOrDefault()?.Content ?? "Brak wiadomości";

        public DateTime LastMessageTime => Messages.LastOrDefault()?.Timestamp ?? CreatedAt;

        public Conversation()
        {
            Messages.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(LastMessage));
                OnPropertyChanged(nameof(LastMessageTime));
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

