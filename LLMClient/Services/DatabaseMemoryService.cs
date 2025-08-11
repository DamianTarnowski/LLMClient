using LLMClient.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LLMClient.Services
{
    public class DatabaseMemoryService : IMemoryService
    {
        private readonly DatabaseService _databaseService;

        public DatabaseMemoryService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<Memory>> GetAllMemoriesAsync()
        {
            System.Diagnostics.Debug.WriteLine("[DatabaseMemoryService] GetAllMemoriesAsync called");
            var memories = await _databaseService.GetAllMemoriesAsync();
            System.Diagnostics.Debug.WriteLine($"[DatabaseMemoryService] Retrieved {memories.Count} memories");
            return memories;
        }

        public async Task<Memory?> GetMemoryByKeyAsync(string key)
        {
            return await _databaseService.GetMemoryByKeyAsync(key);
        }

        public async Task<List<Memory>> SearchMemoriesAsync(string searchTerm)
        {
            return await _databaseService.SearchMemoriesAsync(searchTerm);
        }

        public async Task<List<Memory>> GetMemoriesByCategoryAsync(string category)
        {
            return await _databaseService.GetMemoriesByCategoryAsync(category);
        }

        public async Task<int> AddMemoryAsync(Memory memory)
        {
            return await _databaseService.AddMemoryAsync(memory);
        }

        public async Task<int> UpdateMemoryAsync(Memory memory)
        {
            return await _databaseService.UpdateMemoryAsync(memory);
        }

        public async Task<int> DeleteMemoryAsync(int memoryId)
        {
            return await _databaseService.DeleteMemoryAsync(memoryId);
        }

        public async Task<int> UpsertMemoryAsync(string key, string value, string category = "", string tags = "", bool isImportant = false)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseMemoryService] UpsertMemoryAsync called - Key: {key}, Value: {value}, Category: {category}");
            var result = await _databaseService.UpsertMemoryAsync(key, value, category, tags, isImportant);
            System.Diagnostics.Debug.WriteLine($"[DatabaseMemoryService] UpsertMemoryAsync result: {result}");
            return result;
        }

        public async Task<List<string>> GetCategoriesAsync()
        {
            return await _databaseService.GetMemoryCategoriesAsync();
        }

        public async Task<List<string>> GetTagsAsync()
        {
            return await _databaseService.GetMemoryTagsAsync();
        }

        public void Dispose()
        {
            // DatabaseService jest singleton, więc nie dispose'ujemy go tutaj
        }
    }
}