# Architektura Aplikacji Synapse AI

Ten dokument opisuje architekturę i kluczowe decyzje projektowe stojące za aplikacją Synapse AI. Zrozumienie tych koncepcji jest kluczowe dla deweloperów chcących rozwijać i utrzymywać projekt.

## 1. Przegląd Architektury

Synapse AI jest zbudowany w oparciu o wzorzec **Model-View-ViewModel (MVVM)**, który jest standardem dla aplikacji .NET MAUI. Taka struktura zapewnia czysty podział odpowiedzialności między interfejsem użytkownika (View), logiką prezentacji (ViewModel) a danymi (Model).

Architektura opiera się na trzech głównych filarach:

1.  **Modułowy, zorientowany na serwisy backend:** Logika biznesowa jest zamknięta w serwisach wstrzykiwanych przez mechanizm Dependency Injection (DI). Każdy serwis ma jasno określoną odpowiedzialność (np. komunikacja z AI, obsługa bazy danych, generowanie osadzeń).
2.  **Reaktywny interfejs użytkownika:** Widoki (XAML) są "głupie" i jedynie wiążą się z danymi i komendami z ViewModeli. Zmiany w stanie ViewModelu są automatycznie odzwierciedlane w UI.
3.  **Natywna integracja dla krytycznych zadań:** Zadania wymagające wysokiej wydajności, takie jak tokenizacja tekstu, zostały zaimplementowane w Rust i zintegrowane z aplikacją C# poprzez FFI (Foreign Function Interface), aby uniknąć wąskich gardeł wydajnościowych platformy .NET.

## 2. Kluczowe Komponenty

### 2.1. Warstwa Widoku (Views)

- **Technologia:** XAML
- **Odpowiedzialność:** Definicja struktury i wyglądu interfejsu użytkownika.
- **Kluczowe cechy:**
    - **Responsywność:** Użycie `VisualStateManager` do dynamicznego dostosowywania layoutu między platformami mobilnymi i desktopowymi.
    - **Wiązanie danych (Data Binding):** Ścisłe powiązanie z właściwościami i komendami w ViewModelach.
    - **Brak logiki w code-behind:** Pliki `.xaml.cs` zawierają minimalną ilość kodu, zazwyczaj ograniczoną do obsługi zdarzeń specyficznych dla UI (np. animacje, obsługa kontrolek).

### 2.2. Warstwa Logiki Prezentacji (ViewModels)

- **Technologia:** C#
- **Odpowiedzialność:** Zarządzanie stanem widoku, implementacja logiki biznesowej i obsługa interakcji użytkownika.
- **Kluczowe cechy:**
    - **`INotifyPropertyChanged`:** Standardowy interfejs do powiadamiania widoku o zmianach w danych.
    - **`ICommand`:** Implementacja komend (np. `SendMessageCommand`), które są wywoływane przez UI w odpowiedzi na akcje użytkownika.
    - **Wstrzykiwanie zależności:** ViewModels otrzymują wymagane serwisy poprzez konstruktor, co ułatwia testowanie i oddzielenie logiki.

### 2.3. Warstwa Modeli (Models)

- **Technologia:** C# (POCO - Plain Old CLR Objects)
- **Odpowiedzialność:** Reprezentacja danych aplikacji (np. `Conversation`, `Message`, `AiModel`).
- **Kluczowe cechy:**
    - **Proste obiekty danych:** Nie zawierają logiki biznesowej.
    - **Atrybuty SQLite:** Oznaczone atrybutami do mapowania obiektowo-relacyjnego (ORM) z bazą danych.

### 2.4. Warstwa Usług (Services)

Serwisy stanowią rdzeń aplikacji. Są rejestrowane jako singletony lub obiekty przejściowe w kontenerze DI (`MauiProgram.cs`).

- **`AiService`**: Abstrakcja nad **Microsoft.SemanticKernel**. Odpowiada za formatowanie zapytań, komunikację z API modeli językowych (OpenAI, Gemini) oraz obsługę odpowiedzi strumieniowych i standardowych.

- **`DatabaseService`**: Zarządza lokalną bazą danych **SQLite**. Kluczowe decyzje:
    - **Szyfrowanie:** Użycie **SQLCipher** do szyfrowania całej bazy danych. Klucz szyfrujący jest generowany i bezpiecznie przechowywany w `SecureStorage` specyficznym dla platformy.
    - **Bezpieczeństwo kluczy API:** Klucze API modeli *nie są* przechowywane w bazie danych. Zamiast tego `SecureApiKeyService` przechowuje je w `SecureStorage`, a `DatabaseService` jedynie zarządza metadanymi modeli.
    - **Relacje:** Użycie `SQLiteNetExtensions.Async` do zarządzania relacjami między tabelami (np. Konwersacja -> Wiadomości).

