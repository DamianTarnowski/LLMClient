using LLMClient.Models;
using System.Text;

namespace LLMClient.Services
{
    public interface IMemoryContextService
    {
        Task<string> GenerateMemoryContextAsync();
        Task<string> SummarizeOldMemoriesAsync(List<Memory> oldMemories);
    }

    public class MemoryContextService : IMemoryContextService
    {
        private readonly IMemoryService _memoryService;
        private readonly Lazy<IAiService?> _lazyAiService;
        private const int MAX_MEMORY_CHARS = 30000; // pełny kontekst dla dużych LLM
        private const int SUMMARY_TARGET_CHARS = 5000; // Docelowy rozmiar streszczenia

        public MemoryContextService(IMemoryService memoryService, Lazy<IAiService?> lazyAiService)
        {
            _memoryService = memoryService;
            _lazyAiService = lazyAiService;
        }

        public async Task<string> GenerateMemoryContextAsync()
        {
            System.Diagnostics.Debug.WriteLine("[MemoryContextService] Generating memory context");
            
            try
            {
                var allMemories = await _memoryService.GetAllMemoriesAsync();
                if (!allMemories.Any())
                {
                    System.Diagnostics.Debug.WriteLine("[MemoryContextService] No memories found");
                    return string.Empty;
                }

                System.Diagnostics.Debug.WriteLine($"[MemoryContextService] Processing {allMemories.Count} memories");

                // Sortuj wspomnienia: najważniejsze i najnowsze na górze
                var sortedMemories = allMemories
                    .OrderByDescending(m => m.IsImportant)
                    .ThenByDescending(m => m.UpdatedAt)
                    .ToList();

                // Buduj kontekst pamięci
                var memoryContext = new StringBuilder();
                memoryContext.AppendLine("=== PAMIĘĆ UŻYTKOWNIKA ===");
                
                var currentLength = memoryContext.Length;
                var includedMemories = new List<Memory>();
                var excludedMemories = new List<Memory>();

                // Dodawaj wspomnienia dopóki nie przekroczysz limitu
                foreach (var memory in sortedMemories)
                {
                    var memoryText = FormatMemoryForContext(memory);
                    
                    if (currentLength + memoryText.Length <= MAX_MEMORY_CHARS)
                    {
                        memoryContext.AppendLine(memoryText);
                        currentLength += memoryText.Length + Environment.NewLine.Length;
                        includedMemories.Add(memory);
                    }
                    else
                    {
                        excludedMemories.Add(memory);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[MemoryContextService] Included {includedMemories.Count} memories directly");
                System.Diagnostics.Debug.WriteLine($"[MemoryContextService] {excludedMemories.Count} memories need summarization");

                // Jeśli są wspomnienia, które nie zmieściły się, spróbuj je streścić
                if (excludedMemories.Any())
                {
                    var availableSpace = MAX_MEMORY_CHARS - currentLength;
                    if (availableSpace > 500) // Zostawiamy miejsce na streszczenie
                    {
                        var summary = await SummarizeOldMemoriesAsync(excludedMemories);
                        if (!string.IsNullOrWhiteSpace(summary) && summary.Length <= availableSpace)
                        {
                            memoryContext.AppendLine();
                            memoryContext.AppendLine("=== STRESZCZENIE STARSZYCH WSPOMNIEŃ ===");
                            memoryContext.AppendLine(summary);
                        }
                    }
                }

                memoryContext.AppendLine("=== KONIEC PAMIĘCI ===");

                var finalContext = memoryContext.ToString();
                System.Diagnostics.Debug.WriteLine($"[MemoryContextService] Generated context: {finalContext.Length} characters");
                
                return finalContext;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MemoryContextService] Error generating context: {ex.Message}");
                return "=== PAMIĘĆ UŻYTKOWNIKA ===\n[Błąd podczas ładowania pamięci]\n=== KONIEC PAMIĘCI ===";
            }
        }

        public async Task<string> SummarizeOldMemoriesAsync(List<Memory> oldMemories)
        {
            var aiService = _lazyAiService.Value;
            if (!oldMemories.Any() || aiService == null || !aiService.IsConfigured)
            {
                System.Diagnostics.Debug.WriteLine("[MemoryContextService] Cannot summarize - no memories or AI service not configured");
                return CreateBasicSummary(oldMemories);
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"[MemoryContextService] AI summarizing {oldMemories.Count} old memories");
                
                var memoriesToSummarize = new StringBuilder();
                foreach (var memory in oldMemories.Take(50)) // Ogranicz do 50 najstarszych
                {
                    memoriesToSummarize.AppendLine($"• {memory.Key}: {memory.Value}");
                    if (!string.IsNullOrEmpty(memory.Category))
                        memoriesToSummarize.Append($" [Kategoria: {memory.Category}]");
                }

                var prompt = $"Streść następujące informacje o użytkowniku w zwięzły sposób (maksymalnie {SUMMARY_TARGET_CHARS} znaków). " +
                            "Zachowaj najważniejsze fakty, preferencje i szczegóły osobiste. Grupuj podobne informacje razem:\n\n" +
                            memoriesToSummarize.ToString();

                // Użyj pustej historii dla streszczenia
                var summary = await aiService.GetResponseAsync(prompt, new List<Message>());
                
                System.Diagnostics.Debug.WriteLine($"[MemoryContextService] AI summary generated: {summary.Length} characters");
                return summary;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MemoryContextService] AI summarization failed: {ex.Message}");
                return CreateBasicSummary(oldMemories);
            }
        }

        private string CreateBasicSummary(List<Memory> memories)
        {
            if (!memories.Any()) return string.Empty;

            var summary = new StringBuilder();
            var categories = memories.GroupBy(m => string.IsNullOrEmpty(m.Category) ? "Różne" : m.Category);

            foreach (var categoryGroup in categories.Take(10)) // Maksymalnie 10 kategorii
            {
                summary.AppendLine($"• {categoryGroup.Key}: {categoryGroup.Count()} informacji");
                
                // Pokaż kilka przykładów z tej kategorii
                var examples = categoryGroup.Take(3).Select(m => $"{m.Key}={m.Value}");
                if (categoryGroup.Count() > 3)
                    examples = examples.Concat(new[] { "..." });
                
                summary.AppendLine($"  Przykłady: {string.Join(", ", examples)}");
            }

            return summary.ToString();
        }

        private string FormatMemoryForContext(Memory memory)
        {
            var formatted = new StringBuilder();
            
            // Podstawowe informacje
            formatted.Append($"• {memory.Key}: {memory.Value}");
            
            // Dodatkowe oznaczenia
            var tags = new List<string>();
            if (!string.IsNullOrEmpty(memory.Category))
                tags.Add($"Kategoria: {memory.Category}");
            if (memory.IsImportant)
                tags.Add("WAŻNE");
            if (!string.IsNullOrEmpty(memory.Tags))
                tags.Add($"Tagi: {memory.Tags}");
                
            if (tags.Any())
                formatted.Append($" [{string.Join(", ", tags)}]");
            
            return formatted.ToString();
        }
    }
}