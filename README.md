# LLMClient

> **Note**: This README is available in English first, followed by the Polish version below.

LLMClient is an advanced, cross-platform AI client built with .NET MAUI that enables seamless interaction with leading language models (LLMs) like GPT and Gemini. The application is designed with security, performance, and flexibility in mind, offering local data storage with encryption and native text tokenization.

![image](https://github.com/user-attachments/assets/e5f7e521-171a-4489-9b3e-58a4f6f74217)

## Key Features

- **Cross-Platform:** Runs natively on Windows, Android, iOS, and macOS thanks to .NET MAUI.
- **Multi-Model Support:** Easy configuration and switching between models from OpenAI, Google (Gemini), and any OpenAI-compatible API providers.
- **Local and Secure Data:** All conversations are stored locally in an encrypted SQLite database (using SQLCipher), ensuring privacy.
- **Efficient Tokenization:** Uses a custom Rust library for fast and efficient tokenization, minimizing latency.
- **Image Support:** Ability to send multimodal queries (text + image) to models that support it (e.g., Gemini, GPT-4 Vision).
- **AI Memory System:** Intelligent memory system that automatically remembers user information and provides it in conversation context (30,000 character limit with automatic summarization).
- **Automatic Memory Extraction:** Advanced extraction of personal information from conversations using regular expressions and AI analysis.
- **Semantic Search:** Advanced search in conversation history using embedding vectors.
- **Data Export:** Easy export of conversations to JSON, Markdown, or TXT formats.
- **Responsive Interface:** Clean and modern user interface that adapts to different screen sizes.

## Technology Stack

- **Framework:** .NET 10 / .NET MAUI
- **Languages:** C#, XAML, Rust
- **Architecture:** MVVM (Model-View-ViewModel)
- **AI & LLM:**
  - Microsoft.SemanticKernel
  - OpenAI, Google Gemini, OpenAI-Compatible Models
- **Database:** SQLite with SQLCipher (encryption)
- **Tokenization:** Native Rust library with `tokenizers`
- **Semantic Search:** ONNX Runtime, Microsoft.ML.Tokenizers

## Getting Started

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Rust toolchain (for compiling the tokenizer library)
- Configured .NET MAUI environment (according to [official documentation](https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation?view=net-maui-10.0&tabs=visual-studio))

### Building and Running

1. **Clone the repository:**
   ```bash
   git clone https://github.com/DamianTarnowski/LLMClient.git
   cd LLMClient
   ```

2. **Build the Rust library:**
   ```bash
   cd TokenizerRust
   cargo build --release
   ```
   This will place the `tokenizer_rust.dll` file (or equivalent for your system) in the `target/release` folder. The .NET MAUI project is configured to automatically copy this file during building.

3. **Open and run the .NET MAUI project:**
   - Open the `LLMClient.sln` file in Visual Studio 2022.
   - Select the target platform (e.g., Windows Machine, Android Emulator).
   - Press F5 to build and run the application.

### Configuration

1. Launch the application.
2. Go to the `Settings` screen (gear icon).
3. Add a new model configuration by providing its name, provider (OpenAI, Gemini, etc.), API key, and model ID.
4. Activate the model and start a new conversation!

## AI Memory System

LLMClient features an advanced memory system that automatically personalizes interactions with AI models:

### How It Works

1. **Automatic Extraction:** The system automatically extracts personal information from conversations (name, age, occupation, preferences, etc.) using:
   - Regular expressions for basic information (name, age, location)
   - AI analysis for more complex personal data

2. **Context in Conversation:** Each conversation receives full memory context (maximum 30,000 characters) in the system message, allowing models to provide personalized responses.

3. **Intelligent Management:** When memory exceeds the character limit:
   - Most important and recent information remains in full form
   - Older memories are automatically summarized by AI
   - Maximum amount of useful information is preserved

4. **Memory Management:** An interface is available for viewing, editing, and categorizing saved memories.

## Supported Languages

- The app adapts to the language used by the user in each message.
- Language coverage depends on the selected model:
  - Cloud models (OpenAI/Gemini/OpenAI‑compatible): broad multilingual support.
  - Local model (Phi‑4‑mini‑instruct via ONNX): best in English/Polish; quality may drop in some languages.
- UI strings: English and Polish available (Polish resources included).

### Supported Information

The system automatically recognizes and saves:
- **Personal Data:** Name, age, origin, occupation
- **Preferences:** Hobbies, interests, likes
- **Life Facts:** Family, education, experiences
- **Conversation Context:** Important topics and details from previous conversations

### Security

All memory data is:
- Stored locally in encrypted SQLite database
- Available only to the user on their device
- Not shared with external services

## Architecture

The project uses the MVVM pattern and is divided into the following layers:

- **`LLMClient/`**: Main .NET MAUI project.
  - **`Views/`**: XAML files defining the user interface.
  - **`ViewModels/`**: Business logic and state for individual views.
  - **`Models/`**: Data models (e.g., `Conversation`, `Message`).
  - **`Services/`**: Services responsible for key functionalities (e.g., `AiService`, `DatabaseService`, `EmbeddingService`, `MemoryContextService`, `MemoryExtractionService`).
 - **`TokenizerRust/`**: Rust project containing tokenization logic, exposed as a native library.

## Local Model Services (overview)

- Local backend: ONNX Runtime GenAI (Phi‑4‑mini‑instruct) with streaming, unified chat template
- Safety layer: `SafeLocalModelWrapper` wraps `ILocalModelService`, tracks failures, enforces cooldowns
- Switchable engines: `RobustLocalModelService`, `LlamaSharpLocalModelService` selectable via `EngineSettings`
- Diagnostics: `LocalModelDiagnosticService` with periodic checks and persisted reports
- Downloads: `NetworkAwareDownloadService` with auto‑resume and connectivity awareness
- UI flow: `MainPageViewModel` listens to `MessagingCenter` events (`LocalModelLoaded`/`LocalModelUnloaded`, `ModelsChanged`), locks cloud picker while local is active

See the project roadmap for planned enhancements: [ROADMAP.md](./ROADMAP.md)

## Testing

LLMClient has comprehensive test coverage with **500+ automated tests** covering all major functionality.

### Test Structure

```
LLMClient.Tests/
├── Models/              (~163 unit tests)
│   ├── ModelsTests.cs           - Conversation, Message, Memory, AiModel
│   ├── EdgeCaseTests.cs         - Edge cases and boundary conditions
│   ├── RagTraceTests.cs         - RAG tracing and timing
│   ├── ErrorReportTests.cs      - Error reporting models
│   ├── EmbeddingModelTests.cs   - Embedding model selection
│   ├── DocumentAnalysisTests.cs - Document analysis models
│   ├── EngineSettingsTests.cs   - Engine configuration
│   └── ModelSettingsTests.cs    - Model settings
├── Services/            (~41 tests)
│   ├── MockServiceTests.cs      - Service mocks
│   └── ServiceInterfaceContractTests.cs - Interface contracts
└── Integration/         (~300 tests)
    ├── MemoryIntegrationTests.cs      - Memory CRUD, search, context
    ├── EmbeddingIntegrationTests.cs   - Embedding generation, similarity
    ├── RagIntegrationTests.cs         - RAG document ingestion, retrieval
    ├── AdvancedRagTests.cs            - Hybrid search, reranking
    ├── ConversationIntegrationTests.cs - Conversation and message handling
    ├── SearchIntegrationTests.cs      - Message search, highlighting
    ├── LocalModelIntegrationTests.cs  - Local model engine, streaming
    ├── RobustLocalModelTests.cs       - Model state, download, memory
    ├── IngestionPipelineTests.cs      - Document queue, processing
    ├── MemoryContextIntegrationTests.cs - Context generation, summarization
    ├── AiServiceIntegrationTests.cs   - AI generation, summarization
    ├── EndToEndWorkflowTests.cs       - Complete workflows
    ├── ErrorHandlingIntegrationTests.cs - Error scenarios, retry
    ├── CacheOptimizationTests.cs      - Caching, batching, lazy loading
    ├── DocumentProcessingTests.cs     - Chunking, analysis
    ├── LocalizationIntegrationTests.cs - Polish/English, formatting
    ├── SecurityIntegrationTests.cs    - API keys, sanitization
    ├── ExportImportIntegrationTests.cs - Export, backup
    ├── OnboardingIntegrationTests.cs  - First-run experience
    ├── ModelDownloadIntegrationTests.cs - Download progress, installation
    ├── ConcurrencyIntegrationTests.cs - Thread safety, rate limiting
    ├── PerformanceIntegrationTests.cs - Response time, throughput
    └── OpenRouterIntegrationTests.cs  - Real API integration tests
```

### Running Tests

```bash
# Run all unit tests (excluding integration tests with real API)
dotnet test LLMClient.Tests --filter "Category!=Integration"

# Run all tests including integration tests
dotnet test LLMClient.Tests

# Run specific test category
dotnet test LLMClient.Tests --filter "FullyQualifiedName~Memory"
```

### Test Coverage Areas

| Area | Tests | Description |
|------|-------|-------------|
| **Models** | 163 | Data models, enums, properties |
| **Memory** | 23 | Memory CRUD, search, context building |
| **Embeddings** | 20 | Generation, similarity, model selection |
| **RAG** | 28 | Document ingestion, retrieval, hybrid search |
| **Conversations** | 15 | CRUD, messages, pagination |
| **Search** | 12 | Highlighting, Polish chars, async |
| **Local Models** | 28 | State management, download, inference |
| **AI Service** | 20 | Generation, streaming, summarization |
| **Error Handling** | 15 | Network, database, retry logic |
| **Performance** | 13 | Response time, memory, throughput |
| **Security** | 17 | API keys, sanitization, validation |
| **Localization** | 12 | Polish/English, date/number formats |
| **Export** | 9 | JSON, Markdown, backup/restore |

### Integration Tests with Real API

Some tests require an OpenRouter API key stored at `~/.llmclient/openrouter_api_key.txt`:

```bash
# Create the file with your API key
echo "sk-or-your-api-key" > ~/.llmclient/openrouter_api_key.txt
```

These tests verify real API behavior including streaming, system prompts, and error handling.

## License

This project is licensed under the [MIT](LICENSE) license.

---

# LLMClient (Polski)

LLMClient to zaawansowany, wieloplatformowy klient AI, stworzony w .NET MAUI, który umożliwia płynną interakcję z wiodącymi modelami językowymi (LLM), takimi jak GPT i Gemini. Aplikacja została zaprojektowana z myślą o bezpieczeństwie, wydajności i elastyczności, oferując lokalne przechowywanie danych z szyfrowaniem oraz natywną tokenizację tekstu.

## Kluczowe Funkcje

- **Wieloplatformowość:** Działa natywnie na systemach Windows, Android, iOS i macOS dzięki .NET MAUI.
- **Obsługa Wielu Modeli:** Łatwa konfiguracja i przełączanie się między modelami od OpenAI, Google (Gemini) oraz dowolnymi dostawcami kompatybilnymi z API OpenAI.
- **Lokalne i Bezpieczne Dane:** Wszystkie konwersacje są przechowywane lokalnie w zaszyfrowanej bazie danych SQLite (przy użyciu SQLCipher), co gwarantuje prywatność.
- **Wydajna Tokenizacja:** Wykorzystuje niestandardową bibliotekę napisaną w Rust do szybkiej i efektywnej tokenizacji, minimalizując opóźnienia.
- **Obsługa Obrazów:** Możliwość wysyłania zapytań multimodalnych (tekst + obraz) do modeli, które to wspierają (np. Gemini, GPT-4 Vision).
- **System Pamięci AI:** Inteligentny system pamięci, który automatycznie zapamiętuje informacje o użytkowniku i dostarcza je w kontekście konwersacji (limit 30,000 znaków z automatycznym streszczaniem).
- **Automatyczna Ekstraktacja Pamięci:** Zaawansowane wydobywanie informacji osobistych z konwersacji przy użyciu wyrażeń regularnych i analizy AI.
- **Wyszukiwanie Semantyczne:** Zaawansowane wyszukiwanie w historii konwersacji z wykorzystaniem wektorów osadzeń (embeddings).
- **Eksport Danych:** Łatwe eksportowanie konwersacji do formatów JSON, Markdown lub TXT.
- **Responsywny Interfejs:** Czysty i nowoczesny interfejs użytkownika, który dostosowuje się do różnych rozmiarów ekranu.

## Stos Technologiczny

- **Framework:** .NET 10 / .NET MAUI
- **Języki:** C#, XAML, Rust
- **Architektura:** MVVM (Model-View-ViewModel)
- **AI & LLM:**
  - Microsoft.SemanticKernel
  - OpenAI, Google Gemini, Modele OpenAI-Compatible
- **Baza Danych:** SQLite z SQLCipher (szyfrowanie)
- **Tokenizacja:** Natywna biblioteka w Rust z `tokenizers`
- **Wyszukiwanie Semantyczne:** ONNX Runtime, Microsoft.ML.Tokenizers

## Pierwsze Kroki

### Wymagania

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Rust toolchain (do kompilacji biblioteki tokenizera)
- Skonfigurowane środowisko dla .NET MAUI (zgodnie z [oficjalną dokumentacją](https://learn.microsoft.com/pl-pl/dotnet/maui/get-started/installation?view=net-maui-10.0&tabs=visual-studio))

### Budowanie i Uruchomienie

1. **Sklonuj repozytorium:**
   ```bash
   git clone https://github.com/DamianTarnowski/LLMClient.git
   cd LLMClient
   ```

2. **Zbuduj bibliotekę Rust:**
   ```bash
   cd TokenizerRust
   cargo build --release
   ```
   Spowoduje to umieszczenie pliku `tokenizer_rust.dll` (lub odpowiednika dla Twojego systemu) w folderze `target/release`. Projekt .NET MAUI jest skonfigurowany, aby automatycznie kopiować ten plik podczas budowania.

3. **Otwórz i uruchom projekt .NET MAUI:**
   - Otwórz plik `LLMClient.sln` w programie Visual Studio 2022.
   - Wybierz platformę docelową (np. Windows Machine, Android Emulator).
   - Naciśnij F5, aby zbudować i uruchomić aplikację.

### Konfiguracja

1. Uruchom aplikację.
2. Przejdź do ekranu `Ustawienia` (ikona koła zębatego).
3. Dodaj nową konfigurację modelu, podając jego nazwę, dostawcę (OpenAI, Gemini, etc.), klucz API oraz ID modelu.
4. Aktywuj model i rozpocznij nową konwersację!

## System Pamięci AI

LLMClient posiada zaawansowany system pamięci, który automatycznie personalizuje interakcje z modelami AI:

### Jak działa

1. **Automatyczna Ekstraktacja:** System automatycznie wydobywa informacje osobiste z konwersacji (imię, wiek, zawód, preferencje, itp.) przy użyciu:
   - Wyrażeń regularnych dla podstawowych informacji (imię, wiek, lokalizacja)
   - Analizy AI dla bardziej złożonych danych osobowych

2. **Kontekst w Konwersacji:** Każda konwersacja otrzymuje pełny kontekst pamięci (maksymalnie 30,000 znaków) w wiadomości systemowej, pozwalając modelom na spersonalizowane odpowiedzi.

3. **Inteligentne Zarządzanie:** Gdy pamięć przekroczy limit znaków:
   - Najważniejsze i najnowsze informacje pozostają w pełnej formie
   - Starsze wspomnienia są automatycznie streszczane przez AI
   - Zachowana zostaje maksymalna ilość przydatnych informacji

4. **Zarządzanie Pamięcią:** Dostępny jest interfejs do przeglądania, edycji i kategoryzowania zapisanych wspomnień.

## Obsługiwane Języki

- Aplikacja dostosowuje język odpowiedzi do języka wiadomości użytkownika.
- Zakres języków zależy od wybranego modelu:
  - Modele chmurowe (OpenAI/Gemini/OpenAI‑compatible): szeroka obsługa wielu języków.
  - Model lokalny (Phi‑4‑mini‑instruct przez ONNX): najlepsza jakość EN/PL; jakość w innych językach może być niższa.
- UI: dostępne zasoby PL i EN (w zestawie są zasoby PL).

### Obsługiwane Informacje

System automatycznie rozpoznaje i zapisuje:
- **Dane osobowe:** Imię, wiek, pochodzenie, zawód
- **Preferencje:** Hobby, zainteresowania, upodobania
- **Fakty życiowe:** Rodzina, wykształcenie, doświadczenia
- **Kontekst rozmów:** Ważne tematy i szczegóły z poprzednich konwersacji

### Bezpieczeństwo

Wszystkie dane pamięci są:
- Przechowywane lokalnie w zaszyfrowanej bazie SQLite
- Dostępne tylko dla użytkownika na jego urządzeniu
- Nieudostępniane zewnętrznym usługom

## Architektura

Projekt wykorzystuje wzorzec MVVM i jest podzielony na następujące warstwy:

- **`LLMClient/`**: Główny projekt .NET MAUI.
  - **`Views/`**: Pliki XAML definiujące interfejs użytkownika.
  - **`ViewModels/`**: Logika biznesowa i stan dla poszczególnych widoków.
  - **`Models/`**: Modele danych (np. `Conversation`, `Message`).
  - **`Services/`**: Serwisy odpowiedzialne za kluczowe funkcjonalności (np. `AiService`, `DatabaseService`, `EmbeddingService`, `MemoryContextService`, `MemoryExtractionService`).
 - **`TokenizerRust/`**: Projekt Rust zawierający logikę tokenizacji, udostępnioną jako natywna biblioteka.

## Usługi Modelu Lokalnego (przegląd)

- Backend lokalny: ONNX Runtime GenAI (Phi‑4‑mini‑instruct) ze strumieniowaniem i ujednoliconym szablonem czatu
- Warstwa bezpieczeństwa: `SafeLocalModelWrapper` opakowuje `ILocalModelService`, śledzi błędy i wymusza cooldown
- Przełączalne silniki: `RobustLocalModelService`, `LlamaSharpLocalModelService` wybierane przez `EngineSettings`
- Diagnostyka: `LocalModelDiagnosticService` z okresowymi checkami i zapisem raportów
- Pobieranie: `NetworkAwareDownloadService` z auto‑wznawianiem i świadomością łączności
- Przepływ UI: `MainPageViewModel` subskrybuje zdarzenia `MessagingCenter` (`LocalModelLoaded`/`LocalModelUnloaded`, `ModelsChanged`), blokuje wybór chmury gdy lokalny jest aktywny

Planowane ulepszenia znajdziesz tutaj: [ROADMAP.md](./ROADMAP.md)

## Testowanie

LLMClient posiada kompleksowe pokrycie testami z **ponad 500 automatycznymi testami** obejmującymi wszystkie główne funkcjonalności.

### Struktura Testów

```
LLMClient.Tests/
├── Models/              (~163 testy jednostkowe)
│   ├── ModelsTests.cs           - Conversation, Message, Memory, AiModel
│   ├── EdgeCaseTests.cs         - Przypadki brzegowe
│   ├── RagTraceTests.cs         - Śledzenie RAG i timing
│   ├── ErrorReportTests.cs      - Modele raportowania błędów
│   ├── EmbeddingModelTests.cs   - Wybór modelu embeddingowego
│   ├── DocumentAnalysisTests.cs - Modele analizy dokumentów
│   ├── EngineSettingsTests.cs   - Konfiguracja silnika
│   └── ModelSettingsTests.cs    - Ustawienia modelu
├── Services/            (~41 testów)
│   ├── MockServiceTests.cs      - Mocki serwisów
│   └── ServiceInterfaceContractTests.cs - Kontrakty interfejsów
└── Integration/         (~300 testów)
    ├── MemoryIntegrationTests.cs      - Pamięć CRUD, wyszukiwanie
    ├── EmbeddingIntegrationTests.cs   - Generowanie embeddingów
    ├── RagIntegrationTests.cs         - Ingestion dokumentów RAG
    ├── AdvancedRagTests.cs            - Hybrid search, reranking
    ├── ConversationIntegrationTests.cs - Obsługa konwersacji
    ├── SearchIntegrationTests.cs      - Wyszukiwanie, highlighting
    ├── LocalModelIntegrationTests.cs  - Silnik modeli lokalnych
    ├── RobustLocalModelTests.cs       - Stan modelu, pobieranie
    ├── IngestionPipelineTests.cs      - Kolejka dokumentów
    ├── MemoryContextIntegrationTests.cs - Generowanie kontekstu
    ├── AiServiceIntegrationTests.cs   - Generowanie AI
    ├── EndToEndWorkflowTests.cs       - Kompletne workflow
    ├── ErrorHandlingIntegrationTests.cs - Obsługa błędów
    ├── CacheOptimizationTests.cs      - Cache, batching
    ├── DocumentProcessingTests.cs     - Chunking, analiza
    ├── LocalizationIntegrationTests.cs - PL/EN, formatowanie
    ├── SecurityIntegrationTests.cs    - API keys, sanityzacja
    ├── ExportImportIntegrationTests.cs - Eksport, backup
    ├── OnboardingIntegrationTests.cs  - Pierwsze uruchomienie
    ├── ModelDownloadIntegrationTests.cs - Pobieranie modeli
    ├── ConcurrencyIntegrationTests.cs - Bezpieczeństwo wątków
    ├── PerformanceIntegrationTests.cs - Wydajność
    └── OpenRouterIntegrationTests.cs  - Testy z prawdziwym API
```

### Uruchamianie Testów

```bash
# Uruchom testy jednostkowe (bez testów API)
dotnet test LLMClient.Tests --filter "Category!=Integration"

# Uruchom wszystkie testy
dotnet test LLMClient.Tests

# Uruchom konkretną kategorię
dotnet test LLMClient.Tests --filter "FullyQualifiedName~Memory"
```

### Pokrycie Testami

| Obszar | Testy | Opis |
|--------|-------|------|
| **Modele** | 163 | Modele danych, enumy, właściwości |
| **Pamięć** | 23 | CRUD, wyszukiwanie, budowanie kontekstu |
| **Embeddingi** | 20 | Generowanie, podobieństwo, wybór modelu |
| **RAG** | 28 | Ingestion, retrieval, hybrid search |
| **Konwersacje** | 15 | CRUD, wiadomości, paginacja |
| **Wyszukiwanie** | 12 | Highlighting, polskie znaki |
| **Modele lokalne** | 28 | Zarządzanie stanem, pobieranie |
| **AI Service** | 20 | Generowanie, streaming |
| **Błędy** | 15 | Sieć, baza danych, retry |
| **Wydajność** | 13 | Czas odpowiedzi, pamięć |
| **Bezpieczeństwo** | 17 | API keys, sanityzacja |
| **Lokalizacja** | 12 | PL/EN, formaty dat/liczb |
| **Eksport** | 9 | JSON, Markdown, backup |

### Testy z Prawdziwym API

Niektóre testy wymagają klucza API OpenRouter w `~/.llmclient/openrouter_api_key.txt`:

```bash
# Utwórz plik z kluczem API
echo "sk-or-your-api-key" > ~/.llmclient/openrouter_api_key.txt
```

## Licencja

Ten projekt jest udostępniany na licencji [MIT](LICENSE).

## Embedding Models for Semantic Search

LLMClient offers two embedding models for semantic search and RAG (Retrieval-Augmented Generation):

### Available Models

| Model | Quality | Speed | Size | Min RAM | Best For |
|-------|---------|-------|------|---------|----------|
| **E5-Large Multilingual** | 🎯 95% | 60% | ~2.2GB | 8GB | Desktop, high-quality search |
| **Gemma 300M** | 🚀 75% | 90% | ~1.2GB | 4GB | Mobile, resource-constrained devices |

### Automatic Model Selection

The app automatically selects the best model based on device RAM:
- **≥8GB RAM** → E5-Large (higher quality for Polish/multilingual)
- **<8GB RAM** → Gemma 300M (faster, lighter)

Users can override this in Settings → Embedding Model.

### Quality Comparison (Integration Test Results)

Tests performed on Polish language pairs comparing semantic similarity scores:

#### Similar Sentences (higher = better)
| Pair | E5-Large | Gemma 300M | Δ |
|------|----------|------------|---|
| "Lubię programować" ↔ "Uwielbiam kodować" | **0.936** | 0.840 | +11% |
| "Kot śpi" ↔ "Kotek drzemie" | **0.944** | 0.614 | +54% |
| "Auto jedzie" ↔ "Samochód pędzi" | **0.952** | 0.682 | +40% |
| **Average** | **0.944** | 0.712 | **+33%** |

#### Different Sentences (lower = better discrimination)
| Pair | E5-Large | Gemma 300M | Δ |
|------|----------|------------|---|
| "Lubię programować" ↔ "Pierogi są pyszne" | 0.829 | **0.441** | -47% |
| "Kot śpi" ↔ "Samolot leci" | 0.833 | **0.440** | -47% |
| "Auto jedzie" ↔ "Książka leży" | 0.788 | **0.393** | -50% |
| **Average** | 0.817 | **0.425** | **-48%** |

#### Cross-Lingual (PL ↔ EN)
| Pair | E5-Large | Gemma 300M |
|------|----------|------------|
| PL: "Lubię programować" ↔ EN: "I like programming" | **0.966** | 0.701 |
| PL: "Kot jest zwierzęciem" ↔ EN: "A cat is an animal" | **0.920** | 0.794 |
| **Average** | **0.943** | 0.748 |

### Interpretation

1. **E5-Large excels at Polish semantic similarity** - 33% higher scores for similar sentences, making it ideal for precise semantic search in Polish documents.

2. **Gemma better discriminates unrelated content** - 48% lower scores for different sentences means fewer false positives in search results.

3. **E5-Large superior for cross-lingual** - Critical for applications mixing Polish and English content.

4. **Trade-off**: E5-Large requires 2x more RAM but provides significantly better quality for Polish language semantic search. For mobile devices with <8GB RAM, Gemma offers acceptable quality with much better performance.

### Tokenization

Both models use native Rust tokenizers for optimal performance:
- **E5-Large**: HuggingFace tokenizers (BPE)
- **Gemma**: Kitoken with SentencePiece fallback (Unigram)

## Recent additions (English)

- Local ONNX Runtime GenAI (Phi‑4‑mini‑instruct) with streaming, unified chat template, and safety controls
- Editable system prompt stored in DB; if empty, no system message is injected
- Optional memory injection (cloud models only) controlled by a toggle in Settings
- Memory extraction disabled while a local model is active (reduces noise and hallucinations)
- Adaptive language: the assistant answers in the same language as the user
- Automatic conversation creation when none is active (Android/mobile friendly)
- Cloud model picker automatically locks while a local model is loaded; unlocks after unload
- Spinner overlay both for downloading and for loading the local model into RAM

How to use the local model
1) Settings → Local model → Download (if needed)
2) Load model → overlay spinner shows during load
3) Local model becomes active; cloud picker is disabled
4) Unload to return to cloud models


