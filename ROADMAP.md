# LLMClient Roadmap

> English version first, Polish below.

## Done (Local Model Services)
- ONNX Runtime GenAI local backend (Phi‑4‑mini‑instruct) with streaming and unified chat template
- SafeLocalModelWrapper guarding ILocalModelService with failure thresholds and cooldown
- Switchable local engines pattern (RobustLocalModelService, LlamaSharpLocalModelService) via DI and EngineSettings
- LocalModelDiagnosticService with periodic health checks and persisted diagnostics
- NetworkAwareDownloadService with auto‑resume and connectivity events
- AiService switching between cloud and local backends based on configuration
- UI integration in MainPageViewModel: MessagingCenter events (LocalModelLoaded/Unloaded, ModelsChanged), busy/overlay states, cloud picker lock while local is active
- OnboardingService optionally using local model when available

## Planned / In Progress
- Dynamic runtime updates for local generation parameters (temperature, repetition penalty) without reload
- Better error surfaces and user‑friendly recovery actions for local failures (with retry/backoff hints)
- More granular diagnostics: token/s throughput, RAM/VRAM snapshots, last N failures timeline
- Download manager enhancements: checksum verification, partial file repair, bandwidth limiter
- Unit/integration tests for SafeLocalModelWrapper and switching logic, plus ViewModel message flow tests
- Improved UX cues: distinct states for downloading vs loading vs generating; detailed progress percentages
- Unified model settings persistence across engines; schema for engine‑specific overrides
- Extend local model support matrix (additional ONNX GenAI models) and engine abstraction
- Telemetry toggles (local only) for opt‑in anonymized performance stats

---

# LLMClient Roadmap (Polski)

## Zrobione (Usługi modelu lokalnego)
- Lokalny backend ONNX Runtime GenAI (Phi‑4‑mini‑instruct) ze strumieniowaniem i ujednoliconym szablonem czatu
- SafeLocalModelWrapper zabezpieczający ILocalModelService (progi błędów, cooldown)
- Wzorzec przełączalnych silników lokalnych (RobustLocalModelService, LlamaSharpLocalModelService) przez DI i EngineSettings
- LocalModelDiagnosticService z okresowymi health‑checkami i zapisem diagnostyki
- NetworkAwareDownloadService z auto‑wznawianiem i zdarzeniami łączności
- AiService przełącza między chmurą a lokalnym backendem zależnie od konfiguracji
- Integracja w MainPageViewModel: zdarzenia MessagingCenter (LocalModelLoaded/Unloaded, ModelsChanged), stany busy/overlay, blokada wyboru chmurowego
- OnboardingService opcjonalnie korzysta z modelu lokalnego

## Planowane / W toku
- Dynamiczna zmiana parametrów generacji w runtime (temperature, repetition penalty) bez przeładowania
- Lepsza prezentacja błędów i akcje naprawcze dla użytkownika (retry/backoff)
- Bardziej szczegółowa diagnostyka: przepustowość tokenów/s, RAM/VRAM, oś czasu ostatnich N błędów
- Ulepszenia managera pobrań: weryfikacja checksum, naprawa częściowych plików, ogranicznik pasma
- Testy jednostkowe/integracyjne dla SafeLocalModelWrapper i logiki przełączania, oraz przepływu wiadomości w ViewModelach
- Ulepszone sygnały UX: rozróżnienie stanów pobieranie/ładowanie/generowanie; dokładniejsze procenty postępu
- Ujednolicone zapisy ustawień modeli między silnikami; schemat dla ustawień specyficznych per silnik
- Rozszerzenie wsparcia modeli lokalnych (dodatkowe modele ONNX GenAI) i warstwy abstrakcji silników
- Przełączniki telemetrii (lokalnej) dla opcjonalnych, zanonimizowanych statystyk wydajności
