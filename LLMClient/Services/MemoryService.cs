using LLMClient.Models;
using SQLite;
using System.Collections.ObjectModel;

namespace LLMClient.Services
{
    public interface IMemoryService : IDisposable
    {
        Task<List<Memory>> GetAllMemoriesAsync();
        Task<Memory?> GetMemoryByKeyAsync(string key);
        Task<List<Memory>> SearchMemoriesAsync(string searchTerm);
        Task<List<Memory>> GetMemoriesByCategoryAsync(string category);
        Task<int> AddMemoryAsync(Memory memory);
        Task<int> UpdateMemoryAsync(Memory memory);
        Task<int> DeleteMemoryAsync(int memoryId);
        Task<int> UpsertMemoryAsync(string key, string value, string category = "", string tags = "", bool isImportant = false);
        Task<List<string>> GetCategoriesAsync();
        Task<List<string>> GetTagsAsync();
    }

    public class MemoryService : IMemoryService
    {
        private readonly SQLiteAsyncConnection _database;

        public MemoryService(string dbPath)
        {
            _database = new SQLiteAsyncConnection(dbPath);
            _database.CreateTableAsync<Memory>().Wait();
        }

        public async Task<List<Memory>> GetAllMemoriesAsync()
        {
            return await _database.Table<Memory>()
                .OrderByDescending(m => m.UpdatedAt)
                .ToListAsync();
        }

        public async Task<Memory?> GetMemoryByKeyAsync(string key)
        {
            return await _database.Table<Memory>()
                .Where(m => m.Key == key)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Memory>> SearchMemoriesAsync(string searchTerm)
        {
            var lowerSearchTerm = searchTerm.ToLower();
            return await _database.Table<Memory>()
                .Where(m => m.Key.ToLower().Contains(lowerSearchTerm) || 
                           m.Value.ToLower().Contains(lowerSearchTerm) ||
                           m.Category.ToLower().Contains(lowerSearchTerm) ||
                           m.Tags.ToLower().Contains(lowerSearchTerm))
                .OrderByDescending(m => m.UpdatedAt)
                .ToListAsync();
        }

        public async Task<List<Memory>> GetMemoriesByCategoryAsync(string category)
        {
            return await _database.Table<Memory>()
                .Where(m => m.Category == category)
                .OrderByDescending(m => m.UpdatedAt)
                .ToListAsync();
        }

        public async Task<int> AddMemoryAsync(Memory memory)
        {
            memory.CreatedAt = DateTime.Now;
            memory.UpdatedAt = DateTime.Now;
            return await _database.InsertAsync(memory);
        }

        public async Task<int> UpdateMemoryAsync(Memory memory)
        {
            memory.UpdatedAt = DateTime.Now;
            return await _database.UpdateAsync(memory);
        }

        public async Task<int> DeleteMemoryAsync(int memoryId)
        {
            return await _database.DeleteAsync<Memory>(memoryId);
        }

        public async Task<int> UpsertMemoryAsync(string key, string value, string category = "", string tags = "", bool isImportant = false)
        {
            var existingMemory = await GetMemoryByKeyAsync(key);
            
            if (existingMemory != null)
            {
                existingMemory.Value = value;
                existingMemory.Category = category;
                existingMemory.Tags = tags;
                existingMemory.IsImportant = isImportant;
                return await UpdateMemoryAsync(existingMemory);
            }
            else
            {
                var newMemory = new Memory
                {
                    Key = key,
                    Value = value,
                    Category = category,
                    Tags = tags,
                    IsImportant = isImportant
                };
                return await AddMemoryAsync(newMemory);
            }
        }

        public async Task<List<string>> GetCategoriesAsync()
        {
            var memories = await _database.Table<Memory>().ToListAsync();
            return memories.Where(m => !string.IsNullOrEmpty(m.Category))
                          .Select(m => m.Category)
                          .Distinct()
                          .OrderBy(c => c)
                          .ToList();
        }

        public async Task<List<string>> GetTagsAsync()
        {
            var memories = await _database.Table<Memory>().ToListAsync();
            var allTags = new List<string>();
            
            foreach (var memory in memories.Where(m => !string.IsNullOrEmpty(m.Tags)))
            {
                var tags = memory.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(tag => tag.Trim())
                                    .Where(tag => !string.IsNullOrEmpty(tag));
                allTags.AddRange(tags);
            }
            
            return allTags.Distinct().OrderBy(t => t).ToList();
        }

        public void Dispose()
        {
            _database?.CloseAsync()?.Wait();
        }
    }
}