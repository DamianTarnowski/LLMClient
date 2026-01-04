using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFLLM.Models;
using WPFLLM.Services;

namespace WPFLLM.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly IChatService _chatService;
    private readonly IRagService _ragService;
    private readonly ISettingsService _settingsService;
    private readonly IExportService _exportService;
    private CancellationTokenSource? _streamCts;

    [ObservableProperty]
    private ObservableCollection<Conversation> _conversations = [];

    [ObservableProperty]
    private Conversation? _selectedConversation;

    [ObservableProperty]
    private ObservableCollection<ChatMessageViewModel> _messages = [];

    [ObservableProperty]
    private bool _hasMessages;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _showSearchResults;

    [ObservableProperty]
    private ObservableCollection<SearchResultViewModel> _searchResults = [];

    [ObservableProperty]
    private int _pendingEmbeddingsCount;

    [ObservableProperty]
    private bool _isGeneratingEmbeddings;

    [ObservableProperty]
    private string _embeddingProgress = string.Empty;

    // RAG Debug Panel - Flight Recorder
    [ObservableProperty]
    private bool _showDebugPanel;

    [ObservableProperty]
    private RetrievalResult? _lastRetrievalResult;

    [ObservableProperty]
    private RagTrace? _lastRagTrace;

    [ObservableProperty]
    private ObservableCollection<RetrievedChunkViewModel> _retrievedChunks = [];

    [ObservableProperty]
    private ObservableCollection<RagCandidateViewModel> _allCandidates = [];

    [ObservableProperty]
    private ObservableCollection<RagTimingViewModel> _timings = [];

    [ObservableProperty]
    private RetrievalMode _selectedRetrievalMode = RetrievalMode.Hybrid;

    [ObservableProperty]
    private int _selectedDebugTab;

    public IReadOnlyList<RetrievalMode> RetrievalModes { get; } = Enum.GetValues<RetrievalMode>();

    [ObservableProperty]
    private string _warningMessage = string.Empty;

    [ObservableProperty]
    private bool _hasWarning;

    [ObservableProperty]
    private bool _isRagEnabled;

    [ObservableProperty]
    private int _ragChunksFound;

    [ObservableProperty]
    private string _ragStatusText = string.Empty;

    [ObservableProperty]
    private bool _useRag;

    public ChatViewModel(IChatService chatService, IRagService ragService, ISettingsService settingsService, IExportService exportService)
    {
        _chatService = chatService;
        _ragService = ragService;
        _settingsService = settingsService;
        _exportService = exportService;
        
        _chatService.RagContextRetrieved += OnRagContextRetrieved;
        _settingsService.SettingsChanged += OnSettingsChanged;
        _ = InitializeAsync();
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            UseRag = settings.UseRag;
        });
    }

    async partial void OnUseRagChanged(bool value)
    {
        var settings = await _settingsService.GetSettingsAsync();
        if (settings.UseRag != value)
        {
            settings.UseRag = value;
            await _settingsService.SaveSettingsAsync(settings);
        }
    }

    private void OnRagContextRetrieved(object? sender, RagContextInfo info)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsRagEnabled = info.IsEnabled;
            RagChunksFound = info.ChunksFound;
            if (info.IsEnabled)
            {
                RagStatusText = info.ChunksFound > 0 
                    ? $"📚 RAG: {info.ChunksFound} chunks" 
                    : "📚 RAG: brak dopasowań";
                
                // Populate debug panel with full trace data
                if (info.Result != null && info.Trace != null)
                {
                    LastRetrievalResult = info.Result;
                    LastRagTrace = info.Trace;
                    
                    RetrievedChunks.Clear();
                    foreach (var chunk in info.Result.Chunks)
                    {
                        RetrievedChunks.Add(new RetrievedChunkViewModel(chunk));
                    }

                    AllCandidates.Clear();
                    foreach (var candidate in info.Trace.Candidates)
                    {
                        AllCandidates.Add(new RagCandidateViewModel(candidate));
                    }

                    Timings.Clear();
                    foreach (var timing in info.Trace.Timings)
                    {
                        Timings.Add(new RagTimingViewModel(timing));
                    }
                }
            }
            else
            {
                RagStatusText = string.Empty;
            }
        });
    }

    private async Task InitializeAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        UseRag = settings.UseRag;
        await LoadConversationsAsync();
        await UpdatePendingEmbeddingsCountAsync();
    }

    private async Task UpdatePendingEmbeddingsCountAsync()
    {
        PendingEmbeddingsCount = await _chatService.GetMessagesWithoutEmbeddingsCountAsync();
    }

    private async Task LoadConversationsAsync()
    {
        var conversations = await _chatService.GetConversationsAsync();
        Conversations = new ObservableCollection<Conversation>(conversations);
    }

    async partial void OnSelectedConversationChanged(Conversation? value)
    {
        Messages.Clear();
        HasMessages = false;
        if (value == null) return;

        var messages = await _chatService.GetMessagesAsync(value.Id);
        foreach (var msg in messages)
        {
            Messages.Add(new ChatMessageViewModel(msg));
        }
        HasMessages = Messages.Count > 0;
    }

    [RelayCommand]
    private async Task NewConversationAsync()
    {
        var conversation = await _chatService.CreateConversationAsync($"Chat {DateTime.Now:g}");
        Conversations.Insert(0, conversation);
        SelectedConversation = conversation;
    }

    [RelayCommand]
    private async Task DeleteConversationAsync()
    {
        if (SelectedConversation == null) return;
        
        await _chatService.DeleteConversationAsync(SelectedConversation.Id);
        Conversations.Remove(SelectedConversation);
        SelectedConversation = Conversations.FirstOrDefault();
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        // Validate API configuration
        var settings = await _settingsService.GetSettingsAsync();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            WarningMessage = GetLocalizedString("Validation_NoApiKey");
            HasWarning = true;
            return;
        }

        HasWarning = false;
        WarningMessage = string.Empty;

        if (SelectedConversation == null)
        {
            await NewConversationAsync();
        }

        var userMessage = InputText.Trim();
        InputText = string.Empty;

        var userMsgVm = new ChatMessageViewModel(new ChatMessage
        {
            Role = "user",
            Content = userMessage,
            ConversationId = SelectedConversation!.Id
        });
        Messages.Add(userMsgVm);
        HasMessages = true;

        var assistantMsgVm = new ChatMessageViewModel(new ChatMessage
        {
            Role = "assistant",
            Content = string.Empty,
            ConversationId = SelectedConversation.Id
        });
        Messages.Add(assistantMsgVm);

        IsStreaming = true;
        StatusText = "Streaming...";
        _streamCts = new CancellationTokenSource();

        var responseBuilder = new StringBuilder();

        try
        {
            await foreach (var chunk in _chatService.SendMessageAsync(
                SelectedConversation.Id, 
                userMessage, 
                _streamCts.Token))
            {
                responseBuilder.Append(chunk);
                assistantMsgVm.Content = responseBuilder.ToString();
            }

            var savedMessage = await _chatService.AddMessageAsync(
                SelectedConversation.Id, 
                "assistant", 
                responseBuilder.ToString());
            assistantMsgVm.Message = savedMessage;

            // Generate title after first exchange (2 messages: user + assistant)
            if (Messages.Count == 2 && SelectedConversation.Title.StartsWith("Chat "))
            {
                _ = GenerateTitleAsync(userMessage, responseBuilder.ToString());
            }
        }
        catch (OperationCanceledException)
        {
            assistantMsgVm.Content += "\n[Cancelled]";
        }
        catch (Exception ex)
        {
            assistantMsgVm.Content += $"\n[Error: {ex.Message}]";
        }
        finally
        {
            IsStreaming = false;
            StatusText = "Ready";
            _streamCts?.Dispose();
            _streamCts = null;
        }
    }

    [RelayCommand]
    private void StopStreaming()
    {
        _streamCts?.Cancel();
    }

    private async Task GenerateTitleAsync(string userMessage, string assistantResponse)
    {
        try
        {
            var title = await _chatService.GenerateConversationTitleAsync(userMessage, assistantResponse);
            if (!string.IsNullOrWhiteSpace(title) && SelectedConversation != null)
            {
                SelectedConversation.Title = title;
                await _chatService.UpdateConversationAsync(SelectedConversation);
            }
        }
        catch
        {
            // Silently fail - title generation is not critical
        }
    }

    [RelayCommand]
    private async Task UpdateConversationTitleAsync(Conversation? conv)
    {
        if (conv == null) return;
        await _chatService.UpdateConversationAsync(conv);
    }

    [RelayCommand]
    private async Task DeleteMessageAsync(ChatMessageViewModel? messageVm)
    {
        if (messageVm?.Message.Id == 0) return;
        
        await _chatService.DeleteMessageAsync(messageVm!.Message.Id);
        Messages.Remove(messageVm);
        HasMessages = Messages.Count > 0;
    }

    [RelayCommand]
    private async Task SemanticSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        IsSearching = true;
        ShowSearchResults = true;
        SearchResults.Clear();
        StatusText = "Searching...";

        try
        {
            var results = await _chatService.SemanticSearchAsync(SearchQuery);
            foreach (var result in results)
            {
                SearchResults.Add(new SearchResultViewModel(result));
            }
            StatusText = $"Found {results.Count} results";
        }
        catch (Exception ex)
        {
            StatusText = $"Search error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private void GoToSearchResult(SearchResultViewModel? result)
    {
        if (result == null) return;

        ShowSearchResults = false;
        SearchQuery = string.Empty;

        var conv = Conversations.FirstOrDefault(c => c.Id == result.Result.Conversation.Id);
        if (conv != null)
        {
            SelectedConversation = conv;
        }
    }

    [RelayCommand]
    private void CloseSearch()
    {
        ShowSearchResults = false;
        SearchQuery = string.Empty;
        SearchResults.Clear();
    }

    [RelayCommand]
    private void ToggleDebugPanel()
    {
        ShowDebugPanel = !ShowDebugPanel;
    }

    [RelayCommand]
    private async Task TestRetrievalAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            MessageBox.Show(
                "Wpisz zapytanie w pole tekstowe, a następnie kliknij 'Test RAG'.\n\n" +
                "Ta funkcja testuje wyszukiwanie w bazie wiedzy bez wysyłania do LLM.\n" +
                "Zobaczysz jakie fragmenty dokumentów zostaną użyte jako kontekst.",
                "Test RAG - Instrukcja",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        StatusText = "Testing retrieval...";
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            var (result, trace) = await _ragService.RetrieveWithTraceAsync(
                InputText, 
                settings.RagTopK, 
                settings.RagMinSimilarity, 
                settings.RagRetrievalMode,
                settings.RrfK,
                settings.HybridBalance);
            LastRetrievalResult = result;
            LastRagTrace = trace;
            
            // Update chunks (selected only)
            RetrievedChunks.Clear();
            foreach (var chunk in result.Chunks)
            {
                RetrievedChunks.Add(new RetrievedChunkViewModel(chunk));
            }

            // Update all candidates (for debug table)
            AllCandidates.Clear();
            foreach (var candidate in trace.Candidates)
            {
                AllCandidates.Add(new RagCandidateViewModel(candidate));
            }

            // Update timings
            Timings.Clear();
            foreach (var timing in trace.Timings)
            {
                Timings.Add(new RagTimingViewModel(timing));
            }

            ShowDebugPanel = true;
            StatusText = $"Retrieved {result.Chunks.Count}/{trace.TotalCandidates} chunks in {trace.TotalTimeMs}ms";
        }
        catch (Exception ex)
        {
            StatusText = $"Retrieval error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GenerateEmbeddingsAsync()
    {
        if (IsGeneratingEmbeddings) return;

        IsGeneratingEmbeddings = true;
        var progress = new Progress<string>(p => EmbeddingProgress = p);

        try
        {
            await _chatService.GenerateMessageEmbeddingsAsync(progress);
            await UpdatePendingEmbeddingsCountAsync();
        }
        catch (Exception ex)
        {
            EmbeddingProgress = $"Error: {ex.Message}";
        }
        finally
        {
            IsGeneratingEmbeddings = false;
        }
    }

    private static string GetLocalizedString(string key)
    {
        return Application.Current.Resources[key] as string ?? key;
    }

    [RelayCommand]
    private async Task ExportConversationAsync()
    {
        if (SelectedConversation == null) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Conversation",
            FileName = $"{SelectedConversation.Title.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}",
            Filter = "JSON (*.json)|*.json|Markdown (*.md)|*.md",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var messages = await _chatService.GetMessagesAsync(SelectedConversation.Id);
                var format = dialog.FilterIndex == 1 ? ExportFormat.Json : ExportFormat.Markdown;
                await _exportService.ExportToFileAsync(SelectedConversation, messages, dialog.FileName, format);
                StatusText = $"Exported to {System.IO.Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                StatusText = $"Export failed: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private async Task ImportConversationAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import Conversation",
            Filter = "JSON files (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var (conversation, messages) = await _exportService.ImportFromJsonAsync(dialog.FileName);
                
                // Create new conversation in database
                var newConv = await _chatService.CreateConversationAsync(conversation.Title + " (imported)");
                
                // Add messages
                foreach (var msg in messages)
                {
                    await _chatService.AddMessageAsync(newConv.Id, msg.Role, msg.Content);
                }
                
                await LoadConversationsAsync();
                SelectedConversation = Conversations.FirstOrDefault(c => c.Id == newConv.Id);
                StatusText = $"Imported: {newConv.Title}";
            }
            catch (Exception ex)
            {
                StatusText = $"Import failed: {ex.Message}";
            }
        }
    }
}