## Modele Embeddingowe dla Wyszukiwania Semantycznego

LLMClient oferuje dwa modele embeddingowe do wyszukiwania semantycznego i RAG:

### Dostępne Modele

| Model | Jakość | Szybkość | Rozmiar | Min RAM | Najlepszy dla |
|-------|--------|----------|---------|---------|---------------|
| **E5-Large Multilingual** | 🎯 95% | 60% | ~2.2GB | 8GB | Desktop, wysokiej jakości wyszukiwanie |
| **Gemma 300M** | 🚀 75% | 90% | ~1.2GB | 4GB | Mobile, urządzenia z ograniczeniami |

### Automatyczny Wybór Modelu

Aplikacja automatycznie wybiera najlepszy model na podstawie RAM urządzenia:
- **≥8GB RAM** → E5-Large (wyższa jakość dla polskiego)
- **<8GB RAM** → Gemma 300M (szybszy, lżejszy)

Użytkownik może to zmienić w Ustawieniach → Model Embeddingowy.

### Porównanie Jakości (Wyniki Testów Integracyjnych)

Testy przeprowadzone na polskich parach zdań porównujące wyniki podobieństwa semantycznego:

#### Podobne Zdania (wyższy wynik = lepiej)
| Para | E5-Large | Gemma 300M | Δ |
|------|----------|------------|---|
| "Lubię programować" ↔ "Uwielbiam kodować" | **0.936** | 0.840 | +11% |
| "Kot śpi" ↔ "Kotek drzemie" | **0.944** | 0.614 | +54% |
| "Auto jedzie" ↔ "Samochód pędzi" | **0.952** | 0.682 | +40% |
| **Średnia** | **0.944** | 0.712 | **+33%** |

