#if ANDROID
using Android.Content;
using Android.Runtime;
using Java.Lang;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace LLMClient.Platforms.Android.MlcLlm
{
    /// <summary>
    /// C# Bridge to MLC LLM TVM Runtime for Android
    /// Enables high-performance GPU-accelerated inference via OpenCL
    ///
    /// Supported models (precompiled kernels in libtvm4j_runtime_packed.so):
    /// - Qwen2.5-0.5B-Instruct-q4f16_1-MLC (model_lib: qwen2_q4f16_1_dbc9845947d563a3c13bf93ebf315c83)
    /// - Qwen2.5-1.5B-Instruct-q4f16_1-MLC (model_lib: qwen2_q4f16_1_2e221f430380225c03990ad24c3d030e)
    /// - Qwen2.5-3B-Instruct-q4f16_1-MLC (model_lib: qwen2_q4f16_1_58f9491506e358f89ec20dac03cfe80d)
    /// - Phi-3.5-mini-instruct-q4f16_0-MLC (model_lib: phi3_q4f16_0_7e3edeb1a479d33c19bf5d3a2077d0b5)
    /// </summary>
    public class MlcLlmBridge : Java.Lang.Object, IDisposable
    {
        private readonly ILogger? _logger;
        private bool _isLibraryLoaded = false;
        private bool _isInitialized = false;
        private bool _disposed = false;
        private string? _currentModelPath;
        private string? _currentModelLib;
        private string? _lastJavaEngineError;

        // Java wrapper object for MLC Engine
        private Java.Lang.Object? _mlcEngine;
        private Java.Lang.Class? _mlcEngineClass;

        // Token streaming buffer
        private readonly ConcurrentQueue<string> _tokenBuffer = new();
        private volatile bool _isGenerating = false;

        // Model library mappings (from mlc-app-config.json)
        private static readonly Dictionary<string, string> ModelLibMappings = new()
        {
            { "Qwen3-1.7B", "Qwen3-1.7B-q4f16_1" },
            { "Qwen3-1.7B-q4f16_1", "Qwen3-1.7B-q4f16_1" },
            { "Qwen2.5-1.5B", "qwen2_q4f16_1_2e221f430380225c03990ad24c3d030e" },
            { "Phi-3.5-mini", "phi3_q4f16_0_7e3edeb1a479d33c19bf5d3a2077d0b5" },
            { "Phi-3.5-mini-instruct", "phi3_q4f16_0_7e3edeb1a479d33c19bf5d3a2077d0b5" },
            { "Phi-3.5-mini-instruct-q4f16_0-MLC", "phi3_q4f16_0_7e3edeb1a479d33c19bf5d3a2077d0b5" },
            { "Gemma-2-2B", "gemma2_q4f16_1_5cc7dbd3ae3d1040984d9720b2d7b7d4" },
            { "Llama-3.2-3B", "llama_q4f16_0_2d32572d8a4ab2af20a1f587ef6c8c63" },
            { "Mistral-7B", "mistral_q4f16_1_c2cba77a6def4dd52f7e20b5d8576ab5" },
            { "Phi-2", "phi2_q4f16_1_" },
            { "phi-2", "phi2_q4f16_1_" },
            { "phi2", "phi2_q4f16_1_" },
            { "phi-2-q4f16_1", "phi2_q4f16_1_" },
            { "phi-2-q4f16_1-MLC", "phi2_q4f16_1_" }
        };

        public bool IsReady => _isLibraryLoaded && _isInitialized;
        public bool IsLibraryLoaded => _isLibraryLoaded;

        public event Action<string>? OnToken;
        public event Action<string>? OnComplete;
        public event Action<string>? OnError;

        public MlcLlmBridge(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Load the TVM native library
        /// </summary>
        public bool LoadNativeLibrary()
        {
            if (_isLibraryLoaded) return true;

            try
            {
                _logger?.LogInformation("[MlcLlmBridge] Loading libtvm4j_runtime_packed.so...");

                // Try multiple approaches to load the library
                bool loaded = false;
                string? lastError = null;

                // Approach 1: Standard loadLibrary (works for most cases)
                try
                {
                    Java.Lang.JavaSystem.LoadLibrary("tvm4j_runtime_packed");
                    loaded = true;
                    _logger?.LogInformation("[MlcLlmBridge] Loaded via System.loadLibrary");
                }
                catch (Java.Lang.UnsatisfiedLinkError ex1)
                {
                    lastError = ex1.Message;
                    _logger?.LogWarning($"[MlcLlmBridge] loadLibrary failed: {ex1.Message}");
                }

                // Approach 2: Try loading from native library dir
                if (!loaded)
                {
                    try
                    {
                        var context = global::Android.App.Application.Context;
                        var nativeLibDir = context.ApplicationInfo?.NativeLibraryDir;
                        if (!string.IsNullOrEmpty(nativeLibDir))
                        {
                            var libPath = Path.Combine(nativeLibDir, "libtvm4j_runtime_packed.so");
                            _logger?.LogInformation($"[MlcLlmBridge] Trying full path: {libPath}");
                            
                            if (File.Exists(libPath))
                            {
                                Java.Lang.JavaSystem.Load(libPath);
                                loaded = true;
                                _logger?.LogInformation("[MlcLlmBridge] Loaded via System.load with full path");
                            }
                            else
                            {
                                _logger?.LogWarning($"[MlcLlmBridge] Library not found at: {libPath}");
                                
                                // List what's in the native lib directory
                                var files = Directory.GetFiles(nativeLibDir, "*.so");
                                _logger?.LogInformation($"[MlcLlmBridge] Available .so files in {nativeLibDir}:");
                                foreach (var f in files)
                                {
                                    _logger?.LogInformation($"[MlcLlmBridge]   - {Path.GetFileName(f)}");
                                }
                            }
                        }
                    }
                    catch (System.Exception ex2)
                    {
                        lastError = ex2.Message;
                        _logger?.LogWarning($"[MlcLlmBridge] Full path load failed: {ex2.Message}");
                    }
                }

                if (loaded)
                {
                    _isLibraryLoaded = true;
                    _logger?.LogInformation("[MlcLlmBridge] Native library loaded successfully!");
                    return true;
                }
                else
                {
                    OnError?.Invoke($"Failed to load MLC LLM library: {lastError}");
                    return false;
                }
            }
            catch (Java.Lang.UnsatisfiedLinkError ex)
            {
                _logger?.LogError($"[MlcLlmBridge] Failed to load native library: {ex.Message}");
                OnError?.Invoke($"Failed to load MLC LLM library: {ex.Message}");
                return false;
            }
            catch (System.Exception ex)
            {
                _logger?.LogError(ex, "[MlcLlmBridge] Exception loading native library");
                OnError?.Invoke($"Exception loading library: {ex.Message}");
                return false;
            }
        }

        private string ResolveModelLibForEngine(string modelLib)
        {
            try
            {
                // If it already looks like a path/URI, do not modify.
                if (modelLib.Contains("://", StringComparison.OrdinalIgnoreCase) || modelLib.Contains("/"))
                    return modelLib;

                var context = global::Android.App.Application.Context;
                var nativeLibDir = context.ApplicationInfo?.NativeLibraryDir;
                if (string.IsNullOrEmpty(nativeLibDir))
                    return modelLib;

                var soPath = Path.Combine(nativeLibDir, $"lib{modelLib}.so");
                if (File.Exists(soPath))
                    return soPath;
            }
            catch (System.Exception ex)
            {
                _logger?.LogWarning($"[MlcLlmBridge] Unable to resolve model lib for engine '{modelLib}': {ex.Message}");
            }

            return modelLib;
        }

        /// <summary>
        /// Get the model_lib identifier for a given model name
        /// </summary>
        public string? GetModelLib(string modelName)
        {
            foreach (var kvp in ModelLibMappings)
            {
                if (modelName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Check if a model is supported by the precompiled runtime
        /// </summary>
        public bool IsModelSupported(string modelName)
        {
            return GetModelLib(modelName) != null;
        }

        /// <summary>
        /// Initialize MLC LLM Engine with model
        /// </summary>
        public async Task<bool> InitializeAsync(string modelPath, string? modelLib = null)
        {
            try
            {
                _logger?.LogInformation($"[MlcLlmBridge] Initializing with model: {modelPath}");
                global::Android.Util.Log.Info("MlcLlmBridge", $"InitializeAsync modelPath={modelPath}, modelLib={(string.IsNullOrEmpty(modelLib) ? "<empty>" : modelLib)}");

                // First, load native library
                if (!LoadNativeLibrary())
                {
                    return false;
                }

                // Auto-detect model_lib if not provided
                if (string.IsNullOrEmpty(modelLib))
                {
                    modelLib = TryReadModelLibFromConfig(modelPath) ?? GetModelLib(modelPath);
                    global::Android.Util.Log.Info("MlcLlmBridge", $"Auto-detected modelLib={(string.IsNullOrEmpty(modelLib) ? "<null>" : modelLib)} from config or mapping");
                    if (string.IsNullOrEmpty(modelLib))
                    {
                        _logger?.LogError($"[MlcLlmBridge] Model not supported. Path: {modelPath}");
                        _logger?.LogError("[MlcLlmBridge] Supported models (hardcoded fallback): Qwen2.5-1.5B, Phi-3.5-mini, gemma-2-2b, Llama-3.2-3B, Mistral-7B");
                        OnError?.Invoke("Model not supported by precompiled MLC runtime");
                        global::Android.Util.Log.Error("MlcLlmBridge", $"Model not supported. modelPath={modelPath}");
                        return false;
                    }
                }

                var hasExternalModelSo = HasBundledModelNativeLibrary(modelLib);
                global::Android.Util.Log.Info("MlcLlmBridge", $"hasExternalModelSo={hasExternalModelSo}");

                if (hasExternalModelSo)
                {
                    TryLoadMlcRuntimeNativeLibraries(loadTvmFfi: true);
                    TryLoadModelNativeLibrary(modelLib);
                }
                global::Android.Util.Log.Info("MlcLlmBridge", $"Using modelLib={modelLib}");

                // If we have an external model library file, pass its full path to the engine.
                // This avoids relying on system:// lookup, which only works for precompiled embedded system libs.
                modelLib = ResolveModelLibForEngine(modelLib);
                global::Android.Util.Log.Info("MlcLlmBridge", $"Engine model_lib={modelLib}");

                _currentModelPath = modelPath;
                _currentModelLib = modelLib;

                // Check if model files exist
                if (!Directory.Exists(modelPath))
                {
                    _logger?.LogError($"[MlcLlmBridge] Model path does not exist: {modelPath}");
                    OnError?.Invoke($"Model path not found: {modelPath}");
                    return false;
                }

                var configPath = Path.Combine(modelPath, "mlc-chat-config.json");
                if (!File.Exists(configPath))
                {
                    _logger?.LogError($"[MlcLlmBridge] mlc-chat-config.json not found in {modelPath}");
                    OnError?.Invoke("mlc-chat-config.json not found");
                    return false;
                }

                // Initialize Java SimpleMlcEngine via reflection
                bool javaInitSuccess = await Task.Run(() => InitializeJavaEngine());

                if (javaInitSuccess)
                {
                    _logger?.LogInformation($"[MlcLlmBridge] Loading model into Java engine. modelPath={modelPath}, modelLib={modelLib}");

                    // Load model into engine
                    var loadTask = Task.Run(() => LoadModelIntoEngine(modelPath, modelLib));
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(90));
                    var finished = await Task.WhenAny(loadTask, timeoutTask);
                    if (finished != loadTask)
                    {
                        string? err = null;
                        try
                        {
                            if (_mlcEngineClass != null && _mlcEngine != null)
                            {
                                var getLastError = _mlcEngineClass.GetMethod("getLastError", System.Array.Empty<Java.Lang.Class>());
                                err = getLastError?.Invoke(_mlcEngine)?.ToString();
                            }
                        }
                        catch (System.Exception ex)
                        {
                            global::Android.Util.Log.Warn("MlcLlmBridge", $"Unable to read SimpleMlcEngine.getLastError() after load timeout: {ex.Message}");
                        }

                        _lastJavaEngineError = err;
                        var details = string.IsNullOrWhiteSpace(err) ? "" : $" Details: {err}";
                        global::Android.Util.Log.Error("MlcLlmBridge", $"Model load timed out after 90s.{details}");
                        OnError?.Invoke($"Model loading timed out after 90s.{details}");
                        return false;
                    }

                    bool modelLoaded = await loadTask;
                    if (!modelLoaded)
                    {
                        _logger?.LogWarning("[MlcLlmBridge] Java engine init OK but model load failed");
                        OnError?.Invoke("Model loading failed. Check if model files are complete.");
                        return false;
                    }
                    
                    _isInitialized = true;
                    _logger?.LogInformation($"[MlcLlmBridge] Initialization complete. Model lib: {modelLib}");
                    return true;
                }
                else
                {
                    _logger?.LogError("[MlcLlmBridge] Java engine initialization failed");
                    var details = string.IsNullOrWhiteSpace(_lastJavaEngineError) ? "" : $" Details: {_lastJavaEngineError}";
                    global::Android.Util.Log.Error("MlcLlmBridge", $"Java engine initialization failed.{details}");
                    OnError?.Invoke($"MLC engine initialization failed.{details}");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                _logger?.LogError(ex, "[MlcLlmBridge] Failed to initialize");
                OnError?.Invoke($"Initialization failed: {ex.Message}");
                return false;
            }
        }

        private void TryLoadModelNativeLibrary(string modelLib)
        {
            try
            {
                var context = global::Android.App.Application.Context;
                var nativeLibDir = context.ApplicationInfo?.NativeLibraryDir;
                if (string.IsNullOrEmpty(nativeLibDir))
                    return;

                var soPath = Path.Combine(nativeLibDir, $"lib{modelLib}.so");
                if (!File.Exists(soPath))
                {
                    global::Android.Util.Log.Warn("MlcLlmBridge", $"Model .so not found at {soPath}");
                    return;
                }

                _logger?.LogInformation($"[MlcLlmBridge] Loading model native library: {soPath}");

                // Use RTLD_GLOBAL to load model library in the same namespace as TVM runtime
                // This allows libphi2_q4f16_1_.so to resolve TVMFFIFunctionCall from libtvm_ffi.so
                TryDlopenGlobal(soPath);
            }
            catch (System.Exception ex)
            {
                _logger?.LogWarning($"[MlcLlmBridge] Unable to load model native library '{modelLib}': {ex.Message}");
                global::Android.Util.Log.Warn("MlcLlmBridge", $"Unable to load model native library '{modelLib}': {ex.Message}");
            }
        }

        private bool HasBundledModelNativeLibrary(string modelLib)
        {
            try
            {
                var context = global::Android.App.Application.Context;
                var nativeLibDir = context.ApplicationInfo?.NativeLibraryDir;
                if (string.IsNullOrEmpty(nativeLibDir))
                    return false;

                var soPath = Path.Combine(nativeLibDir, $"lib{modelLib}.so");
                return File.Exists(soPath);
            }
            catch
            {
                return false;
            }
        }

        private void TryLoadMlcRuntimeNativeLibraries(bool loadTvmFfi)
        {
            try
            {
                var context = global::Android.App.Application.Context;
                var nativeLibDir = context.ApplicationInfo?.NativeLibraryDir;
                if (!string.IsNullOrEmpty(nativeLibDir))
                {
                    if (loadTvmFfi)
                    {
                        TryDlopenGlobal(Path.Combine(nativeLibDir, "libtvm_ffi.so"));
                    }
                    TryDlopenGlobal(Path.Combine(nativeLibDir, "libtvm4j_runtime_packed.so"));
                }
            }
            catch (System.Exception ex)
            {
                global::Android.Util.Log.Warn("MlcLlmBridge", $"Unable to dlopen runtime libs with RTLD_GLOBAL: {ex.Message}");
            }

            try
            {
                if (loadTvmFfi)
                {
                    Java.Lang.JavaSystem.LoadLibrary("tvm_ffi");
                    global::Android.Util.Log.Info("MlcLlmBridge", "Loaded tvm_ffi");
                }
            }
            catch (System.Exception ex)
            {
                global::Android.Util.Log.Warn("MlcLlmBridge", $"Unable to load tvm_ffi: {ex.Message}");
            }

            try
            {
                Java.Lang.JavaSystem.LoadLibrary("tvm4j_runtime_packed");
                global::Android.Util.Log.Info("MlcLlmBridge", "Loaded tvm4j_runtime_packed");
            }
            catch (System.Exception ex)
            {
                global::Android.Util.Log.Warn("MlcLlmBridge", $"Unable to load tvm4j_runtime_packed: {ex.Message}");
            }
        }

        private void TryDlopenGlobal(string soPath)
        {
            try
            {
                if (!File.Exists(soPath))
                {
                    global::Android.Util.Log.Warn("MlcLlmBridge", $"RTLD_GLOBAL dlopen skipped; missing {soPath}");
                    return;
                }

                var loaderClass = Java.Lang.Class.ForName("com.llmclient.mlcllm.RtldGlobalLoader");
                var stringClass = Java.Lang.Class.ForName("java.lang.String");
                var method = loaderClass.GetMethod("loadWithRtldGlobal", stringClass);

                var result = method.Invoke(null, new Java.Lang.String(soPath));
                var success = result != null && ((Java.Lang.Boolean)result).BooleanValue();

                if (success)
                {
                    global::Android.Util.Log.Info("MlcLlmBridge", $"dlopen RTLD_GLOBAL OK: {soPath}");
                }
                else
                {
                    global::Android.Util.Log.Warn("MlcLlmBridge", $"dlopen RTLD_GLOBAL returned false for {soPath}");
                }
            }
            catch (System.Exception ex)
            {
                global::Android.Util.Log.Warn("MlcLlmBridge", $"dlopen RTLD_GLOBAL failed for {soPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialize the Java SimpleMlcEngine class via JNI
        /// </summary>
        private bool InitializeJavaEngine()
        {
            try
            {
                _lastJavaEngineError = null;
                _logger?.LogInformation("[MlcLlmBridge] Initializing Java SimpleMlcEngine...");
                global::Android.Util.Log.Info("MlcLlmBridge", "InitializeJavaEngine start");

                // Use application class loader instead of default ForName
                var context = global::Android.App.Application.Context;
                var classLoader = context.ClassLoader;
                
                if (classLoader == null)
                {
                    _logger?.LogError("[MlcLlmBridge] ClassLoader is null");
                    return false;
                }

                // Load class using application's class loader
                _mlcEngineClass = Java.Lang.Class.ForName("ai.mlc.mlcllm.SimpleMlcEngine", true, classLoader);
                if (_mlcEngineClass == null)
                {
                    _logger?.LogError("[MlcLlmBridge] SimpleMlcEngine class not found via ClassLoader");
                    return false;
                }
                
                _logger?.LogInformation("[MlcLlmBridge] SimpleMlcEngine class loaded successfully");
                global::Android.Util.Log.Info("MlcLlmBridge", "SimpleMlcEngine class loaded");

                // Create instance using default constructor
                var constructor = _mlcEngineClass.GetConstructor();
                if (constructor == null)
                {
                    _logger?.LogError("[MlcLlmBridge] SimpleMlcEngine constructor not found");
                    return false;
                }

                _mlcEngine = constructor.NewInstance();
                if (_mlcEngine == null)
                {
                    _logger?.LogError("[MlcLlmBridge] Failed to create SimpleMlcEngine instance");
                    return false;
                }
                
                _logger?.LogInformation("[MlcLlmBridge] SimpleMlcEngine instance created");
                global::Android.Util.Log.Info("MlcLlmBridge", "SimpleMlcEngine instance created");

                // Call initialize() method
                var initMethod = _mlcEngineClass.GetMethod("initialize");
                if (initMethod != null)
                {
                    _logger?.LogInformation("[MlcLlmBridge] Calling SimpleMlcEngine.initialize()...");
                    var result = initMethod.Invoke(_mlcEngine);
                    var success = result?.ToString().ToLower() == "true";
                    _logger?.LogInformation($"[MlcLlmBridge] SimpleMlcEngine.initialize() returned: {success}");
                    global::Android.Util.Log.Info("MlcLlmBridge", $"SimpleMlcEngine.initialize returned={success}");

                    if (!success)
                    {
                        try
                        {
                            var getLastError = _mlcEngineClass.GetMethod("getLastError");
                            var err = getLastError?.Invoke(_mlcEngine)?.ToString();
                            _lastJavaEngineError = err;
                            global::Android.Util.Log.Error("MlcLlmBridge", $"SimpleMlcEngine.initialize failed. lastError={err}");
                        }
                        catch (System.Exception ex)
                        {
                            global::Android.Util.Log.Warn("MlcLlmBridge", $"Unable to read SimpleMlcEngine.getLastError(): {ex.Message}");
                        }
                    }
                    return success;
                }

                _logger?.LogWarning("[MlcLlmBridge] initialize() method not found");
                return true; // Engine created, just no init method
            }
            catch (Java.Lang.ClassNotFoundException ex)
            {
                _logger?.LogError($"[MlcLlmBridge] SimpleMlcEngine class not found: {ex.Message}");
                global::Android.Util.Log.Error("MlcLlmBridge", $"ClassNotFoundException: {ex.Message}");
                return false;
            }
            catch (Java.Lang.Exception jex)
            {
                // Log full Java exception details
                _logger?.LogError($"[MlcLlmBridge] Java exception: {jex.GetType().Name}: {jex.Message}");
                global::Android.Util.Log.Error("MlcLlmBridge", $"Java exception: {jex.GetType().Name}: {jex.Message}");
                if (jex.Cause != null)
                {
                    _logger?.LogError($"[MlcLlmBridge] Caused by: {jex.Cause.GetType().Name}: {jex.Cause.Message}");
                    global::Android.Util.Log.Error("MlcLlmBridge", $"Caused by: {jex.Cause.GetType().Name}: {jex.Cause.Message}");
                }
                return false;
            }
            catch (System.Exception ex)
            {
                _logger?.LogError($"[MlcLlmBridge] Java engine init exception: {ex.GetType().Name}: {ex.Message}");
                global::Android.Util.Log.Error("MlcLlmBridge", $"Java engine init exception: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load model into the Java engine
        /// </summary>
        private bool LoadModelIntoEngine(string modelPath, string modelLib)
        {
            if (_mlcEngine == null || _mlcEngineClass == null) return false;

            try
            {
                var sw = Stopwatch.StartNew();
                _logger?.LogInformation($"[MlcLlmBridge] Calling SimpleMlcEngine.loadModel(modelPath={modelPath}, modelLib={modelLib})...");

                // Get loadModel method: boolean loadModel(String modelPath, String modelLib)
                var loadMethod = _mlcEngineClass.GetMethod("loadModel",
                    Java.Lang.Class.FromType(typeof(Java.Lang.String)),
                    Java.Lang.Class.FromType(typeof(Java.Lang.String)));

                if (loadMethod == null)
                {
                    _logger?.LogWarning("[MlcLlmBridge] loadModel method not found");
                    return false;
                }

                var result = loadMethod.Invoke(_mlcEngine,
                    new Java.Lang.String(modelPath),
                    new Java.Lang.String(modelLib));

                var success = result?.ToString() == "true";
                sw.Stop();
                _logger?.LogInformation($"[MlcLlmBridge] loadModel() returned: {success} (elapsed={sw.ElapsedMilliseconds}ms)");
                return success;
            }
            catch (Java.Lang.Throwable t)
            {
                _logger?.LogWarning($"[MlcLlmBridge] loadModel Java throwable: {t.GetType().Name}: {t.Message}");
                if (t.Cause != null)
                {
                    _logger?.LogWarning($"[MlcLlmBridge] loadModel caused by: {t.Cause.GetType().Name}: {t.Cause.Message}");
                }
                _logger?.LogWarning($"[MlcLlmBridge] loadModel Java stack: {t.StackTrace}");
                return false;
            }
            catch (System.Exception ex)
            {
                _logger?.LogWarning($"[MlcLlmBridge] loadModel exception: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Generate response (non-streaming)
        /// </summary>
        public async Task<string> GenerateAsync(string prompt, int maxTokens = 512, double temperature = 0.7)
        {
            if (!IsReady)
            {
                return "Error: Engine not initialized. Call InitializeAsync first.";
            }

            // Try Java engine first
            if (_mlcEngine != null && _mlcEngineClass != null)
            {
                try
                {
                    var result = await Task.Run(() => GenerateViaJavaEngine(prompt, maxTokens, temperature));
                    if (!string.IsNullOrEmpty(result) && !result.StartsWith("Error:"))
                    {
                        return result;
                    }
                    _logger?.LogWarning($"[MlcLlmBridge] Java engine generate failed: {result}");
                }
                catch (System.Exception ex)
                {
                    _logger?.LogWarning($"[MlcLlmBridge] Java engine exception: {ex.Message}");
                }
            }

            // Fallback: return status info
            return await Task.FromResult($"[MLC LLM Status]\n" +
                $"Library loaded: {_isLibraryLoaded}\n" +
                $"Java engine: {_mlcEngine != null}\n" +
                $"Model path: {_currentModelPath}\n" +
                $"Model lib: {_currentModelLib}\n" +
                $"Prompt received: {prompt.Length} chars\n" +
                $"Max tokens: {maxTokens}\n" +
                $"Temperature: {temperature}\n\n" +
                $"Note: Java engine not fully initialized. Check device logs.");
        }

        /// <summary>
        /// Generate via Java SimpleMlcEngine
        /// </summary>
        private string GenerateViaJavaEngine(string prompt, int maxTokens, double temperature)
        {
            if (_mlcEngine == null || _mlcEngineClass == null) return "Error: Java engine not available";

            try
            {
                var stringClass = Java.Lang.Class.FromType(typeof(Java.Lang.String));
                var intClass = Java.Lang.Integer.Type;
                var doubleClass = Java.Lang.Double.Type;
                if (stringClass == null || intClass == null || doubleClass == null)
                {
                    return "Error: Unable to resolve Java parameter types";
                }

                // Get generate method: String generate(String prompt, int maxTokens, double temperature)
                var generateMethod = _mlcEngineClass.GetMethod("generate",
                    stringClass,
                    intClass,
                    doubleClass);

                if (generateMethod == null)
                {
                    _logger?.LogWarning("[MlcLlmBridge] generate method not found");
                    return "Error: generate method not found";
                }

                var result = generateMethod.Invoke(_mlcEngine,
                    new Java.Lang.String(prompt),
                    Java.Lang.Integer.ValueOf(maxTokens),
                    Java.Lang.Double.ValueOf(temperature));

                return result?.ToString() ?? "Error: null response";
            }
            catch (System.Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Generate with real JNI streaming using Android.Webkit.ValueCallback
        /// </summary>
        public async Task GenerateStreamingAsync(string prompt, int maxTokens = 512, double temperature = 0.7,
            Action<string>? onToken = null, CancellationToken cancellationToken = default)
        {
            if (!IsReady)
            {
                OnError?.Invoke("Engine not initialized. Call InitializeAsync first.");
                return;
            }

            if (_mlcEngine == null || _mlcEngineClass == null)
            {
                OnError?.Invoke("Java engine not available");
                return;
            }

            try
            {
                var stringClass = Java.Lang.Class.FromType(typeof(Java.Lang.String));
                var intClass = Java.Lang.Integer.Type;
                var doubleClass = Java.Lang.Double.Type;
                var callbackClass = Java.Lang.Class.FromType(typeof(global::Android.Webkit.IValueCallback));
                if (stringClass == null || intClass == null || doubleClass == null || callbackClass == null)
                {
                    OnError?.Invoke("Unable to resolve Java parameter types for streaming");
                    return;
                }

                // Create the callback wrapper
                var tcs = new TaskCompletionSource<string>();
                var callback = new MlcStreamingCallback(
                    token => {
                        onToken?.Invoke(token);
                        OnToken?.Invoke(token);
                    },
                    fullResponse => {
                        tcs.TrySetResult(fullResponse);
                        OnComplete?.Invoke(fullResponse);
                    },
                    error => {
                        tcs.TrySetException(new System.Exception(error));
                        OnError?.Invoke(error);
                    }
                );

                // Get streaming method: void generateStreaming(String, int, double, ValueCallback)
                // Note: Android.Webkit.ValueCallback is used as the interface type
                var method = _mlcEngineClass.GetMethod("generateStreaming",
                    stringClass,
                    intClass,
                    doubleClass,
                    callbackClass);

                if (method == null)
                {
                    OnError?.Invoke("generateStreaming method not found");
                    return;
                }

                // Invoke Java method
                _logger?.LogInformation("[MlcLlmBridge] Starting native streaming...");
                method.Invoke(_mlcEngine,
                    new Java.Lang.String(prompt),
                    Java.Lang.Integer.ValueOf(maxTokens),
                    Java.Lang.Double.ValueOf(temperature),
                    callback); // Pass our C# callback which implements IValueCallback

                // Wait for completion or cancellation
                using (cancellationToken.Register(() => tcs.TrySetCanceled()))
                {
                    await tcs.Task;
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("[MlcLlmBridge] Generation cancelled");
                // Implement abort logic if needed (e.g., call reset() on engine)
            }
            catch (System.Exception ex)
            {
                _logger?.LogError(ex, "[MlcLlmBridge] Streaming error");
                OnError?.Invoke($"Streaming error: {ex.Message}");
            }
        }

        /// <summary>
        /// Callback class implementing Android.Webkit.IValueCallback to receive data from Java
        /// </summary>
        private class MlcStreamingCallback : Java.Lang.Object, global::Android.Webkit.IValueCallback
        {
            private readonly Action<string> _onToken;
            private readonly Action<string> _onComplete;
            private readonly Action<string> _onError;

            public MlcStreamingCallback(Action<string> onToken, Action<string> onComplete, Action<string> onError)
            {
                _onToken = onToken;
                _onComplete = onComplete;
                _onError = onError;
            }

            public void OnReceiveValue(Java.Lang.Object? value)
            {
                var message = value?.ToString();
                if (string.IsNullOrEmpty(message)) return;

                if (message.StartsWith("TOKEN:"))
                {
                    _onToken(message.Substring(6));
                }
                else if (message.StartsWith("DONE:"))
                {
                    _onComplete(message.Substring(5));
                }
                else if (message.StartsWith("ERROR:"))
                {
                    _onError(message.Substring(6));
                }
            }
        }

        /// <summary>
        /// Build chat prompt using Qwen2 chat template
        /// </summary>
        public string BuildChatPrompt(string systemPrompt, string[] history, string userMessage)
        {
            // Qwen2 uses <|im_start|> and <|im_end|> tokens
            var sb = new System.Text.StringBuilder();

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                sb.AppendLine("<|im_start|>system");
                sb.AppendLine(systemPrompt);
                sb.AppendLine("<|im_end|>");
            }

            if (history != null)
            {
                for (int i = 0; i < history.Length; i++)
                {
                    var role = i % 2 == 0 ? "user" : "assistant";
                    sb.AppendLine($"<|im_start|>{role}");
                    sb.AppendLine(history[i]);
                    sb.AppendLine("<|im_end|>");
                }
            }

            sb.AppendLine("<|im_start|>user");
            sb.AppendLine(userMessage);
            sb.AppendLine("<|im_end|>");
            sb.AppendLine("<|im_start|>assistant");

            return sb.ToString();
        }

        /// <summary>
        /// Reset conversation state
        /// </summary>
        public void ResetChat()
        {
            _logger?.LogInformation("[MlcLlmBridge] Chat reset");
        }

        /// <summary>
        /// Unload model and release resources
        /// </summary>
        public void Unload()
        {
            _isInitialized = false;
            _currentModelPath = null;
            _currentModelLib = null;
            _logger?.LogInformation("[MlcLlmBridge] Model unloaded");
        }

        /// <summary>
        /// Get model info as JSON
        /// </summary>
        public string GetModelInfo()
        {
            if (!IsReady)
            {
                return "{}";
            }

            var info = new
            {
                library_loaded = _isLibraryLoaded,
                initialized = _isInitialized,
                model_path = _currentModelPath,
                model_lib = _currentModelLib,
                supported_models = ModelLibMappings.Keys.ToArray()
            };

            return JsonSerializer.Serialize(info);
        }

        /// <summary>
        /// Get list of supported model names
        /// </summary>
        public string[] GetSupportedModels()
        {
            return ModelLibMappings.Keys.ToArray();
        }

        private string? TryReadModelLibFromConfig(string modelPath)
        {
            try
            {
                var configPath = Path.Combine(modelPath, "mlc-chat-config.json");
                if (!File.Exists(configPath)) return null;

                using var stream = File.OpenRead(configPath);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("model_lib", out var libProp) && libProp.ValueKind == JsonValueKind.String)
                {
                    var lib = libProp.GetString();
                    if (!string.IsNullOrWhiteSpace(lib))
                    {
                        _logger?.LogInformation($"[MlcLlmBridge] model_lib from config: {lib}");
                        return lib;
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger?.LogWarning($"[MlcLlmBridge] Failed to read model_lib from config: {ex.Message}");
            }
            return null;
        }

        private IEnumerable<string> ChunkString(string text, int chunkSize)
        {
            for (int i = 0; i < text.Length; i += chunkSize)
            {
                yield return text.Substring(i, System.Math.Min(chunkSize, text.Length - i));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Unload();
                }
                _disposed = true;
            }
            base.Dispose(disposing);
        }
    }
}
#endif
