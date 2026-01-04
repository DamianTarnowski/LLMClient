# Plan Ulepszenia Mechanizmu Pamięci LLM

> Data utworzenia: 2025-01-04
> Status: W trakcie planowania

## Obecny stan

### Co mamy:
- `Memory` model - Key-Value + Category + Tags + IsImportant
- `MemoryService` - CRUD na SQLite, wyszukiwanie tekstowe
- `MemoryContextService` - Budowanie kontekstu z limitami (30k chars)
- `MemoryExtractionService` - Regex + AI ekstrakcja

### Problemy:
1. ❌ Brak embeddingów dla pamięci (tylko wyszukiwanie tekstowe)
2. ❌ Brak Episodic Memory (nie uczymy się z doświadczenia)
3. ❌ Prosta ekstrakcja (regex + AI tylko dla chmury)
4. ❌ Brak memory scoring (tylko IsImportant + UpdatedAt)
5. ❌ Brak automatycznego zapominania (pamięć rośnie w nieskończoność)
6. ⚠️ Brak integracji z RAG

---

## Faza 1: Vector Memory 🔴 PRIORYTET WYSOKI

**Cel:** Dodanie embeddingów do pamięci i wyszukiwania semantycznego

### Zadania:
- [ ] Dodać pole `byte[] Embedding` do modelu `Memory`
- [ ] Dodać pole `DateTime EmbeddingGeneratedAt` (cache invalidation)
- [ ] Rozszerzyć `IMemoryService` o:
  ```csharp
  Task<List<(Memory mem, float similarity)>> SemanticSearchAsync(string query, int topK = 10, float minSimilarity = 0.5f);
  Task GenerateEmbeddingsForAllAsync(IProgress<int>? progress = null);
  Task<int> GetMemoriesWithoutEmbeddingsCountAsync();
  ```
- [ ] Generować embeddingi przy zapisie pamięci (używając `IEmbeddingService`)
- [ ] Zintegrować z `MemoryContextService`:
  - Pobierać relewantne wspomnienia semantycznie zamiast wszystkich
  - Fallback na tekstowe wyszukiwanie gdy brak embeddingów
- [ ] Migracja bazy danych (dodanie kolumny Embedding)
- [ ] UI: Przycisk "Generuj embeddingi" w ustawieniach pamięci
- [ ] Testy integracyjne dla semantic search

### Zależności:
- Wymaga działającego `IEmbeddingService` na wszystkich platformach
- **⚠️ BLOCKER: Dobór modeli embeddingowych dla Windows/macOS/Android/iOS**

---

## Faza 2: Memory Scoring & Decay 🟡 PRIORYTET ŚREDNI

**Cel:** Inteligentne rankingowanie wspomnień

### Nowe pola w modelu Memory:
```csharp
public int AccessCount { get; set; } = 0;
public DateTime LastAccessedAt { get; set; }
public float Importance { get; set; } = 0.5f;  // 0.0 - 1.0
public float Strength { get; set; } = 1.0f;    // Decay over time
```

### Formuła scoringu:
```
FinalScore = similarity * importance * recencyDecay * frequencyBoost
```

Gdzie:
- `recencyDecay = exp(-daysSinceAccess / halfLifeDays)`
- `frequencyBoost = log(1 + accessCount) / log(10)`

### Zadania:
- [ ] Dodać nowe pola do modelu `Memory`
- [ ] Implementować `CalculateMemoryScore()` w `MemoryService`
- [ ] Background job: obniżanie `Strength` nieużywanych wspomnień
- [ ] Automatyczna archiwizacja wspomnień z `Strength < 0.1`
- [ ] UI: Wizualizacja siły wspomnień (opacity/color)
- [ ] Ustawienie: konfiguracja half-life, minimalnego progu

---

## Faza 3: Episodic Memory 🟡 PRIORYTET ŚREDNI

**Cel:** Zapisywanie udanych interakcji jako przykładów (few-shot learning)

### Nowy model Episode:
```csharp
public class Episode
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    public string Situation { get; set; }      // Kontekst/pytanie użytkownika
    public string Approach { get; set; }       // Jak AI odpowiedziało
    public string Outcome { get; set; }        // Wynik/feedback użytkownika
    public float SuccessScore { get; set; }    // 0.0 - 1.0
    public byte[] Embedding { get; set; }      // Embedding sytuacji
    
    public DateTime CreatedAt { get; set; }
    public string Category { get; set; }       // np. "code", "explanation", "creative"
}
```

