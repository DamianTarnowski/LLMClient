using System.Text.Json;
using System.Text.Json.Serialization;

namespace LLMClient.Models
{
    /// <summary>
    /// Catalog of available MLC LLM models for mobile deployment.
    /// Models are sourced from HuggingFace mlc-ai organization.
    /// </summary>
    public static class MlcModelCatalog
    {
        /// <summary>
        /// All available MLC models sorted by recommended order for mobile.
        /// </summary>
        public static readonly MlcModelInfo[] AllModels = new[]
        {
            // === QWEN3 (Latest 2024) ===
            new MlcModelInfo
            {
                Id = "Qwen3-1.7B",
                HuggingFaceId = "mlc-ai/Qwen3-1.7B-q4f16_1-MLC",
                DisplayName = "Qwen 3 1.7B ⭐",
                Description = "Najnowszy Qwen3, świetna jakość, szybki",
                SizeMB = 950,
                ParameterCount = "1.7B",
                Category = ModelCategory.Small,
                RecommendedRamGB = 3,
                Languages = new[] { "en", "zh", "pl", "de", "es", "fr", "ja", "ko", "ru", "ar", "vi" },
                IsRecommended = false,
                IsDefault = false
            },

            // === COMPACT (< 1GB) - Best for limited RAM ===
            new MlcModelInfo
            {
                Id = "Qwen2.5-0.5B",
                HuggingFaceId = "mlc-ai/Qwen2.5-0.5B-Instruct-q4f16_1-MLC",
                DisplayName = "Qwen 2.5 0.5B",
                Description = "Ultra-compact, fastest inference",
                SizeMB = 350,
                ParameterCount = "0.5B",
                Category = ModelCategory.Compact,
                RecommendedRamGB = 2,
                Languages = new[] { "en", "zh", "pl", "de", "es", "fr", "ja", "ko", "ru" },
                IsRecommended = true
            },
            new MlcModelInfo
            {
                Id = "Qwen2-0.5B",
                HuggingFaceId = "mlc-ai/Qwen2-0.5B-Instruct-q4f16_1-MLC",
                DisplayName = "Qwen 2 0.5B",
                Description = "Minimal footprint, basic tasks",
                SizeMB = 320,
                ParameterCount = "0.5B",
                Category = ModelCategory.Compact,
                RecommendedRamGB = 2,
                Languages = new[] { "en", "zh" }
            },
            new MlcModelInfo
            {
                Id = "TinyLlama-1.1B",
                HuggingFaceId = "mlc-ai/TinyLlama-1.1B-Chat-v0.4-q4f16_1-MLC",
                DisplayName = "TinyLlama 1.1B",
                Description = "Compact Llama variant",
                SizeMB = 650,
                ParameterCount = "1.1B",
                Category = ModelCategory.Compact,
                RecommendedRamGB = 2,
                Languages = new[] { "en" }
            },
            new MlcModelInfo
            {
                Id = "Phi-2",
                HuggingFaceId = "mlc-ai/phi-2-q4f16_1-MLC",
                DisplayName = "Phi-2 (Local Build)",
                Description = "Microsoft Phi-2, locally compiled for Vulkan/OpenCL",
                SizeMB = 1500,
                ParameterCount = "2.7B",
                Category = ModelCategory.Small,
                RecommendedRamGB = 3,
                Languages = new[] { "en" },
                IsRecommended = true
            },

            // === SMALL (1-2GB) - Good balance ===
            new MlcModelInfo
            {
                Id = "Llama-3.2-1B",
                HuggingFaceId = "mlc-ai/Llama-3.2-1B-Instruct-q4f16_1-MLC",
                DisplayName = "Llama 3.2 1B",
                Description = "Latest Llama, excellent quality",
                SizeMB = 700,
                ParameterCount = "1B",
                Category = ModelCategory.Small,
                RecommendedRamGB = 3,
                Languages = new[] { "en", "de", "fr", "it", "pt", "hi", "es", "th" },
                IsRecommended = true
            },
            new MlcModelInfo
            {
                Id = "Qwen2.5-1.5B",
                HuggingFaceId = "mlc-ai/Qwen2.5-1.5B-Instruct-q4f16_1-MLC",
                DisplayName = "Qwen 2.5 1.5B",
                Description = "Best multilingual, coding support",
                SizeMB = 950,
                ParameterCount = "1.5B",
                Category = ModelCategory.Small,
                RecommendedRamGB = 3,
                Languages = new[] { "en", "zh", "pl", "de", "es", "fr", "ja", "ko", "ru", "ar", "vi" },
                IsRecommended = true
            },
            new MlcModelInfo
            {
                Id = "Phi-3-mini",
                HuggingFaceId = "mlc-ai/Phi-3-mini-4k-instruct-q4f16_1-MLC",
                DisplayName = "Phi-3 Mini",
                Description = "Microsoft's efficient model",
                SizeMB = 2200,
                ParameterCount = "3.8B",
                Category = ModelCategory.Small,
                RecommendedRamGB = 4,
                Languages = new[] { "en" }
            },

            // === MEDIUM (2-4GB) - Better quality ===
            new MlcModelInfo
            {
                Id = "Gemma-2-2B",
                HuggingFaceId = "mlc-ai/gemma-2-2b-it-q4f16_1-MLC",
                DisplayName = "Gemma 2 2B",
                Description = "Google's latest, high quality",
                SizeMB = 1400,
                ParameterCount = "2B",
                Category = ModelCategory.Medium,
                RecommendedRamGB = 4,
                Languages = new[] { "en" },
                IsRecommended = true
            },
            new MlcModelInfo
            {
                Id = "Llama-3.2-3B",
                HuggingFaceId = "mlc-ai/Llama-3.2-3B-Instruct-q4f16_1-MLC",
                DisplayName = "Llama 3.2 3B",
                Description = "Best mobile Llama",
                SizeMB = 1900,
                ParameterCount = "3B",
                Category = ModelCategory.Medium,
                RecommendedRamGB = 4,
                Languages = new[] { "en", "de", "fr", "it", "pt", "hi", "es", "th" }
            },
            new MlcModelInfo
            {
                Id = "Phi-3.5-mini",
                HuggingFaceId = "mlc-ai/Phi-3.5-mini-instruct-q4f16_0-MLC",
                DisplayName = "Phi-3.5 Mini",
                Description = "Microsoft's improved model, strong reasoning",
                SizeMB = 2050,
                ParameterCount = "3.8B",
                Category = ModelCategory.Medium,
                RecommendedRamGB = 4,
                Languages = new[] { "en" },
                IsRecommended = true,
                IsDefault = true
            },
            new MlcModelInfo
            {
                Id = "SmolLM2-1.7B",
                HuggingFaceId = "mlc-ai/SmolLM2-1.7B-Instruct-q4f16_1-MLC",
                DisplayName = "SmolLM2 1.7B",
                Description = "HuggingFace's compact model",
                SizeMB = 1100,
                ParameterCount = "1.7B",
                Category = ModelCategory.Medium,
                RecommendedRamGB = 3,
                Languages = new[] { "en" }
            },

            // === LARGE (4-8GB) - High-end devices only ===
            new MlcModelInfo
            {
                Id = "Qwen2.5-3B",
                HuggingFaceId = "mlc-ai/Qwen2.5-3B-Instruct-q4f16_1-MLC",
                DisplayName = "Qwen 2.5 3B",
                Description = "Best quality multilingual",
                SizeMB = 2000,
                ParameterCount = "3B",
                Category = ModelCategory.Large,
                RecommendedRamGB = 6,
                Languages = new[] { "en", "zh", "pl", "de", "es", "fr", "ja", "ko", "ru", "ar" }
            },
            new MlcModelInfo
            {
                Id = "Mistral-7B",
                HuggingFaceId = "mlc-ai/Mistral-7B-Instruct-v0.3-q4f16_1-MLC",
                DisplayName = "Mistral 7B",
                Description = "Powerful 7B model (8GB+ RAM)",
                SizeMB = 4100,
                ParameterCount = "7B",
                Category = ModelCategory.Large,
                RecommendedRamGB = 8,
                Languages = new[] { "en", "fr", "de", "es", "it" }
            },
            new MlcModelInfo
            {
                Id = "Llama-3.1-8B",
                HuggingFaceId = "mlc-ai/Llama-3.1-8B-Instruct-q4f16_1-MLC",
                DisplayName = "Llama 3.1 8B",
                Description = "Most capable (8GB+ RAM)",
                SizeMB = 4600,
                ParameterCount = "8B",
                Category = ModelCategory.Large,
                RecommendedRamGB = 8,
                Languages = new[] { "en", "de", "fr", "it", "pt", "hi", "es", "th" }
            }
        };

