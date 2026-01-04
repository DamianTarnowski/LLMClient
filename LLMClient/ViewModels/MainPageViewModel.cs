using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using LLMClient.Models;
using LLMClient.Services;
using LLMClient.Views;
using Microsoft.Maui.Controls;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using LLMClient.Messaging;

namespace LLMClient.ViewModels
{
    public class MainPageViewModel : INotifyPropertyChanged, IQueryAttributable
    {
        private readonly IAiService _aiService;
        private readonly DatabaseService _databaseService;
        private readonly IStreamingBatchService _streamingBatchService;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly ISearchService _searchService;
        private readonly IExportService _exportService;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILocalizationService _localizationService;
        private readonly ILocalModelService _localModelService;
        private readonly IMemoryExtractionService? _memoryExtractionService;
        private ObservableCollection<Conversation> _conversations = new();
        private Conversation? _selectedConversation;
        private string _newMessage = string.Empty;
        private bool _isSending;
        private AiConfiguration _aiConfiguration = new();
        private bool _streamingEnabled = true;
        private string _searchTerm = string.Empty;
        private ObservableCollection<Message> _filteredMessages = new();
        private string _encryptionStatus = string.Empty;
        private string _applicationInfo = string.Empty;
        private string? _selectedImagePath;
        private string? _selectedImageBase64;
        private int _messagesOffset = 0;
        private const int PAGE_SIZE = 50;
        private double _downloadProgressValue;
        private bool _isCloudModelsEnabled = true;
        private string? _currentActiveModel;
        private bool _isLocalModelBusy;
        private bool _isUpdatingFilteredMessages;

        public ObservableCollection<Conversation> Conversations
        {
            get => _conversations;
            set
            {
                _conversations = value;
                OnPropertyChanged();
            }
        }

        public Conversation? SelectedConversation
        {
            get => _selectedConversation;
            set
            {
                _selectedConversation = value;
                OnPropertyChanged();
                UpdateFilteredMessages();
            }
        }

        public ObservableCollection<Message> FilteredMessages
        {
            get => _filteredMessages;
            set
            {
                _filteredMessages = value;
                OnPropertyChanged();
            }
        }

        public string NewMessage
        {
            get => _newMessage;
            set
            {
                _newMessage = value;
                OnPropertyChanged();
                ((Command)SendMessageCommand).ChangeCanExecute();
            }
        }

        public bool IsSending
        {
            get => _isSending;
            set
            {
                _isSending = value;
                OnPropertyChanged();
                ((Command)SendMessageCommand).ChangeCanExecute();
            }
        }

        public AiConfiguration AiConfiguration
        {
            get => _aiConfiguration;
            set
            {
                _aiConfiguration = value;
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

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                _searchTerm = value;
                OnPropertyChanged();
                PerformSearch();
            }
        }

        public bool HasSearchResults => _searchService.HasResults;

        public string SearchResultText 
        {
            get
            {
                if (!_searchService.HasResults) return string.Empty;
                return $"{_searchService.CurrentResultIndex + 1}/{_searchService.CurrentResults.Count}";
            }
        }

        public string EncryptionStatus
        {
            get => _encryptionStatus;
            set
            {
                _encryptionStatus = value;
                OnPropertyChanged();
            }
        }

        public string ApplicationInfo
        {
            get => _applicationInfo;
            set
            {
                _applicationInfo = value;
                OnPropertyChanged();
            }
        }

