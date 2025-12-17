using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LLMClient.Models;
using LLMClient.Services;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel; // MainThread

namespace LLMClient.ViewModels
{
    // Helper to get the current page for displaying alerts (handles .NET 10 deprecation of MainPage)
    internal static class PageHelper
    {
        public static Page? CurrentPage => Application.Current?.Windows.FirstOrDefault()?.Page;
    }

    public class SemanticSearchResult : INotifyPropertyChanged
    {
        private Message _message = null!;
        private float _similarityScore;
        private string _conversationTitle = string.Empty;
        private DateTime _messageTimestamp;

        public Message Message
        {
            get => _message;
            set
            {
                _message = value;
                OnPropertyChanged();
            }
        }

        public float SimilarityScore
        {
            get => _similarityScore;
            set
            {
                _similarityScore = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SimilarityPercentage));
            }
        }

        public string ConversationTitle
        {
            get => _conversationTitle;
            set
            {
                _conversationTitle = value;
                OnPropertyChanged();
            }
        }

        public DateTime MessageTimestamp
        {
            get => _messageTimestamp;
            set
            {
                _messageTimestamp = value;
                OnPropertyChanged();
            }
        }

        public string SimilarityPercentage => $"{SimilarityScore * 100:F1}%";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SemanticSearchViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _databaseService;
        private readonly IEmbeddingService? _embeddingService;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IEmbeddingPipelineService _embeddingPipelineService;
        private readonly ILogger<SemanticSearchViewModel> _logger;

        private string _searchQuery = string.Empty;
        private bool _isSearching;
        private bool _isEmbeddingInitialized;
        private string _statusMessage = "Gotowy do wyszukiwania";
        private float _minSimilarity = 0.3f;
        private int _maxResults = 20;
        private bool _isGeneratingEmbeddings;
        private string _embeddingProgress = string.Empty;
        private int _totalMessages;
        private int _messagesWithEmbeddings;
        private double _embeddingCoverage;
        private bool _isDownloadingModel;
        private bool _isInitializing;

        public ObservableCollection<SemanticSearchResult> SearchResults { get; } = new();

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                OnPropertyChanged();
                ((Command)SearchCommand).ChangeCanExecute();
            }
        }

        public bool IsSearching
        {
            get => _isSearching;
            set
            {
                if (_isSearching != value)
                {
                    _isSearching = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsBusy));
                }
            }
        }

        public bool IsEmbeddingInitialized
        {
            get => _isEmbeddingInitialized;
            set
            {
                _isEmbeddingInitialized = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSearch));
                ((Command)SearchCommand).ChangeCanExecute();
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

        public float MinSimilarity
        {
            get => _minSimilarity;
            set
            {
                _minSimilarity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MinSimilarityPercentage));
            }
        }

        public int MaxResults
        {
            get => _maxResults;
            set
            {
                _maxResults = value;
                OnPropertyChanged();
            }
        }

        public string MinSimilarityPercentage => $"{MinSimilarity * 100:F0}%";
        public bool CanSearch => IsEmbeddingInitialized && !IsSearching;

        public bool IsGeneratingEmbeddings
        {
            get => _isGeneratingEmbeddings;
            set
            {
                if (_isGeneratingEmbeddings != value)
                {
                    _isGeneratingEmbeddings = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsBusy));
                    OnPropertyChanged(nameof(CanGenerateEmbeddings));
                }
            }
        }

        public string EmbeddingProgress
        {
            get => _embeddingProgress;
            set
            {
                _embeddingProgress = value;
                OnPropertyChanged();
            }
        }

        public int TotalMessages
        {
            get => _totalMessages;
            set
            {
                _totalMessages = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EmbeddingStatsText));
            }
        }

        public int MessagesWithEmbeddings
        {
            get => _messagesWithEmbeddings;
            set
            {
                _messagesWithEmbeddings = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EmbeddingStatsText));
            }
        }

        public double EmbeddingCoverage
        {
            get => _embeddingCoverage;
            set
            {
                _embeddingCoverage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EmbeddingStatsText));
            }
        }

        public bool IsDownloadingModel
        {
            get => _isDownloadingModel;
            set
            {
                if (_isDownloadingModel != value)
                {
                    _isDownloadingModel = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsBusy));
                    OnPropertyChanged(nameof(CanGenerateEmbeddings));
                }
            }
        }

        /// <summary>
        /// Ogólny stan zajętości – true, gdy trwa pobieranie modelu, generowanie embeddingów lub wyszukiwanie.
        /// </summary>
        public bool IsBusy => IsDownloadingModel || IsGeneratingEmbeddings || IsSearching;

        public string EmbeddingStatsText => $"Embeddingi: {MessagesWithEmbeddings}/{TotalMessages} ({EmbeddingCoverage:F1}%)";
        public bool CanGenerateEmbeddings => !IsGeneratingEmbeddings && !IsSearching;

        public bool IsModelReady => IsEmbeddingInitialized;

        public ICommand SearchCommand { get; }
        public ICommand InitializeCommand { get; }
        public ICommand ClearResultsCommand { get; }
        public ICommand NavigateToMessageCommand { get; }
        public ICommand GenerateEmbeddingsCommand { get; }
        public ICommand CheckEmbeddingStatsCommand { get; }
        public ICommand DownloadModelCommand { get; }
        public ICommand GoBackCommand { get; }

        public SemanticSearchViewModel(
            DatabaseService databaseService,
            IEmbeddingService? embeddingService,
            IErrorHandlingService errorHandlingService,
            IEmbeddingPipelineService embeddingPipelineService,
            ILogger<SemanticSearchViewModel> logger)
        {
            _databaseService = databaseService;
            _embeddingService = embeddingService;
            _errorHandlingService = errorHandlingService;
            _embeddingPipelineService = embeddingPipelineService;
            _logger = logger;

            SearchCommand = new Command(async () => await SearchAsync(), CanPerformSearch);
            InitializeCommand = new Command(async () => await InitializeEmbeddingServiceAsync(), () => !IsSearching);
            ClearResultsCommand = new Command(ClearResults);
            NavigateToMessageCommand = new Command<SemanticSearchResult>(async (result) => await NavigateToMessageAsync(result));
            GenerateEmbeddingsCommand = new Command(async () => await GenerateEmbeddingsAsync(), () => CanGenerateEmbeddings);
            // Przycisk 🔄 na UI ma nie tylko odczytać statystyki, ale też ewentualnie wygenerować brakujące embeddingi.
            CheckEmbeddingStatsCommand = new Command(async () => await RefreshAsync());
            DownloadModelCommand = new Command(async () => await DownloadModelAsync(), () => !IsDownloadingModel);
            GoBackCommand = new Command(async () => await GoBackAsync());

            // Check if embedding service is ready
            CheckEmbeddingServiceStatus();
            // Load initial embedding stats
            _ = Task.Run(async () => await CheckEmbeddingStatsAsync());
            // Uruchamiamy inicjalizację na wątku UI – DisplayAlert musi działać na głównym wątku
            // MainThread.BeginInvokeOnMainThread(async () => await InitializeEmbeddingServiceAsync());
        }

        public async Task OnAppearingAsync()
        {
            await InitializeEmbeddingServiceAsync();
            await RefreshAsync();
        }

        /// <summary>
        /// Wywoływane przez stronę SemanticSearchPage.OnAppearing
        /// Odświeża statystyki embeddingów i generuje brakujące, jeśli ViewModel już istnieje na stosie.
        /// </summary>
        public async Task RefreshAsync()
        {
            _logger?.LogInformation("SemanticSearchViewModel.RefreshAsync invoked");
            await CheckEmbeddingStatsAsync();

            if (MessagesWithEmbeddings < TotalMessages && !_isGeneratingEmbeddings)
            {
                _logger?.LogInformation("Found missing embeddings after refresh: {Missing}", TotalMessages - MessagesWithEmbeddings);
                await GenerateEmbeddingsAsync();
            }
        }

        private bool CanPerformSearch()
        {
            return IsEmbeddingInitialized && !IsSearching && !string.IsNullOrWhiteSpace(SearchQuery);
        }

        private void CheckEmbeddingServiceStatus()
        {
            if (_embeddingService != null)
            {
                IsEmbeddingInitialized = _embeddingService.IsInitialized;
                if (IsEmbeddingInitialized)
                {
                    StatusMessage = "Semantic search gotowy";
                }
                else
                {
                    StatusMessage = "Embeddings nie są zainicjalizowane";
                }
            }
            else
            {
                StatusMessage = "Embedding service nie jest dostępny";
            }
        }

        private async Task InitializeEmbeddingServiceAsync()
        {
            if (_isInitializing || _embeddingService == null) return;

            if (_embeddingService.IsInitialized)
            {
                IsEmbeddingInitialized = true;
                StatusMessage = "Semantic search gotowy";
                return;
            }

            _isInitializing = true;

            // Zapytaj użytkownika, czy chce pobrać duży model, jeśli nie jest jeszcze zainicjalizowany
            if (!await _embeddingService.IsModelDownloadedAsync())
            {
                bool proceed = await (PageHelper.CurrentPage?.DisplayAlertAsync(
                    "Pobieranie modelu",
                    "Model E5-large (~1,6 GB) zostanie pobrany. Może to potrwać kilka minut. Czy kontynuować?",
                    "Tak", "Nie") ?? Task.FromResult(false));
                if (!proceed)
                {
                    await Shell.Current.GoToAsync("//MainPage");
                    _isInitializing = false;
                    return;
                }
            }

            IsSearching = true;
            StatusMessage = "🔄 Inicjalizacja modelu embeddingów...";

            try
            {
                await _embeddingService.InitializeAsync();
                IsEmbeddingInitialized = _embeddingService.IsInitialized;
                
                if (IsEmbeddingInitialized)
                {
                    StatusMessage = "✅ Model embeddingów zainicjalizowany";
                    // Check embedding statistics
                    var (withEmbeddings, total) = await _databaseService.GetEmbeddingStatsAsync();
                    StatusMessage += $" | Wiadomości z embeddingami: {withEmbeddings}/{total}";

                    // Automatycznie generuj embeddingi jeśli są braki
                    if (withEmbeddings < total)
                    {
                        StatusMessage += " | Generowanie embeddingów dla brakujących wiadomości...";
                        await GenerateEmbeddingsAsync();
                    }
                }
                else
                {
                    StatusMessage = "❌ Nie udało się zainicjalizować modelu";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Błąd inicjalizacji: {_errorHandlingService.GetUserFriendlyErrorMessage(ex, "embedding initialization")}";
            }
            finally
            {
                IsSearching = false;
                _isInitializing = false;
            }
        }

        private async Task SearchAsync()
        {
            _logger?.LogInformation("Search button clicked | query='{Query}'", SearchQuery);
            if (!CanPerformSearch()) return;

            IsSearching = true;
            StatusMessage = "🔍 Wyszukiwanie...";
            SearchResults.Clear();

            try
            {
                // Generate embedding for search query
                var queryEmbedding = await _embeddingService!.GenerateEmbeddingAsync(SearchQuery, true);
                if (queryEmbedding == null)
                {
                    _logger?.LogWarning("Failed to generate embedding for query");
                    StatusMessage = "❌ Nie udało się wygenerować embeddingu dla zapytania";
                    return;
                }

                // Perform semantic search
                var results = await _databaseService.SemanticSearchAcrossConversationsAsync(
                    queryEmbedding, MinSimilarity, MaxResults);

                _logger?.LogInformation("Semantic search returned {Count} raw results", results.Count);

                // Convert to ViewModel results
                foreach (var (message, similarity, conversationTitle) in results)
                {
                    SearchResults.Add(new SemanticSearchResult
                    {
                        Message = message,
                        SimilarityScore = similarity,
                        ConversationTitle = conversationTitle,
                        MessageTimestamp = message.Timestamp
                    });
                }

                StatusMessage = SearchResults.Count > 0 
                    ? $"✅ Znaleziono {SearchResults.Count} wyników"
                    : "ℹ️ Nie znaleziono pasujących wiadomości";
                _logger?.LogInformation("Filtered results count (after similarity threshold): {Count}", SearchResults.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during semantic search");
                StatusMessage = $"❌ Błąd wyszukiwania: {_errorHandlingService.GetUserFriendlyErrorMessage(ex, "semantic search")}";
            }
            finally
            {
                IsSearching = false;
            }
        }

        private void ClearResults()
        {
            SearchResults.Clear();
            StatusMessage = "Wyniki wyczyszczone";
        }

        private async Task NavigateToMessageAsync(SemanticSearchResult? result)
        {
            if (result?.Message == null) return;

            // Nawigacja do MainPage z parametrami
            var conversationId = result.Message.ConversationId;
            var messageId = result.Message.Id;
            try
            {
                await Shell.Current.GoToAsync($"//MainPage?conversationId={conversationId}&messageId={messageId}", true);
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Błąd nawigacji: {ex.Message}";
            }
        }

        private async Task GenerateEmbeddingsAsync()
        {
            if (IsGeneratingEmbeddings) return;

            try
            {
                IsGeneratingEmbeddings = true;
                StatusMessage = "🔄 Generowanie embeddingów...";
                EmbeddingProgress = "Inicjalizacja...";

                var progress = new Progress<EmbeddingPipelineProgress>(OnEmbeddingProgress);
                var result = await _embeddingPipelineService.GenerateEmbeddingsForAllMessagesAsync(progress);

                if (result.Success)
                {
                    StatusMessage = $"✅ Wygenerowano embeddingi dla {result.SuccessfulEmbeddings}/{result.TotalProcessed} wiadomości w {result.TotalTime:mm\\:ss}";
                    EmbeddingProgress = "Zakończono";
                }
                else
                {
                    StatusMessage = $"⚠️ Częściowy sukces: {result.SuccessfulEmbeddings}/{result.TotalProcessed} embeddingów. Błędy: {result.FailedEmbeddings}";
                    EmbeddingProgress = result.ErrorMessage;
                }

                // Refresh stats after generation
                await CheckEmbeddingStatsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Błąd generowania embeddingów: {_errorHandlingService.GetUserFriendlyErrorMessage(ex, "embedding generation")}";
                EmbeddingProgress = "Błąd";
            }
            finally
            {
                IsGeneratingEmbeddings = false;
            }
        }

        private async Task DownloadModelAsync()
        {
            if (_embeddingService == null)
            {
                StatusMessage = "❌ Embedding service nie jest dostępny";
                return;
            }
            IsDownloadingModel = true;
            StatusMessage = "🔄 Pobieranie i inicjalizacja modelu embeddingów...";
            try
            {
                await _embeddingService.InitializeAsync();
                IsEmbeddingInitialized = _embeddingService.IsInitialized;
                if (IsEmbeddingInitialized)
                {
                    StatusMessage = "✅ Model embeddingów pobrany i zainicjalizowany";
                    var (withEmbeddings, total) = await _databaseService.GetEmbeddingStatsAsync();
                    StatusMessage += $" | Wiadomości z embeddingami: {withEmbeddings}/{total}";
                }
                else
                {
                    StatusMessage = "❌ Nie udało się pobrać lub zainicjalizować modelu";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Błąd pobierania/instalacji: {_errorHandlingService.GetUserFriendlyErrorMessage(ex, "pobieranie modelu embeddingów")}";
            }
            finally
            {
                IsDownloadingModel = false;
            }
        }

        private void OnEmbeddingProgress(EmbeddingPipelineProgress progress)
        {
            EmbeddingProgress = $"{progress.ProcessedMessages}/{progress.TotalMessages} ({progress.ProgressPercentage:F1}%) - {progress.CurrentMessage}";
            
            if (progress.EstimatedTimeRemaining > TimeSpan.Zero)
            {
                EmbeddingProgress += $" - ETA: {progress.EstimatedTimeRemaining:mm\\:ss}";
            }
        }

        private async Task CheckEmbeddingStatsAsync()
        {
            try
            {
                var stats = await _embeddingPipelineService.GetEmbeddingStatsAsync();
                
                TotalMessages = stats.TotalMessages;
                MessagesWithEmbeddings = stats.MessagesWithEmbeddings;
                EmbeddingCoverage = stats.EmbeddingCoverage;
            }
            catch (Exception ex)
            {
                // Silent error - just log it
                System.Diagnostics.Debug.WriteLine($"Error checking embedding stats: {ex.Message}");
            }
        }

        private async Task GoBackAsync()
        {
            try
            {
                await Shell.Current.GoToAsync("//MainPage", true); // nawigacja na stronę główną
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Błąd nawigacji: {ex.Message}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 