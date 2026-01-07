namespace LLMClient.Core.Models
{
    /// <summary>
    /// Informacje o modelu embeddingowym
    /// </summary>
    public class EmbeddingModelInfo
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public int Dimensions { get; set; }
        public long SizeInMB { get; set; }
        public string HuggingFaceRepo { get; set; } = "";
        public string[] SupportedLanguages { get; set; } = Array.Empty<string>();
        public bool IsRecommended { get; set; }
        public bool IsDefault { get; set; }
        
        /// <summary>
        /// Czy model wymaga specjalnego prefixu dla query (np. E5 wymaga "query: ")
        /// </summary>
        public bool RequiresQueryPrefix { get; set; }
        
        /// <summary>
        /// Prefix dla query (jeśli RequiresQueryPrefix = true)
        /// </summary>
        public string QueryPrefix { get; set; } = "";
        
        /// <summary>
        /// Prefix dla passage/document
        /// </summary>
        public string PassagePrefix { get; set; } = "";
        
        /// <summary>
        /// Minimalna ilość RAM (GB) zalecana dla tego modelu
        /// </summary>
        public int MinRAMGB { get; set; } = 4;
        
        /// <summary>
        /// Jakość semantyczna (0-100) - im wyższa tym lepsze wyniki
        /// </summary>
        public int QualityScore { get; set; } = 70;
        
        /// <summary>
        /// Szybkość (0-100) - im wyższa tym szybszy
        /// </summary>
        public int SpeedScore { get; set; } = 50;
    }

    /// <summary>
    /// Dostępne modele embeddingowe
    /// </summary>
    public static class EmbeddingModels
    {
        public static readonly EmbeddingModelInfo EmbeddingGemma = new()
        {
            Id = "embeddinggemma-300m",
            DisplayName = "Gemma 300M (Szybki)",
            Description = "🚀 Szybki i lekki. Jakość ~75% E5, ale 2x szybszy i zajmuje mniej RAM. Idealny dla urządzeń z <8GB RAM.",
            Dimensions = 768,
            SizeInMB = 1200,
            HuggingFaceRepo = "onnx-community/embeddinggemma-300m-ONNX",
            SupportedLanguages = new[] { "pl", "en", "de", "fr", "es", "it", "pt", "ru", "zh", "ja", "ko", "ar", "hi", "100+" },
            IsRecommended = false,
            IsDefault = false,
            RequiresQueryPrefix = false,
            QueryPrefix = "",
            PassagePrefix = "",
            MinRAMGB = 4,
            QualityScore = 75,
            SpeedScore = 90
        };

        public static readonly EmbeddingModelInfo E5LargeMultilingual = new()
        {
            Id = "intfloat-e5-large-multilingual-v1",
            DisplayName = "E5-Large (Dokładny)",
            Description = "🎯 Najwyższa jakość wyszukiwania semantycznego. ~25% lepszy dla polskiego niż Gemma. Wymaga więcej RAM.",
            Dimensions = 1024,
            SizeInMB = 2200,
            HuggingFaceRepo = "intfloat/multilingual-e5-large",
            SupportedLanguages = new[] { "pl", "en", "de", "fr", "es", "it", "pt", "ru", "zh", "ja", "ko", "ar", "100+" },
            IsRecommended = true,
            IsDefault = true,
            RequiresQueryPrefix = true,
            QueryPrefix = "query: ",
            PassagePrefix = "passage: ",
            MinRAMGB = 8,
            QualityScore = 95,
            SpeedScore = 60
        };

        public static readonly IReadOnlyList<EmbeddingModelInfo> All = new[]
        {
            EmbeddingGemma,
            E5LargeMultilingual
        };

        public static EmbeddingModelInfo GetById(string id)
            => All.FirstOrDefault(m => m.Id == id) ?? EmbeddingGemma;

        public static EmbeddingModelInfo GetDefault()
            => All.First(m => m.IsDefault);
        
        /// <summary>
        /// Zwraca rekomendowany model na podstawie dostępnego RAM
        /// </summary>
        public static EmbeddingModelInfo GetRecommendedForRAM(long availableRAMBytes)
        {
            var availableGB = availableRAMBytes / (1024L * 1024L * 1024L);
            // E5-Large wymaga 8GB+, w przeciwnym razie Gemma
            if (availableGB >= 8)
                return E5LargeMultilingual;
            return EmbeddingGemma;
        }
    }
}
