using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LLMClient.Models;

namespace LLMClient.Services
{
    /// <summary>
    /// Allows switching between different local model engine implementations at runtime.
    /// Delegates ILocalModelService calls to the currently selected engine and rewires events.
    /// Supports: ONNX GenAI (CPU), LLamaSharp (llama.cpp), MediaPipe GenAI (Google AI Edge)
    /// </summary>
    public class SwitchableLocalModelService : ILocalModelService, IDisposable
    {
        private readonly ILogger<SwitchableLocalModelService> _logger;
        private readonly Func<ILocalModelService> _onnxFactory;
        private readonly Func<ILocalModelService> _llamaFactory;
        private readonly Func<ILocalModelService?> _mediaPipeFactory;

        private ILocalModelService? _onnxService;
        private ILocalModelService? _llamaService;
        private ILocalModelService? _mediaPipeService;

        private readonly object _swapLock = new();

        private ILocalModelService _current;
        private EngineType _currentEngine;

        public ILocalModelService CurrentService => _current;
        public EngineType CurrentEngine => _currentEngine;

        public SwitchableLocalModelService(
            ILogger<SwitchableLocalModelService> logger,
            Func<ILocalModelService> onnxFactory,
            Func<ILocalModelService> llamaFactory,
            Func<ILocalModelService?>? mediaPipeFactory = null)
        {
            _logger = logger;
            _onnxFactory = onnxFactory;
            _llamaFactory = llamaFactory;
            _mediaPipeFactory = mediaPipeFactory ?? (() => null);

            _currentEngine = EngineSettings.LoadSelectedEngine();
            _current = ChooseService(_currentEngine);
            WireEvents(_current);

            // Ensure UI reflects current engine state immediately
            SafeRaiseStateChanged(_current.State);

            // Subscribe to engine changes for runtime hot-swap
            EngineSettings.EngineChanged += OnEngineChanged;
        }

        private ILocalModelService GetOnnxService()
        {
            return _onnxService ??= _onnxFactory();
        }

        private ILocalModelService GetLlamaService()
        {
            return _llamaService ??= _llamaFactory();
        }

        private ILocalModelService? GetMediaPipeService()
        {
            return _mediaPipeService ??= _mediaPipeFactory();
        }

        private ILocalModelService ChooseService(EngineType engine)
        {
            switch (engine)
            {
                case EngineType.LLamaSharp:
                    return GetLlamaService();
                case EngineType.MediaPipeGenAI:
                    var mp = GetMediaPipeService();
                    if (mp != null) return mp;
                    _logger.LogWarning("[Switchable] MediaPipe niedostępny, fallback do ONNX");
                    return GetOnnxService();
                default:
                    return GetOnnxService();
            }
        }

        public LocalModelState State => _current.State;
        public bool IsLoaded => _current.IsLoaded;
        public bool IsDownloading => _current.IsDownloading;

        public event Action<LocalModelState>? StateChanged;
        public event Action<double>? DownloadProgress;
        public event Action<string>? ErrorOccurred;

        private void WireEvents(ILocalModelService svc)
        {
            svc.StateChanged += OnInnerStateChanged;
            svc.DownloadProgress += OnInnerDownloadProgress;
            svc.ErrorOccurred += OnInnerErrorOccurred;
        }

        private void UnwireEvents(ILocalModelService svc)
        {
            svc.StateChanged -= OnInnerStateChanged;
            svc.DownloadProgress -= OnInnerDownloadProgress;
            svc.ErrorOccurred -= OnInnerErrorOccurred;
        }

        private void OnInnerStateChanged(LocalModelState s) => SafeRaiseStateChanged(s);
        private void OnInnerDownloadProgress(double p) => DownloadProgress?.Invoke(p);
        private void OnInnerErrorOccurred(string e) => ErrorOccurred?.Invoke(e);

        private void SafeRaiseStateChanged(LocalModelState s)
        {
            try { StateChanged?.Invoke(s); }
            catch (Exception ex) { _logger.LogWarning(ex, "[Switchable] Błąd podczas propagacji zdarzenia StateChanged"); }
        }

        private void OnEngineChanged(EngineType newEngine)
        {
            // Fire-and-forget to avoid blocking the setter or UI thread
            _ = SwitchEngineAsync(newEngine);
        }

        private async Task SwitchEngineAsync(EngineType newEngine)
        {
            ILocalModelService oldService;
            ILocalModelService newService;

            lock (_swapLock)
            {
                if (newEngine == _currentEngine)
                {
                    return;
                }

                oldService = _current;
                newService = ChooseService(newEngine);

                try
                {
                    UnwireEvents(oldService);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Switchable] Błąd podczas odpinania zdarzeń starego serwisu");
                }

                _current = newService;
                _currentEngine = newEngine;

                WireEvents(_current);
            }

            _logger.LogInformation("[Switchable] Przełączono silnik lokalny na: {Engine}", _currentEngine);

            // Propagate current state of new engine to update UI immediately
            SafeRaiseStateChanged(_current.State);

            // Try to unload old engine to free resources
            try
            {
                if (oldService.IsLoaded)
                {
                    await oldService.UnloadModelAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Switchable] Błąd podczas zwalniania zasobów poprzedniego silnika");
            }
        }

        // Delegations
        public Task<LocalModelInfo> GetModelInfoAsync() => _current.GetModelInfoAsync();
        public Task<bool> IsModelDownloadedAsync() => _current.IsModelDownloadedAsync();
        public Task<bool> DownloadModelAsync(IProgress<double>? progress = null) => _current.DownloadModelAsync(progress);
        public Task<bool> LoadModelAsync() => _current.LoadModelAsync();
        public Task UnloadModelAsync() => _current.UnloadModelAsync();
        public Task<bool> DeleteModelAsync() => _current.DeleteModelAsync();
        public Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default) => _current.GenerateResponseAsync(prompt, cancellationToken);
        public Task<string> GenerateResponseAsync(List<Message> conversationHistory, string newMessage, CancellationToken cancellationToken = default) => _current.GenerateResponseAsync(conversationHistory, newMessage, cancellationToken);
        public IAsyncEnumerable<string> GenerateStreamingResponseAsync(string prompt, CancellationToken cancellationToken = default) => _current.GenerateStreamingResponseAsync(prompt, cancellationToken);
        public Task<string> GenerateOnboardingResponseAsync(string userLanguage, string topic = "general", CancellationToken cancellationToken = default) => _current.GenerateOnboardingResponseAsync(userLanguage, topic, cancellationToken);
        public Task<string> GenerateHelpResponseAsync(string question, string userLanguage, CancellationToken cancellationToken = default) => _current.GenerateHelpResponseAsync(question, userLanguage, cancellationToken);

        public void Dispose()
        {
            try
            {
                EngineSettings.EngineChanged -= OnEngineChanged;
                // Do not dispose inner services here; DI or app lifetime handles them
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Switchable] Błąd podczas Dispose");
            }
        }
    }
}
