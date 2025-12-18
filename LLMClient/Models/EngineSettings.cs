using System;
using Microsoft.Maui.Storage;

namespace LLMClient.Models
{
    public enum EngineType
    {
        OnnxGenAI,   // CPU-based, cross-platform
        LLamaSharp,  // llama.cpp - Windows + Android ARM64
        MlcLlm       // GPU (OpenCL/Vulkan/Metal) - iOS only, Android disabled
    }

    public static class EngineSettings
    {
        private const string PrefKey = "LocalModelEngine";
        public static event Action<EngineType>? EngineChanged;

        public static EngineType LoadSelectedEngine()
        {
            try
            {
                var value = Preferences.Get(PrefKey, GetDefaultEngine().ToString());
                if (Enum.TryParse<EngineType>(value, out var engine))
                    return engine;
            }
            catch { }
            return GetDefaultEngine();
        }

        /// <summary>
        /// Returns the default engine for the current platform.
        /// Android: LLamaSharp (llama.cpp) - CPU-based but well-optimized
        /// iOS: MLC LLM with Metal GPU
        /// Windows: ONNX GenAI or LLamaSharp
        /// MLC LLM temporarily disabled on Android - see docs/MLC_LLM_ISSUES.md
        /// </summary>
        public static EngineType GetDefaultEngine()
        {
#if ANDROID
            return EngineType.LLamaSharp; // llama.cpp for Android
#elif IOS
            return EngineType.MlcLlm; // Metal GPU on iOS
#else
            return EngineType.OnnxGenAI;
#endif
        }

        public static void SaveSelectedEngine(EngineType engine)
        {
            try
            {
                Preferences.Set(PrefKey, engine.ToString());
                EngineChanged?.Invoke(engine);
            }
            catch { }
        }
    }
}
