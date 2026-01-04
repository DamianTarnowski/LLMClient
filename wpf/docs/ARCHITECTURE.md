# 🏗 Architecture Documentation

> **Version**: 1.0.0 | **Last Updated**: January 2026

## Table of Contents

- [Overview](#overview)
- [Layer Diagram](#layer-diagram)
- [Key Components](#key-components)
- [Data Flow](#data-flow)
- [Dependency Injection](#dependency-injection)
- [Database Schema](#database-schema)
- [Security Architecture](#security-architecture)
- [External Dependencies](#external-dependencies)

---

## Overview

WPFLLM follows a clean **MVVM (Model-View-ViewModel)** architecture with **Dependency Injection** for loose coupling and testability. The application is designed with an **offline-first** philosophy, ensuring all core features work without network connectivity.

## Layer Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                        VIEWS (XAML)                         │
│   MainWindow │ ChatView │ RagView │ SettingsView │ etc.    │
└─────────────────────────┬───────────────────────────────────┘
                          │ Data Binding
┌─────────────────────────▼───────────────────────────────────┐
│                      VIEWMODELS                              │
│   MainViewModel │ ChatViewModel │ RagViewModel │ etc.       │
│   - ObservableObject (CommunityToolkit.Mvvm)                │
│   - RelayCommand for actions                                 │
└─────────────────────────┬───────────────────────────────────┘
                          │ Dependency Injection
┌─────────────────────────▼───────────────────────────────────┐
│                       SERVICES                               │
│   ┌─────────────┐ ┌─────────────┐ ┌──────────────────┐      │
│   │ ChatService │ │  LlmService │ │    RagService    │      │
│   └─────────────┘ └─────────────┘ └──────────────────┘      │
│   ┌─────────────────────┐ ┌─────────────────────────┐       │
│   │ LocalEmbeddingService│ │  ModelDownloadService  │       │
│   └─────────────────────┘ └─────────────────────────┘       │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│                        MODELS                                │
│   AppSettings │ Conversation │ Message │ Document │ etc.    │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│                    DATA STORAGE                              │
│   SQLite (Dapper) │ Local Files │ ONNX Models               │
└─────────────────────────────────────────────────────────────┘
```

## Key Components

### Services

#### `LlmService`
Handles communication with OpenAI-compatible APIs.

```csharp
public interface ILlmService
{
    IAsyncEnumerable<string> StreamChatAsync(
        List<Message> messages, 
        CancellationToken ct);
    
    Task<float[]> GetEmbeddingAsync(string text);
}
```

**Features:**
- Streaming responses using `IAsyncEnumerable`
- Configurable API endpoint and model
- Embedding generation (API or local)
- Error handling and retry logic

#### `RagService`
Implements Retrieval Augmented Generation pipeline with full debug tracing.

```csharp
public interface IRagService
{
    Task<RetrievalResult> RetrieveAsync(
        string query, 
        int topK = 5,
        double minSimilarity = 0.7,
        RetrievalMode mode = RetrievalMode.Hybrid);
    
    Task<(RetrievalResult Result, RagTrace Trace)> RetrieveWithTraceAsync(
        string query, ...);
}
```

**Features:**
- Document chunking with overlap
- **Hybrid retrieval**: Vector + FTS5 keyword search
- **RRF fusion**: Reciprocal Rank Fusion (k=60)
- Full pipeline tracing with `RagTrace`
- Configurable chunk size and overlap

#### `RagTrace` (Flight Recorder)
Captures complete debug information for each RAG query:

```csharp
public sealed class RagTrace
{
    public string Query { get; init; }
    public List<RagChunkCandidate> Candidates { get; }  // All evaluated
    public List<RagTiming> Timings { get; }             // Pipeline steps
    public RagTokenBreakdown? Tokens { get; set; }      // Token analysis
    public string FusionFormula { get; set; }           // "RRF(k=60)"
    public long TotalTimeMs => Timings.Sum(t => t.ElapsedMs);
}
```

**Timing measurement with extensions:**
```csharp
// Automatic timing capture
var embedding = await trace.MeasureAsync("EmbedQuery", 
    () => _llmService.GetEmbeddingAsync(query));
```

**What gets traced:**
| Step | Description |
|------|-------------|
| EmbedQuery | Query embedding generation time |
| LoadChunks | Database chunk loading |
| VectorSearch | Cosine similarity computation |
| KeywordSearch | FTS5 full-text search |
| Merge+Rerank | RRF fusion and sorting |
| BuildTrace | Debug info construction |

#### `LocalEmbeddingService`
Runs ONNX embedding models locally with Rust FFI tokenizer.

```csharp
public interface ILocalEmbeddingService
{
    Task InitializeAsync(string modelId);
    Task<float[]> GetEmbeddingAsync(string text);
    Task<float[]> GetEmbeddingAsync(string text, bool isQuery);
    Task<bool> IsAvailableAsync();
    int GetDimensions();
}
```

**Features:**
- Lazy model loading
- Thread-safe initialization
- E5 prefix handling (query:/passage:)
- **Rust HuggingFace tokenizer** (add_special_tokens=true)
- Mean pooling + L2 normalization
- Memory-efficient inference

#### `RustTokenizer`
Native Rust FFI wrapper for HuggingFace tokenizers library.

```csharp
public static class RustTokenizer
{
    public static bool Initialize(string tokenizerPath);
    public static int[] Encode(string text, int maxLength = 512);
    public static bool IsInitialized { get; }
}
```

**Why Rust?**
- .NET tokenizer libraries don't support `add_special_tokens=true`
- Without special tokens (`<s>`, `</s>`), E5 embeddings have poor discrimination
- Rust tokenizer improved GAP from **0.7% to 14.5%** (20x better!)

#### `ModelDownloadService`
Manages model downloads from HuggingFace.

```csharp
public interface IModelDownloadService
{
    Task DownloadModelAsync(
        string modelId, 
        IProgress<ModelDownloadProgress> progress,
        CancellationToken ct);
    
    Task<bool> IsModelDownloadedAsync(string modelId);
    Task<ModelDownloadStatus> GetDownloadStatusAsync(string modelId);
}
```

**Features:**
- Resumable downloads
- Progress tracking
- Partial download detection
- Cancellation support

### ViewModels

All ViewModels inherit from `ObservableObject` and use `[RelayCommand]` attributes:

```csharp
public partial class ChatViewModel : ObservableObject
{
    [ObservableProperty]
    private string _userInput = "";
    
    [RelayCommand]
    private async Task SendMessageAsync()
    {
        // Implementation
    }
}
```

### Data Flow

```
User Input → ViewModel Command → Service Call → Data Update → UI Binding
```

## Dependency Injection

Configured in `App.xaml.cs`:

```csharp
private static void ConfigureServices(IServiceCollection services)
{
    // Core services
    services.AddSingleton<IDatabaseService, DatabaseService>();
    services.AddSingleton<ISettingsService, SettingsService>();
    
    // LLM services
    services.AddSingleton<ILlmService, LlmService>();
    services.AddSingleton<IChatService, ChatService>();
    
    // RAG services
    services.AddSingleton<IRagService, RagService>();
    
    // Local embedding services
    services.AddSingleton<ILocalEmbeddingService, LocalEmbeddingService>();
    services.AddSingleton<IModelDownloadService, ModelDownloadService>();
    
    // HTTP client
    services.AddHttpClient();
    
    // ViewModels
    services.AddTransient<MainViewModel>();
    services.AddTransient<ChatViewModel>();
    services.AddTransient<RagViewModel>();
    services.AddTransient<SettingsViewModel>();
    services.AddTransient<EmbeddingsViewModel>();
}
```

## Database Schema

```sql
-- Conversations table
CREATE TABLE Conversations (
    Id TEXT PRIMARY KEY,
    Title TEXT,
    CreatedAt TEXT,
    UpdatedAt TEXT
);

-- Messages table
CREATE TABLE Messages (
    Id TEXT PRIMARY KEY,
    ConversationId TEXT,
    Role TEXT,
    Content TEXT,
    CreatedAt TEXT,
    FOREIGN KEY (ConversationId) REFERENCES Conversations(Id)
);

-- Documents table
CREATE TABLE Documents (
    Id TEXT PRIMARY KEY,
    FileName TEXT,
    Content TEXT,
    ChunkIndex INTEGER,
    Embedding BLOB,
    CreatedAt TEXT
);

-- Settings table
CREATE TABLE Settings (
    Key TEXT PRIMARY KEY,
    Value TEXT
);
```

---

## Security Architecture

WPFLLM implements enterprise-grade security with defense in depth:

```
┌─────────────────────────────────────────────────────────────┐
│                    Security Layers                           │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────┐    │
│  │                 Application Layer                    │    │
│  │   - Input validation                                 │    │
│  │   - Secure API key handling                          │    │
│  │   - No hardcoded secrets                             │    │
│  └─────────────────────────────────────────────────────┘    │
│  ┌─────────────────────────────────────────────────────┐    │
│  │                 Encryption Layer                     │    │
│  │   - AES-256-GCM for data encryption                  │    │
│  │   - Random nonces per encryption                     │    │
│  │   - Authenticated encryption (integrity)             │    │
│  └─────────────────────────────────────────────────────┘    │
│  ┌─────────────────────────────────────────────────────┐    │
│  │                 Key Management                       │    │
│  │   - DPAPI for master key protection                  │    │
│  │   - User-scoped key storage                          │    │
│  │   - Automatic key generation                         │    │
│  └─────────────────────────────────────────────────────┘    │
│  ┌─────────────────────────────────────────────────────┐    │
│  │                 Storage Layer                        │    │
│  │   - Encrypted SQLite database                        │    │
│  │   - Local file system only                           │    │
│  │   - No cloud sync                                    │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

### Key Security Features

| Feature | Implementation |
|---------|---------------|
| **Encryption at Rest** | AES-256-GCM with 96-bit nonces |
| **Key Protection** | Windows DPAPI (user-scoped) |
| **API Key Storage** | Encrypted in database |
| **Offline Mode** | Full functionality without network |
| **Zero Telemetry** | No data sent to external services |
| **Network Transparency** | Call counter in status bar |

---

## External Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| CommunityToolkit.Mvvm | 8.x | MVVM framework |
| Microsoft.Extensions.DependencyInjection | 9.x | DI container |
| Microsoft.Data.Sqlite | 9.x | SQLite provider |
| Dapper | 2.x | Micro-ORM |
| Microsoft.ML.OnnxRuntime | 1.21.0 | ONNX inference |
| FluentAssertions | 8.x | Test assertions |
| NSubstitute | 5.x | Mocking framework |
| **hf_tokenizer.dll** | Custom | Rust tokenizer (HuggingFace) |

---

## Future Improvements

### Planned Features

- [ ] Plugin system for custom tools
- [ ] Voice input/output (Whisper integration)
- [ ] Image understanding (GPT-4V, Claude Vision)
- [ ] Conversation export/import (JSON, Markdown)
- [ ] Custom themes and UI customization
- [ ] Additional languages (DE, FR, ES)

### Technical Debt

- [ ] Migrate to source generators for DI
- [ ] Add OpenTelemetry instrumentation
- [ ] Implement connection pooling for SQLite
- [ ] Add health check endpoints

---

## Related Documentation

- [Embeddings System](EMBEDDINGS.md) - Local embedding models and Rust tokenizer
- [Contributing](../CONTRIBUTING.md) - How to contribute to the project
- [Changelog](../CHANGELOG.md) - Version history and release notes
