using LLMClient.Models;
using System.Text.RegularExpressions;

namespace LLMClient.Services
{
    public interface IMemoryExtractionService
    {
        Task ExtractAndSaveMemoryFromConversationAsync(List<Message> recentMessages);
    }

    public class MemoryExtractionService : IMemoryExtractionService
    {
        private readonly IMemoryService _memoryService;
        private readonly IAiService _aiService;

        public MemoryExtractionService(IMemoryService memoryService, IAiService aiService)
        {
            _memoryService = memoryService;
            _aiService = aiService;
        }

        public async Task ExtractAndSaveMemoryFromConversationAsync(List<Message> recentMessages)
        {
            if (!recentMessages.Any() || !_aiService.IsConfigured)
                return;

            try
            {
                // Pobierz tylko najnowsze wiadomości użytkownika (max 5)
                var userMessages = recentMessages
                    .Where(m => m.IsUser)
                    .OrderByDescending(m => m.Timestamp)
                    .Take(5)
                    .Reverse() // Przywróć chronologiczną kolejność
                    .ToList();

                if (!userMessages.Any())
                    return;

                System.Diagnostics.Debug.WriteLine($"[MemoryExtractionService] Extracting memory from {userMessages.Count} user messages");

                // Spróbuj najpierw prostymi regexami
                await ExtractSimpleMemoryAsync(userMessages);

                // Jeśli nie znajdzie nic prostego, użyj AI do wydobycia informacji
                await ExtractComplexMemoryAsync(userMessages);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MemoryExtractionService] Error extracting memory: {ex.Message}");
            }
        }

        private async Task ExtractSimpleMemoryAsync(List<Message> userMessages)
        {
            var simplePatterns = new Dictionary<string, string>
            {
                { @"(?:nazywam się|mam na imię|jestem|to ja)\s+(\w+)", "imie" },
                { @"(?:mam|jestem|to)\s+(\d+)\s+(?:lat|roku|roki)", "wiek" },
                { @"(?:pochodzę|jestem)\s+(?:z|ze)\s+(\w+)", "miasto" },
                { @"(?:pracuję|jestem|robię)\s+(?:jako|w|na)\s+([\w\s]+)", "praca" },
                { @"(?:lubię|uwielbiam|moją|moich)\s+([\w\s,]+)", "preferencje" }
            };

            foreach (var message in userMessages)
            {
                foreach (var pattern in simplePatterns)
                {
                    var matches = Regex.Matches(message.Content, pattern.Key, RegexOptions.IgnoreCase);
                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            var value = match.Groups[1].Value.Trim();
                            if (value.Length > 2 && value.Length < 100) // Rozsądne ograniczenia
                            {
                                await SaveMemoryAsync(pattern.Value, value, "osobiste", $"wydobyte_automatycznie,{pattern.Value}");
                                System.Diagnostics.Debug.WriteLine($"[MemoryExtractionService] Simple extraction: {pattern.Value} = {value}");
                            }
                        }
                    }
                }
            }
        }

        private async Task ExtractComplexMemoryAsync(List<Message> userMessages)
        {
            var conversationText = string.Join("\n", userMessages.Select(m => $"Użytkownik: {m.Content}"));
            
            var extractionPrompt = $@"Przeanalizuj poniższą rozmowę i wydobądź z niej informacje osobiste o użytkowniku. 
Zwróć TYLKO listę w formacie 'klucz=wartość', po jednym w linii. 
Ignoruj pytania, szukaj tylko stwierdzeń faktów. Używaj polskich kluczy.

Przykłady dobrych wyników:
imie=Jan
wiek=25
miasto=Warszawa
praca=programista
hobby=piłka nożna

Rozmowa:
{conversationText}

Lista informacji (klucz=wartość):";

            try
            {
                var response = await _aiService.GetResponseAsync(extractionPrompt, new List<Message>());
                await ParseAndSaveExtractedMemory(response);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MemoryExtractionService] AI extraction failed: {ex.Message}");
            }
        }

        private async Task ParseAndSaveExtractedMemory(string extractedData)
        {
            var lines = extractedData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Contains('='))
                {
                    var parts = trimmed.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim().ToLower();
                        var value = parts[1].Trim();
                        
                        if (key.Length > 0 && value.Length > 1 && value.Length < 200)
                        {
                            await SaveMemoryAsync(key, value, "osobiste", "wydobyte_przez_ai");
                            System.Diagnostics.Debug.WriteLine($"[MemoryExtractionService] AI extraction: {key} = {value}");
                        }
                    }
                }
            }
        }

        private async Task SaveMemoryAsync(string key, string value, string category, string tags)
        {
            try
            {
                // Sprawdź czy już istnieje - jeśli tak, nie nadpisuj (może być lepsza informacja)
                var existing = await _memoryService.GetMemoryByKeyAsync(key);
                if (existing == null)
                {
                    await _memoryService.UpsertMemoryAsync(key, value, category, tags, false);
                    System.Diagnostics.Debug.WriteLine($"[MemoryExtractionService] Saved new memory: {key} = {value}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[MemoryExtractionService] Memory already exists, skipping: {key}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MemoryExtractionService] Failed to save memory {key}: {ex.Message}");
            }
        }
    }
}