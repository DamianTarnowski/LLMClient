# Analiza Modeli Embeddingowych dla Cross-Platform MAUI

> Data: 2025-01-04
> Platformy docelowe: Windows, macOS, Android, iOS

## Podsumowanie wymagań

| Platforma | Runtime | Ograniczenia |
|-----------|---------|--------------|
| **Windows** | ONNX Runtime (pełny) | Brak istotnych |
| **macOS** | ONNX Runtime + CoreML | Brak istotnych |
| **Android** | ONNX Runtime Mobile + NNAPI | RAM ~2-4GB, storage |
| **iOS** | ONNX Runtime Mobile + CoreML | RAM ~2-4GB, storage |

---

## Kandydaci na modele embeddingowe

### 1. 🏆 EmbeddingGemma-300M (NOWOŚĆ 2025)

**Google DeepMind - najlepszy model on-device**

| Parametr | Wartość |
|----------|---------|
| Rozmiar | 308M params (~600MB, ~200MB quantized) |
| Wymiary | 768 (Matryoshka: 512, 256, 128) |
| Kontekst | 2048 tokenów |
| Języki | 100+ (w tym polski) |
| Inference | <15ms na EdgeTPU, ~50-100ms na CPU mobile |
| Format | ONNX, GGUF, LiteRT, MLX |

**Zalety:**
- ✅ Najlepszy w klasie <500M na MTEB/MMTEB
- ✅ Multilingualny (100+ języków)
- ✅ Matryoshka - możliwość redukcji wymiarów bez retrainingu
- ✅ Oficjalne wsparcie ONNX Runtime
- ✅ Quantization-Aware Training (QAT) - <200MB RAM
- ✅ Ten sam tokenizer co Gemma 3n

**Wady:**
- ⚠️ Nowszy model - mniej testowany w produkcji
- ⚠️ Wymaga specjalnych prefixów (`task: search result | query:`)

**Linki:**
- HuggingFace: `onnx-community/embeddinggemma-300m-ONNX`
- Ollama: `embeddinggemma`

---

### 2. all-MiniLM-L6-v2 (Klasyka)

**Sentence Transformers - najszybszy model**

| Parametr | Wartość |
|----------|---------|
| Rozmiar | 22M params (~90MB ONNX) |
| Wymiary | 384 |
| Kontekst | 256 tokenów |
| Języki | Głównie angielski |
| Inference | ~15ms/1K tokenów CPU |

**Zalety:**
- ✅ Bardzo mały i szybki
- ✅ Sprawdzony w produkcji
- ✅ Łatwy w integracji
- ✅ Idealny dla edge/mobile

**Wady:**
- ❌ Słaby dla polskiego i innych języków
- ❌ Krótki kontekst (256 tokenów)
- ❌ Niższa jakość retrieval (~78% vs 86% Top-5)

**Już mamy w projekcie:** `all-MiniLM-L6-v2.onnx` (90MB)

---

### 3. multilingual-e5-large (Obecnie używany)

**Intfloat - najlepszy multilingualny**

| Parametr | Wartość |
|----------|---------|
| Rozmiar | 560M params (~2.2GB ONNX) |
| Wymiary | 1024 |
| Kontekst | 512 tokenów |
| Języki | 100+ (świetny polski) |
| Inference | ~58ms na desktop CPU |

**Zalety:**
- ✅ Świetna jakość multilingualnych embeddingów
- ✅ Sprawdzony w naszych testach (PL-EN: 0.94 similarity)
- ✅ Dobry dla RAG i semantic search

**Wady:**
- ❌ Za duży na mobile (2.2GB!)
- ❌ Wolniejszy inference
- ❌ Wymaga natywnego tokenizera Rust

**Już mamy w projekcie:** `intfloat-e5-large-multilingual-v1/`

---

### 4. multilingual-e5-base

**Intfloat - mniejsza wersja e5**

| Parametr | Wartość |
|----------|---------|
| Rozmiar | 278M params (~1.1GB ONNX) |
| Wymiary | 768 |
| Kontekst | 512 tokenów |
| Języki | 100+ |
| Inference | ~30-40ms desktop |

**Zalety:**
- ✅ Dobra jakość multilingualnych embeddingów
- ✅ Mniejszy niż e5-large
- ✅ Ten sam tokenizer

**Wady:**
- ⚠️ Wciąż za duży na mobile (1.1GB)
- ⚠️ Nieco gorsza jakość niż e5-large

**Już mamy w projekcie:** `e5-base-multilingual.onnx` (1.1GB)

---

### 5. paraphrase-multilingual-MiniLM-L12-v2

**Sentence Transformers - kompromis**

| Parametr | Wartość |
|----------|---------|
| Rozmiar | 118M params (~470MB ONNX) |
| Wymiary | 384 |
| Kontekst | 256 tokenów |
| Języki | 50+ |
| Inference | ~25-35ms desktop |

**Zalety:**
- ✅ Multilingualny
- ✅ Średni rozmiar
- ✅ Szybki

**Wady:**
- ⚠️ Krótki kontekst
- ⚠️ Mniejsze wymiary (384)

**Już mamy w projekcie:** `paraphrase-multilingual-MiniLM-L12-v2.onnx` (470MB)

---

## Porównanie benchmarków