- **`EmbeddingService`**: Odpowiada za generowanie wektorów osadzeń (embeddings) dla tekstu. To jeden z najbardziej krytycznych komponentów:
    - **Model ONNX:** Wykorzystuje model `multilingual-e5-large` w formacie ONNX, uruchamiany przez `Microsoft.ML.OnnxRuntime`. Model jest pobierany przy pierwszym uruchomieniu z Hugging Face.
    - **Natywna Tokenizacja:** Zamiast używać tokenizera w C#, co byłoby wolne, `EmbeddingService` komunikuje się z biblioteką Rust poprzez `TokenizerNative` (wrapper FFI). To zapewnia niemal natywną wydajność tokenizacji.
    - **Normalizacja i Pooling:** Implementuje logikę `mean pooling` i normalizacji L2, aby uzyskać końcowy wektor osadzenia, gotowy do porównania cosinusowego.

- **`SearchService`**: Implementuje logikę wyszukiwania:
    - **Wyszukiwanie Pełnotekstowe:** Proste wyszukiwanie oparte na wyrażeniach regularnych.
    - **Wyszukiwanie Semantyczne:** Wykorzystuje `EmbeddingService` do konwersji zapytania na wektor, a następnie prosi `DatabaseService` o znalezienie najbardziej podobnych wektorów w bazie danych za pomocą podobieństwa cosinusowego.

- **`TokenizerRust` (Natywna Biblioteka):**
    - **Powód decyzji:** Wydajność. Tokenizery oparte na C# są znacznie wolniejsze niż natywne implementacje. Rust został wybrany ze względu na bezpieczeństwo pamięci, wydajność i doskonałe biblioteki ekosystemu (np. `tokenizers` od Hugging Face).
    - **Implementacja:** Biblioteka Rust (`tokenizer_rust.dll`/`.so`/`.dylib`) eksponuje prosty interfejs C (`tokenizer_init`, `tokenizer_encode`, `tokenizer_decode`), który jest wywoływany z C#.
    - **Zarządzanie Pamięcią:** Pamięć jest zarządzana po stronie Rusta, co minimalizuje ryzyko wycieków w kodzie C#.

## 3. Przepływ Danych

### Przepływ wysyłania wiadomości (strumieniowo):

1.  Użytkownik wpisuje wiadomość w `MainPage.xaml` i klika "Wyślij".
2.  `SendMessageCommand` w `MainPageViewModel` zostaje wywołany.
3.  ViewModel tworzy obiekt `Message`, zapisuje go w `DatabaseService` i dodaje do kolekcji w UI.
4.  ViewModel wywołuje `_aiService.GetStreamingResponseAsync()`.
5.  `AiService` buduje historię czatu i wysyła zapytanie do odpowiedniego modelu (np. Gemini).
6.  Gdy fragmenty odpowiedzi (chunk) napływają, `AiService` zwraca je jako `IAsyncEnumerable<string>`.
7.  `MainPageViewModel` odbiera fragmenty i za pomocą `StreamingBatchService` aktualizuje ostatnią wiadomość w UI w czasie rzeczywistym.
8.  Po zakończeniu strumienia, pełna wiadomość jest zapisywana w `DatabaseService`.

### Przepływ wyszukiwania semantycznego:

1.  Użytkownik wpisuje zapytanie w `SemanticSearchPage.xaml`.
2.  `SemanticSearchViewModel` wywołuje `_searchService.SemanticSearchAcrossConversationsAsync()`.
3.  `SearchService` prosi `_embeddingService` o wygenerowanie wektora dla zapytania.
4.  `EmbeddingService` tokenizuje zapytanie (przez Rust FFI) i przepuszcza je przez model ONNX, aby uzyskać wektor.
5.  `SearchService` przekazuje wektor zapytania do `DatabaseService`.
6.  `DatabaseService` wykonuje zapytanie, które pobiera wszystkie wektory wiadomości, oblicza podobieństwo cosinusowe w pamięci i zwraca posortowane wyniki.
7.  Wyniki są propagowane z powrotem do `SemanticSearchViewModel` i wyświetlane w UI.

## 4. Podsumowanie Decyzji Architektonicznych

- **.NET MAUI:** Wybrane dla maksymalizacji współdzielenia kodu między platformami.
- **MVVM:** Dla czystej, testowalnej i skalowalnej architektury UI.
- **SQLite z SQLCipher:** Dla bezpiecznego, lokalnego przechowywania danych bez zależności od zewnętrznych serwerów.
- **Rust dla Tokenizacji:** Krytyczna optymalizacja wydajności, która odróżnia ten projekt od typowych aplikacji .NET.
- **Semantic Kernel:** Zapewnia elastyczną abstrakcję, która ułatwia dodawanie nowych modeli AI w przyszłości.

Ta architektura została zaprojektowana z myślą o solidności, wydajności i łatwości w utrzymaniu, co czyni ją doskonałą bazą dla dalszego rozwoju.