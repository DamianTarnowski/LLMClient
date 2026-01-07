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
        private SearchMode _searchMode = SearchMode.Hybrid;
        private float _vectorWeight = 0.7f;
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

        public SearchMode SelectedSearchMode
        {
            get => _searchMode;
            set
            {
                if (_searchMode != value)
                {
                    _searchMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SearchModeText));
                    OnPropertyChanged(nameof(IsVectorMode));
                    OnPropertyChanged(nameof(IsTextMode));
                    OnPropertyChanged(nameof(IsHybridMode));
                    OnPropertyChanged(nameof(ShowVectorSettings));
                    OnPropertyChanged(nameof(CanSearch));
                }
            }
        }

        public float VectorWeight
        {
            get => _vectorWeight;
            set
            {
                _vectorWeight = Math.Clamp(value, 0f, 1f);
                OnPropertyChanged();
                OnPropertyChanged(nameof(VectorWeightPercentage));
                OnPropertyChanged(nameof(TextWeightPercentage));
            }
        }

        public string SearchModeText => SelectedSearchMode switch
        {
            SearchMode.Vector => "Wektorowe",
            SearchMode.Text => "Tekstowe",
            SearchMode.Hybrid => "Hybrydowe",
            _ => "Hybrydowe"
        };

        public bool IsVectorMode => SelectedSearchMode == SearchMode.Vector;
        public bool IsTextMode => SelectedSearchMode == SearchMode.Text;
        public bool IsHybridMode => SelectedSearchMode == SearchMode.Hybrid;
        public bool ShowVectorSettings => SelectedSearchMode != SearchMode.Text;
        public string VectorWeightPercentage => $"{VectorWeight * 100:F0}%";
        public string TextWeightPercentage => $"{(1f - VectorWeight) * 100:F0}%";

        public bool CanSearch => (SelectedSearchMode == SearchMode.Text || IsEmbeddingInitialized) && !IsSearching;

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
            CheckEmbeddingStatsCommand = new Command(async () => await RefreshAsync());
            DownloadModelCommand = new Command(async () => await DownloadModelAsync(), () => !IsDownloadingModel);
            GoBackCommand = new Command(async () => await GoBackAsync());

            CheckEmbeddingServiceStatus();
            _ = Task.Run(async () => await CheckEmbeddingStatsAsync());
        }

        public async Task OnAppearingAsync()
        {
            await InitializeEmbeddingServiceAsync();
            await RefreshAsync();
        }

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
                    var (withEmbeddings, total) = await _databaseService.GetEmbeddingStatsAsync();
                    StatusMessage += $" | Wiadomości z embeddingami: {withEmbeddings}/{total}";

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

        private bool CanPerformSearch()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery) || IsSearching)
                return false;
            
            if (SelectedSearchMode == SearchMode.Text)
                return true;
            
            return IsEmbeddingInitialized;
        }

        private async Task SearchAsync()
        {
            _logger?.LogInformation("Search button clicked | query='{Query}' | mode={Mode}", SearchQuery, SelectedSearchMode);
            if (!CanPerformSearch()) return;

            IsSearching = true;
            var modeLabel = SearchModeText;
            StatusMessage = $"🔍 Wyszukiwanie ({modeLabel})...";
            SearchResults.Clear();

            try
            {
                List<(Message message, float score, string conversationTitle)> results;

                switch (SelectedSearchMode)
                {
                    case SearchMode.Text:
                        var textResults = await _databaseService.TextSearchAcrossConversationsAsync(SearchQuery, MaxResults);
                        results = textResults.Select(r => (r.message, r.matchScore, r.conversationTitle)).ToList();
                        break;

                    case SearchMode.Hybrid:
                        var hybridResults = await PerformHybridSearchAsync();
                        results = hybridResults;
                        break;

                    case SearchMode.Vector:
                    default:
                        var queryEmbedding = await _embeddingService!.GenerateEmbeddingAsync(SearchQuery, true);
                        if (queryEmbedding == null)
                        {
                            _logger?.LogWarning("Failed to generate embedding for query");
                            StatusMessage = "❌ Nie udało się wygenerować embeddingu dla zapytania";
                            return;
                        }
                        var vectorResults = await _databaseService.SemanticSearchAcrossConversationsAsync(
                            queryEmbedding, MinSimilarity, MaxResults);
                        results = vectorResults.Select(r => (r.message, r.similarity, r.conversationTitle)).ToList();
                        break;
                }

                _logger?.LogInformation("{Mode} search returned {Count} results", SelectedSearchMode, results.Count);

                foreach (var (message, score, conversationTitle) in results)
                {
                    SearchResults.Add(new SemanticSearchResult
                    {
                        Message = message,
                        SimilarityScore = score,
                        ConversationTitle = conversationTitle,
                        MessageTimestamp = message.Timestamp
                    });
                }

                StatusMessage = SearchResults.Count > 0 
                    ? $"✅ Znaleziono {SearchResults.Count} wyników ({modeLabel})"
                    : $"ℹ️ Nie znaleziono pasujących wiadomości ({modeLabel})";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during {Mode} search", SelectedSearchMode);
                StatusMessage = $"❌ Błąd wyszukiwania: {_errorHandlingService.GetUserFriendlyErrorMessage(ex, "search")}";
            }
            finally
            {
                IsSearching = false;
            }
        }

        private async Task<List<(Message message, float score, string conversationTitle)>> PerformHybridSearchAsync()
        {
            var textWeight = 1f - VectorWeight;
            
            var textResults = await _databaseService.TextSearchAcrossConversationsAsync(SearchQuery, MaxResults * 2);
            
            List<(Message message, float similarity, string conversationTitle)> vectorResults = new();
            if (_embeddingService != null && _embeddingService.IsInitialized)
            {
                var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(SearchQuery, true);
                if (queryEmbedding != null)
                {
                    vectorResults = await _databaseService.SemanticSearchAcrossConversationsAsync(
                        queryEmbedding, MinSimilarity, MaxResults * 2);
                }
            }

            var combinedScores = new Dictionary<int, (Message message, string title, float textScore, float vectorScore)>();

            foreach (var r in textResults)
            {
                var msgId = r.message.Id;
                if (!combinedScores.ContainsKey(msgId))
                    combinedScores[msgId] = (r.message, r.conversationTitle, r.matchScore, 0f);
                else
                {
                    var existing = combinedScores[msgId];
                    combinedScores[msgId] = (existing.message, existing.title, r.matchScore, existing.vectorScore);
                }
            }

            foreach (var r in vectorResults)
            {
                var msgId = r.message.Id;
                if (!combinedScores.ContainsKey(msgId))
                    combinedScores[msgId] = (r.message, r.conversationTitle, 0f, r.similarity);
                else
                {
                    var existing = combinedScores[msgId];
                    combinedScores[msgId] = (existing.message, existing.title, existing.textScore, r.similarity);
                }
            }

            return combinedScores.Values
                .Select(x =>
                {
                    var hybridScore = (x.textScore * textWeight) + (x.vectorScore * VectorWeight);
                    return (x.message, hybridScore, x.title);
                })
                .Where(r => r.hybridScore >= MinSimilarity * 0.5f)
                .OrderByDescending(r => r.hybridScore)
                .Take(MaxResults)
                .ToList();
        }

        private void ClearResults()
        {
            SearchResults.Clear();
            StatusMessage = "Wyniki wyczyszczone";
        }

        private async Task NavigateToMessageAsync(SemanticSearchResult? result)
        {
            if (result?.Message == null) return;

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
            var text = $"{progress.ProcessedMessages}/{progress.TotalMessages} ({progress.ProgressPercentage:F1}%) - {progress.CurrentMessage}";
            
            if (progress.EstimatedTimeRemaining > TimeSpan.Zero)
            {
                text += $" - ETA: {progress.EstimatedTimeRemaining:mm\\:ss}";
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                EmbeddingProgress = text;
            });
        }

        private async Task CheckEmbeddingStatsAsync()
        {
            try
            {
                var stats = await _embeddingPipelineService.GetEmbeddingStatsAsync();
                
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    TotalMessages = stats.TotalMessages;
                    MessagesWithEmbeddings = stats.MessagesWithEmbeddings;
                    EmbeddingCoverage = stats.EmbeddingCoverage;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking embedding stats: {ex.Message}");
            }
        }

        private async Task GoBackAsync()
        {
            try
            {
                await Shell.Current.GoToAsync("//MainPage", true);
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
