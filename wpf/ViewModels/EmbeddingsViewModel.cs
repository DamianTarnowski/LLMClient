using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFLLM.Models;
using WPFLLM.Services;

namespace WPFLLM.ViewModels;

public partial class EmbeddingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IModelDownloadService _downloadService;
    private readonly ILocalEmbeddingService _localEmbeddingService;
    private readonly IRagService _ragService;
    private CancellationTokenSource? _downloadCts;
    private bool _isInitializing = true;

    [ObservableProperty]
    private ObservableCollection<EmbeddingModelViewModel> _availableModels = [];

    [ObservableProperty]
    private EmbeddingModelViewModel? _selectedModel;

    [ObservableProperty]
    private bool _useLocalEmbeddings;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatus = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private long _downloadedBytes;

    [ObservableProperty]
    private long _totalBytes;

    [ObservableProperty]
    private string _currentModelStatus = "Nie załadowano";

    public EmbeddingsViewModel(
        ISettingsService settingsService,
        IModelDownloadService downloadService,
        ILocalEmbeddingService localEmbeddingService,
        IRagService ragService)
    {
        _settingsService = settingsService;
        _downloadService = downloadService;
        _localEmbeddingService = localEmbeddingService;
        _ragService = ragService;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[EmbeddingsVM] InitializeAsync started");
            
            var settings = await _settingsService.GetSettingsAsync();
            UseLocalEmbeddings = settings.UseLocalEmbeddings;

            System.Diagnostics.Debug.WriteLine($"[EmbeddingsVM] Loading {EmbeddingModels.Available.Count} models");
            
            foreach (var (id, info) in EmbeddingModels.Available)
            {
                System.Diagnostics.Debug.WriteLine($"[EmbeddingsVM] Processing model: {id}");
                
                var status = await _downloadService.GetDownloadStatusAsync(id);
                var downloadedSize = await _downloadService.GetDownloadedSizeAsync(id);
                
                var vm = new EmbeddingModelViewModel
                {
                    Id = info.Id,
                    DisplayName = info.DisplayName,
                    Description = info.Description,
                    Dimensions = info.Dimensions,
                    SizeBytes = info.SizeBytes,
                    Languages = string.Join(", ", info.Languages),
                    Status = status,
                    DownloadedBytes = downloadedSize,
                    IsSelected = info.Id == settings.LocalEmbeddingModel,
                    QualityRating = info.QualityRating,
                    RamRequired = info.RamRequired,
                    InferenceSpeed = info.InferenceSpeed,
                    RecommendedFor = info.RecommendedFor
                };
                
                AvailableModels.Add(vm);
                
                if (vm.IsSelected)
                    SelectedModel = vm;
            }

            await UpdateCurrentModelStatusAsync();
            _isInitializing = false;
            System.Diagnostics.Debug.WriteLine("[EmbeddingsVM] InitializeAsync completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EmbeddingsVM] ERROR: {ex}");
            StatusMessage = $"Błąd inicjalizacji: {ex.Message}";
        }
    }

    private async Task UpdateCurrentModelStatusAsync()
    {
        if (await _localEmbeddingService.IsAvailableAsync())
        {
            var dims = _localEmbeddingService.GetDimensions();
            CurrentModelStatus = $"Załadowany ({dims} wymiarów)";
        }
        else if (UseLocalEmbeddings && SelectedModel != null)
        {
            if (SelectedModel.Status == ModelDownloadStatus.Downloaded)
                CurrentModelStatus = "Gotowy do załadowania";
            else
                CurrentModelStatus = "Model nie pobrany";
        }
        else
        {
            CurrentModelStatus = "Używam API";
        }
    }

    partial void OnUseLocalEmbeddingsChanged(bool oldValue, bool newValue)
    {
        if (_isInitializing) return;
        
        _ = HandleEmbeddingModeChangeAsync(oldValue, newValue);
    }

    private async Task HandleEmbeddingModeChangeAsync(bool oldValue, bool newValue)
    {
        var documents = await _ragService.GetDocumentsAsync();
        
        if (documents.Count > 0)
        {
            var modeFrom = oldValue 
                ? Application.Current.TryFindResource("Emb_Local") as string ?? "Local"
                : "API";
            var modeTo = newValue 
                ? Application.Current.TryFindResource("Emb_Local") as string ?? "Local" 
                : "API";
            
            var title = Application.Current.TryFindResource("Emb_ConfirmTitle") as string ?? "Confirm embedding mode change";
            var message = string.Format(
                Application.Current.TryFindResource("Emb_ConfirmMessage") as string 
                    ?? "Changing embedding mode from {0} to {1} will delete all {2} documents from the knowledge base.\n\nEmbeddings generated with different models are incompatible.\n\nDo you want to continue?",
                modeFrom, modeTo, documents.Count);
            
            var result = MessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                StatusMessage = Application.Current.TryFindResource("Emb_DeletingDocs") as string ?? "Deleting documents...";
                
                foreach (var doc in documents)
                {
                    await _ragService.DeleteDocumentAsync(doc.Id);
                }
                
                StatusMessage = string.Format(
                    Application.Current.TryFindResource("Emb_DocsDeleted") as string ?? "{0} documents deleted. Ready to add new documents.",
                    documents.Count);
                
                await SaveSettingsAsync();
                await UpdateCurrentModelStatusAsync();
            }
            else
            {
                _isInitializing = true;
                UseLocalEmbeddings = oldValue;
                _isInitializing = false;
            }
        }
        else
        {
            await SaveSettingsAsync();
            await UpdateCurrentModelStatusAsync();
        }
    }

    partial void OnSelectedModelChanged(EmbeddingModelViewModel? value)
    {
        if (value != null)
        {
            foreach (var m in AvailableModels)
                m.IsSelected = m.Id == value.Id;
            
            _ = SaveSettingsAsync();
            _ = UpdateCurrentModelStatusAsync();
        }
    }

    private async Task SaveSettingsAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        settings.UseLocalEmbeddings = UseLocalEmbeddings;
        if (SelectedModel != null)
            settings.LocalEmbeddingModel = SelectedModel.Id;
        await _settingsService.SaveSettingsAsync(settings);
    }

    [RelayCommand]
    private async Task DownloadModelAsync(EmbeddingModelViewModel? model)
    {
        if (model == null || IsDownloading) return;

        IsDownloading = true;
        DownloadProgress = 0;
        DownloadedBytes = 0;
        TotalBytes = model.SizeBytes;
        DownloadStatus = "Rozpoczynanie pobierania...";
        model.Status = ModelDownloadStatus.Downloading;

        _downloadCts = new CancellationTokenSource();
        var progress = new Progress<ModelDownloadProgress>(p =>
        {
            DownloadProgress = p.ProgressPercent;
            DownloadedBytes = p.BytesDownloaded;
            TotalBytes = p.TotalBytes > 0 ? p.TotalBytes : model.SizeBytes;
            DownloadStatus = p.Status;
            model.DownloadedBytes = p.BytesDownloaded;

            if (p.IsComplete)
            {
                model.Status = ModelDownloadStatus.Downloaded;
                StatusMessage = "Model pobrany pomyślnie!";
            }
            else if (p.Error != null)
            {
                model.Status = ModelDownloadStatus.Error;
                StatusMessage = $"Błąd: {p.Error}";
            }
        });

        try
        {
            await _downloadService.DownloadModelAsync(model.Id, progress, _downloadCts.Token);
            await RefreshModelStatusAsync(model);
        }
        catch (OperationCanceledException)
        {
            model.Status = ModelDownloadStatus.PartiallyDownloaded;
            StatusMessage = "Pobieranie anulowane - można wznowić";
        }
        catch (Exception ex)
        {
            model.Status = ModelDownloadStatus.Error;
            StatusMessage = $"Błąd: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
            await UpdateCurrentModelStatusAsync();
        }
    }

    [RelayCommand]
    private Task CancelDownloadAsync()
    {
        _downloadCts?.Cancel();
        DownloadStatus = "Anulowanie...";
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task DeleteModelAsync(EmbeddingModelViewModel? model)
    {
        if (model == null) return;

        try
        {
            await _downloadService.DeleteModelAsync(model.Id);
            model.Status = ModelDownloadStatus.NotDownloaded;
            model.DownloadedBytes = 0;
            StatusMessage = "Model usunięty";
            await UpdateCurrentModelStatusAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd usuwania: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadModelAsync()
    {
        if (SelectedModel == null || SelectedModel.Status != ModelDownloadStatus.Downloaded)
        {
            StatusMessage = "Najpierw pobierz wybrany model";
            return;
        }

        StatusMessage = "Ładowanie modelu...";
        try
        {
            await _localEmbeddingService.InitializeAsync(SelectedModel.Id);
            StatusMessage = "Model załadowany!";
            await UpdateCurrentModelStatusAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd ładowania: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshStatusAsync()
    {
        foreach (var model in AvailableModels)
        {
            await RefreshModelStatusAsync(model);
        }
        await UpdateCurrentModelStatusAsync();
    }

    private async Task RefreshModelStatusAsync(EmbeddingModelViewModel model)
    {
        model.Status = await _downloadService.GetDownloadStatusAsync(model.Id);
        model.DownloadedBytes = await _downloadService.GetDownloadedSizeAsync(model.Id);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EMBEDDING TEST - Polish Word Similarity
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isTestRunning;

    [ObservableProperty]
    private string _testResults = string.Empty;

    [ObservableProperty]
    private bool _hasTestResults;

    [RelayCommand]
    private async Task RunEmbeddingTestAsync()
    {
        System.Diagnostics.Debug.WriteLine("[EmbeddingsVM] RunEmbeddingTestAsync called");
        
        var isAvailable = await _localEmbeddingService.IsAvailableAsync();
        System.Diagnostics.Debug.WriteLine($"[EmbeddingsVM] Model available: {isAvailable}");
        
        if (!isAvailable)
        {
            StatusMessage = "⚠️ Najpierw załaduj model! Kliknij 'Załaduj wybrany model' powyżej.";
            HasTestResults = true;
            TestResults = "❌ Model nie jest załadowany.\n\nAby uruchomić test:\n1. Pobierz model (jeśli nie pobrany)\n2. Kliknij 'Załaduj wybrany model'\n3. Uruchom test ponownie";
            return;
        }

        IsTestRunning = true;
        TestResults = string.Empty;
        HasTestResults = false;
        var sb = new System.Text.StringBuilder();

        try
        {
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine("       TEST EMBEDDINGÓW - Podobieństwo słów polskich");
            sb.AppendLine("═══════════════════════════════════════════════════════════\n");

            var testGroups = new (string Name, string[] Words)[]
            {
                ("🏠 Dom i Mieszkanie", new[] { "dom", "mieszkanie", "budynek", "chata", "willa" }),
                ("🚗 Transport", new[] { "samochód", "auto", "pojazd", "maszyna", "rower" }),
                ("🍎 Jedzenie", new[] { "jabłko", "gruszka", "owoc", "banan", "chleb" }),
                ("👨 Rodzina", new[] { "ojciec", "tata", "rodzic", "matka", "brat" }),
                ("💻 Technologia", new[] { "komputer", "laptop", "telefon", "smartfon", "tablet" }),
            };

            var embeddings = new Dictionary<string, float[]>();

            // Generate embeddings
            StatusMessage = "Generowanie embeddingów...";
            foreach (var (_, words) in testGroups)
            {
                foreach (var word in words)
                {
                    if (!embeddings.ContainsKey(word))
                    {
                        embeddings[word] = await _localEmbeddingService.GetEmbeddingAsync(word);
                    }
                }
            }

            // Show results for each group
            foreach (var (name, words) in testGroups)
            {
                sb.AppendLine($"\n{name}:");
                sb.AppendLine($"  Bazowe słowo: \"{words[0]}\"\n");

                var baseEmb = embeddings[words[0]];
                var similarities = new List<(string word, double sim)>();

                foreach (var word in words.Skip(1))
                {
                    var sim = CosineSimilarity(baseEmb, embeddings[word]);
                    similarities.Add((word, sim));
                }

                foreach (var (word, sim) in similarities.OrderByDescending(x => x.sim))
                {
                    var bar = new string('█', (int)(sim * 15));
                    var empty = new string('░', 15 - (int)(sim * 15));
                    sb.AppendLine($"  {word,-14} [{bar}{empty}] {sim:P1}");
                }
            }

            // Cross-category matrix
            sb.AppendLine("\n═══════════════════════════════════════════════════════════");
            sb.AppendLine("         PORÓWNANIE MIĘDZY KATEGORIAMI");
            sb.AppendLine("═══════════════════════════════════════════════════════════\n");

            var crossWords = new[] { "dom", "samochód", "jabłko", "ojciec", "komputer" };
            sb.Append("              ");
            foreach (var w in crossWords) sb.Append($"{w,-12}");
            sb.AppendLine();

            foreach (var word1 in crossWords)
            {
                sb.Append($"  {word1,-12}");
                foreach (var word2 in crossWords)
                {
                    if (word1 == word2)
                        sb.Append("   ────     ");
                    else
                    {
                        var sim = CosineSimilarity(embeddings[word1], embeddings[word2]);
                        sb.Append($"   {sim:F2}      ");
                    }
                }
                sb.AppendLine();
            }

            sb.AppendLine("\n═══════════════════════════════════════════════════════════");
            sb.AppendLine("✅ Test zakończony pomyślnie!");

            TestResults = sb.ToString();
            HasTestResults = true;
            StatusMessage = "Test embeddingów zakończony!";
        }
        catch (Exception ex)
        {
            TestResults = $"❌ Błąd testu: {ex.Message}";
            HasTestResults = true;
            StatusMessage = $"Błąd testu: {ex.Message}";
        }
        finally
        {
            IsTestRunning = false;
        }
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }
}

public partial class EmbeddingModelViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private int _dimensions;

    [ObservableProperty]
    private long _sizeBytes;

    [ObservableProperty]
    private string _languages = string.Empty;

    [ObservableProperty]
    private ModelDownloadStatus _status;

    [ObservableProperty]
    private long _downloadedBytes;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _qualityRating;

    [ObservableProperty]
    private string _ramRequired = string.Empty;

    [ObservableProperty]
    private string _inferenceSpeed = string.Empty;

    [ObservableProperty]
    private string _recommendedFor = string.Empty;

    public string SizeText => FormatSize(SizeBytes);
    public string DownloadedText => FormatSize(DownloadedBytes);
    public double DownloadPercent => SizeBytes > 0 ? (double)DownloadedBytes / SizeBytes * 100 : 0;

    public string QualityStars => new string('★', QualityRating) + new string('☆', 5 - QualityRating);

    public string StatusText => Status switch
    {
        ModelDownloadStatus.NotDownloaded => "Nie pobrany",
        ModelDownloadStatus.Downloading => "Pobieranie...",
        ModelDownloadStatus.PartiallyDownloaded => $"Częściowo ({DownloadPercent:F0}%)",
        ModelDownloadStatus.Downloaded => "✓ Pobrany",
        ModelDownloadStatus.Error => "Błąd",
        _ => "Nieznany"
    };

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:F1} {sizes[order]}";
    }
}
