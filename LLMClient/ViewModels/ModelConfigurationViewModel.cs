using LLMClient.Models;
using LLMClient.Services;
using CommunityToolkit.Mvvm.Messaging;
using LLMClient.Messaging;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Xml.Linq;
using Microsoft.Maui.Storage;

namespace LLMClient.ViewModels
{
    public class ModelConfigurationViewModel : INotifyPropertyChanged
    {
        private readonly IAiService _aiService;
        private readonly DatabaseService _databaseService;
        private ObservableCollection<AiModel> _models = new();
        private AiModel? _selectedModelForEdit;
        private bool _streamingEnabled = true;
        private string _statusMessage = string.Empty;
        private string _statusColor = "White";
        private bool _isLightTheme;

        public ObservableCollection<AiModel> Models
        {
            get => _models;
            set
            {
                _models = value;
                OnPropertyChanged();
            }
        }

        public AiModel? SelectedModelForEdit
        {
            get => _selectedModelForEdit;
            set
            {
                _selectedModelForEdit = value;
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

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public string StatusColor
        {
            get => _statusColor;
            set
            {
                _statusColor = value;
                OnPropertyChanged();
            }
        }

        public bool IsLightTheme
        {
            get => _isLightTheme;
            set
            {
                _isLightTheme = value;
                OnPropertyChanged();
                _ = ToggleThemeAsync(value);
            }
        }

        public List<AiProvider> AvailableProviders { get; } = new()
        {
            AiProvider.OpenAI,
            AiProvider.Gemini,
            AiProvider.Anthropic,
            AiProvider.Mistral,
            AiProvider.Groq,
            AiProvider.DeepSeek,
            AiProvider.xAI,
            AiProvider.Perplexity,
            AiProvider.Together,
            AiProvider.Fireworks,
            AiProvider.OpenRouter,
            AiProvider.Cohere,
            AiProvider.OpenAICompatible,
            AiProvider.LocalModel
        };

        public ICommand AddNewModelCommand { get; }
        public ICommand SelectModelCommand { get; }
        public ICommand SaveModelCommand { get; }
        public ICommand TestModelCommand { get; }
        public ICommand DeleteModelCommand { get; }
        public ICommand CloseCommand { get; }

        public ModelConfigurationViewModel(IAiService aiService, DatabaseService databaseService)
        {
            _aiService = aiService;
            _databaseService = databaseService;

            AddNewModelCommand = new Command(AddNewModel);
            SelectModelCommand = new Command<AiModel>(SelectModel);
            SaveModelCommand = new Command(async () => await SaveModelAsync());
            TestModelCommand = new Command(async () => await TestModelAsync());
            DeleteModelCommand = new Command(async () => await DeleteModelAsync());
            CloseCommand = new Command(async () => await CloseAsync());

            Task.Run(async () => await LoadModelsAsync());
            Task.Run(async () => await LoadThemeAsync());
        }

        private async Task LoadModelsAsync()
        {
            var models = await _databaseService.GetModelsAsync();
            Models = new ObservableCollection<AiModel>(models);
        }

        private void AddNewModel()
        {
            var newModel = new AiModel
            {
                Name = "Nowy Model",
                Provider = AiProvider.OpenAI,
                ModelId = "",
                IsActive = false,
                SupportsStreaming = true
            };

            Models.Add(newModel);
            SelectedModelForEdit = newModel;
            SetStatus("Dodano nowy model. Wypełnij wymagane pola.", "#FEE75C");
        }

        private async Task SaveModelAsync()
        {
            if (SelectedModelForEdit == null)
                return;

            try
            {
                // Walidacja
                if (string.IsNullOrWhiteSpace(SelectedModelForEdit.Name))
                {
                    SetStatus("Nazwa modelu jest wymagana.", "#ED4245");
                    return;
                }

                if (string.IsNullOrWhiteSpace(SelectedModelForEdit.ModelId))
                {
                    SetStatus("Model ID jest wymagane.", "#ED4245");
                    return;
                }

                if (string.IsNullOrWhiteSpace(SelectedModelForEdit.ApiKey))
                {
                    SetStatus("API Key jest wymagany.", "#ED4245");
                    return;
                }

                if (SelectedModelForEdit.RequiresEndpoint && string.IsNullOrWhiteSpace(SelectedModelForEdit.Endpoint))
                {
                    SetStatus("Endpoint URL jest wymagany dla tego typu dostawcy.", "#ED4245");
                    return;
                }

                await _databaseService.SaveModelAsync(SelectedModelForEdit);
                await LoadModelsAsync(); // Refresh the list after saving
                SetStatus("Model został zapisany pomyślnie.", "#57F287");
                
                // Powiadom MainPageViewModel o zmianie modeli
                WeakReferenceMessenger.Default.Send(new ModelsChangedMessage());
            }
            catch (Exception ex)
            {
                SetStatus($"Błąd podczas zapisywania: {ex.Message}", "#ED4245");
            }
        }

        private async Task DeleteModelAsync()
        {
            if (SelectedModelForEdit == null)
                return;

            await _databaseService.DeleteModelAsync(SelectedModelForEdit);
            Models.Remove(SelectedModelForEdit);
            SelectedModelForEdit = null;
            await LoadModelsAsync(); // Refresh the list after deleting
            SetStatus("Model został usunięty.", "#57F287");
            
            // Powiadom MainPageViewModel o zmianie modeli
            WeakReferenceMessenger.Default.Send(new ModelsChangedMessage());
        }

        private async Task CloseAsync()
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page?.Navigation != null)
            {
                await page.Navigation.PopAsync();
            }
        }

        private void SelectModel(AiModel model)
        {
            SelectedModelForEdit = model;
            SetStatus(string.Empty, "White");
        }

        private async Task TestModelAsync()
        {
            if (SelectedModelForEdit == null)
            {
                SetStatus("Wybierz model do testowania.", "#ED4245");
                return;
            }

            SetStatus("Testowanie modelu...", "#FEE75C");
            try
            {
                await _aiService.UpdateConfiguration(SelectedModelForEdit); // Update AI service with the selected model
                var testMessage = new Models.Message { Content = "Test message", IsUser = true };
                var response = await _aiService.GetResponseAsync("Test message", new List<Models.Message> { testMessage });

                if (!string.IsNullOrEmpty(response))
                {
                    SetStatus("Model działa poprawnie!", "#57F287");
                }
                else
                {
                    SetStatus("Model zwrócił pustą odpowiedź.", "#ED4245");
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Błąd testowania: {ex.Message}", "#ED4245");
            }
        }


        private void SetStatus(string message, string color)
        {
            StatusMessage = message;
            StatusColor = color;

            Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread()?.StartTimer(TimeSpan.FromSeconds(5), () =>
            {
                if (StatusMessage == message)
                {
                    StatusMessage = string.Empty;
                }
                return false;
            });
        }

        private async Task LoadThemeAsync()
        {
            _isLightTheme = Preferences.Get("IsLightTheme", false);
            OnPropertyChanged(nameof(IsLightTheme));
        }

        private async Task ToggleThemeAsync(bool isLight)
        {
            try
            {
                Preferences.Set("IsLightTheme", isLight);
                
                if (MainThread.IsMainThread)
                {
                    if (Application.Current != null)
                        Application.Current.UserAppTheme = isLight ? AppTheme.Light : AppTheme.Dark;
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (Application.Current != null)
                            Application.Current.UserAppTheme = isLight ? AppTheme.Light : AppTheme.Dark;
                    });
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Błąd przełączania theme: {ex.Message}", "#ED4245");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}