### Zadania:
- [ ] Utworzyć model `Episode` i `EpisodeService`
- [ ] Detekcja sukcesu: pozytywny feedback, brak poprawek, kontynuacja tematu
- [ ] Zapisywanie epizodów automatycznie lub przez użytkownika (👍/👎)
- [ ] Wyszukiwanie podobnych epizodów przy nowym pytaniu
- [ ] Wstrzykiwanie przykładów do promptu (few-shot)
- [ ] UI: Historia epizodów, możliwość edycji/usuwania

---

## Faza 4: Background Memory Processing 🟢 PRIORYTET NISKI

**Cel:** Ekstrakcja i konsolidacja w tle po rozmowie

### Nowy serwis BackgroundMemoryProcessor:
```csharp
public interface IBackgroundMemoryProcessor
{
    Task ProcessConversationAsync(Conversation conversation);
    Task ConsolidateMemoriesAsync();
    Task ResolveConflictsAsync();
}
```

### Zadania:
- [ ] Serwis działający po zakończeniu rozmowy (idle detection)
- [ ] Analiza całej rozmowy, nie tylko ostatnich wiadomości
- [ ] Konsolidacja duplikatów (merge similar memories)
- [ ] Konflikt resolution (nowsza informacja > starsza)
- [ ] Generowanie podsumowań długich rozmów
- [ ] Notification o nowych wspomnieniach

---

## Faza 5: Memory Tools dla LLM 🟢 PRIORYTET NISKI (zaawansowane)

**Cel:** Wzorzec MemGPT - LLM zarządza własną pamięcią przez narzędzia

### Narzędzia (Function Calling):
```json
{
  "tools": [
    {
      "name": "remember",
      "description": "Zapisz ważną informację o użytkowniku",
      "parameters": {
        "key": "string",
        "value": "string", 
        "importance": "number (0-1)"
      }
    },
    {
      "name": "recall",
      "description": "Wyszukaj wspomnienia pasujące do zapytania",
      "parameters": {
        "query": "string",
        "limit": "number"
      }
    },
    {
      "name": "forget",
      "description": "Usuń wspomnienie",
      "parameters": {
        "key": "string"
      }
    },
    {
      "name": "update_memory",
      "description": "Zaktualizuj istniejące wspomnienie",
      "parameters": {
        "key": "string",
        "new_value": "string"
      }
    }
  ]
}
```

### Zadania:
- [ ] Implementacja Memory Tools jako Function Calling
- [ ] Prompt engineering - kiedy używać narzędzi
- [ ] Sandbox/limity (max memories per conversation)
- [ ] Audit log zmian w pamięci
- [ ] UI: Podgląd akcji pamięciowych AI

---

## Priorytety i zależności

```
┌─────────────────────────────────────────────────────────┐
│                    BLOCKER                               │
│   Dobór modeli embeddingowych dla wszystkich platform   │
│   (Windows, macOS, Android, iOS)                        │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│              Faza 1: Vector Memory                       │
│              (wymaga embeddingów)                        │
└─────────────────────────────────────────────────────────┘
                          │
            ┌─────────────┴─────────────┐
            ▼                           ▼
┌───────────────────────┐   ┌───────────────────────┐
│ Faza 2: Memory Scoring│   │ Faza 3: Episodic Mem  │
│ (niezależne)          │   │ (wymaga embeddingów)  │
└───────────────────────┘   └───────────────────────┘
            │                           │
            └─────────────┬─────────────┘
                          ▼
┌─────────────────────────────────────────────────────────┐
│           Faza 4: Background Processing                  │
│           (wymaga Fazy 1-3)                              │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│           Faza 5: Memory Tools                           │
│           (wymaga wszystkich poprzednich)                │
└─────────────────────────────────────────────────────────┘
```

---

## Estymacja czasowa

| Faza | Effort | Czas (dni robocze) |
|------|--------|-------------------|
| Blocker: Modele embeddingowe | Średni | 2-3 |
| Faza 1: Vector Memory | Średni | 3-4 |
| Faza 2: Memory Scoring | Niski | 1-2 |
| Faza 3: Episodic Memory | Średni | 2-3 |
| Faza 4: Background Processing | Średni | 2-3 |
| Faza 5: Memory Tools | Wysoki | 4-5 |
| **RAZEM** | | **14-20 dni** |

---

## Następne kroki

1. **[TERAZ]** Rozwiązać blocker: dobór modeli embeddingowych
2. Implementacja Fazy 1
3. Testy jakościowe
4. Iteracja na podstawie wyników
