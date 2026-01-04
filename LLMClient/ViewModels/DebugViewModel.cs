using System.ComponentModel;
using System.Windows.Input;
using LLMClient.Services;
using LLMClient.Models;

namespace LLMClient.ViewModels;

public class DebugViewModel : INotifyPropertyChanged
{
    private readonly IRagService? _ragService;
    private readonly IEmbeddingService? _embeddingService;
    private readonly DatabaseService _databaseService;
    
    private string _testQuery = "";
    private string _testResults = "";
    private string _resultsHeader = "";
    private bool _hasResults;
    private string _tokenizerTestText = "";
    private string _tokenizerResult = "";
    private bool _hasTokenizerResult;
    private string _databaseStats = "Ładowanie...";

    public DebugViewModel(
        IRagService? ragService,
        IEmbeddingService? embeddingService,
        DatabaseService databaseService)
    {
        _ragService = ragService;
        _embeddingService = embeddingService;
        _databaseService = databaseService;
        
        TestVectorCommand = new Command(async () => await TestRagAsync(RetrievalMode.Vector));
        TestKeywordCommand = new Command(async () => await TestRagAsync(RetrievalMode.Keyword));
        TestHybridCommand = new Command(async () => await TestRagAsync(RetrievalMode.Hybrid));
        TestTokenizerCommand = new Command(async () => await TestTokenizerAsync());
        RefreshStatsCommand = new Command(async () => await RefreshStatsAsync());
        ClearEmbeddingsCommand = new Command(async () => await ClearEmbeddingsAsync());
        
        _ = RefreshStatsAsync();
    }

    public string SystemInfo
    {
        get
        {
            var ramGB = MultiModelEmbeddingService.GetDeviceRAMInGB();
            return $"RAM: {ramGB}GB | OS: {DeviceInfo.Platform} {DeviceInfo.VersionString} | Device: {DeviceInfo.Model}";
        }
    }

    public string EmbeddingModelInfo
    {
        get
        {
            if (_embeddingService == null) return "Brak serwisu embeddingowego";
            var model = (_embeddingService as MultiModelEmbeddingService)?.CurrentModel;
            if (model == null) return "Model nieznany";
            
            var recommended = MultiModelEmbeddingService.GetRecommendedModelForDevice();
            var isRecommended = model.Id == recommended.Id ? "✅ (rekomendowany)" : "⚠️ (nie rekomendowany dla tego urządzenia)";
            
            return $"Model: {model.DisplayName} {isRecommended}\n" +
                   $"Jakość: {model.QualityScore}% | Szybkość: {model.SpeedScore}%\n" +
                   $"Wymiary: {model.Dimensions} | Rozmiar: ~{model.SizeInMB}MB | Min RAM: {model.MinRAMGB}GB\n" +
                   $"Initialized: {_embeddingService.IsInitialized}";
        }
    }

    public string TestQuery
    {
        get => _testQuery;
        set { _testQuery = value; OnPropertyChanged(); }
    }

    public string TestResults
    {
        get => _testResults;
        set { _testResults = value; OnPropertyChanged(); }
    }

    public string ResultsHeader
    {
        get => _resultsHeader;
        set { _resultsHeader = value; OnPropertyChanged(); }
    }

    public bool HasResults
    {
        get => _hasResults;
        set { _hasResults = value; OnPropertyChanged(); }
    }

    public string TokenizerTestText
    {
        get => _tokenizerTestText;
        set { _tokenizerTestText = value; OnPropertyChanged(); }
    }

    public string TokenizerResult
    {
        get => _tokenizerResult;
        set { _tokenizerResult = value; OnPropertyChanged(); }
    }

    public bool HasTokenizerResult
    {
        get => _hasTokenizerResult;
        set { _hasTokenizerResult = value; OnPropertyChanged(); }
    }

    public string DatabaseStats
    {
        get => _databaseStats;
        set { _databaseStats = value; OnPropertyChanged(); }
    }

    public ICommand TestVectorCommand { get; }
    public ICommand TestKeywordCommand { get; }
    public ICommand TestHybridCommand { get; }
    public ICommand TestTokenizerCommand { get; }
    public ICommand RefreshStatsCommand { get; }
    public ICommand ClearEmbeddingsCommand { get; }

