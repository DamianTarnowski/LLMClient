using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WPFLLM.Models;
using WPFLLM.Services;

namespace WPFLLM.ViewModels;

public partial class RagViewModel : ObservableObject
{
    private readonly IRagService _ragService;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _embeddingCts;

    [ObservableProperty]
    private ObservableCollection<RagDocument> _documents = [];

    [ObservableProperty]
    private RagDocument? _selectedDocument;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _hasDocuments;

    [ObservableProperty]
    private string _welcomeMessage = "";

    [ObservableProperty]
    private bool _useRag;

    [ObservableProperty]
    private int _ragTopK = 3;

    [ObservableProperty]
    private double _ragMinSimilarity = 0.5;

    [ObservableProperty]
    private RetrievalMode _ragRetrievalMode = RetrievalMode.Hybrid;

    [ObservableProperty]
    private double _rrfK = 60.0;

    [ObservableProperty]
    private double _hybridBalance = 0.5;

    public bool IsHybridMode => RagRetrievalMode == RetrievalMode.Hybrid;

    public IReadOnlyList<RetrievalMode> RetrievalModes { get; } = Enum.GetValues<RetrievalMode>();

    public RagViewModel(IRagService ragService, ISettingsService settingsService)
    {
        _ragService = ragService;
        _settingsService = settingsService;
        _settingsService.SettingsChanged += OnSettingsChanged;
        _ = InitializeAsync();
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Sync without triggering save again (direct field access is intentional)
            #pragma warning disable MVVMTK0034
            if (_useRag != settings.UseRag) { _useRag = settings.UseRag; OnPropertyChanged(nameof(UseRag)); }
            if (_ragTopK != settings.RagTopK) { _ragTopK = settings.RagTopK; OnPropertyChanged(nameof(RagTopK)); }
            if (_ragMinSimilarity != settings.RagMinSimilarity) { _ragMinSimilarity = settings.RagMinSimilarity; OnPropertyChanged(nameof(RagMinSimilarity)); }
            if (_ragRetrievalMode != settings.RagRetrievalMode) { _ragRetrievalMode = settings.RagRetrievalMode; OnPropertyChanged(nameof(RagRetrievalMode)); }
            if (_rrfK != settings.RrfK) { _rrfK = settings.RrfK; OnPropertyChanged(nameof(RrfK)); }
            if (_hybridBalance != settings.HybridBalance) { _hybridBalance = settings.HybridBalance; OnPropertyChanged(nameof(HybridBalance)); }
            OnPropertyChanged(nameof(IsHybridMode));
            #pragma warning restore MVVMTK0034
        });
    }

    private async Task InitializeAsync()
    {
        await LoadSettingsAsync();
        await LoadDocumentsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        UseRag = settings.UseRag;
        RagTopK = settings.RagTopK;
        RagMinSimilarity = settings.RagMinSimilarity;
        RagRetrievalMode = settings.RagRetrievalMode;
        RrfK = settings.RrfK;
        HybridBalance = settings.HybridBalance;
    }

    async partial void OnUseRagChanged(bool value)
    {
        await SaveRagSettingsAsync();
    }

    async partial void OnRagTopKChanged(int value)
    {
        await SaveRagSettingsAsync();
    }

    async partial void OnRagMinSimilarityChanged(double value)
    {
        await SaveRagSettingsAsync();
    }

    async partial void OnRagRetrievalModeChanged(RetrievalMode value)
    {
        await SaveRagSettingsAsync();
    }

    async partial void OnRrfKChanged(double value)
    {
        await SaveRagSettingsAsync();
    }

    async partial void OnHybridBalanceChanged(double value)
    {
        await SaveRagSettingsAsync();
    }

    private async Task SaveRagSettingsAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        settings.UseRag = UseRag;
        settings.RagTopK = RagTopK;
        settings.RagMinSimilarity = RagMinSimilarity;
        settings.RagRetrievalMode = RagRetrievalMode;
        settings.RrfK = RrfK;
        settings.HybridBalance = HybridBalance;
        await _settingsService.SaveSettingsAsync(settings);
    }

    private async Task LoadDocumentsAsync()
    {
        var docs = await _ragService.GetDocumentsAsync();
        Documents = new ObservableCollection<RagDocument>(docs);
        UpdateDocumentStatus();
    }

    private void UpdateDocumentStatus()
    {
        HasDocuments = Documents.Count > 0;
        if (!HasDocuments)
        {
            WelcomeMessage = GetLocalizedString("Validation_EmptyKnowledgeBase");
        }
        else
        {
            WelcomeMessage = "";
        }
    }

    [RelayCommand]
    private async Task AddDocumentAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Wszystkie obsługiwane (*.txt;*.md;*.pdf;*.docx;*.json;*.csv;*.xml;*.html)|*.txt;*.md;*.markdown;*.pdf;*.docx;*.json;*.csv;*.xml;*.html;*.htm|PDF (*.pdf)|*.pdf|Word (*.docx)|*.docx|Tekstowe (*.txt;*.md)|*.txt;*.md;*.markdown|All files (*.*)|*.*",
            Multiselect = true,
            Title = "Wybierz pliki do bazy wiedzy RAG"
        };

        if (dialog.ShowDialog() == true)
        {
            IsProcessing = true;
            foreach (var file in dialog.FileNames)
            {
                StatusMessage = $"Dodawanie {System.IO.Path.GetFileName(file)}...";
                var doc = await _ragService.AddDocumentAsync(file);
                Documents.Add(doc);
            }
            UpdateDocumentStatus();
            
            // Auto-generate embeddings
            StatusMessage = "Generowanie embeddingów...";
            _embeddingCts = new CancellationTokenSource();
            var progress = new Progress<string>(msg => StatusMessage = msg);
            
            try
            {
                await _ragService.GenerateEmbeddingsAsync(progress, _embeddingCts.Token);
                StatusMessage = "Dokumenty dodane i embeddingi wygenerowane!";
                
                // Auto-enable RAG if not already
                if (!UseRag)
                {
                    UseRag = true;
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Generowanie przerwane.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Błąd: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
                _embeddingCts?.Dispose();
                _embeddingCts = null;
            }
        }
    }

    [RelayCommand]
    private async Task DeleteDocumentAsync()
    {
        if (SelectedDocument == null) return;

        await _ragService.DeleteDocumentAsync(SelectedDocument.Id);
        Documents.Remove(SelectedDocument);
        SelectedDocument = null;
        StatusMessage = "Document deleted.";
        UpdateDocumentStatus();
    }

    [RelayCommand]
    private async Task GenerateEmbeddingsAsync()
    {
        IsProcessing = true;
        _embeddingCts = new CancellationTokenSource();

        var progress = new Progress<string>(msg => StatusMessage = msg);

        try
        {
            await _ragService.GenerateEmbeddingsAsync(progress, _embeddingCts.Token);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Embedding generation cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            _embeddingCts?.Dispose();
            _embeddingCts = null;
        }
    }

    [RelayCommand]
    private void CancelEmbeddings()
    {
        _embeddingCts?.Cancel();
    }

    private static string GetLocalizedString(string key)
    {
        return Application.Current.Resources[key] as string ?? key;
    }
}