#### Różne Zdania (niższy wynik = lepsza dyskryminacja)
| Para | E5-Large | Gemma 300M | Δ |
|------|----------|------------|---|
| "Lubię programować" ↔ "Pierogi są pyszne" | 0.829 | **0.441** | -47% |
| "Kot śpi" ↔ "Samolot leci" | 0.833 | **0.440** | -47% |
| "Auto jedzie" ↔ "Książka leży" | 0.788 | **0.393** | -50% |
| **Średnia** | 0.817 | **0.425** | **-48%** |

#### Cross-Lingual (PL ↔ EN)
| Para | E5-Large | Gemma 300M |
|------|----------|------------|
| PL: "Lubię programować" ↔ EN: "I like programming" | **0.966** | 0.701 |
| PL: "Kot jest zwierzęciem" ↔ EN: "A cat is an animal" | **0.920** | 0.794 |
| **Średnia** | **0.943** | 0.748 |

### Interpretacja Wyników

1. **E5-Large doskonały dla polskiego podobieństwa semantycznego** - 33% wyższe wyniki dla podobnych zdań, idealny do precyzyjnego wyszukiwania semantycznego w polskich dokumentach.

2. **Gemma lepiej rozróżnia niepowiązane treści** - 48% niższe wyniki dla różnych zdań oznacza mniej fałszywych trafień w wynikach wyszukiwania.

