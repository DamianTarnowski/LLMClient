using System;

namespace LLMClient.Core.Models
{
    public enum EngineType
    {
        OnnxGenAI,      // CPU-based, cross-platform (ONNX Runtime GenAI)
        LLamaSharp,     // llama.cpp - Windows + Android + iOS
        MediaPipeGenAI  // Google AI Edge - Android + iOS (Gemma-3n multimodal)
    }

    /// <summary>
    /// Abstraction for preferences storage - implemented in platform project
    /// </summary>
    public interface IPreferencesService
    {
        string? Get(string key, string? defaultValue = null);
        void Set(string key, string value);
    }

    public class EngineSettingsService
    {
        private const string PrefKey = "LocalModelEngine";
        private readonly IPreferencesService? _preferences;
        
        public event Action<EngineType>? EngineChanged;

        public EngineSettingsService(IPreferencesService? preferences = null)
        {
            _preferences = preferences;
        }

        public EngineType LoadSelectedEngine()
        {
            try
            {
                var value = _preferences?.Get(PrefKey, GetDefaultEngine().ToString());
                if (value != null && Enum.TryParse<EngineType>(value, out var engine))
                    return engine;
            }
            catch { }
            return GetDefaultEngine();
        }

        /// <summary>
        /// Returns the default engine for the current platform.
        /// </summary>
        public static EngineType GetDefaultEngine()
        {
            // Default to ONNX for cross-platform compatibility
            return EngineType.OnnxGenAI;
        }

        public void SaveSelectedEngine(EngineType engine)
        {
            try
            {
                _preferences?.Set(PrefKey, engine.ToString());
                EngineChanged?.Invoke(engine);
            }
            catch { }
        }
    }
}
