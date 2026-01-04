using System;
using Microsoft.Maui.Storage;

namespace LLMClient.Models
{
    public enum EngineType
    {
        OnnxGenAI,      // CPU-based, cross-platform (ONNX Runtime GenAI)
        LLamaSharp,     // llama.cpp - Windows + Android + iOS
        MediaPipeGenAI  // Google AI Edge - Android + iOS (Gemma-3n multimodal)
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
        /// iOS: LLamaSharp (llama.cpp) - MLC disabled
        /// Windows: ONNX GenAI (default) or LLamaSharp
        /// </summary>
        public static EngineType GetDefaultEngine()
        {
#if ANDROID || IOS
            return EngineType.LLamaSharp; // llama.cpp for mobile
#else
            return EngineType.OnnxGenAI;  // ONNX for Windows
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