3. **E5-Large przewyższa w cross-lingual** - Krytyczne dla aplikacji mieszających polskie i angielskie treści.

4. **Kompromis**: E5-Large wymaga 2x więcej RAM, ale zapewnia znacznie lepszą jakość dla polskiego wyszukiwania semantycznego. Dla urządzeń mobilnych z <8GB RAM, Gemma oferuje akceptowalną jakość przy znacznie lepszej wydajności.

### Tokenizacja

Oba modele używają natywnych tokenizerów Rust dla optymalnej wydajności:
- **E5-Large**: HuggingFace tokenizers (BPE)
- **Gemma**: Kitoken z fallbackiem SentencePiece (Unigram)

## Nowości (Polski)

- Lokalny ONNX Runtime GenAI (Phi‑4‑mini‑instruct) ze strumieniowaniem, ujednoliconym szablonem czatu i kontrolą bezpieczeństwa
- Edytowalny system prompt zapisywany w bazie; jeśli pusty, nie wstrzykujemy system message
- Opcjonalne dołączanie pamięci (tylko dla modeli chmurowych) sterowane przełącznikiem w Ustawieniach
- Ekstrakcja pamięci wyłączona, gdy aktywny jest model lokalny (mniej szumu/halucynacji)
- Adaptacyjny język: asystent odpowiada w tym samym języku, w którym pisze użytkownik
- Automatyczne tworzenie konwersacji, gdy nie ma aktywnej (wygodne na Androidzie)
- Blokada wyboru modeli chmurowych, gdy lokalny model jest załadowany; odblokowanie po rozładowaniu
- Overlay ze spinnerem zarówno podczas pobierania, jak i ładowania modelu lokalnego do RAM

Jak korzystać z modelu lokalnego
1) Ustawienia → Model lokalny → Pobierz (jeśli potrzeba)
2) Załaduj model → w trakcie ładowania widoczny overlay ze spinnerem
3) Model lokalny staje się aktywny; wybór modeli chmurowych jest zablokowany
4) Rozładuj, aby wrócić do modeli chmurowych
