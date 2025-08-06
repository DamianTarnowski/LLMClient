using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMClient.Models
{
    public class AiConfiguration : INotifyPropertyChanged
    {
        private ObservableCollection<AiModel> _models = new();
        private AiModel? _selectedModel;
        private bool _streamingEnabled = true;

        public ObservableCollection<AiModel> Models
        {
            get => _models;
            set
            {
                _models = value;
                OnPropertyChanged();
            }
        }

        public AiModel? SelectedModel
        {
            get => _selectedModel;
            set
            {
                _selectedModel = value;
                OnPropertyChanged();
            }
        }

        public bool StreamingEnabled
        {
            get => _streamingEnabled;
            set
            {
                _streamingEnabled = value;
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
