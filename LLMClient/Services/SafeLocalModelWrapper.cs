using Microsoft.Extensions.Logging;
using LLMClient.Models;

namespace LLMClient.Services
{
    /// <summary>
    /// Wrapper that ensures the app never crashes due to local model issues
    /// Provides graceful degradation and fallback mechanisms
    /// </summary>
    public class SafeLocalModelWrapper : ILocalModelService
    {
        private readonly RobustLocalModelService _innerService;
        private readonly ILogger<SafeLocalModelWrapper> _logger;
        private readonly IErrorHandlingService? _errorHandling;
        private bool _isDisabled = false;
        private DateTime? _lastFailureTime;
        private int _consecutiveFailures = 0;
        private const int MAX_CONSECUTIVE_FAILURES = 3;
        private const int COOLDOWN_MINUTES = 30;

        public SafeLocalModelWrapper(
            ILogger<SafeLocalModelWrapper> logger,
            ILogger<RobustLocalModelService> innerLogger,
            IErrorHandlingService? errorHandling = null,
            DatabaseService? databaseService = null)
        {
            _logger = logger;
            _errorHandling = errorHandling;
            _innerService = new RobustLocalModelService(innerLogger, errorHandling, databaseService);
            
            // Subscribe to inner service events
            _innerService.StateChanged += OnInnerStateChanged;
            _innerService.DownloadProgress += OnInnerDownloadProgress;
            _innerService.ErrorOccurred += OnInnerErrorOccurred;
        }

        public LocalModelState State => _isDisabled ? LocalModelState.Error : _innerService.State;
        public bool IsLoaded => !_isDisabled && _innerService.IsLoaded;
        public bool IsDownloading => !_isDisabled && _innerService.IsDownloading;

        public event Action<LocalModelState>? StateChanged;
        public event Action<double>? DownloadProgress;
        public event Action<string>? ErrorOccurred;

        private void OnInnerStateChanged(LocalModelState state)
        {
            if (!_isDisabled)
            {
                StateChanged?.Invoke(state);
                
                // Reset failure counter on successful state changes
                if (state == LocalModelState.Loaded || state == LocalModelState.Downloaded)
                {
                    _consecutiveFailures = 0;
                    _lastFailureTime = null;
                    _logger.LogInformation("Local model recovered successfully");
                }
            }
        }

        private void OnInnerDownloadProgress(double progress)
        {
            if (!_isDisabled)
            {
                DownloadProgress?.Invoke(progress);
            }
        }

        private void OnInnerErrorOccurred(string error)
        {
            HandleFailure($"Inner service error: {error}");
        }

        private void HandleFailure(string error)
        {
            _consecutiveFailures++;
            _lastFailureTime = DateTime.UtcNow;
            
            _logger.LogWarning($"Local model failure #{_consecutiveFailures}: {error}");
            
            // Disable if too many consecutive failures
            if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
            {
                _isDisabled = true;
                _logger.LogError($"Local model disabled after {MAX_CONSECUTIVE_FAILURES} consecutive failures");
                
                var safeError = "Local model temporarily unavailable. App functionality not affected.";
                ErrorOccurred?.Invoke(safeError);
                StateChanged?.Invoke(LocalModelState.Error);
            }
            else
            {
                ErrorOccurred?.Invoke(error);
            }
        }

        private bool ShouldAttemptOperation()
        {
            // Check if in cooldown period
            if (_lastFailureTime.HasValue && 
                DateTime.UtcNow - _lastFailureTime.Value < TimeSpan.FromMinutes(COOLDOWN_MINUTES))
            {
                return false;
            }

            // Re-enable if cooldown expired
            if (_isDisabled && _lastFailureTime.HasValue && 
                DateTime.UtcNow - _lastFailureTime.Value >= TimeSpan.FromMinutes(COOLDOWN_MINUTES))
            {
                _isDisabled = false;
                _consecutiveFailures = 0;
                _logger.LogInformation("Local model re-enabled after cooldown period");
            }

            return !_isDisabled;
        }

