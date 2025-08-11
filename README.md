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

- **Framework:** .NET 9 / .NET MAUI
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

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Rust toolchain (for compiling the tokenizer library)
- Configured .NET MAUI environment (according to [official documentation](https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation?view=net-maui-9.0&tabs=visual-studio))

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

- **Framework:** .NET 9 / .NET MAUI
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

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Rust toolchain (do kompilacji biblioteki tokenizera)
- Skonfigurowane środowisko dla .NET MAUI (zgodnie z [oficjalną dokumentacją](https://learn.microsoft.com/pl-pl/dotnet/maui/get-started/installation?view=net-maui-9.0&tabs=visual-studio))

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

## Licencja

Ten projekt jest udostępniany na licencji [MIT](LICENSE).