        /// <summary>
        /// Get models suitable for a device with given RAM.
        /// </summary>
        public static MlcModelInfo[] GetModelsForRam(int availableRamGB)
        {
            return AllModels
                .Where(m => m.RecommendedRamGB <= availableRamGB)
                .OrderBy(m => m.Category)
                .ThenByDescending(m => m.IsRecommended)
                .ThenBy(m => m.SizeMB)
                .ToArray();
        }

        /// <summary>
        /// Get recommended models for quick selection.
        /// </summary>
        public static MlcModelInfo[] GetRecommendedModels()
        {
            return AllModels.Where(m => m.IsRecommended).ToArray();
        }

        /// <summary>
        /// Get the default model for new installations.
        /// </summary>
        public static MlcModelInfo GetDefaultModel()
        {
            return AllModels.FirstOrDefault(m => m.IsDefault) ?? AllModels[0];
        }

        /// <summary>
        /// Find model by ID.
        /// </summary>
        public static MlcModelInfo? GetModelById(string id)
        {
            return AllModels.FirstOrDefault(m => m.Id == id);
        }

        /// <summary>
        /// Get models by category.
        /// </summary>
        public static MlcModelInfo[] GetModelsByCategory(ModelCategory category)
        {
            return AllModels.Where(m => m.Category == category).ToArray();
        }
    }

    public enum ModelCategory
    {
        Compact,  // < 1GB, 2GB RAM
        Small,    // 1-2GB, 3GB RAM
        Medium,   // 2-4GB, 4GB RAM
        Large     // 4-8GB, 6-8GB RAM
    }

    public class MlcModelInfo
    {
        public string Id { get; set; } = "";
        public string HuggingFaceId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public long SizeMB { get; set; }
        public string ParameterCount { get; set; } = "";
        public ModelCategory Category { get; set; }
        public int RecommendedRamGB { get; set; }
        public string[] Languages { get; set; } = Array.Empty<string>();
        public bool IsRecommended { get; set; }
        public bool IsDefault { get; set; }

        [JsonIgnore]
        public string SizeDisplay => SizeMB < 1000
            ? $"{SizeMB} MB"
            : $"{SizeMB / 1000.0:F1} GB";

        [JsonIgnore]
        public string CategoryDisplay => Category switch
        {
            ModelCategory.Compact => "Compact",
            ModelCategory.Small => "Small",
            ModelCategory.Medium => "Medium",
            ModelCategory.Large => "Large",
            _ => "Unknown"
        };

        [JsonIgnore]
        public string LanguagesDisplay => Languages.Length > 5
            ? $"{string.Join(", ", Languages.Take(5))}..."
            : string.Join(", ", Languages);
    }
}