        private async Task<T> ExecuteSafelyAsync<T>(Func<Task<T>> operation, T fallbackValue, string operationName)
        {
            if (!ShouldAttemptOperation())
            {
                _logger.LogDebug($"Skipping {operationName} - service disabled or in cooldown");
                return fallbackValue;
            }

            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Safe execution failed for {operationName}");
                HandleFailure($"{operationName} failed: {ex.Message}");
                return fallbackValue;
            }
        }

        private async Task<bool> ExecuteSafelyAsync(Func<Task<bool>> operation, string operationName)
        {
            return await ExecuteSafelyAsync(operation, false, operationName);
        }

        private async Task ExecuteSafelyAsync(Func<Task> operation, string operationName)
        {
            if (!ShouldAttemptOperation())
            {
                _logger.LogDebug($"Skipping {operationName} - service disabled or in cooldown");
                return;
            }

            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Safe execution failed for {operationName}");
                HandleFailure($"{operationName} failed: {ex.Message}");
            }
        }

        // Implement ILocalModelService with safe wrappers
        public async Task<LocalModelInfo> GetModelInfoAsync()
        {
            return await ExecuteSafelyAsync(
                () => _innerService.GetModelInfoAsync(),
                new LocalModelInfo { DisplayName = "Local Model (Unavailable)" },
                "GetModelInfo");
        }

        public async Task<bool> IsModelDownloadedAsync()
        {
            return await ExecuteSafelyAsync(
                () => _innerService.IsModelDownloadedAsync(),
                "IsModelDownloaded");
        }

        public async Task<bool> DownloadModelAsync(IProgress<double>? progress = null)
        {
            if (!ShouldAttemptOperation())
            {
                progress?.Report(0);
                return false;
            }

            // Wrap progress to handle failures gracefully
            var safeProgress = progress != null ? new Progress<double>(p =>
            {
                try
                {
                    progress.Report(p);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Progress reporting failed");
                }
            }) : null;

            return await ExecuteSafelyAsync(
                () => _innerService.DownloadModelAsync(safeProgress),
                "DownloadModel");
        }

        public async Task<bool> LoadModelAsync()
        {
            return await ExecuteSafelyAsync(
                () => _innerService.LoadModelAsync(),
                "LoadModel");
        }

        public async Task UnloadModelAsync()
        {
            await ExecuteSafelyAsync(
                () => _innerService.UnloadModelAsync(),
                "UnloadModel");
        }

        public async Task<bool> DeleteModelAsync()
        {
            return await ExecuteSafelyAsync(
                () => _innerService.DeleteModelAsync(),
                "DeleteModel");
        }

        public async Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (!ShouldAttemptOperation() || !IsLoaded)
            {
                throw new InvalidOperationException("Local model is not available. Please use a cloud-based model instead.");
            }

            try
            {
                return await _innerService.GenerateResponseAsync(prompt, cancellationToken);
            }
            catch (Exception ex)
            {
                HandleFailure($"GenerateResponse failed: {ex.Message}");
                throw new InvalidOperationException("Local model response generation failed. Please try a cloud model.", ex);
            }
        }

        public async Task<string> GenerateResponseAsync(List<Message> conversationHistory, string newMessage, CancellationToken cancellationToken = default)
        {
            if (!ShouldAttemptOperation() || !IsLoaded)
            {
                throw new InvalidOperationException("Local model is not available. Please use a cloud-based model instead.");
            }

            try
            {
                return await _innerService.GenerateResponseAsync(conversationHistory, newMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                HandleFailure($"GenerateResponse with history failed: {ex.Message}");
                throw new InvalidOperationException("Local model response generation failed. Please try a cloud model.", ex);
            }
        }

        public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(string prompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!ShouldAttemptOperation() || !IsLoaded)
            {
                yield break;
            }

            IAsyncEnumerable<string>? stream = null;
            
            try
            {
                stream = _innerService.GenerateStreamingResponseAsync(prompt, cancellationToken);
            }
            catch (Exception ex)
            {
                HandleFailure($"GenerateStreamingResponse failed: {ex.Message}");
                yield break;
            }

            if (stream != null)
            {
                await foreach (var chunk in stream)
                {
                    yield return chunk;
                }
            }
        }

        public async Task<string> GenerateOnboardingResponseAsync(string userLanguage, string topic = "general", CancellationToken cancellationToken = default)
        {
            // For onboarding, we provide fallbacks even if model fails
            try
            {
                if (ShouldAttemptOperation() && IsLoaded)
                {
                    return await _innerService.GenerateOnboardingResponseAsync(userLanguage, topic, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Onboarding AI generation failed, using fallback");
                // Don't call HandleFailure here - we have a fallback
            }

            // Fallback to static responses
            return GetFallbackOnboardingResponse(userLanguage, topic);
        }

        public async Task<string> GenerateHelpResponseAsync(string question, string userLanguage, CancellationToken cancellationToken = default)
        {
            try
            {
                if (ShouldAttemptOperation() && IsLoaded)
                {
                    return await _innerService.GenerateHelpResponseAsync(question, userLanguage, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Help AI generation failed, using fallback");
                // Don't call HandleFailure here - we have a fallback
            }

            // Fallback to static response
            return GetFallbackHelpResponse(question, userLanguage);
        }

        private string GetFallbackOnboardingResponse(string userLanguage, string topic)
        {
            return userLanguage.ToLower() switch
            {
                "pl" or "pl-pl" => "Witaj w LLMClient! 👋 To zaawansowana aplikacja AI z systemem pamięci, wyszukiwaniem semantycznym i obsługą 13 języków. Eksploruj funkcje poprzez menu górne.",
                "de" or "de-de" => "Willkommen bei LLMClient! 👋 Das ist eine fortgeschrittene AI-App mit Gedächtnissystem, semantischer Suche und Unterstützung für 13 Sprachen. Erkunden Sie die Funktionen über das obere Menü.",
                "es" or "es-es" => "¡Bienvenido a LLMClient! 👋 Esta es una app de IA avanzada con sistema de memoria, búsqueda semántica y soporte para 13 idiomas. Explora las funciones a través del menú superior.",
                "fr" or "fr-fr" => "Bienvenue dans LLMClient ! 👋 C'est une app IA avancée avec système de mémoire, recherche sémantique et support de 13 langues. Explorez les fonctions via le menu supérieur.",
                _ => "Welcome to LLMClient! 👋 This is an advanced AI app with memory system, semantic search, and 13-language support. Explore features through the top menu."
            };
        }

        private string GetFallbackHelpResponse(string question, string userLanguage)
        {
            return userLanguage.ToLower() switch
            {
                "pl" or "pl-pl" => $"Pytanie: {question}\n\nLLMClient oferuje rozmowy z AI, system pamięci, wyszukiwanie semantyczne i eksport konwersacji. Sprawdź menu ustawień aby skonfigurować modele AI.",
                "de" or "de-de" => $"Frage: {question}\n\nLLMClient bietet AI-Gespräche, Gedächtnissystem, semantische Suche und Gesprächsexport. Überprüfen Sie das Einstellungsmenü, um AI-Modelle zu konfigurieren.",
                "es" or "es-es" => $"Pregunta: {question}\n\nLLMClient ofrece conversaciones con IA, sistema de memoria, búsqueda semántica y exportación de conversaciones. Revisa el menú de configuración para configurar modelos de IA.",
                "fr" or "fr-fr" => $"Question : {question}\n\nLLMClient offre des conversations IA, un système de mémoire, une recherche sémantique et l'export de conversations. Vérifiez le menu des paramètres pour configurer les modèles IA.",
                _ => $"Question: {question}\n\nLLMClient offers AI conversations, memory system, semantic search, and conversation export. Check the settings menu to configure AI models."
            };
        }

        public void Dispose()
        {
            try
            {
                _innerService?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing SafeLocalModelWrapper");
            }
        }
    }
}