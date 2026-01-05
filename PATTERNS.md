# Design Patterns and Best Practices in LLMClient

This document describes modern programming patterns and best practices used in the LLMClient project.
The project uses .NET 10, MAUI, and C# 12.

---

## Table of Contents

1. [C# 12 / .NET 10 Patterns](#c-12--net-10-patterns)
2. [Async Patterns](#async-patterns)
3. [MVVM Patterns](#mvvm-patterns)
4. [MAUI Patterns](#maui-patterns)
5. [Architectural Patterns](#architectural-patterns)
6. [Security](#security)
7. [.NET 9/10 Additional Patterns](#net-910-additional-patterns)

---

## C# 12 / .NET 10 Patterns

### Primary Constructors (C# 12)

Primary constructors allow defining parameters directly in the class declaration.

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

Force property initialization when creating an object.

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

Immutable data types with built-in equality and deconstruction.

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

// Services/IngestionService.cs - private record
private sealed record IngestionJob(string FilePath, string FileName, DateTime EnqueuedAt);
```

### Init-only Properties

Properties that can only be set during initialization.

```csharp
// Models/RagTrace.cs
public sealed class RagTrace
{
    public string Query { get; init; } = "";
    public DateTime Utc { get; init; } = DateTime.UtcNow;
}
```

### Collection Expressions (C# 12)

Simplified syntax for creating collections.

```csharp
// Models/RagTrace.cs
public List<RagChunkCandidate> Candidates { get; } = [];
public List<RagTiming> Timings { get; } = [];
```

### File-scoped Namespaces

Reduce indentation in files.

```csharp
// Models/RagTrace.cs
namespace LLMClient.Models;

public sealed class RagTrace { ... }
```

### Pattern Matching

Advanced pattern matching.

```csharp
// Services/RagService.cs - switch expression
var content = extension switch
{
    ".txt" or ".md" => await File.ReadAllTextAsync(filePath),
    ".pdf" => ExtractTextFromPdf(filePath),
    ".docx" => ExtractTextFromDocx(filePath),
    _ => throw new NotSupportedException($"Unsupported: {extension}")
};

// Pattern matching with is
if (existing.Chunk is not null)
{
    // ...
}
```

### Sealed Classes

Performance optimization by preventing inheritance.

```csharp
public sealed class RagTrace { ... }
public sealed record RagChunkCandidate(...);
```

### FrozenDictionary (.NET 8+)

Immutable dictionaries optimized for fast read access.

```csharp
// Models/AiModel.cs
using System.Collections.Frozen;

public static class ApiProviders
{
    /// <summary>
    /// FrozenDictionary - immutable, optimized for read access
    /// Ideal for static configuration data
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

**FrozenDictionary Benefits:**
- 🚀 Faster lookup than regular Dictionary (up to 40% faster)
- 🔒 Guaranteed immutability
- 💾 Lower memory usage for large collections

---

## Async Patterns

### IAsyncEnumerable<T> - Async Streaming

Stream data asynchronously - ideal for real-time AI responses.

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

Thread-safe queue for producer/consumer pattern.

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

Full support for operation cancellation.

```csharp
// Throughout all services
public async Task<string> GenerateResponseAsync(
    string prompt, 
    CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    // ...
}
```

### IProgress<T>

Operation progress reporting.

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

### SemaphoreSlim - Concurrency Control

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

## MVVM Patterns

### INotifyPropertyChanged

Notify UI about property changes.

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

Collection that automatically notifies UI about changes.

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

Bind actions to UI.

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

Loosely-coupled communication between components.

```csharp
// Messaging/Messages.cs
public record ScrollToBottomMessage;
public record LocalModelLoadedMessage;
public record ModelsChangedMessage;

// ViewModels/MainPageViewModel.cs - sending
WeakReferenceMessenger.Default.Send(new ScrollToBottomMessage());
WeakReferenceMessenger.Default.Send(new LocalModelLoadedMessage());

// Views/MainPage.xaml.cs - receiving
WeakReferenceMessenger.Default.Register<ScrollToBottomMessage>(this, (r, m) =>
{
    ScrollToBottom();
});

// Unregister in OnDisappearing
protected override void OnDisappearing()
{
    base.OnDisappearing();
    WeakReferenceMessenger.Default.Unregister<ScrollToBottomMessage>(this);
}
```

### IQueryAttributable

Pass parameters between pages.

```csharp
// ViewModels/MainPageViewModel.cs
public class MainPageViewModel : INotifyPropertyChanged, IQueryAttributable
{
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("conversationId", out var id))
        {
            // Load conversation
        }
    }
}
```

---

## MAUI Patterns

### Behaviors

Extend control functionality without inheritance.

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

Custom XAML extensions for localization.

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

// Usage in XAML
<Label Text="{helpers:Translate WelcomeMessage}"/>
```

### BindableProperty

Create bindable properties in controls.

```csharp
public static readonly BindableProperty SendCommandProperty = 
    BindableProperty.Create(
        propertyName: nameof(SendCommand),
        returnType: typeof(ICommand),
        declaringType: typeof(EditorKeyboardBehavior),
        defaultValue: null);
```

### Shell Navigation

Navigation with parameters.

```csharp
// Navigation with parameters
await Shell.Current.GoToAsync($"//conversation?id={conversationId}");

// Query parameters
[QueryProperty(nameof(ConversationId), "id")]
public partial class ConversationPage : ContentPage
{
    public int ConversationId { get; set; }
}
```

### Platform-specific Code

Platform-specific code with preprocessor directives.

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

## Architectural Patterns

### Dependency Injection

Constructor-based dependency injection.

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

Data access abstraction.

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

Business logic separation.

```csharp
// Services/RagService.cs - RAG logic
public class RagService : IRagService
{
    public async Task<string> GetRelevantContextAsync(
        string query, 
        int topK = 3, 
        float minSimilarity = 0.5f)
    {
        // Semantic search logic
    }
}

// Services/EmbeddingService.cs - embedding generation
public class EmbeddingService : IEmbeddingService
{
    public async Task<float[]?> GenerateEmbeddingAsync(string text)
    {
        // ONNX logic
    }
}
```

### Dispose Pattern

Proper resource disposal.

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

## Security

### SecureStorage for API Keys

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

### SQLCipher - Encrypted Database

```csharp
// Services/DatabaseService.cs
var encryptionKey = await GetOrGenerateEncryptionKeyAsync();
var connectionString = new SQLiteConnectionString(dbPath, true, key: encryptionKey);
_database = new SQLiteAsyncConnection(connectionString);
```

---

## .NET 9/10 Additional Patterns

### TimeProvider (Testability)

Time abstraction for easier testing.

```csharp
// Instead of DateTime.Now use TimeProvider
public class MyService(TimeProvider timeProvider)
{
    public DateTime GetCurrentTime() => timeProvider.GetUtcNow().DateTime;
}

// In tests
var fakeTime = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
var service = new MyService(fakeTime);
```

### Interlocked for Thread-Safety

```csharp
// Services/IngestionService.cs
private int _queueCount;

Interlocked.Increment(ref _queueCount);
Interlocked.Decrement(ref _queueCount);
```

### ConfigureAwait(false)

Avoid deadlocks in libraries.

```csharp
// In services (not in UI)
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

## Summary

LLMClient project demonstrates the use of latest C# 12 and .NET 10 features:

| Category | Patterns |
|----------|----------|
| **C# 12** | Primary constructors, required, collection expressions |
| **.NET 8+** | FrozenDictionary, TimeProvider |
| **Async** | IAsyncEnumerable, Channel<T>, CancellationToken, IProgress |
| **MVVM** | INotifyPropertyChanged, ObservableCollection, WeakReferenceMessenger |
| **MAUI** | Behaviors, BindableProperty, Markup Extensions |
| **Architecture** | DI, Repository, Service Layer, Dispose Pattern |
| **Security** | SecureStorage, SQLCipher |
| **Thread-Safety** | Interlocked, SemaphoreSlim, Lock |

### Showcase Files (for interviews)

1. **`Services/IngestionService.cs`** - Channel<T>, producer/consumer, record, exception filters
2. **`Services/AiService.cs`** - IAsyncEnumerable streaming, CancellationToken
3. **`Models/RagTrace.cs`** - Record types, init properties, sealed, collection expressions
4. **`Models/AiModel.cs`** - FrozenDictionary, record ApiProviderInfo
5. **`Services/IIngestionService.cs`** - Primary constructors, required properties
6. **`Behaviors/EditorKeyboardBehavior.cs`** - MAUI Behaviors, BindableProperty
7. **`ViewModels/MainPageViewModel.cs`** - MVVM, WeakReferenceMessenger, ICommand

---

*Last updated: January 2026*

---
---

# 🇵🇱 Polskie Tłumaczenie / Polish Translation

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

### Słowniczek Wzorców

| Wzorzec | Opis po polsku |
|---------|----------------|
| **Primary Constructors** | Konstruktory główne - definiowanie parametrów bezpośrednio w deklaracji klasy |
| **Required Properties** | Wymuszenie inicjalizacji właściwości przy tworzeniu obiektu |
| **Record Types** | Niezmienne typy danych z wbudowanym porównaniem i dekonstrukcją |
| **Init-only Properties** | Właściwości ustawiane tylko podczas inicjalizacji |
| **Collection Expressions** | Uproszczona składnia tworzenia kolekcji `[]` |
| **File-scoped Namespaces** | Redukcja wcięć w plikach |
| **Pattern Matching** | Zaawansowane dopasowywanie wzorców (switch expressions, is not null) |
| **Sealed Classes** | Optymalizacja wydajności przez zapobieganie dziedziczeniu |
| **FrozenDictionary** | Niezmienne słowniki zoptymalizowane pod kątem szybkiego odczytu |
| **IAsyncEnumerable** | Strumieniowanie danych asynchronicznie |
| **Channel<T>** | Bezpieczna kolejka dla wzorca producent/konsument |
| **CancellationToken** | Pełne wsparcie dla anulowania operacji |
| **IProgress<T>** | Raportowanie postępu operacji |
| **SemaphoreSlim** | Kontrola współbieżności |
| **INotifyPropertyChanged** | Powiadamianie UI o zmianach właściwości |
| **ObservableCollection** | Kolekcja automatycznie powiadamiająca UI o zmianach |
| **WeakReferenceMessenger** | Luźno powiązana komunikacja między komponentami |
| **Behaviors** | Rozszerzanie funkcjonalności kontrolek bez dziedziczenia |
| **BindableProperty** | Tworzenie właściwości bindowalnych w kontrolkach |
| **Dependency Injection** | Wstrzykiwanie zależności przez konstruktor |
| **Repository Pattern** | Abstrakcja dostępu do danych |
| **Service Layer** | Separacja logiki biznesowej |
| **Dispose Pattern** | Prawidłowe zwalnianie zasobów |
| **SecureStorage** | Bezpieczne przechowywanie kluczy API |
| **SQLCipher** | Szyfrowana baza danych SQLite |
| **Interlocked** | Atomowe operacje na zmiennych (thread-safety) |
| **ConfigureAwait** | Unikanie deadlocków w bibliotekach |
| **Exception Filters** | Filtrowanie wyjątków z warunkiem `when` |

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
