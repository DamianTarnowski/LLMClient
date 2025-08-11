using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using LLMClient.Models;
using LLMClient.Services;
using LLMClient.Views;
using Microsoft.Maui.Controls;
using System.Globalization;

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
        private readonly IEmbeddingService _embeddingService;
        private readonly IMemoryExtractionService? _memoryExtractionService;
        private double _downloadProgressValue;

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

        public bool IsConversationsEmpty => Conversations.Count == 0;

        public MainPageViewModel(IAiService aiService, DatabaseService databaseService, IStreamingBatchService streamingBatchService, IErrorHandlingService errorHandlingService, ISearchService searchService, IExportService exportService, IEmbeddingService embeddingService, IMemoryExtractionService? memoryExtractionService = null)
        {
            _aiService = aiService;
            _databaseService = databaseService;
            _streamingBatchService = streamingBatchService;
            _errorHandlingService = errorHandlingService;
            _searchService = searchService;
            _exportService = exportService;
            _embeddingService = embeddingService;
            _memoryExtractionService = memoryExtractionService;

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

            // Subskrypcja na powiadomienia o zmianach modeli
            MessagingCenter.Subscribe<ModelConfigurationViewModel>(this, "ModelsChanged", async (sender) =>
            {
                await RefreshModelsAsync();
            });

            Task.Run(async () => await LoadDataAsync());
            Task.Run(async () => await LoadThemeAsync());
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
                            MessagingCenter.Send(this, "ScrollToMessage", message);
                        }
                    }
                }
            }
        }

        private async Task DeleteMessageAsync(Message message)
        {
            if (message == null || SelectedConversation == null)
                return;

            bool confirm = await Application.Current.MainPage.DisplayAlert("Usuń wiadomość", "Czy na pewno chcesz usunąć tę wiadomość?", "Tak", "Nie");
            if (confirm)
            {
                try
                {
                    await _databaseService.DeleteMessageAsync(message);
                    SelectedConversation.Messages.Remove(message);
                    UpdateFilteredMessages(); // Aktualizuj UI po usunięciu wiadomości
                    await Application.Current.MainPage.DisplayAlert("Sukces", "Wiadomość została usunięta.", "OK");
                }
                catch (Exception ex)
                {
                    await Application.Current.MainPage.DisplayAlert("Błąd", $"Nie udało się usunąć wiadomości: {ex.Message}", "OK");
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
                await Application.Current.MainPage.DisplayAlert("Sukces", "Wiadomość skopiowana do schowka.", "OK");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Błąd", $"Nie udało się skopiować wiadomości: {ex.Message}", "OK");
            }
        }

        private async Task DeleteConversationAsync(Conversation conversation)
        {
            if (conversation == null)
                return;

            bool confirm = await Application.Current.MainPage.DisplayAlert("Usuń konwersację", $"Czy na pewno chcesz usunąć konwersację '{conversation.Title}'?", "Tak", "Nie");
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
                    }
                    OnPropertyChanged(nameof(IsConversationsEmpty));
                    await Application.Current.MainPage.DisplayAlert("Sukces", "Konwersacja została usunięta.", "OK");
                }
                catch (Exception ex)
                {
                    await Application.Current.MainPage.DisplayAlert("Błąd", $"Nie udało się usunąć konwersacji: {ex.Message}", "OK");
                }
            }
        }

        private async void AiConfiguration_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AiConfiguration.SelectedModel) && AiConfiguration.SelectedModel != null)
            {
                try
                {
                    _aiService.UpdateConfiguration(AiConfiguration.SelectedModel);
                    // Save the ID of the selected model
                    Preferences.Set("LastSelectedModelId", AiConfiguration.SelectedModel.Id);
                    // Powiadom o zmianie obsługi obrazków
                    OnPropertyChanged(nameof(SupportsImages));
                }
                catch (Exception ex)
                {
                    await Application.Current?.MainPage?.DisplayAlert("Błąd konfiguracji AI", $"Nie udało się skonfigurować wybranego modelu AI: {ex.Message}", "OK");
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
                    _aiService.UpdateConfiguration(modelToSelect);
                }
            }
            catch (Exception ex)
            {
                await Application.Current?.MainPage?.DisplayAlert("Błąd konfiguracji AI", $"Nie udało się skonfigurować aktywnego modelu AI: {ex.Message}", "OK");
            }

            StreamingEnabled = Preferences.Get("StreamingEnabled", true);
            
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
            // Sprawdź czy jest wybrany model AI
            if (AiConfiguration.SelectedModel == null)
            {
                await ShowConfigurationRequiredAsync();
                return;
            }

            var newConversation = new Conversation
            {
                Title = "Nowa konwersacja",
                CreatedAt = DateTime.Now,
                AiModelId = AiConfiguration.SelectedModel.Id
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
            MessagingCenter.Send(this, "ScrollToBottom");
        }

        private void SelectConversation(Conversation conversation)
        {
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
            if (SelectedConversation == null)
            {
                await Application.Current.MainPage.DisplayAlert("Brak konwersacji", "Utwórz nową konwersację zanim wyślesz wiadomość.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(NewMessage))
                return;

            // Nowa walidacja
            if (NewMessage.Trim().Length > MAX_MESSAGE_LENGTH)
            {
                await Application.Current.MainPage.DisplayAlert("Błąd", $"Wiadomość zbyt długa (max {MAX_MESSAGE_LENGTH} znaków).", "OK");
                return;
            }

            if (!string.IsNullOrEmpty(SelectedImageBase64) && Convert.FromBase64String(SelectedImageBase64).Length > MAX_IMAGE_SIZE_BYTES)
            {
                await Application.Current.MainPage.DisplayAlert("Błąd", "Obrazek zbyt duży (max 5MB).", "OK");
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
                MessagingCenter.Send(this, "ScrollToBottom");

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
                MessagingCenter.Send(this, "ScrollToBottom");

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
                        MessagingCenter.Send(this, "ScrollToBottom");
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
                    MessagingCenter.Send(this, "ScrollToBottom");
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
            await Application.Current?.MainPage?.DisplayAlert(
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
                await Application.Current?.MainPage?.DisplayAlert("Błąd", $"Nie udało się otworzyć konfiguracji: {ex.Message}", "OK");
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
            try
            {
                var titlePrompt = $"Stwórz krótki tytuł (max 5 słów) po polsku dla konwersacji o: {userMessageContent}";
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
                SelectedConversation.Title = "Nowa konwersacja";
                await _databaseService.SaveConversationAsync(SelectedConversation);
                OnPropertyChanged(nameof(SelectedConversation));
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
            FilteredMessages.Clear();
            
            if (SelectedConversation?.Messages != null)
            {
                foreach (var message in SelectedConversation.Messages)
                {
                    FilteredMessages.Add(message);
                }
            }
        }

        private void FilterMessagesBySearchResults()
        {
            FilteredMessages.Clear();
            
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
                MessagingCenter.Send(this, "ScrollToMessage", currentResult.Message);
            }
        }

        #endregion

        #region Export Methods

        private async Task ExportConversationAsync()
        {
            if (SelectedConversation == null || SelectedConversation.Messages.Count == 0)
            {
                await Application.Current?.MainPage?.DisplayAlert("Błąd", "Brak konwersacji do eksportu.", "OK");
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
                    await Application.Current?.MainPage?.DisplayAlert("Sukces", 
                        $"Konwersacja została wyeksportowana do:\n{result.FilePath}", "OK");
                }
                else
                {
                    await Application.Current?.MainPage?.DisplayAlert("Błąd", 
                        $"Nie udało się wyeksportować konwersacji:\n{result.ErrorMessage}", "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current?.MainPage?.DisplayAlert("Błąd", 
                    $"Wystąpił błąd podczas eksportu: {ex.Message}", "OK");
            }
        }

        private async Task<ExportFormat?> ShowExportFormatSelectionAsync()
        {
            var action = await Application.Current?.MainPage?.DisplayActionSheet(
                "Wybierz format eksportu", "Anuluj", null, 
                "JSON (strukturalne dane)", 
                "Markdown (czytelny format)", 
                "TXT (prosty tekst)");

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
                        _aiService.UpdateConfiguration(currentModel);
                    }
                    else
                    {
                        // Jeśli aktualny model już nie istnieje, wybierz pierwszy dostępny
                        var modelToSelect = models.FirstOrDefault(m => m.IsActive) ?? models.FirstOrDefault();
                        if (modelToSelect != null)
                        {
                            AiConfiguration.SelectedModel = modelToSelect;
                            _aiService.UpdateConfiguration(modelToSelect);
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
                        _aiService.UpdateConfiguration(modelToSelect);
                    }
                }
            }
            catch (Exception ex)
            {
                await Application.Current?.MainPage?.DisplayAlert("Błąd", $"Nie udało się odświeżyć listy modeli: {ex.Message}", "OK");
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
                await Application.Current.MainPage.DisplayAlert("Błąd", $"Nie udało się wybrać obrazka: {ex.Message}", "OK");
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
                    Application.Current.UserAppTheme = isLight ? AppTheme.Light : AppTheme.Dark;
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
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

        private async Task SetDatabasePassphraseAsync()
        {
            var passphrase = await Application.Current.MainPage.DisplayPromptAsync("Ustaw hasło bazy", "Podaj nowe hasło (min. 8 znaków):", "OK", "Anuluj", maxLength: 50, keyboard: Keyboard.Text);
            if (string.IsNullOrEmpty(passphrase) || passphrase.Length < 8)
                return;

            var confirm = await Application.Current.MainPage.DisplayPromptAsync("Potwierdź hasło", "Powtórz hasło:", "OK", "Anuluj", maxLength: 50, keyboard: Keyboard.Text);
            if (passphrase != confirm)
            {
                await Application.Current.MainPage.DisplayAlert("Błąd", "Hasła nie pasują.", "OK");
                return;
            }

            var success = await _databaseService.SetCustomPassphraseAsync(passphrase);
            if (success)
                await Application.Current.MainPage.DisplayAlert("Sukces", "Hasło ustawione. Zrestartuj aplikację, by zmiany weszły w życie.", "OK");
            else
                await Application.Current.MainPage.DisplayAlert("Błąd", "Nie udało się ustawić hasła.", "OK");
        }
    }
}