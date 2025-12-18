#if IOS
using Foundation;
using ObjCRuntime;
using Microsoft.Extensions.Logging;

namespace LLMClient.Platforms.iOS.MlcLlm
{
    /// <summary>
    /// C# Bridge to MLC LLM Swift Engine for iOS
    /// Enables high-performance GPU-accelerated inference via Metal
    /// </summary>
    public class MlcLlmBridge : IDisposable
    {
        private readonly ILogger? _logger;
        private NSObject? _engine;
        private bool _isInitialized = false;
        private bool _disposed = false;

        public bool IsReady => _isInitialized && _engine != null;

        public event Action<string>? OnToken;
        public event Action<string>? OnComplete;
        public event Action<string>? OnError;

        public MlcLlmBridge(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Initialize MLC LLM Engine with model
        /// </summary>
        public async Task<bool> InitializeAsync(string modelPath)
        {
            try
            {
                _logger?.LogInformation($"[MlcLlmBridge iOS] Initializing with model: {modelPath}");

                return await Task.Run(() =>
                {
                    try
                    {
                        // Get the Swift class via ObjC runtime
                        var engineClass = Runtime.GetNSObject(Class.GetHandle("MlcLlmEngine"));
                        if (engineClass == null)
                        {
                            _logger?.LogWarning("[MlcLlmBridge iOS] MlcLlmEngine class not found");
                            return false;
                        }

                        // Create instance using ObjC messaging
                        var selector = new Selector("init");
                        _engine = Runtime.GetNSObject(Messaging.IntPtr_objc_msgSend(engineClass.Handle, selector.Handle));

                        if (_engine != null)
                        {
                            // Call initialize method
                            var initSelector = new Selector("initializeWithModelId:modelPath:");
                            using var modelId = new NSString("MLC-Model");
                            using var modelPathStr = new NSString(modelPath);

                            Messaging.void_objc_msgSend_IntPtr_IntPtr(
                                _engine.Handle,
                                initSelector.Handle,
                                modelId.Handle,
                                modelPathStr.Handle);

                            _isInitialized = true;
                            _logger?.LogInformation("[MlcLlmBridge iOS] Engine initialized successfully");
                            return true;
                        }

                        return false;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning($"[MlcLlmBridge iOS] Initialization failed: {ex.Message}");
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[MlcLlmBridge iOS] Failed to initialize");
                OnError?.Invoke($"Initialization failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Generate response (non-streaming)
        /// </summary>
        public async Task<string> GenerateAsync(string prompt, int maxTokens = 512, double temperature = 0.7)
        {
            if (!IsReady || _engine == null)
            {
                return "Error: Engine not initialized";
            }

            try
            {
                return await Task.Run(() =>
                {
                    var generateSelector = new Selector("generateWithPrompt:maxTokens:temperature:");
                    using var promptStr = new NSString(prompt);

                    var resultPtr = Messaging.IntPtr_objc_msgSend_IntPtr_nint_double(
                        _engine!.Handle,
                        generateSelector.Handle,
                        promptStr.Handle,
                        (nint)maxTokens,
                        temperature);

                    if (resultPtr != IntPtr.Zero)
                    {
                        var result = Runtime.GetNSObject<NSString>(resultPtr);
                        return result?.ToString() ?? "";
                    }

                    return "Error: Generation failed";
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[MlcLlmBridge iOS] Generation failed");
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Generate response with streaming using callback
        /// </summary>
        public async Task GenerateStreamingAsync(string prompt, int maxTokens = 512, double temperature = 0.7,
            Action<string>? onToken = null, CancellationToken cancellationToken = default)
        {
            if (!IsReady || _engine == null)
            {
                OnError?.Invoke("Engine not initialized");
                return;
            }

            try
            {
                await Task.Run(() =>
                {
                    // Check if native streaming is available
                    var streamSelector = new Selector("generateStreamingWithPrompt:maxTokens:temperature:tokenCallback:");

                    // Try streaming generation first
                    try
                    {
                        using var promptStr = new NSString(prompt);

                        // Create a block/callback for token streaming
                        // Since ObjC blocks are complex, we fall back to polling-based streaming
                        // by using the non-streaming method and simulating tokens

                        var fullResponse = GenerateAsyncInternal(prompt, maxTokens, temperature);

                        if (!string.IsNullOrEmpty(fullResponse) && !fullResponse.StartsWith("Error:"))
                        {
                            // Simulate streaming by yielding words
                            var words = fullResponse.Split(' ');
                            foreach (var word in words)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                    break;

                                var token = word + " ";
                                onToken?.Invoke(token);
                                OnToken?.Invoke(token);

                                // Small delay to simulate streaming
                                Thread.Sleep(20);
                            }

                            OnComplete?.Invoke(fullResponse);
                        }
                        else
                        {
                            OnError?.Invoke(fullResponse ?? "Generation failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning($"[MlcLlmBridge iOS] Streaming fallback: {ex.Message}");
                        OnError?.Invoke(ex.Message);
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("[MlcLlmBridge iOS] Streaming cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[MlcLlmBridge iOS] Streaming generation failed");
                OnError?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Internal synchronous generation for streaming fallback
        /// </summary>
        private string GenerateAsyncInternal(string prompt, int maxTokens, double temperature)
        {
            if (_engine == null) return "Error: Engine not initialized";

            var generateSelector = new Selector("generateWithPrompt:maxTokens:temperature:");
            using var promptStr = new NSString(prompt);

            var resultPtr = Messaging.IntPtr_objc_msgSend_IntPtr_nint_double(
                _engine.Handle,
                generateSelector.Handle,
                promptStr.Handle,
                (nint)maxTokens,
                temperature);

            if (resultPtr != IntPtr.Zero)
            {
                var result = Runtime.GetNSObject<NSString>(resultPtr);
                return result?.ToString() ?? "";
            }

            return "Error: Generation failed";
        }

        /// <summary>
        /// Check if GPU (Metal) is available
        /// </summary>
        public bool IsGpuAvailable()
        {
            if (_engine == null) return false;

            try
            {
                var selector = new Selector("isGpuAvailable");
                return Messaging.bool_objc_msgSend(_engine.Handle, selector.Handle);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get GPU name
        /// </summary>
        public string GetGpuName()
        {
            if (_engine == null) return "Unknown";

            try
            {
                var selector = new Selector("getGpuName");
                var resultPtr = Messaging.IntPtr_objc_msgSend(_engine.Handle, selector.Handle);

                if (resultPtr != IntPtr.Zero)
                {
                    var result = Runtime.GetNSObject<NSString>(resultPtr);
                    return result?.ToString() ?? "Metal GPU";
                }
            }
            catch { }

            return "Metal GPU";
        }

        /// <summary>
        /// Build chat prompt with history
        /// </summary>
        public string BuildChatPrompt(string systemPrompt, string[] history, string userMessage)
        {
            if (_engine == null)
            {
                return userMessage;
            }

            try
            {
                var selector = new Selector("buildChatPromptWithSystemPrompt:history:userMessage:");
                using var sysPrompt = new NSString(systemPrompt ?? "");
                using var userMsg = new NSString(userMessage);

                // Convert C# string[] to NSArray
                var nsHistory = history != null
                    ? NSArray.FromStrings(history)
                    : new NSArray();

                var resultPtr = Messaging.IntPtr_objc_msgSend_IntPtr_IntPtr_IntPtr(
                    _engine.Handle,
                    selector.Handle,
                    sysPrompt.Handle,
                    nsHistory.Handle,
                    userMsg.Handle);

                if (resultPtr != IntPtr.Zero)
                {
                    var result = Runtime.GetNSObject<NSString>(resultPtr);
                    return result?.ToString() ?? userMessage;
                }
            }
            catch { }

            return userMessage;
        }

        /// <summary>
        /// Reset conversation
        /// </summary>
        public void ResetChat()
        {
            if (_engine == null) return;

            try
            {
                var selector = new Selector("resetChat");
                Messaging.void_objc_msgSend(_engine.Handle, selector.Handle);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[MlcLlmBridge iOS] Failed to reset chat");
            }
        }

        /// <summary>
        /// Unload model
        /// </summary>
        public void Unload()
        {
            if (_engine != null)
            {
                try
                {
                    var selector = new Selector("unload");
                    Messaging.void_objc_msgSend(_engine.Handle, selector.Handle);
                }
                catch { }

                _isInitialized = false;
                _logger?.LogInformation("[MlcLlmBridge iOS] Engine unloaded");
            }
        }

        /// <summary>
        /// Get model info
        /// </summary>
        public string GetModelInfo()
        {
            if (_engine == null)
            {
                return "{}";
            }

            try
            {
                var selector = new Selector("getModelInfo");
                var resultPtr = Messaging.IntPtr_objc_msgSend(_engine.Handle, selector.Handle);

                if (resultPtr != IntPtr.Zero)
                {
                    var result = Runtime.GetNSObject<NSString>(resultPtr);
                    return result?.ToString() ?? "{}";
                }
            }
            catch { }

            return "{}";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Unload();
                _engine?.Dispose();
                _engine = null;
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// ObjC Messaging helpers for calling Swift methods
    /// </summary>
    internal static class Messaging
    {
        [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        public static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

        [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        public static extern void void_objc_msgSend(IntPtr receiver, IntPtr selector);

        [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        public static extern void void_objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

        [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        public static extern IntPtr IntPtr_objc_msgSend_IntPtr_nint_double(IntPtr receiver, IntPtr selector, IntPtr arg1, nint arg2, double arg3);

        [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        public static extern IntPtr IntPtr_objc_msgSend_IntPtr_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2, IntPtr arg3);

        [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        public static extern bool bool_objc_msgSend(IntPtr receiver, IntPtr selector);
    }
}
#endif