public partial class ChatMessageViewModel : ObservableObject
{
    [ObservableProperty]
    private ChatMessage _message;

    [ObservableProperty]
    private string _content;

    public string Role => Message.Role;
    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";

    public ChatMessageViewModel(ChatMessage message)
    {
        _message = message;
        _content = message.Content;
    }
}

public partial class SearchResultViewModel : ObservableObject
{
    public MessageSearchResult Result { get; }
    
    public string ConversationTitle => Result.Conversation.Title;
    public string MessageContent => Result.Message.Content.Length > 200 
        ? Result.Message.Content[..200] + "..." 
        : Result.Message.Content;
    public string Role => Result.Message.Role;
    public bool IsUser => Role == "user";
    public double Score => Result.Score;
    public string ScoreText => $"{Score:P0}";
    public DateTime CreatedAt => Result.Message.CreatedAt;

    public SearchResultViewModel(MessageSearchResult result)
    {
        Result = result;
    }
}

public partial class RetrievedChunkViewModel : ObservableObject
{
    public RetrievedChunk Chunk { get; }
    
    public string DocumentName => Chunk.DocumentName;
    public string Content => Chunk.Content.Length > 300 
        ? Chunk.Content[..300] + "..." 
        : Chunk.Content;
    public string FullContent => Chunk.Content;
    public int ChunkIndex => Chunk.ChunkIndex;
    public double VectorScore => Chunk.VectorScore;
    public double KeywordScore => Chunk.KeywordScore;
    public double FusedScore => Chunk.FusedScore;
    