        public string? SelectedImagePath
        {
            get => _selectedImagePath;
            set
            {
                _selectedImagePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedImage));
            }
        }

        public string? SelectedImageBase64
        {
            get => _selectedImageBase64;
            set
            {
                _selectedImageBase64 = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedImage));
            }
        }

        public bool HasSelectedImage => !string.IsNullOrEmpty(SelectedImagePath);
        
        public bool SupportsImages => AiConfiguration?.SelectedModel?.SupportsImages == true;

        private bool _isLightTheme;
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

        public double DownloadProgressValue
        {
            get => _downloadProgressValue;
            set
            {
                _downloadProgressValue = value;
                OnPropertyChanged();
            }
        }

        public ILocalizationService L => _localizationService;

        private LanguageOption? _selectedLanguage;
        public LanguageOption? SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (_selectedLanguage != value)
                {
                    _selectedLanguage = value;
                    OnPropertyChanged();
                    if (value != null)
                    {
                        _localizationService.SetCulture(value.Code);
                    }
                }
            }
        }

        public string? CurrentActiveModel 
        {
            get => _currentActiveModel;
            set
            {
                _currentActiveModel = value;
                OnPropertyChanged();
            }
        }

        public bool IsLocalModelBusy
        {
            get => _isLocalModelBusy;
            set
            {
                _isLocalModelBusy = value;
                OnPropertyChanged();
            }
        }

        public bool IsCloudModelsEnabled
        {
            get => _isCloudModelsEnabled;
            set
            {
                _isCloudModelsEnabled = value;
                OnPropertyChanged();
            }
        }

        // UI toggle for switching between local model and cloud models
        public bool UseLocalModel
        {
            get => _localModelService.State == LocalModelState.Loaded;
            set
            {
                // Execute asynchronously; revert UI if operation fails
                _ = SetUseLocalModelAsync(value);
            }
        }

        // Commands for segmented toggle
        public ICommand EnableLocalModelCommand { get; }
        public ICommand EnableApiModelCommand { get; }

        public List<LanguageOption> AvailableLanguages => _localizationService.AvailableLanguages;

        public ICommand NewConversationCommand { get; }
        public ICommand SelectConversationCommand { get; }
        public ICommand SendMessageCommand { get; }
        public ICommand OpenModelConfigCommand { get; }
        public ICommand DeleteConversationCommand { get; }
        public ICommand DeleteMessageCommand { get; }
        public ICommand CopyMessageCommand { get; }
        public ICommand NextSearchResultCommand { get; }
        public ICommand PreviousSearchResultCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand ExportConversationCommand { get; }
        public ICommand PickImageCommand { get; }
        public ICommand ClearImageCommand { get; }
        public ICommand ToggleThemeCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand GoToSearchCommand { get; }
        public ICommand GoToMemoryCommand { get; }
        public ICommand SetPassphraseCommand { get; }
        public ICommand LoadMoreMessagesCommand { get; }
        public ICommand ModelSettingsCommand { get; }
        public ICommand GoToRagCommand { get; }
        public ICommand GoToDiagnosticsCommand { get; }

        public bool IsConversationsEmpty => Conversations.Count == 0;

        public MainPageViewModel(IAiService aiService, DatabaseService databaseService, IStreamingBatchService streamingBatchService, IErrorHandlingService errorHandlingService, ISearchService searchService, IExportService exportService, IEmbeddingService embeddingService, ILocalizationService localizationService, ILocalModelService localModelService, IMemoryExtractionService? memoryExtractionService = null)
        {
            _aiService = aiService;
            _databaseService = databaseService;
            _streamingBatchService = streamingBatchService;
            _errorHandlingService = errorHandlingService;
            _searchService = searchService;
            _exportService = exportService;
            _embeddingService = embeddingService;
            _localizationService = localizationService;
            _localModelService = localModelService;
            _memoryExtractionService = memoryExtractionService;

            // Subscribe to local model state changes
            _localModelService.StateChanged += OnLocalModelStateChanged;
            
            // Subscribe to local model download progress and errors
            _localModelService.DownloadProgress += (p) =>
            {
                MainThread.BeginInvokeOnMainThread(() => DownloadProgressValue = p);
            };
            _localModelService.ErrorOccurred += (err) =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    IsLocalModelBusy = false;
                    await DisplayAlertAsync("Błąd modelu lokalnego", err, "OK");
                    OnPropertyChanged(nameof(UseLocalModel));
                });
            };
            
            // Subscribe to messages from LocalModelStatusViewModel via WeakReferenceMessenger
            WeakReferenceMessenger.Default.Register<LocalModelLoadedMessage>(this, async (r, m) =>
            {
                await OnLocalModelLoadedAsync();
            });

            WeakReferenceMessenger.Default.Register<LocalModelUnloadedMessage>(this, async (r, m) =>
            {
                await OnLocalModelUnloadedAsync();
            });

            // Initialize AiConfiguration and subscribe to its PropertyChanged event
            _aiConfiguration = new AiConfiguration();
            _aiConfiguration.PropertyChanged += AiConfiguration_PropertyChanged;

            NewConversationCommand = new Command(async () => await CreateNewConversationAsync());
            SelectConversationCommand = new Command<Conversation>(SelectConversation);
            SendMessageCommand = new Command(async () => await SendMessageAsync(), CanSendMessage);
            OpenModelConfigCommand = new Command(async () => await OpenModelConfigurationAsync());
            DeleteConversationCommand = new Command<Conversation>(async (conversation) => await DeleteConversationAsync(conversation));
            DeleteMessageCommand = new Command<Message>(async (message) => await DeleteMessageAsync(message));
            CopyMessageCommand = new Command<Message>(async (message) => await CopyMessageAsync(message));
            NextSearchResultCommand = new Command(NextSearchResult);
            PreviousSearchResultCommand = new Command(PreviousSearchResult);
            ClearSearchCommand = new Command(ClearSearch);
            ExportConversationCommand = new Command(async () => await ExportConversationAsync());
            PickImageCommand = new Command(async () => await PickImageAsync());
            ClearImageCommand = new Command(ClearSelectedImage);
            ToggleThemeCommand = new Command(() => IsLightTheme = !IsLightTheme);
            SettingsCommand = new Command(async () => await GoToSettingsAsync());
            GoToSearchCommand = new Command(async () => await GoToSearchAsync());
            GoToMemoryCommand = new Command(async () => await GoToMemoryAsync());
            SetPassphraseCommand = new Command(async () => await SetDatabasePassphraseAsync());
            LoadMoreMessagesCommand = new Command(async () => await LoadMoreMessagesAsync());
            ModelSettingsCommand = new Command(async () => await GoToModelSettingsAsync());
            GoToRagCommand = new Command(async () => await GoToRagAsync());
            GoToDiagnosticsCommand = new Command(async () => await GoToDiagnosticsAsync());

            // Segment toggle commands
            EnableLocalModelCommand = new Command(async () => await SetUseLocalModelAsync(true));
            EnableApiModelCommand = new Command(async () => await SetUseLocalModelAsync(false));

            // Subskrypcja na powiadomienia o zmianach modeli
            WeakReferenceMessenger.Default.Register<ModelsChangedMessage>(this, async (r, m) =>
            {
                await RefreshModelsAsync();
            });

            Task.Run(async () => await LoadDataAsync());
            Task.Run(async () => await LoadThemeAsync());
            
            // Initialize selected language
            var currentCulture = _localizationService.CurrentCulture;
            _selectedLanguage = _localizationService.AvailableLanguages.FirstOrDefault(l => l.Code == currentCulture);
        }

        // Helper accessors for current Page (MAUI recommendation: use Windows[0].Page instead of obsolete MainPage)
        private Page? CurrentPage => Application.Current?.Windows?.FirstOrDefault()?.Page;

        private Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel)
            => CurrentPage != null ? CurrentPage.DisplayAlertAsync(title, message, accept, cancel) : Task.FromResult(false);

        private Task DisplayAlertAsync(string title, string message, string cancel)
            => CurrentPage != null ? CurrentPage.DisplayAlertAsync(title, message, cancel) : Task.CompletedTask;

        private Task<string?> DisplayPromptAsync(string title, string message, string accept, string cancel, int maxLength, Keyboard keyboard)
            => CurrentPage != null ? CurrentPage.DisplayPromptAsync(title, message, accept, cancel, maxLength: maxLength, keyboard: keyboard) : Task.FromResult<string?>(null);

        private void OnLocalModelStateChanged(LocalModelState state)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                switch (state)
                {
                    case LocalModelState.Loaded:
                        // When local model is loaded, disable cloud models and set as active
                        IsCloudModelsEnabled = false;
                        IsLocalModelBusy = false;
                        CurrentActiveModel = "Phi-4-mini (Local)";
                        
                        // Notify that cloud model selector should be disabled
                        WeakReferenceMessenger.Default.Send(new LocalModelActiveChangedMessage(true));
                        break;
                    case LocalModelState.Loading:
                        // Pokaż spinner w trakcie ładowania
                        IsLocalModelBusy = true;
                        CurrentActiveModel = "Phi-4-mini (Local)";
                        break;
                    case LocalModelState.Downloading:
                        IsLocalModelBusy = true;
                        break;
                        
                    case LocalModelState.Downloaded:
                    case LocalModelState.NotDownloaded:
                    case LocalModelState.Error:
                        // When local model is not loaded, re-enable cloud models
                        IsCloudModelsEnabled = true;
                        IsLocalModelBusy = false;
                        CurrentActiveModel = AiConfiguration?.SelectedModel?.Name;
                        
                        // Notify that cloud model selector should be re-enabled
                        WeakReferenceMessenger.Default.Send(new LocalModelActiveChangedMessage(false));
                        break;
                }

                // Update toggle state in UI
                OnPropertyChanged(nameof(UseLocalModel));
            });
        }
        
        private async Task OnLocalModelLoadedAsync()
        {
            try
            {
                // Create a local model entry for AiService
                var localModel = new AiModel
                {
                    Id = -1, // Special ID for local model
                    Name = "Phi-4-mini (Local)",
                    ModelId = "phi-4-mini",
                    Provider = AiProvider.LocalModel,
                    IsActive = true,
                    // IsLocalModel is read-only property based on Provider
                    SupportsStreaming = true,
                    SupportsImages = false
                };
                
                // Switch AiService to use local model
                await _aiService.UpdateConfiguration(localModel);
                // Ustaw też w UI jako wybrany model
                AiConfiguration.SelectedModel = localModel;
                
                // Update UI state
                IsCloudModelsEnabled = false;
                IsLocalModelBusy = false;
                CurrentActiveModel = "Phi-4-mini (Local)";
                
                // Save preference for local model usage
                Preferences.Set("UseLocalModel", true);

                // Jeśli nie ma żadnej aktywnej konwersacji – utwórz ją automatycznie
                if (SelectedConversation == null)
                {
                    if (!Conversations.Any())
                    {
                        await CreateNewConversationAsync();
                    }
                    else
                    {
                        SelectedConversation = Conversations.First();
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("[MainPageViewModel] Local model activated successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPageViewModel] Error activating local model: {ex.Message}");
            }
        }
        
        private async Task OnLocalModelUnloadedAsync()
        {
            try
            {
                // Re-enable cloud models
                IsCloudModelsEnabled = true;
                IsLocalModelBusy = false;
                
                // Switch back to previously selected cloud model or first available
                var modelToSelect = AiConfiguration.Models.FirstOrDefault(m => m.IsActive) ??
                                      AiConfiguration.Models.FirstOrDefault();
                
                if (modelToSelect != null)
                {
                    AiConfiguration.SelectedModel = modelToSelect;
                    await _aiService.UpdateConfiguration(modelToSelect);
                    CurrentActiveModel = modelToSelect.Name;
                }
                else
                {
                    CurrentActiveModel = null;
                }
                
                // Clear preference for local model usage
                Preferences.Set("UseLocalModel", false);
                
                System.Diagnostics.Debug.WriteLine("[MainPageViewModel] Switched back to cloud model");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPageViewModel] Error switching to cloud model: {ex.Message}");
            }
        }

        private async Task SetUseLocalModelAsync(bool enable)
        {
            try
            {
                // Prevent re-entrant operations while busy
                if (IsLocalModelBusy)
                {
                    System.Diagnostics.Debug.WriteLine("[MainPageViewModel] Local model operation already in progress");
                    OnPropertyChanged(nameof(UseLocalModel));
                    return;
                }

                if (enable)
                {
                    if (_localModelService.State == LocalModelState.Loaded)
                    {
                        OnPropertyChanged(nameof(UseLocalModel));
                        return;
                    }

                    if (_localModelService.State == LocalModelState.NotDownloaded || _localModelService.State == LocalModelState.Error)
                    {
                        // Get model info dynamically instead of hardcoding
                        var modelInfo = await _localModelService.GetModelInfoAsync();
                        var sizeText = modelInfo.SizeInMB > 0 
                            ? (modelInfo.SizeInMB < 1024 ? $"{modelInfo.SizeInMB:F0} MB" : $"{modelInfo.SizeInMB / 1024.0:F1} GB") 
                            : "~4.9 GB"; // Fallback for Phi-4 default
                        var modelName = !string.IsNullOrEmpty(modelInfo.DisplayName) ? modelInfo.DisplayName : "Model lokalny";

                        bool confirm = await DisplayAlertAsync(
                            "Pobieranie modelu",
                            $"Model '{modelName}' nie jest pobrany ({sizeText}). Czy chcesz pobrać go teraz?",
                            "Tak",
                            "Nie");

                        if (!confirm)
                        {
                            OnPropertyChanged(nameof(UseLocalModel));
                            return;
                        }

                        IsLocalModelBusy = true;
                        var progress = new Progress<double>(p => DownloadProgressValue = p);
                        
                        // Execute download
                        var downloaded = await _localModelService.DownloadModelAsync(progress);
                        
                        // Check if state is valid for loading (Downloaded or already Loaded)
                        bool readyToLoad = downloaded || _localModelService.State == LocalModelState.Downloaded;
                        
                        if (!readyToLoad)
                        {
                            if (_localModelService.IsDownloading)
                            {
                                // Download started in background/service
                                void ContinueAfterDownload(LocalModelState s)
                                {
                                    if (s == LocalModelState.Downloaded)
                                    {
                                        _localModelService.StateChanged -= ContinueAfterDownload;
                                        MainThread.BeginInvokeOnMainThread(async () =>
                                        {
                                            IsLocalModelBusy = true;
                                            var loadedAfter = await _localModelService.LoadModelAsync();
                                            IsLocalModelBusy = false;
                                            if (loadedAfter)
                                            {
                                                WeakReferenceMessenger.Default.Send(new LocalModelLoadedMessage());
                                            }
                                        });
                                    }
                                    else if (s == LocalModelState.Error)
                                    {
                                        _localModelService.StateChanged -= ContinueAfterDownload;
                                        MainThread.BeginInvokeOnMainThread(async () => 
                                        {
                                            IsLocalModelBusy = false;
                                            await DisplayAlertAsync("Błąd", "Pobieranie modelu zakończyło się błędem.", "OK");
                                            OnPropertyChanged(nameof(UseLocalModel));
                                        });
                                    }
                                }
                                _localModelService.StateChanged += ContinueAfterDownload;
                                return; // UI stays busy or updates via progress
                            }
                            else
                            {
                                IsLocalModelBusy = false;
                                await DisplayAlertAsync("Błąd", "Nie udało się zainicjować pobierania modelu.", "OK");
                                OnPropertyChanged(nameof(UseLocalModel));
                                return;
                            }
                        }
                    }

                    // Try to load the model when downloaded
                    IsLocalModelBusy = true;
                    var loaded = await _localModelService.LoadModelAsync();
                    IsLocalModelBusy = false;

                    if (loaded)
                    {
                        WeakReferenceMessenger.Default.Send(new LocalModelLoadedMessage());
                    }
                    else
                    {
                        await DisplayAlertAsync("Błąd", "Nie udało się załadować modelu lokalnego.", "OK");
                        OnPropertyChanged(nameof(UseLocalModel));
                    }
                }
                else
                {
                    if (_localModelService.State == LocalModelState.Loaded)
                    {
                        IsLocalModelBusy = true;
                        await _localModelService.UnloadModelAsync();
                        IsLocalModelBusy = false;
                        WeakReferenceMessenger.Default.Send(new LocalModelUnloadedMessage());
                    }

                    OnPropertyChanged(nameof(UseLocalModel));
                }
            }
            catch (Exception ex)
            {
                IsLocalModelBusy = false;
                System.Diagnostics.Debug.WriteLine($"[MainPageViewModel] Toggle local model error: {ex.Message}");
                await DisplayAlertAsync("Błąd", $"Wystąpił błąd: {ex.Message}", "OK");
                OnPropertyChanged(nameof(UseLocalModel));
            }
        }

        // Implementacja obsługi parametrów nawigacji
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("conversationId", out var convIdObj) && int.TryParse(convIdObj?.ToString(), out int conversationId))
            {
                var conversation = Conversations.FirstOrDefault(c => c.Id == conversationId);
                if (conversation != null)
                {
                    SelectedConversation = conversation;
                    if (query.TryGetValue("messageId", out var msgIdObj) && int.TryParse(msgIdObj?.ToString(), out int messageId))
                    {
                        var message = conversation.Messages.FirstOrDefault(m => m.Id == messageId);
                        if (message != null)
                        {
                            // Wyślij komunikat do widoku, aby przewinąć do tej wiadomości
                            WeakReferenceMessenger.Default.Send(new ScrollToMessageMessage(message));
                        }
                    }
                }
            }
        }

        private async Task DeleteMessageAsync(Message message)
        {
            if (message == null || SelectedConversation == null)
                return;

            bool confirm = await DisplayAlertAsync("Usuń wiadomość", "Czy na pewno chcesz usunąć tę wiadomość?", "Tak", "Nie");
            if (confirm)
            {
                try
                {
                    await _databaseService.DeleteMessageAsync(message);
                    SelectedConversation.Messages.Remove(message);
                    UpdateFilteredMessages(); // Aktualizuj UI po usunięciu wiadomości
                    await DisplayAlertAsync("Sukces", "Wiadomość została usunięta.", "OK");
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync("Błąd", $"Nie udało się usunąć wiadomości: {ex.Message}", "OK");
                }
            }
        }

        private async Task CopyMessageAsync(Message message)
        {
            if (message == null || string.IsNullOrEmpty(message.Content))
                return;

            try
            {
                await Clipboard.SetTextAsync(message.Content);
                await DisplayAlertAsync("Sukces", "Wiadomość skopiowana do schowka.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Błąd", $"Nie udało się skopiować wiadomości: {ex.Message}", "OK");
            }
        }

        private async Task DeleteConversationAsync(Conversation conversation)
        {
            if (conversation == null)
                return;

            bool confirm = await DisplayAlertAsync("Usuń konwersację", $"Czy na pewno chcesz usunąć konwersację '{conversation.Title}'?", "Tak", "Nie");
            if (confirm)
            {
                try
                {
                    await _databaseService.DeleteConversationAsync(conversation);
                    Conversations.Remove(conversation);

                    if (SelectedConversation == conversation)
                    {
                        SelectedConversation = null;
                        if (Conversations.Any())
                        {
                            SelectedConversation = Conversations.First();
                        }
                        else
                        {
                            // Jeśli nic nie zostało – utwórz nową, aby UI zawsze miał aktywną konwersację
                            await CreateNewConversationAsync();
                        }
                    }
                    OnPropertyChanged(nameof(IsConversationsEmpty));
                    await DisplayAlertAsync("Sukces", "Konwersacja została usunięta.", "OK");
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync("Błąd", $"Nie udało się usunąć konwersacji: {ex.Message}", "OK");
                }
            }
        }

        private async void AiConfiguration_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AiConfiguration.SelectedModel) && AiConfiguration.SelectedModel != null)
            {
                try
                {
                    var model = AiConfiguration.SelectedModel;
                    
                    // Walidacja - nie konfiguruj modelu bez wymaganych danych
                    if (model.Provider != AiProvider.LocalModel)
                    {
                        if (string.IsNullOrEmpty(model.ApiKey) || string.IsNullOrEmpty(model.ModelId))
                        {
                            System.Diagnostics.Debug.WriteLine($"[MainPageViewModel] Model {model.Name} nie ma wymaganych danych - pomijam");
                            return;
                        }
                    }
                    
                    await Task.Run(() => _aiService.UpdateConfiguration(model));
                    // Save the ID of the selected model
                    Preferences.Set("LastSelectedModelId", model.Id);
                    // Powiadom o zmianie obsługi obrazków
                    OnPropertyChanged(nameof(SupportsImages));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainPageViewModel] Błąd konfiguracji: {ex.Message}");
                }
            }
        }

        private async Task LoadDataAsync()
        {
            var conversations = await _databaseService.GetConversationsAsync();
            Conversations = new ObservableCollection<Conversation>(conversations.OrderByDescending(c => c.CreatedAt));
            OnPropertyChanged(nameof(IsConversationsEmpty));

            if (Conversations.Any())
            {
                SelectedConversation = Conversations.First();
            }
            else
            {
                // Brak konwersacji? Utwórz automatycznie nową i ustaw jako aktywną
                await CreateNewConversationAsync();
            }

            var models = await _databaseService.GetModelsAsync();
            AiConfiguration.Models = new ObservableCollection<AiModel>(models);

            try
            {
                int lastSelectedModelId = Preferences.Get("LastSelectedModelId", 0);
                AiModel? modelToSelect = null;

                if (lastSelectedModelId != 0)
                {
                    modelToSelect = models.FirstOrDefault(m => m.Id == lastSelectedModelId);
                }

                if (modelToSelect == null)
                {
                    // Fallback to active model if last selected not found or not set
                    modelToSelect = models.FirstOrDefault(m => m.IsActive);
                }

                if (modelToSelect == null)
                {
                    // Fallback to the first model if no active or last selected found
                    modelToSelect = models.FirstOrDefault();
                }

                if (modelToSelect != null)
                {
                    AiConfiguration.SelectedModel = modelToSelect;
                    await _aiService.UpdateConfiguration(modelToSelect);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Błąd konfiguracji AI", $"Nie udało się skonfigurować aktywnego modelu AI: {ex.Message}", "OK");
            }

            StreamingEnabled = Preferences.Get("StreamingEnabled", true);
            
            // Jeśli brak wybranego modelu chmurowego, ale lokalny jest już ZAŁADOWANY, ustaw lokalny jako aktywny
            if (AiConfiguration.SelectedModel == null && _localModelService.State == LocalModelState.Loaded)
            {
                var localModel = new AiModel
                {
                    Id = -1,
                    Name = "Phi-4-mini (Local)",
                    ModelId = "phi-4-mini",
                    Provider = AiProvider.LocalModel,
                    IsActive = true,
                    SupportsStreaming = true,
                    SupportsImages = false
                };
                AiConfiguration.SelectedModel = localModel;
                await _aiService.UpdateConfiguration(localModel);
                IsCloudModelsEnabled = false;
                CurrentActiveModel = localModel.Name;
            }

            // Ustal dostępność wyboru modeli chmurowych zależnie od stanu lokalnego modelu
            IsCloudModelsEnabled = _localModelService.State != LocalModelState.Loaded;
            
            // Przywróć preferencję używania modelu lokalnego po starcie aplikacji
            try
            {
                var preferLocal = Preferences.Get("UseLocalModel", false);
                if (preferLocal && _localModelService.State == LocalModelState.Downloaded)
                {
                    var loaded = await _localModelService.LoadModelAsync();
                    if (loaded)
                    {
                        WeakReferenceMessenger.Default.Send(new LocalModelLoadedMessage());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPageViewModel] Failed to restore local model preference: {ex.Message}");
            }
            
            // Sprawdź status szyfrowania bazy danych
            try
            {
                EncryptionStatus = await _databaseService.GetEncryptionInfoAsync();
            }
            catch (Exception ex)
            {
                EncryptionStatus = $"Błąd sprawdzania szyfrowania: {ex.Message}";
            }

            // Sprawdź identyfikator aplikacji
            try
            {
                ApplicationInfo = await _databaseService.GetApplicationInfoAsync();
            }
            catch (Exception ex)
            {
                ApplicationInfo = $"Błąd pobierania ID: {ex.Message}";
            }

            // Initialize embedding service with progress reporting
            _embeddingService.DownloadProgress += (progress) => DownloadProgressValue = progress;
            // await _embeddingService.InitializeAsync(); // Moved to SemanticSearchViewModel
        }



        private async Task CreateNewConversationAsync()
        {
            // Zapewnij aktywny model: jeśli brak chmurowego, a lokalny jest załadowany, użyj lokalnego
            if (AiConfiguration.SelectedModel == null)
            {
                if (_localModelService.State == LocalModelState.Loaded)
                {
                    var localModel = new AiModel
                    {
                        Id = -1,
                        Name = "Phi-4-mini (Local)",
                        ModelId = "phi-4-mini",
                        Provider = AiProvider.LocalModel,
                        IsActive = true,
                        SupportsStreaming = true,
                        SupportsImages = false
                    };
                    AiConfiguration.SelectedModel = localModel;
                    await _aiService.UpdateConfiguration(localModel);
                }
                else
                {
                    // Na Androidzie UX: tworzymy konwersację bez modelu, by UI nie był zablokowany
                    // Użytkownik i tak zobaczy komunikat przy próbie wysyłki wiadomości
                }
            }

            var newConversation = new Conversation
            {
                Title = "Nowa konwersacja",
                CreatedAt = DateTime.Now,
                AiModelId = AiConfiguration.SelectedModel?.Id ?? 0
            };

            // Teraz to będzie działać - metoda zwraca Task<int>
            var conversationId = await _databaseService.SaveConversationAsync(newConversation);
            newConversation.Id = conversationId;

            Conversations.Insert(0, newConversation);
            SelectedConversation = newConversation;
            OnPropertyChanged(nameof(IsConversationsEmpty));
        }

        private async Task LoadMoreMessagesAsync()
        {
            if (SelectedConversation == null) return;
            var newMessages = await _databaseService.GetMessagesAsync(SelectedConversation.Id, PAGE_SIZE, _messagesOffset);
            if (newMessages.Count == 0) return;

            foreach (var msg in newMessages)
                SelectedConversation.Messages.Add(msg);

            UpdateFilteredMessages();
            _messagesOffset += newMessages.Count;
            WeakReferenceMessenger.Default.Send(new ScrollToBottomMessage());
        }

        private void SelectConversation(Conversation conversation)
        {
            if (conversation == null)
                return;

            SelectedConversation = conversation;
            _messagesOffset = 0;
            SelectedConversation.Messages.Clear();
            _ = LoadMoreMessagesAsync(); // Initial load
        }

        private bool CanSendMessage()
        {
            return !string.IsNullOrWhiteSpace(NewMessage) &&
                   !IsSending &&
                   SelectedConversation != null;
        }

        private const int MAX_MESSAGE_LENGTH = 2000;
        private const long MAX_IMAGE_SIZE_BYTES = 5 * 1024 * 1024; // 5MB

        private async Task SendMessageAsync()
        {
            // Jeśli brak aktywnej konwersacji – utwórz ją automatycznie
            if (SelectedConversation == null)
            {
                await CreateNewConversationAsync();
                if (SelectedConversation == null)
                    return;
            }

            if (string.IsNullOrWhiteSpace(NewMessage))
                return;

            // Nowa walidacja
            if (NewMessage.Trim().Length > MAX_MESSAGE_LENGTH)
            {
                await DisplayAlertAsync("Błąd", $"Wiadomość zbyt długa (max {MAX_MESSAGE_LENGTH} znaków).", "OK");
                return;
            }

            if (!string.IsNullOrEmpty(SelectedImageBase64) && Convert.FromBase64String(SelectedImageBase64).Length > MAX_IMAGE_SIZE_BYTES)
            {
                await DisplayAlertAsync("Błąd", "Obrazek zbyt duży (max 5MB).", "OK");
                return;
            }

            if (!_aiService.IsConfigured || AiConfiguration.SelectedModel == null)
            {
                await ShowConfigurationRequiredAsync();
                return;
            }

            IsSending = true;

            try
            {
                var userMessage = new Message
                {
                    Content = NewMessage.Trim(),
                    IsUser = true,
                    Timestamp = DateTime.Now,
                    ConversationId = SelectedConversation.Id,
                    ImagePath = SelectedImagePath,
                    ImageBase64 = SelectedImageBase64
                };

                // Save user message with error handling
                userMessage.Id = await _errorHandlingService.ExecuteWithRetryAsync(
                    () => _databaseService.SaveMessageAsync(userMessage),
                    "saving user message");
                
                SelectedConversation.Messages.Add(userMessage);
                UpdateFilteredMessages(); // Dodaj wiadomość do UI natychmiast
                WeakReferenceMessenger.Default.Send(new ScrollToBottomMessage());

                var messageToSend = NewMessage.Trim();
                var imageToSend = SelectedImageBase64;
                NewMessage = string.Empty;
                ClearSelectedImage(); // Wyczyść wybrany obrazek po wysłaniu

                var botMessage = new Message
                {
                    Content = "",
                    IsUser = false,
                    Timestamp = DateTime.Now,
                    ConversationId = SelectedConversation.Id
                };

                // Save empty bot message with error handling
                botMessage.Id = await _errorHandlingService.ExecuteWithRetryAsync(
                    () => _databaseService.SaveMessageAsync(botMessage),
                    "saving bot message");
                
                SelectedConversation.Messages.Add(botMessage);
                UpdateFilteredMessages(); // Dodaj pustą wiadomość bota do UI
                WeakReferenceMessenger.Default.Send(new ScrollToBottomMessage());

                var conversationHistory = SelectedConversation.Messages
                    .Where(m => m != botMessage)
                    .ToList();

                if (StreamingEnabled && AiConfiguration.SelectedModel.SupportsStreaming)
                {
                    // Start batch processing for streaming
                    _streamingBatchService.StartBatching(botMessage, () =>
                    {
                        // Aktualizuj UI na głównym wątku podczas streamingu
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            OnPropertyChanged(nameof(SelectedConversation));
                            OnPropertyChanged(nameof(FilteredMessages));
                        });
                    });

                    try
                    {
                        // Execute streaming with retry policy
                        await _errorHandlingService.ExecuteWithRetryAsync(async () =>
                        {
                            await foreach (var chunk in _aiService.GetStreamingResponseAsync(messageToSend, imageToSend, conversationHistory))
                            {
                                _streamingBatchService.AddChunk(chunk);
                            }
                        }, "streaming AI response");
                    }
                    finally
                    {
                        _streamingBatchService.StopBatching();
                        WeakReferenceMessenger.Default.Send(new ScrollToBottomMessage());
                    }
                }
                else
                {
                    // Execute non-streaming with retry policy
                    var response = await _errorHandlingService.ExecuteWithRetryAsync(
                        () => _aiService.GetResponseAsync(messageToSend, imageToSend, conversationHistory),
                        "getting AI response");
                    
                    botMessage.Content = response;
                    await _errorHandlingService.ExecuteWithRetryAsync(
                        () => _databaseService.SaveMessageAsync(botMessage),
                        "saving AI response");
                    
                    OnPropertyChanged(nameof(FilteredMessages)); // Aktualizuj UI z pełną odpowiedzią
                    WeakReferenceMessenger.Default.Send(new ScrollToBottomMessage());
                }

                // Generate conversation title if this is the first exchange
                if (SelectedConversation.Messages.Count == 2)
                {
                    _ = Task.Run(() => GenerateConversationTitleAsync(userMessage.Content));
                }

                // Automatically extract memory from recent messages
                if (_memoryExtractionService != null)
                {
                    _ = Task.Run(() => _memoryExtractionService.ExtractAndSaveMemoryFromConversationAsync(
                        SelectedConversation.Messages.TakeLast(10).ToList()));
                }
            }
            catch (Exception ex)
            {
                // Clean up empty bot message if exists
                var lastMessage = SelectedConversation.Messages.LastOrDefault();
                if (lastMessage != null && !lastMessage.IsUser && string.IsNullOrEmpty(lastMessage.Content))
                {
                    SelectedConversation.Messages.Remove(lastMessage);
                    UpdateFilteredMessages(); // Aktualizuj UI po usunięciu pustej wiadomości
                    try
                    {
                        await _databaseService.DeleteMessageAsync(lastMessage);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }

                // Create user-friendly error message
                var friendlyErrorMessage = _errorHandlingService.GetUserFriendlyErrorMessage(ex, "sending message");
                
                var errorMessage = new Message
                {
                    Content = friendlyErrorMessage,
                    IsUser = false,
                    Timestamp = DateTime.Now,
                    ConversationId = SelectedConversation.Id
                };

                try
                {
                    errorMessage.Id = await _databaseService.SaveMessageAsync(errorMessage);
                    SelectedConversation.Messages.Add(errorMessage);
                    UpdateFilteredMessages(); // Aktualizuj UI z wiadomością błędu
                }
                catch
                {
                    // If we can't save error message, at least show it in UI
                    SelectedConversation.Messages.Add(errorMessage);
                    UpdateFilteredMessages(); // Aktualizuj UI z wiadomością błędu
                }
            }
            finally
            {
                IsSending = false;
            }
        }


        private async Task ShowConfigurationRequiredAsync()
        {
            await DisplayAlertAsync(
                "Konfiguracja wymagana",
                "Aby rozpocząć rozmowę, skonfiguruj model AI w ustawieniach.",
                "OK");
        }

        private async Task OpenModelConfigurationAsync()
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(ModelConfigurationPage));
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Błąd", $"Nie udało się otworzyć konfiguracji: {ex.Message}", "OK");
            }
        }

        

        private static string TruncateString(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
                return input;

            return input.Substring(0, maxLength) + "...";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private async Task GenerateConversationTitleAsync(string userMessageContent)
        {
            if (SelectedConversation == null)
                return;

            try
            {
                var titlePrompt = $"Create a short conversation title (max 5 words) matching the user's language for: {userMessageContent}";
                var titleResponse = await _aiService.GetResponseAsync(titlePrompt, new List<Message>());

                string newTitle;
                if (!string.IsNullOrWhiteSpace(titleResponse))
                    newTitle = TruncateString(titleResponse, 30);
                else
                    newTitle = "Konwersacja o " + TruncateString(userMessageContent, 20);

                SelectedConversation.Title = newTitle;
                await _databaseService.SaveConversationAsync(SelectedConversation);
                OnPropertyChanged(nameof(SelectedConversation));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas generowania tytułu: {ex.Message}");
                if (SelectedConversation != null)
                {
                    SelectedConversation.Title = "Nowa konwersacja";
                    await _databaseService.SaveConversationAsync(SelectedConversation);
                    OnPropertyChanged(nameof(SelectedConversation));
                }
            }
        }

        #region Search Methods

        private void PerformSearch()
        {
            if (SelectedConversation == null)
            {
                _searchService.ClearResults();
                OnPropertyChanged(nameof(HasSearchResults));
                OnPropertyChanged(nameof(SearchResultText));
                UpdateFilteredMessages();
                return;
            }

            if (string.IsNullOrWhiteSpace(SearchTerm))
            {
                _searchService.ClearResults();
                UpdateFilteredMessages(); // Pokaż wszystkie wiadomości
            }
            else
            {
                _searchService.SearchInConversation(SelectedConversation, SearchTerm);
                FilterMessagesBySearchResults();
            }

            OnPropertyChanged(nameof(HasSearchResults));
            OnPropertyChanged(nameof(SearchResultText));

            // Scroll to first result if found
            if (_searchService.HasResults)
            {
                ScrollToSearchResult();
            }
        }

        private void UpdateFilteredMessages()
        {
            if (_isUpdatingFilteredMessages)
                return;
            
            _isUpdatingFilteredMessages = true;
            try
            {
                var messages = SelectedConversation?.Messages?.ToList() ?? new List<Message>();
                
                FilteredMessages.Clear();
                foreach (var message in messages)
                {
                    FilteredMessages.Add(message);
                }
            }
            finally
            {
                _isUpdatingFilteredMessages = false;
            }
        }

        private void FilterMessagesBySearchResults()
        {
            if (_isUpdatingFilteredMessages)
                return;
            
            if (_searchService.HasResults)
            {
                var searchResults = _searchService.CurrentResults;
                var filteredMessageIds = searchResults.Select(r => r.Message.Id).Distinct();
                
                if (SelectedConversation?.Messages != null)
                {
                    foreach (var message in SelectedConversation.Messages)
                    {
                        if (filteredMessageIds.Contains(message.Id))
                        {
                            FilteredMessages.Add(message);
                        }
                    }
                }
            }
        }

        private void NextSearchResult()
        {
            if (_searchService.HasResults)
            {
                _searchService.GetNextResult();
                OnPropertyChanged(nameof(SearchResultText));
                ScrollToSearchResult();
            }
        }

        private void PreviousSearchResult()
        {
            if (_searchService.HasResults)
            {
                _searchService.GetPreviousResult();
                OnPropertyChanged(nameof(SearchResultText));
                ScrollToSearchResult();
            }
        }

        private void ClearSearch()
        {
            SearchTerm = string.Empty;
            _searchService.ClearResults();
            OnPropertyChanged(nameof(HasSearchResults));
            OnPropertyChanged(nameof(SearchResultText));
        }

        private void ScrollToSearchResult()
        {
            // Send message to scroll to the current search result
            var currentResult = _searchService.GetCurrentResult();
            if (currentResult != null)
            {
                WeakReferenceMessenger.Default.Send(new ScrollToMessageMessage(currentResult.Message));
            }
        }

        #endregion

        #region Export Methods

        private async Task ExportConversationAsync()
        {
            if (SelectedConversation == null || SelectedConversation.Messages.Count == 0)
            {
                await DisplayAlertAsync("Błąd", "Brak konwersacji do eksportu.", "OK");
                return;
            }

            try
            {
                // Show format selection dialog
                var format = await ShowExportFormatSelectionAsync();
                if (format == null) return;

                // Perform export
                var result = await _exportService.ExportConversationAsync(SelectedConversation, format.Value);

                if (result.Success)
                {
                    await DisplayAlertAsync("Sukces", 
                        $"Konwersacja została wyeksportowana do:\n{result.FilePath}", "OK");
                }
                else
                {
                    await DisplayAlertAsync("Błąd", 
                        $"Nie udało się wyeksportować konwersacji:\n{result.ErrorMessage}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Błąd", 
                    $"Wystąpił błąd podczas eksportu: {ex.Message}", "OK");
            }
        }

        private async Task<ExportFormat?> ShowExportFormatSelectionAsync()
        {
            string? action = null;
            if (CurrentPage != null)
            {
                action = await CurrentPage.DisplayActionSheetAsync(
                    "Wybierz format eksportu", "Anuluj", null,
                    "JSON (strukturalne dane)",
                    "Markdown (czytelny format)",
                    "TXT (prosty tekst)");
            }

            return action switch
            {
                "JSON (strukturalne dane)" => ExportFormat.Json,
                "Markdown (czytelny format)" => ExportFormat.Markdown,
                "TXT (prosty tekst)" => ExportFormat.PlainText,
                _ => null
            };
        }

        private async Task RefreshModelsAsync()
        {
            try
            {
                var models = await _databaseService.GetModelsAsync();
                AiConfiguration.Models = new ObservableCollection<AiModel>(models);

                // Zachowaj aktualnie wybrany model jeśli nadal istnieje
                if (AiConfiguration.SelectedModel != null)
                {
                    var currentModel = models.FirstOrDefault(m => m.Id == AiConfiguration.SelectedModel.Id);
                    if (currentModel != null)
                    {
                        AiConfiguration.SelectedModel = currentModel;
                        await _aiService.UpdateConfiguration(currentModel);
                    }
                    else
                    {
                        // Jeśli aktualny model już nie istnieje, wybierz pierwszy dostępny
                        var modelToSelect = models.FirstOrDefault(m => m.IsActive) ?? models.FirstOrDefault();
                        if (modelToSelect != null)
                        {
                            AiConfiguration.SelectedModel = modelToSelect;
                            await _aiService.UpdateConfiguration(modelToSelect);
                        }
                    }
                }
                else
                {
                    // Jeśli nie ma wybranego modelu, wybierz domyślny
                    var modelToSelect = models.FirstOrDefault(m => m.IsActive) ?? models.FirstOrDefault();
                    if (modelToSelect != null)
                    {
                        AiConfiguration.SelectedModel = modelToSelect;
                        await _aiService.UpdateConfiguration(modelToSelect);
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Błąd", $"Nie udało się odświeżyć listy modeli: {ex.Message}", "OK");
            }
        }

        #endregion

        #region Image Methods

        private async Task PickImageAsync()
        {
            try
            {
                var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.image" } },
                    { DevicePlatform.Android, new[] { "image/*" } },
                    { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" } },
                    { DevicePlatform.macOS, new[] { "public.image" } }
                });

                var options = new PickOptions
                {
                    PickerTitle = "Wybierz obrazek",
                    FileTypes = customFileType
                };

                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    SelectedImagePath = result.FullPath;
                    
                    // Konwertuj do Base64 dla API
                    var bytes = await File.ReadAllBytesAsync(result.FullPath);
                    SelectedImageBase64 = Convert.ToBase64String(bytes);
                    
                    OnPropertyChanged(nameof(SupportsImages));
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Błąd", $"Nie udało się wybrać obrazka: {ex.Message}", "OK");
            }
        }

        private void ClearSelectedImage()
        {
            SelectedImagePath = null;
            SelectedImageBase64 = null;
            OnPropertyChanged(nameof(SupportsImages));
        }

        #endregion

        #region Theme Management

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
                // Handle theme toggle error silently or show error message
                System.Diagnostics.Debug.WriteLine($"Błąd przełączania theme: {ex.Message}");
            }
        }

        #endregion

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async Task GoToSettingsAsync()
        {
            await Shell.Current.GoToAsync("///ModelConfigurationPage");
        }

        private async Task GoToSearchAsync()
        {
            await Shell.Current.GoToAsync("///SemanticSearchPage");
        }

        private async Task GoToMemoryAsync()
        {
            await Shell.Current.GoToAsync("///MemoryPage");
        }

        private async Task GoToRagAsync()
        {
            await Shell.Current.GoToAsync("///RagDocumentsPage");
        }

        private async Task GoToDiagnosticsAsync()
        {
            await Shell.Current.GoToAsync("DebugPage");
        }

        private async Task GoToModelSettingsAsync()
        {
            // Use absolute route to ShellContent
            await Shell.Current.GoToAsync("///ModelSettingsPage");
        }

        private async Task SetDatabasePassphraseAsync()
        {
            var passphrase = await DisplayPromptAsync("Ustaw hasło bazy", "Podaj nowe hasło (min. 8 znaków):", "OK", "Anuluj", maxLength: 50, keyboard: Keyboard.Text);
            if (string.IsNullOrEmpty(passphrase) || passphrase.Length < 8)
                return;

            var confirm = await DisplayPromptAsync("Potwierdź hasło", "Powtórz hasło:", "OK", "Anuluj", maxLength: 50, keyboard: Keyboard.Text);
            if (passphrase != confirm)
            {
                await DisplayAlertAsync("Błąd", "Hasła nie pasują.", "OK");
                return;
            }

            var success = await _databaseService.SetCustomPassphraseAsync(passphrase);
            if (success)
                await DisplayAlertAsync("Sukces", "Hasło ustawione. Zrestartuj aplikację, by zmiany weszły w życie.", "OK");
            else
                await DisplayAlertAsync("Błąd", "Nie udało się ustawić hasła.", "OK");
        }
    }
}