    private async Task TestRagAsync(RetrievalMode mode)
    {
        if (_ragService == null)
        {
            TestResults = "❌ Serwis RAG niedostępny";
            HasResults = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(TestQuery))
        {
            TestResults = "❌ Wprowadź zapytanie testowe";
            HasResults = true;
            return;
        }

        try
        {
            ResultsHeader = $"🔍 Wyniki ({mode})...";
            HasResults = true;
            TestResults = "⏳ Wyszukiwanie...";

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _ragService.GetRelevantContextWithTraceAsync(TestQuery, topK: 5, minSimilarity: 0.3f, mode: mode);
            sw.Stop();

            ResultsHeader = $"✅ Wyniki ({mode}) - {sw.ElapsedMilliseconds}ms";

            if (result.Chunks.Count == 0)
            {
                TestResults = "Brak wyników. Sprawdź czy masz dodane dokumenty RAG.";
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Znaleziono {result.Chunks.Count} fragmentów:\n");
                
                foreach (var chunk in result.Chunks.Take(5))
                {
                    sb.AppendLine($"📄 [{chunk.DocumentName}] Score: {chunk.Score:F3}");
                    sb.AppendLine($"   {Truncate(chunk.Content, 100)}");
                    sb.AppendLine();
                }
                
                TestResults = sb.ToString();
            }
        }
        catch (Exception ex)
        {
            ResultsHeader = $"❌ Błąd ({mode})";
            TestResults = $"Błąd: {ex.Message}";
        }
    }

    private async Task TestTokenizerAsync()
    {
        if (string.IsNullOrWhiteSpace(TokenizerTestText))
        {
            TokenizerResult = "❌ Wprowadź tekst do tokenizacji";
            HasTokenizerResult = true;
            return;
        }

        try
        {
            HasTokenizerResult = true;
            TokenizerResult = "⏳ Tokenizacja...";

            // Test E5 tokenizer
            var e5Result = await TokenizerNative.InitNamedAsync("e5_test", 
                Path.Combine(FileSystem.AppDataDirectory, "models", "intfloat-e5-large-multilingual-v1", "tokenizer.json"));
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Tekst: \"{TokenizerTestText}\"");
            sb.AppendLine($"Długość: {TokenizerTestText.Length} znaków\n");

            if (e5Result == 0)
            {
                var ids = new int[512];
                var len = await TokenizerNative.EncodeNamedAsync("e5_test", TokenizerTestText, ids, 512);
                var tokens = ids.Take(Math.Max(len, 0)).ToArray();
                sb.AppendLine($"E5 Tokenizer: {tokens.Length} tokenów");
                sb.AppendLine($"   IDs: [{string.Join(", ", tokens.Take(20))}{(tokens.Length > 20 ? "..." : "")}]");
            }
            else
            {
                sb.AppendLine("E5 Tokenizer: niedostępny");
            }

            // Test Gemma tokenizer
            var gemmaResult = await TokenizerNative.InitNamedAsync("gemma_test",
                Path.Combine(FileSystem.AppDataDirectory, "models", "embeddinggemma-300m", "tokenizer.json"));
            
            if (gemmaResult == 0)
            {
                var ids = new int[512];
                var len = await TokenizerNative.EncodeNamedAsync("gemma_test", TokenizerTestText, ids, 512);
                var tokens = ids.Take(Math.Max(len, 0)).ToArray();
                sb.AppendLine($"\nGemma Tokenizer: {tokens.Length} tokenów");
                sb.AppendLine($"   IDs: [{string.Join(", ", tokens.Take(20))}{(tokens.Length > 20 ? "..." : "")}]");
            }
            else
            {
                sb.AppendLine("\nGemma Tokenizer: niedostępny");
            }

            TokenizerResult = sb.ToString();
        }
        catch (Exception ex)
        {
            TokenizerResult = $"❌ Błąd: {ex.Message}";
        }
    }

    private async Task RefreshStatsAsync()
    {
        try
        {
            var conversations = await _databaseService.GetConversationsAsync();
            var messages = 0;
            foreach (var conv in conversations)
            {
                messages += conv.Messages?.Count ?? 0;
            }

            var docs = _ragService != null ? await _ragService.GetDocumentsAsync() : new List<RagDocument>();
            var pendingChunks = _ragService != null ? await _ragService.GetPendingChunksCountAsync() : 0;

            DatabaseStats = $"Konwersacje: {conversations.Count}\n" +
                           $"Wiadomości: {messages}\n" +
                           $"Dokumenty RAG: {docs.Count}\n" +
                           $"Chunki bez embeddingów: {pendingChunks}";
        }
        catch (Exception ex)
        {
            DatabaseStats = $"Błąd: {ex.Message}";
        }
    }

    private async Task ClearEmbeddingsAsync()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null) return;

        var confirm = await page.DisplayAlert(
            "⚠️ Potwierdź",
            "Czy na pewno chcesz wyczyścić wszystkie embeddingi RAG? Będą musiały zostać wygenerowane ponownie.",
            "Tak, wyczyść", "Anuluj");

        if (!confirm) return;

        try
        {
            if (_ragService != null)
            {
                await _ragService.ClearAllEmbeddingsAsync();
                await page.DisplayAlert("✅ Sukces", "Embeddingi zostały wyczyszczone.", "OK");
                await RefreshStatsAsync();
            }
        }
        catch (Exception ex)
        {
            await page.DisplayAlert("❌ Błąd", ex.Message, "OK");
        }
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        text = text.Replace("\n", " ").Replace("\r", " ");
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