    public string VectorScoreText => VectorScore > 0 ? $"{VectorScore:P1}" : "-";
    public string KeywordScoreText => KeywordScore > 0 ? $"{KeywordScore:F2}" : "-";
    public string FusedScoreText => $"{FusedScore:F4}";

    public RetrievedChunkViewModel(RetrievedChunk chunk)
    {
        Chunk = chunk;
    }
}

/// <summary>
/// ViewModel for RAG candidate in debug table (all candidates, not just selected)
/// </summary>
public partial class RagCandidateViewModel : ObservableObject
{
    public RagChunkCandidate Candidate { get; }
    
    public int Rank => Candidate.Rank;
    public string SourceName => Candidate.SourceName ?? "Unknown";
    public int? ChunkIndex => Candidate.ChunkIndex;
    public float VectorScore => Candidate.VectorScore;
    public float KeywordScore => Candidate.KeywordScore;
    public float FinalScore => Candidate.FinalScore;
    public int TokenCount => Candidate.TokenCount;
    public bool Included => Candidate.Included;
    public string Preview => Candidate.Preview;
    
    public string VectorScoreText => VectorScore > 0 ? $"{VectorScore:P1}" : "-";
    public string KeywordScoreText => KeywordScore > 0 ? $"{KeywordScore:F2}" : "-";
    public string FinalScoreText => $"{FinalScore:F4}";
    public string TokensText => $"{TokenCount} tok";
    public string IncludedIcon => Included ? "✓" : "";
    
    // Row styling
    public string RowBackground => Included ? "#1A3B82F6" : "Transparent";

    public RagCandidateViewModel(RagChunkCandidate candidate)
    {
        Candidate = candidate;
    }
}

/// <summary>
/// ViewModel for pipeline timing measurement
/// </summary>
public partial class RagTimingViewModel : ObservableObject
{
    public RagTiming Timing { get; }
    
    public string Name => Timing.Name;
    public long ElapsedMs => Timing.ElapsedMs;
    public string ElapsedText => $"{ElapsedMs}ms";
    
    // Bar width for visual representation (max 200px for 1000ms)
    public double BarWidth => Math.Min(200, ElapsedMs * 0.2);

    public RagTimingViewModel(RagTiming timing)
    {
        Timing = timing;
    }
}
