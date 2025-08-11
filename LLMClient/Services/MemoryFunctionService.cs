using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using LLMClient.Services;

namespace LLMClient.Services
{
    public class MemoryFunctionService
    {
        private readonly IMemoryService _memoryService;

        public MemoryFunctionService(IMemoryService memoryService)
        {
            _memoryService = memoryService;
        }

        [KernelFunction("remember_information")]
        [Description("Zapamiętuje ważne informacje podane przez użytkownika. Używaj tej funkcji, gdy użytkownik mówi o swoich preferencjach, danych osobowych, celach, czy innych rzeczach, które powinieneś zapamiętać.")]
        public async Task<string> RememberInformationAsync(
            [Description("Klucz identyfikujący informację (np. 'imie_uzytkownika', 'ulubiony_kolor', 'cele_zawodowe')")] string key,
            [Description("Wartość do zapamiętania")] string value,
            [Description("Kategoria informacji (np. 'osobiste', 'preferencje', 'cele', 'techniczne')")] string category = "",
            [Description("Tagi oddzielone przecinkami dla łatwiejszego wyszukiwania")] string tags = "",
            [Description("Czy informacja jest szczególnie ważna")] bool isImportant = false)
        {
            System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] remember_information called - Key: {key}, Value: {value}, Category: {category}");
            
            try
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                    return "Błąd: Klucz i wartość nie mogą być puste.";

                var result = await _memoryService.UpsertMemoryAsync(key, value, category, tags, isImportant);
                
