# LLMClient - Dokumentacja Rozwoju

## Opis Projektu
LLMClient to zaawansowany klient AI napisany w .NET MAUI z systemem pamięci, wyszukiwaniem semantycznym i obsługą wielu dostawców AI.

## Architektura Systemu Pamięci

### Przegląd
System pamięci został przeprojektowany z function calling na bezpośrednie wstrzykiwanie kontekstu do wiadomości systemowej.

### Kluczowe Komponenty

#### MemoryContextService
- **Lokalizacja**: `LLMClient/Services/MemoryContextService.cs`
- **Funkcja**: Generuje kontekst pamięci dla wiadomości systemowych
- **Limit**: 30,000 znaków z automatycznym streszczaniem
- **Działanie**:
  - Pobiera wszystkie wspomnienia z bazy danych
  - Sortuje według ważności i daty aktualizacji
  - Buduje kontekst do limitu 30K znaków
  - Automatycznie streszcza starsze wspomnienia przy użyciu AI

#### MemoryExtractionService
- **Lokalizacja**: `LLMClient/Services/MemoryExtractionService.cs`
- **Funkcja**: Automatyczne wydobywanie informacji z konwersacji
- **Metody**:
  - Wyrażenia regularne dla podstawowych danych (imię, wiek, lokalizacja)
  - Analiza AI dla złożonych informacji osobistych
- **Uruchomienie**: Automatycznie po każdej konwersacji (ostatnie 10 wiadomości)

#### DatabaseMemoryService
- **Lokalizacja**: `LLMClient/Services/DatabaseMemoryService.cs`
- **Funkcja**: Wrapper dla operacji pamięci wykorzystujący zaszyfrowaną bazę danych
- **Integracja**: Używa tego samego DatabaseService co reszta aplikacji

### Przepływ Danych Pamięci

1. **Ekstraktacja**:
   ```
   Konwersacja → MemoryExtractionService → Analiza regex/AI → Zapis do bazy
   ```

2. **Wstrzykiwanie kontekstu**:
   ```
   AiService.CreateChatHistoryAsync() → MemoryContextService.GenerateMemoryContextAsync() → Kontekst systemowy
   ```

3. **Zarządzanie**:
   ```
   MemoryPage → MemoryPageViewModel → DatabaseMemoryService → SQLite
   ```

## Struktura Bazy Danych

### Tabela Memories
```sql
CREATE TABLE Memories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Key TEXT NOT NULL,
    Value TEXT NOT NULL,
    Category TEXT,
    Tags TEXT,
    IsImportant BOOLEAN DEFAULT 0,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

## Testy

### Uruchamianie Testów
```bash
dotnet test LLMClient.Tests/
```

### Pokrycie Testami
- **MemoryService**: Operacje CRUD, walidacja danych
- **MemoryContextService**: Generowanie kontekstu, streszczanie
- **MemoryExtractionService**: Regex i AI extraction
- **DatabaseService**: Integracja pamięci z główną bazą

## Konfiguracja Dependency Injection

```csharp
// Memory Services
builder.Services.AddSingleton<IMemoryService>(provider =>
{
    var databaseService = provider.GetRequiredService<DatabaseService>();
    return new DatabaseMemoryService(databaseService);
});

builder.Services.AddSingleton<IMemoryContextService>(provider =>
{
    var memoryService = provider.GetRequiredService<IMemoryService>();
    var lazyAiService = new Lazy<IAiService?>(() => provider.GetService<IAiService>());
    return new MemoryContextService(memoryService, lazyAiService);
});

builder.Services.AddSingleton<IMemoryExtractionService>(provider =>
{
    var memoryService = provider.GetRequiredService<IMemoryService>();
    var aiService = provider.GetRequiredService<IAiService>();
    return new MemoryExtractionService(memoryService, aiService);
});
```

## Debugging

### Logs Pamięci
System używa `System.Diagnostics.Debug.WriteLine()` dla logowania:
- `[MemoryContextService]`: Generowanie kontekstu
- `[MemoryExtractionService]`: Ekstraktacja danych
- `[AiService]`: Ładowanie kontekstu pamięci

### Problemy i Rozwiązania

1. **Circular Dependency**: 
   - Problem: MemoryContextService potrzebuje AiService do streszczania, ale AiService potrzebuje MemoryContextService
   - Rozwiązanie: Użycie `Lazy<IAiService?>` w MemoryContextService

2. **Database Persistence**:
   - Problem: Pamięć nie utrzymuje się między sesjami
   - Rozwiązanie: DatabaseMemoryService wykorzystuje tę samą zaszyfrowaną bazę co reszta aplikacji

## Rozszerzenia

### Dodawanie Nowych Wzorców Ekstraktacji
W `MemoryExtractionService.ExtractSimpleMemoryAsync()`:

```csharp
var simplePatterns = new Dictionary<string, string>
{
    { @"nowy wzorzec regex", "klucz_pamięci" }
};
```

### Modyfikacja Limitu Pamięci
W `MemoryContextService`:
```csharp
private const int MAX_MEMORY_CHARS = 30000; // Zmień tutaj
```

### Dodawanie Kategorii Pamięci
System obsługuje dowolne kategorie - dodaj je w UI MemoryPage lub pozwól AI na automatyczne kategoryzowanie.