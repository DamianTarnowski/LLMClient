# Wzorce i Dobre Praktyki w LLMClient

Ten dokument opisuje nowoczesne wzorce programistyczne i dobre praktyki użyte w projekcie LLMClient.
Projekt wykorzystuje .NET 10, MAUI i C# 12.

---

## Spis Treści

1. [Wzorce C# 12 / .NET 10](#wzorce-c-12--net-10)
2. [Wzorce Asynchroniczne](#wzorce-asynchroniczne)
3. [Wzorce MVVM](#wzorce-mvvm)
4. [Wzorce MAUI](#wzorce-maui)
5. [Wzorce Architektoniczne](#wzorce-architektoniczne)
6. [Bezpieczeństwo](#bezpieczeństwo)

---

## Wzorce C# 12 / .NET 10

### Primary Constructors (C# 12)

Konstruktory główne pozwalają definiować parametry bezpośrednio w deklaracji klasy.

```csharp
// Services/IIngestionService.cs
public sealed class IngestionProgressEventArgs(
    string fileName, 
    string status, 
    int currentItem, 
    int totalItems
) : EventArgs
{
    public string FileName { get; } = fileName;
    public string Status { get; } = status;
    public int CurrentItem { get; } = currentItem;
    public int TotalItems { get; } = totalItems;
}
```

### Required Properties (C# 11+)

Wymuszenie inicjalizacji właściwości przy tworzeniu obiektu.

```csharp
// Services/IIngestionService.cs
public sealed class IngestionErrorEventArgs : EventArgs
{
    public required string FileName { get; init; }
    public required string ErrorMessage { get; init; }
    public bool WillRetry { get; init; }
}
```

### Record Types

Niezmienne typy danych z wbudowanym porównaniem i dekonstrukcją.

```csharp
// Models/RagTrace.cs
public sealed record RagChunkCandidate(
    int ChunkId,
    string SourceName,
    string? Section,
    int? ChunkIndex,
    float VectorScore,
    float KeywordScore,
    float FinalScore,
    int TokenCount,
    bool Included,
    string Preview
);

public sealed record RagTiming(string Name, long ElapsedMs)
{
    public override string ToString() => $"{Name}: {ElapsedMs}ms";
}

// Services/IngestionService.cs - prywatny record
private sealed record IngestionJob(string FilePath, string FileName, DateTime EnqueuedAt);
```

### Init-only Properties

Właściwości ustawiane tylko podczas inicjalizacji.

```csharp
// Models/RagTrace.cs
public sealed class RagTrace
{
    public string Query { get; init; } = "";
    public DateTime Utc { get; init; } = DateTime.UtcNow;
}
```

### Collection Expressions (C# 12)

Uproszczona składnia tworzenia kolekcji.

```csharp
// Models/RagTrace.cs
public List<RagChunkCandidate> Candidates { get; } = [];
public List<RagTiming> Timings { get; } = [];
```

### File-scoped Namespaces

Redukcja wcięć w plikach.

```csharp
// Models/RagTrace.cs
namespace LLMClient.Models;

public sealed class RagTrace { ... }
```

### Pattern Matching

Zaawansowane dopasowywanie wzorców.

```csharp
// Services/RagService.cs - switch expression
var content = extension switch
{
    ".txt" or ".md" => await File.ReadAllTextAsync(filePath),
    ".pdf" => ExtractTextFromPdf(filePath),
    ".docx" => ExtractTextFromDocx(filePath),
    _ => throw new NotSupportedException($"Unsupported: {extension}")
};

// Pattern matching z is
if (existing.Chunk is not null)
{
    // ...
}
```

### Sealed Classes

Optymalizacja wydajności przez zapobieganie dziedziczeniu.

```csharp
public sealed class RagTrace { ... }
public sealed record RagChunkCandidate(...);
```

### FrozenDictionary (.NET 8+)

Niezmienne słowniki zoptymalizowane pod kątem szybkiego odczytu.

```csharp
// Models/AiModel.cs
using System.Collections.Frozen;

public static class ApiProviders
{
    /// <summary>
    /// FrozenDictionary - niezmienne, zoptymalizowane pod kątem odczytu
    /// Idealne dla statycznych danych konfiguracyjnych
    /// </summary>
    public static readonly FrozenDictionary<AiProvider, ApiProviderInfo> ProviderInfo = 
        new Dictionary<AiProvider, ApiProviderInfo>
        {
            [AiProvider.OpenAI] = new("OpenAI", "https://api.openai.com/v1", ...),
            [AiProvider.Anthropic] = new("Anthropic", "https://api.anthropic.com/v1", ...),
            // ...
        }.ToFrozenDictionary();
}
```

**Zalety FrozenDictionary:**
- 🚀 Szybszy lookup niż zwykły Dictionary (do 40% szybciej)
- 🔒 Gwarantowana niezmienność
- 💾 Mniejsze zużycie pamięci dla dużych kolekcji

---

## Wzorce Asynchroniczne

### IAsyncEnumerable<T> - Async Streaming

Strumieniowanie danych asynchronicznie - idealne dla odpowiedzi AI w czasie rzeczywistym.

```csharp
// Services/AiService.cs
public async IAsyncEnumerable<string> GetStreamingResponseAsync(
    string message,
    List<Message> conversationHistory,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await foreach (var chunk in _chatService.GetStreamingChatMessageContentsAsync(...))
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (chunk.Content is not null)
        {
            yield return chunk.Content;
        }
    }
}
```

### Channel<T> - Producer/Consumer

Bezpieczna kolejka dla wzorca producent/konsument.

```csharp
// Services/IngestionService.cs
private readonly Channel<IngestionJob> _channel;

public IngestionService(IRagService ragService)
{
    _channel = Channel.CreateBounded<IngestionJob>(new BoundedChannelOptions(100)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });
    
    _processingTask = Task.Run(ProcessQueueAsync);
}

private async Task ProcessQueueAsync()
{
    await foreach (var job in _channel.Reader.ReadAllAsync(_cts.Token))
    {
        await ProcessJobWithRetryAsync(job);
    }
}

public async Task EnqueueFileAsync(string filePath)
{
    var job = new IngestionJob(filePath, Path.GetFileName(filePath), DateTime.UtcNow);
    await _channel.Writer.WriteAsync(job, _cts.Token);
}
```

### CancellationToken

Pełne wsparcie dla anulowania operacji.

```csharp
// Wszędzie w serwisach
public async Task<string> GenerateResponseAsync(
    string prompt, 
    CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    // ...
}
```

### IProgress<T>

Raportowanie postępu operacji.

```csharp
// Services/RagService.cs
public async Task GenerateEmbeddingsAsync(
    IProgress<string>? progress = null, 
    CancellationToken cancellationToken = default)
{
    progress?.Report($"Przetwarzanie {chunksToProcess.Count} chunków...");
    
    for (int i = 0; i < chunksToProcess.Count; i++)
    {
        progress?.Report($"Embedding chunk {i + 1}/{chunksToProcess.Count}");
        // ...
    }
}
```

### SemaphoreSlim - Kontrola Współbieżności

```csharp
// Services/RobustLocalModelService.cs
private readonly SemaphoreSlim _downloadSemaphore = new(1, 1);
private readonly SemaphoreSlim _inferenceSemaphore = new(1, 1);

public async Task<bool> DownloadModelAsync(IProgress<double>? progress = null)
{
    if (!await _downloadSemaphore.WaitAsync(100))
    {
        _logger.LogWarning("Download already in progress");
        return false;
    }
    
    try
    {
        // ... operacja
    }
    finally
    {
        _downloadSemaphore.Release();
    }
}
```

---

## Wzorce MVVM

### INotifyPropertyChanged

Powiadamianie UI o zmianach właściwości.

```csharp
// ViewModels/MainPageViewModel.cs
public class MainPageViewModel : INotifyPropertyChanged
{
    private bool _isSending;
    
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
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

### ObservableCollection<T>

Kolekcja automatycznie powiadamiająca UI o zmianach.

```csharp
// ViewModels/MainPageViewModel.cs
private ObservableCollection<Conversation> _conversations = new();

public ObservableCollection<Conversation> Conversations
{
    get => _conversations;
    set
    {
        _conversations = value;
        OnPropertyChanged();
    }
}
```

### ICommand / Command

Bindowanie akcji do UI.

```csharp
// ViewModels/MainPageViewModel.cs
public ICommand SendMessageCommand { get; }
public ICommand NewConversationCommand { get; }

public MainPageViewModel(...)
{
    SendMessageCommand = new Command(
        async () => await SendMessageAsync(),
        () => !string.IsNullOrWhiteSpace(NewMessage) && !IsSending
    );
}
```

### WeakReferenceMessenger (CommunityToolkit.Mvvm)

Luźno powiązana komunikacja między komponentami.

```csharp
// Messaging/Messages.cs
public record ScrollToBottomMessage;
public record LocalModelLoadedMessage;
public record ModelsChangedMessage;

// ViewModels/MainPageViewModel.cs - wysyłanie
WeakReferenceMessenger.Default.Send(new ScrollToBottomMessage());
WeakReferenceMessenger.Default.Send(new LocalModelLoadedMessage());

// Views/MainPage.xaml.cs - odbieranie
WeakReferenceMessenger.Default.Register<ScrollToBottomMessage>(this, (r, m) =>
{
    ScrollToBottom();
});

// Wyrejestrowanie w OnDisappearing
protected override void OnDisappearing()
{
    base.OnDisappearing();
    WeakReferenceMessenger.Default.Unregister<ScrollToBottomMessage>(this);
}
```

### IQueryAttributable

Przekazywanie parametrów między stronami.

```csharp
// ViewModels/MainPageViewModel.cs
public class MainPageViewModel : INotifyPropertyChanged, IQueryAttributable
{
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("conversationId", out var id))
        {
            // Załaduj konwersację
        }
    }
}
```

---

## Wzorce MAUI

### Behaviors

Rozszerzanie funkcjonalności kontrolek bez dziedziczenia.

```csharp
// Behaviors/EditorKeyboardBehavior.cs
public class EditorKeyboardBehavior : Behavior<Editor>
{
    public static readonly BindableProperty SendCommandProperty = 
        BindableProperty.Create(
            nameof(SendCommand), 
            typeof(ICommand), 
            typeof(EditorKeyboardBehavior));

    public ICommand SendCommand
    {
        get => (ICommand)GetValue(SendCommandProperty);
        set => SetValue(SendCommandProperty, value);
    }

    protected override void OnAttachedTo(Editor editor)
    {
        base.OnAttachedTo(editor);
        editor.Completed += OnEditorCompleted;
    }

    protected override void OnDetachingFrom(Editor editor)
    {
        editor.Completed -= OnEditorCompleted;
        base.OnDetachingFrom(editor);
    }

    private void OnEditorCompleted(object? sender, EventArgs e)
    {
        if (SendCommand?.CanExecute(null) == true)
        {
            SendCommand.Execute(null);
        }
    }
}
```

### XAML Markup Extensions

Niestandardowe rozszerzenia XAML dla lokalizacji.

```csharp
// Helpers/TranslateExtension.cs
[ContentProperty(nameof(Key))]
public class TranslateExtension : IMarkupExtension<string>
{
    public string Key { get; set; } = string.Empty;

    public string ProvideValue(IServiceProvider serviceProvider)
    {
        return LocalizationService.GetString(Key);
    }
}

// Użycie w XAML
<Label Text="{helpers:Translate WelcomeMessage}"/>
```

### BindableProperty

Tworzenie właściwości bindowalnych w kontrolkach.

```csharp
public static readonly BindableProperty SendCommandProperty = 
    BindableProperty.Create(
        propertyName: nameof(SendCommand),
        returnType: typeof(ICommand),
        declaringType: typeof(EditorKeyboardBehavior),
        defaultValue: null);
```

### Shell Navigation

Nawigacja z parametrami.

```csharp
// Nawigacja z parametrami
await Shell.Current.GoToAsync($"//conversation?id={conversationId}");

// Query parameters
[QueryProperty(nameof(ConversationId), "id")]
public partial class ConversationPage : ContentPage
{
    public int ConversationId { get; set; }
}
```

### Platform-specific Code

Kod specyficzny dla platformy z dyrektywami preprocesora.

```csharp
// Services/RobustLocalModelService.cs
private long GetTotalDeviceMemory()
{
#if ANDROID
    var activityManager = Android.App.Application.Context
        .GetSystemService(Android.Content.Context.ActivityService) 
        as Android.App.ActivityManager;
    if (activityManager != null)
    {
        var memInfo = new Android.App.ActivityManager.MemoryInfo();
        activityManager.GetMemoryInfo(memInfo);
        return memInfo.TotalMem;
    }
    return 0;
#elif WINDOWS
    return (long)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
#else
    return 0;
#endif
}
```

---

## Wzorce Architektoniczne

### Dependency Injection

Wstrzykiwanie zależności przez konstruktor.

```csharp
// MauiProgram.cs
builder.Services.AddSingleton<IAiService, AiService>();
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();

// ViewModels/MainPageViewModel.cs
public MainPageViewModel(
    IAiService aiService,
    DatabaseService databaseService,
    IStreamingBatchService streamingBatchService,
    IErrorHandlingService errorHandlingService,
    ISearchService searchService,
    IExportService exportService,
    IEmbeddingService embeddingService,
    ILocalizationService localizationService,
    ILocalModelService localModelService)
{
    _aiService = aiService;
    _databaseService = databaseService;
    // ...
}
```

### Repository Pattern

Abstrakcja dostępu do danych.

```csharp
// Services/DatabaseService.cs
public interface IDatabaseService
{
    Task<List<Conversation>> GetConversationsAsync();
    Task<Conversation> SaveConversationAsync(Conversation conversation);
    Task DeleteConversationAsync(int id);
    // ...
}
```

### Service Layer

Separacja logiki biznesowej.

```csharp
// Services/RagService.cs - logika RAG
public class RagService : IRagService
{
    public async Task<string> GetRelevantContextAsync(
        string query, 
        int topK = 3, 
        float minSimilarity = 0.5f)
    {
        // Logika wyszukiwania semantycznego
    }
}

// Services/EmbeddingService.cs - generowanie embeddingów
public class EmbeddingService : IEmbeddingService
{
    public async Task<float[]?> GenerateEmbeddingAsync(string text)
    {
        // Logika ONNX
    }
}
```

### Dispose Pattern

Prawidłowe zwalnianie zasobów.

```csharp
// Services/EmbeddingService.cs
public class EmbeddingService : IEmbeddingService, IDisposable
{
    private InferenceSession? _session;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _session?.Dispose();
        TokenizerNative.Cleanup();
        
        GC.SuppressFinalize(this);
    }
}
```

---

## Bezpieczeństwo

### SecureStorage dla API Keys

```csharp
// Services/SecureApiKeyService.cs
public async Task SaveApiKeyAsync(string modelId, string apiKey)
{
    await SecureStorage.SetAsync($"apikey_{modelId}", apiKey);
}

public async Task<string?> GetApiKeyAsync(string modelId)
{
    return await SecureStorage.GetAsync($"apikey_{modelId}");
}
```

### SQLCipher - Szyfrowana Baza Danych

```csharp
// Services/DatabaseService.cs
var encryptionKey = await GetOrGenerateEncryptionKeyAsync();
var connectionString = new SQLiteConnectionString(dbPath, true, key: encryptionKey);
_database = new SQLiteAsyncConnection(connectionString);
```

---

## Dodatkowe Wzorce .NET 9/10

### TimeProvider (Testability)

Abstrakcja czasu dla łatwiejszego testowania.

```csharp
// Zamiast DateTime.Now używaj TimeProvider
public class MyService(TimeProvider timeProvider)
{
    public DateTime GetCurrentTime() => timeProvider.GetUtcNow().DateTime;
}

// W testach
var fakeTime = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
var service = new MyService(fakeTime);
```

### Interlocked dla Thread-Safety

```csharp
// Services/IngestionService.cs
private int _queueCount;

Interlocked.Increment(ref _queueCount);
Interlocked.Decrement(ref _queueCount);
```

### ConfigureAwait(false)

Unikanie deadlocków w bibliotekach.

```csharp
// W serwisach (nie w UI)
await SomeAsyncOperation().ConfigureAwait(false);
```

### Exception Filters

```csharp
// Services/IngestionService.cs
catch (Exception ex) when (attempts < maxRetries)
{
    attempts++;
    await Task.Delay(1000 * attempts); // Exponential backoff
}
```

---

## Podsumowanie

Projekt LLMClient demonstruje wykorzystanie najnowszych funkcji C# 12 i .NET 10:

| Kategoria | Wzorce |
|-----------|--------|
| **C# 12** | Primary constructors, required, collection expressions |
| **.NET 8+** | FrozenDictionary, TimeProvider |
| **Async** | IAsyncEnumerable, Channel<T>, CancellationToken, IProgress |
| **MVVM** | INotifyPropertyChanged, ObservableCollection, WeakReferenceMessenger |
| **MAUI** | Behaviors, BindableProperty, Markup Extensions |
| **Architektura** | DI, Repository, Service Layer, Dispose Pattern |
| **Bezpieczeństwo** | SecureStorage, SQLCipher |
| **Thread-Safety** | Interlocked, SemaphoreSlim, Lock |

### Pliki Showcase (do pokazania na rekrutacji)

1. **`Services/IngestionService.cs`** - Channel<T>, producer/consumer, record, exception filters
2. **`Services/AiService.cs`** - IAsyncEnumerable streaming, CancellationToken
3. **`Models/RagTrace.cs`** - Record types, init properties, sealed, collection expressions
4. **`Models/AiModel.cs`** - FrozenDictionary, record ApiProviderInfo
5. **`Services/IIngestionService.cs`** - Primary constructors, required properties
6. **`Behaviors/EditorKeyboardBehavior.cs`** - MAUI Behaviors, BindableProperty
7. **`ViewModels/MainPageViewModel.cs`** - MVVM, WeakReferenceMessenger, ICommand

---

*Ostatnia aktualizacja: Styczeń 2026*