                if (result > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] Successfully remembered: {key} = {value}");
                    return $"✅ Zapamiętałem: {key} = {value}";
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] Failed to remember: {key} = {value}");
                    return "❌ Nie udało się zapamiętać informacji.";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] Error in remember_information: {ex.Message}");
                return $"❌ Błąd podczas zapisywania pamięci: {ex.Message}";
            }
        }

        [KernelFunction("recall_information")]
        [Description("Wyszukuje zapamiętane informacje na podstawie klucza lub frazy wyszukiwania.")]
        public async Task<string> RecallInformationAsync(
            [Description("Klucz lub fraza do wyszukania w pamięci")] string searchTerm)
        {
            System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] recall_information called - SearchTerm: {searchTerm}");
            
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return "Błąd: Podaj klucz lub frazę do wyszukania.";

                // Najpierw spróbuj znaleźć dokładnie po kluczu
                var directMatch = await _memoryService.GetMemoryByKeyAsync(searchTerm);
                System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] Direct match for '{searchTerm}': {(directMatch != null ? "FOUND" : "NOT FOUND")}");
                
                if (directMatch != null)
                {
                    var result = FormatMemoryResult(directMatch);
                    System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] Returning direct match: {result}");
                    return result;
                }

                // Jeśli nie ma dokładnego dopasowania, wyszukaj
                var searchResults = await _memoryService.SearchMemoriesAsync(searchTerm);
                System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] Search results for '{searchTerm}': {searchResults.Count} found");
                
                if (!searchResults.Any())
                {
                    // Spróbuj jeszcze raz ze wszystkimi wspomnieniami żeby zobaczyć co jest w bazie
                    var allMemories = await _memoryService.GetAllMemoriesAsync();
                    System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] Total memories in database: {allMemories.Count}");
                    foreach (var mem in allMemories)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] Available memory: Key='{mem.Key}', Value='{mem.Value}'");
                    }
                    
                    return $"❌ Nie znalazłem informacji związanych z '{searchTerm}'.";
                }

                if (searchResults.Count == 1)
                {
                    var result = FormatMemoryResult(searchResults.First());
                    System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] Returning single search result: {result}");
                    return result;
                }

                // Jeśli jest więcej wyników, pokaż listę
                var resultText = $"🔍 Znaleziono {searchResults.Count} informacji dla '{searchTerm}':\n\n";
                foreach (var memory in searchResults.Take(5))
                {
                    resultText += FormatMemoryResult(memory) + "\n";
                }

                if (searchResults.Count > 5)
                    resultText += $"\n... i jeszcze {searchResults.Count - 5} innych wyników.";

                System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] Returning multiple search results: {resultText}");
                return resultText;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] Error in recall_information: {ex.Message}");
                return $"❌ Błąd podczas wyszukiwania w pamięci: {ex.Message}";
            }
        }

        [KernelFunction("list_memories")]
        [Description("Wyświetla listę wszystkich zapamiętanych informacji, opcjonalnie filtrowaną po kategorii.")]
        public async Task<string> ListMemoriesAsync(
            [Description("Opcjonalna kategoria do filtrowania (pozostaw puste dla wszystkich)")] string category = "")
        {
            System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] list_memories called - Category: '{category}'");
            
            try
            {
                var memories = string.IsNullOrWhiteSpace(category) 
                    ? await _memoryService.GetAllMemoriesAsync()
                    : await _memoryService.GetMemoriesByCategoryAsync(category);

                System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] Retrieved {memories.Count} memories for listing");

                if (!memories.Any())
                {
                    return string.IsNullOrWhiteSpace(category) 
                        ? "📝 Nie mam jeszcze żadnych wspomnień."
                        : $"📝 Nie mam wspomnień w kategorii '{category}'.";
                }

                var resultText = string.IsNullOrWhiteSpace(category) 
                    ? $"📝 Wszystkie zapamiętane informacje ({memories.Count}):\n\n"
                    : $"📝 Wspomnienia w kategorii '{category}' ({memories.Count}):\n\n";

                foreach (var memory in memories.Take(10))
                {
                    resultText += FormatMemoryResult(memory, showCategory: string.IsNullOrWhiteSpace(category)) + "\n";
                }

                if (memories.Count > 10)
                    resultText += $"\n... i jeszcze {memories.Count - 10} innych wspomnień.";

                return resultText;
            }
            catch (Exception ex)
            {
                return $"❌ Błąd podczas pobierania listy wspomnień: {ex.Message}";
            }
        }

        [KernelFunction("update_memory")]
        [Description("Aktualizuje istniejącą informację w pamięci.")]
        public async Task<string> UpdateMemoryAsync(
            [Description("Klucz informacji do aktualizacji")] string key,
            [Description("Nowa wartość")] string newValue,
            [Description("Nowa kategoria (opcjonalna)")] string category = "",
            [Description("Nowe tagi (opcjonalne)")] string tags = "",
            [Description("Czy informacja jest szczególnie ważna")] bool isImportant = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(newValue))
                    return "Błąd: Klucz i nowa wartość nie mogą być puste.";

                var existingMemory = await _memoryService.GetMemoryByKeyAsync(key);
                if (existingMemory == null)
                    return $"❌ Nie znaleziono informacji o kluczu '{key}'.";

                var result = await _memoryService.UpsertMemoryAsync(key, newValue, category, tags, isImportant);
                
                if (result > 0)
                    return $"✅ Zaktualizowałem informację: {key} = {newValue}";
                else
                    return "❌ Nie udało się zaktualizować informacji.";
            }
            catch (Exception ex)
            {
                return $"❌ Błąd podczas aktualizacji pamięci: {ex.Message}";
            }
        }

        [KernelFunction("forget_information")]
        [Description("Usuwa informację z pamięci na podstawie klucza.")]
        public async Task<string> ForgetInformationAsync(
            [Description("Klucz informacji do usunięcia")] string key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                    return "Błąd: Klucz nie może być pusty.";

                var existingMemory = await _memoryService.GetMemoryByKeyAsync(key);
                if (existingMemory == null)
                    return $"❌ Nie znaleziono informacji o kluczu '{key}'.";

                var result = await _memoryService.DeleteMemoryAsync(existingMemory.Id);
                
                if (result > 0)
                    return $"✅ Zapomniałem informację: {key}";
                else
                    return "❌ Nie udało się usunąć informacji.";
            }
            catch (Exception ex)
            {
                return $"❌ Błąd podczas usuwania z pamięci: {ex.Message}";
            }
        }

        [KernelFunction("get_memory_categories")]
        [Description("Pobiera listę wszystkich kategorii używanych w pamięci.")]
        public async Task<string> GetMemoryCategoriesAsync()
        {
            try
            {
                var categories = await _memoryService.GetCategoriesAsync();
                
                if (!categories.Any())
                    return "📂 Nie ma jeszcze żadnych kategorii.";

                return $"📂 Dostępne kategorie ({categories.Count}): {string.Join(", ", categories)}";
            }
            catch (Exception ex)
            {
                return $"❌ Błąd podczas pobierania kategorii: {ex.Message}";
            }
        }

        [KernelFunction("show_memory_keys")]
        [Description("Pokazuje wszystkie dostępne klucze pamięci - użyteczne gdy nie wiesz jak szukać konkretnej informacji.")]
        public async Task<string> ShowMemoryKeysAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] show_memory_keys called");
            
            try
            {
                var memories = await _memoryService.GetAllMemoriesAsync();
                
                if (!memories.Any())
                    return "❌ Brak wspomnień w pamięci.";

                var keys = memories.Select(m => $"'{m.Key}' -> {m.Value}").ToList();
                var result = $"🔑 Dostępne klucze pamięci ({keys.Count}):\n\n" + string.Join("\n", keys);
                
                System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] Returning {keys.Count} memory keys");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MemoryFunctionService] Error in show_memory_keys: {ex.Message}");
                return $"❌ Błąd podczas pobierania kluczy pamięci: {ex.Message}";
            }
        }

        private string FormatMemoryResult(Models.Memory memory, bool showCategory = true)
        {
            var result = $"🔑 **{memory.Key}**: {memory.Value}";
            
            if (showCategory && !string.IsNullOrWhiteSpace(memory.Category))
                result += $"\n   📂 Kategoria: {memory.Category}";
            
            if (!string.IsNullOrWhiteSpace(memory.Tags))
                result += $"\n   🏷️ Tagi: {memory.Tags}";
            
            if (memory.IsImportant)
                result += $"\n   ⭐ Ważne";
            
            result += $"\n   📅 Zaktualizowano: {memory.UpdatedAt:yyyy-MM-dd HH:mm}";
            
            return result;
        }
    }
}