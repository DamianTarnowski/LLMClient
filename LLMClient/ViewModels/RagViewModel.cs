using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LLMClient.Models;
using LLMClient.Services;

namespace LLMClient.ViewModels;

public class RagViewModel : INotifyPropertyChanged
{
    private readonly IRagService _ragService;
    private readonly IIngestionService _ingestionService;
    private readonly IEmbeddingService? _embeddingService;

    private bool _isLoading;
    private bool _isProcessing;
    private string _statusMessage = string.Empty;
    private int _pendingEmbeddings;
    private double _ingestionProgress;

    public ObservableCollection<RagDocument> Documents { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set { _isProcessing = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanProcess)); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public int PendingEmbeddings
    {
        get => _pendingEmbeddings;
        set { _pendingEmbeddings = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasPendingEmbeddings)); }
    }

    public bool HasPendingEmbeddings => PendingEmbeddings > 0;

    public double IngestionProgress
    {
        get => _ingestionProgress;
        set { _ingestionProgress = value; OnPropertyChanged(); }
    }

    public bool CanProcess => !IsProcessing && Documents.Count > 0;

    public ICommand LoadDocumentsCommand { get; }
    public ICommand AddDocumentCommand { get; }
    public ICommand DeleteDocumentCommand { get; }
    public ICommand GenerateEmbeddingsCommand { get; }

    public RagViewModel(IRagService ragService, IIngestionService ingestionService, IEmbeddingService? embeddingService)
    {
        _ragService = ragService;
        _ingestionService = ingestionService;
        _embeddingService = embeddingService;

        LoadDocumentsCommand = new Command(async () => await LoadDocumentsAsync());
        AddDocumentCommand = new Command(async () => await AddDocumentAsync());
        DeleteDocumentCommand = new Command<RagDocument>(async (doc) => await DeleteDocumentAsync(doc));
        GenerateEmbeddingsCommand = new Command(async () => await GenerateEmbeddingsAsync(), () => CanProcess);

        _ingestionService.ProgressChanged += OnIngestionProgress;
        _ingestionService.ItemCompleted += OnIngestionCompleted;
        _ingestionService.ErrorOccurred += OnIngestionError;

        // Initial load
        Task.Run(LoadDocumentsAsync);
    }

    private async Task LoadDocumentsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Ładowanie dokumentów...";

            var docs = await _ragService.GetDocumentsAsync();
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Documents.Clear();
                foreach (var doc in docs)
                {
                    Documents.Add(doc);
                }
            });

            PendingEmbeddings = await _ragService.GetPendingChunksCountAsync();
            StatusMessage = $"Załadowano {docs.Count} dokumentów";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[RagViewModel] LoadDocumentsAsync error: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AddDocumentAsync()
    {
        try
        {
            var customFileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".pdf", ".docx", ".txt", ".md" } },
                { DevicePlatform.Android, new[] { "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "text/plain", "text/markdown" } },
                { DevicePlatform.iOS, new[] { "public.pdf", "org.openxmlformats.wordprocessingml.document", "public.plain-text", "net.daringfireball.markdown" } },
                { DevicePlatform.MacCatalyst, new[] { "public.pdf", "org.openxmlformats.wordprocessingml.document", "public.plain-text" } }
            });

            var options = new PickOptions
            {
                PickerTitle = "Wybierz dokumenty do dodania",
                FileTypes = customFileTypes
            };

            var results = await FilePicker.Default.PickMultipleAsync(options);
            if (results == null || !results.Any()) return;

            StatusMessage = $"Dodawanie {results.Count()} dokumentów...";

            foreach (var file in results)
            {
                await _ingestionService.EnqueueFileAsync(file.FullPath);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd wyboru pliku: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[RagViewModel] AddDocumentAsync error: {ex}");
        }
    }

    private async Task DeleteDocumentAsync(RagDocument? document)
    {
        if (document == null) return;

        try
        {
            var confirm = await Application.Current!.MainPage!.DisplayAlert(
                "Usuń dokument",
                $"Czy na pewno chcesz usunąć dokument '{document.FileName}'?",
                "Usuń", "Anuluj");

            if (!confirm) return;

            StatusMessage = $"Usuwanie {document.FileName}...";
            await _ragService.DeleteDocumentAsync(document.Id);

            MainThread.BeginInvokeOnMainThread(() => Documents.Remove(document));
            StatusMessage = $"Usunięto {document.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd usuwania: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[RagViewModel] DeleteDocumentAsync error: {ex}");
        }
    }

    private async Task GenerateEmbeddingsAsync()
    {
        if (_embeddingService == null || !_embeddingService.IsInitialized)
        {
            StatusMessage = "Model embeddingów nie jest załadowany";
            return;
        }

        try
        {
            IsProcessing = true;
            var progress = new Progress<string>(msg => StatusMessage = msg);
            
            await _ragService.GenerateEmbeddingsAsync(progress);
            
            PendingEmbeddings = await _ragService.GetPendingChunksCountAsync();
            StatusMessage = "Embeddingi wygenerowane";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd generowania: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[RagViewModel] GenerateEmbeddingsAsync error: {ex}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private void OnIngestionProgress(object? sender, IngestionProgressEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IngestionProgress = e.ProgressPercent;
            StatusMessage = $"{e.FileName}: {e.Status}";
        });
    }

    private void OnIngestionCompleted(object? sender, IngestionCompletedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            StatusMessage = $"Dodano {e.FileName} ({e.ChunkCount} chunków, {e.Duration.TotalSeconds:F1}s)";
            await LoadDocumentsAsync();
        });
    }

    private void OnIngestionError(object? sender, IngestionErrorEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var retry = e.WillRetry ? " (ponowna próba...)" : "";
            StatusMessage = $"Błąd {e.FileName}: {e.ErrorMessage}{retry}";
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