| Model | Rozmiar | Dims | MTEB Avg | Mobile-ready | Polski |
|-------|---------|------|----------|--------------|--------|
| **EmbeddingGemma-300M** | 200MB* | 768 | 65.2 | ✅ Tak | ✅ Tak |
| all-MiniLM-L6-v2 | 90MB | 384 | 56.3 | ✅ Tak | ❌ Słaby |
| multilingual-e5-large | 2.2GB | 1024 | 64.1 | ❌ Nie | ✅ Świetny |
| multilingual-e5-base | 1.1GB | 768 | 61.5 | ⚠️ Graniczny | ✅ Dobry |
| paraphrase-MiniLM-L12 | 470MB | 384 | 57.8 | ⚠️ Graniczny | ✅ OK |

*po kwantyzacji INT8

---

## Rekomendacja: Strategia "Tiered Models"

### Propozycja: Różne modele dla różnych platform

```
┌─────────────────────────────────────────────────────────────────┐
│                        DESKTOP                                   │
│              (Windows, macOS - pełna moc)                       │
│                                                                  │
│   Model: multilingual-e5-large (2.2GB)                          │
│   - Najlepsza jakość                                            │
│   - 1024 wymiarów                                                │
│   - Pełny kontekst 512 tokenów                                  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ Sync/Convert
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        MOBILE                                    │
│                   (Android, iOS)                                 │
│                                                                  │
│   Model: EmbeddingGemma-300M (200MB quantized)                  │
│   - Najlepszy on-device                                          │
│   - 768 wymiarów (lub 256 dla oszczędności)                     │
│   - Multilingualny                                               │
│   - <200MB RAM                                                   │
└─────────────────────────────────────────────────────────────────┘
```

### Problem: Różne wymiary embeddingów

Jeśli używamy różnych modeli na różnych platformach, embeddingi nie są kompatybilne:
- e5-large: 1024 dims
- EmbeddingGemma: 768 dims

**Rozwiązania:**

1. **Opcja A: Jeden model wszędzie (EmbeddingGemma)**
   - ✅ Spójność embeddingów
   - ✅ Sync między urządzeniami
   - ⚠️ Nieco gorsza jakość na desktop niż e5-large

2. **Opcja B: Dwa modele, re-embedding przy sync**
   - ✅ Najlepsza jakość na każdej platformie
   - ❌ Złożoność sync
   - ❌ Dodatkowe koszty obliczeniowe

3. **Opcja C: Adapter/Projection layer**
   - ✅ Mapowanie między przestrzeniami
   - ❌ Wymaga treningu adaptera
   - ❌ Potencjalna utrata jakości

### Moja rekomendacja: **Opcja A - EmbeddingGemma wszędzie**

**Argumenty:**
1. Prostota - jeden model, jedna przestrzeń embeddingów
2. EmbeddingGemma jest state-of-the-art dla swojego rozmiaru
3. Matryoshka pozwala na 768→256 redukcję gdy potrzebna oszczędność
4. Oficjalne wsparcie ONNX Runtime na wszystkich platformach
5. Multilingualny z dobrym wsparciem polskiego

---

## Plan testów jakościowych

### Testy do wykonania:

1. **Test cross-lingual similarity**
   - PL-EN: "Sztuczna inteligencja" vs "Artificial intelligence"
   - PL-DE: jak wyżej
   - Oczekiwany wynik: >0.85 similarity

2. **Test query-passage retrieval**
   - Pytania po polsku, dokumenty po polsku
   - Mierzyć Top-1, Top-5, Top-10 accuracy
   - Porównać z obecnym e5-large

3. **Test długich tekstów**
   - Dokumenty >512 tokenów
   - Jak model radzi sobie z truncation

4. **Test wydajności mobile**
   - Inference time na Android emulatorze
   - Memory usage
   - Battery impact

5. **Test Matryoshka dimensions**
   - Porównać jakość: 768 vs 512 vs 256 vs 128
   - Znaleźć sweet spot dla mobile

### Metryki:
- Cosine similarity dla known pairs
- Retrieval accuracy (Top-K)
- Inference latency (ms)
- Memory footprint (MB)
- Model loading time (ms)

---

## Następne kroki

1. [ ] **Pobrać EmbeddingGemma ONNX** i przetestować na Windows
2. [ ] **Napisać testy porównawcze** e5-large vs EmbeddingGemma
3. [ ] **Przetestować na Android emulatorze** - wydajność i pamięć
4. [ ] **Zdecydować o strategii** - jeden model vs tiered
5. [ ] **Zaimplementować IEmbeddingService** dla nowego modelu
6. [ ] **Dodać obsługę różnych platform** w MauiProgram.cs

---

## Obecne modele w projekcie

```
%LocalAppData%\User Name\com.companyname.llmclient\Data\models\
├── intfloat-e5-large-multilingual-v1\   # 2.2GB - Desktop
│   ├── model.onnx
│   ├── model.onnx_data
│   └── tokenizer.json
├── all-MiniLM-L6-v2.onnx                # 90MB - Mały
├── e5-base-multilingual.onnx            # 1.1GB - Średni
└── paraphrase-multilingual-MiniLM-L12-v2.onnx  # 470MB
```

**Do pobrania:**
- `onnx-community/embeddinggemma-300m-ONNX` - ~600MB (lub quantized ~200MB)
