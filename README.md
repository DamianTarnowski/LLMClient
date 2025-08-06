# LLMClient

LLMClient to zaawansowany, wieloplatformowy klient AI, stworzony w .NET MAUI, który umożliwia płynną interakcję z wiodącymi modelami językowymi (LLM), takimi jak GPT i Gemini. Aplikacja została zaprojektowana z myślą o bezpieczeństwie, wydajności i elastyczności, oferując lokalne przechowywanie danych z szyfrowaniem oraz natywną tokenizację tekstu.

![image](https://github.com/user-attachments/assets/e5f7e521-171a-4489-9b3e-58a4f6f74217)


## Kluczowe Funkcje

- **Wieloplatformowość:** Działa natywnie na systemach Windows, Android, iOS i macOS dzięki .NET MAUI.
- **Obsługa Wielu Modeli:** Łatwa konfiguracja i przełączanie się między modelami od OpenAI, Google (Gemini) oraz dowolnymi dostawcami kompatybilnymi z API OpenAI.
- **Lokalne i Bezpieczne Dane:** Wszystkie konwersacje są przechowywane lokalnie w zaszyfrowanej bazie danych SQLite (przy użyciu SQLCipher), co gwarantuje prywatność.
- **Wydajna Tokenizacja:** Wykorzystuje niestandardową bibliotekę napisaną w Rust do szybkiej i efektywnej tokenizacji, minimalizując opóźnienia.
- **Obsługa Obrazów:** Możliwość wysyłania zapytań multimodalnych (tekst + obraz) do modeli, które to wspierają (np. Gemini, GPT-4 Vision).
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

## Architektura

Projekt wykorzystuje wzorzec MVVM i jest podzielony na następujące warstwy:

- **`LLMClient/`**: Główny projekt .NET MAUI.
  - **`Views/`**: Pliki XAML definiujące interfejs użytkownika.
  - **`ViewModels/`**: Logika biznesowa i stan dla poszczególnych widoków.
  - **`Models/`**: Modele danych (np. `Conversation`, `Message`).
  - **`Services/`**: Serwisy odpowiedzialne za kluczowe funkcjonalności (np. `AiService`, `DatabaseService`, `EmbeddingService`).
- **`TokenizerRust/`**: Projekt Rust zawierający logikę tokenizacji, udostępnioną jako natywna biblioteka.

## Licencja

Ten projekt jest udostępniany na licencji [MIT](LICENSE).
