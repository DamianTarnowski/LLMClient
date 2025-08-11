using LLMClient.Models;
using LLMClient.Services;
using NUnit.Framework;
using System.IO;
using System.Threading.Tasks;

namespace LLMClient.Tests.Services
{
    [TestFixture]
    public class MemoryServiceTests
    {
        private MemoryService _memoryService;
        private string _testDbPath;

        [SetUp]
        public void SetUp()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_memory_{Guid.NewGuid()}.db3");
            _memoryService = new MemoryService(_testDbPath);
        }

        [TearDown]
        public void TearDown()
        {
            _memoryService?.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
            try
            {
                if (File.Exists(_testDbPath))
                {
                    File.Delete(_testDbPath);
                }
            }
            catch (IOException)
            {
                // File may still be locked, try again after a small delay
                Task.Delay(100).Wait();
                try
                {
                    if (File.Exists(_testDbPath))
                    {
                        File.Delete(_testDbPath);
                    }
                }
                catch (IOException)
                {
                    // Ignore if still can't delete - it will be cleaned up later
                    System.Diagnostics.Debug.WriteLine($"Could not delete test database: {_testDbPath}");
                }
            }
        }

        [Test]
        public async Task AddMemoryAsync_ShouldAddNewMemory()
        {
            // Arrange
            var memory = new Memory
            {
                Key = "test_key",
                Value = "test_value",
                Category = "test",
                Tags = "tag1,tag2",
                IsImportant = true
            };

            // Act
            var result = await _memoryService.AddMemoryAsync(memory);

            // Assert
            Assert.That(result, Is.GreaterThan(0));
            Assert.That(memory.Id, Is.GreaterThan(0));
            Assert.That(memory.CreatedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(memory.UpdatedAt, Is.Not.EqualTo(default(DateTime)));
        }

        [Test]
        public async Task GetMemoryByKeyAsync_ShouldReturnCorrectMemory()
        {
            // Arrange
            var memory = new Memory
            {
                Key = "unique_key",
                Value = "unique_value",
                Category = "test"
            };
            await _memoryService.AddMemoryAsync(memory);

            // Act
            var result = await _memoryService.GetMemoryByKeyAsync("unique_key");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Key, Is.EqualTo("unique_key"));
            Assert.That(result.Value, Is.EqualTo("unique_value"));
            Assert.That(result.Category, Is.EqualTo("test"));
        }

        [Test]
        public async Task GetMemoryByKeyAsync_ShouldReturnNullForNonExistentKey()
        {
            // Act
            var result = await _memoryService.GetMemoryByKeyAsync("non_existent_key");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task UpdateMemoryAsync_ShouldUpdateExistingMemory()
        {
            // Arrange
            var memory = new Memory
            {
                Key = "update_key",
                Value = "original_value",
                Category = "original"
            };
            await _memoryService.AddMemoryAsync(memory);
            var originalUpdatedAt = memory.UpdatedAt;

            // Wait a bit to ensure timestamp difference
            await Task.Delay(100);

            // Act
            memory.Value = "updated_value";
            memory.Category = "updated";
            var result = await _memoryService.UpdateMemoryAsync(memory);

            // Assert
            Assert.That(result, Is.GreaterThan(0));
            Assert.That(memory.UpdatedAt, Is.GreaterThan(originalUpdatedAt));

            var updatedMemory = await _memoryService.GetMemoryByKeyAsync("update_key");
            Assert.That(updatedMemory.Value, Is.EqualTo("updated_value"));
            Assert.That(updatedMemory.Category, Is.EqualTo("updated"));
        }

        [Test]
        public async Task DeleteMemoryAsync_ShouldRemoveMemory()
        {
            // Arrange
            var memory = new Memory
            {
                Key = "delete_key",
                Value = "delete_value"
            };
            await _memoryService.AddMemoryAsync(memory);

            // Act
            var result = await _memoryService.DeleteMemoryAsync(memory.Id);

            // Assert
            Assert.That(result, Is.GreaterThan(0));

            var deletedMemory = await _memoryService.GetMemoryByKeyAsync("delete_key");
            Assert.That(deletedMemory, Is.Null);
        }

        [Test]
        public async Task UpsertMemoryAsync_ShouldInsertNewMemory()
        {
            // Act
            var result = await _memoryService.UpsertMemoryAsync("new_key", "new_value", "category", "tag1,tag2", true);

            // Assert
            Assert.That(result, Is.GreaterThan(0));

            var memory = await _memoryService.GetMemoryByKeyAsync("new_key");
            Assert.That(memory, Is.Not.Null);
            Assert.That(memory.Value, Is.EqualTo("new_value"));
            Assert.That(memory.Category, Is.EqualTo("category"));
            Assert.That(memory.Tags, Is.EqualTo("tag1,tag2"));
            Assert.That(memory.IsImportant, Is.True);
        }

        [Test]
        public async Task UpsertMemoryAsync_ShouldUpdateExistingMemory()
        {
            // Arrange
            await _memoryService.UpsertMemoryAsync("existing_key", "original_value", "original", "old_tags", false);
            
            // Act
            var result = await _memoryService.UpsertMemoryAsync("existing_key", "updated_value", "updated", "new_tags", true);

            // Assert
            Assert.That(result, Is.GreaterThan(0));

            var memory = await _memoryService.GetMemoryByKeyAsync("existing_key");
            Assert.That(memory, Is.Not.Null);
            Assert.That(memory.Value, Is.EqualTo("updated_value"));
            Assert.That(memory.Category, Is.EqualTo("updated"));
            Assert.That(memory.Tags, Is.EqualTo("new_tags"));
            Assert.That(memory.IsImportant, Is.True);
        }

        [Test]
        public async Task SearchMemoriesAsync_ShouldFindMatchingMemories()
        {
            // Arrange
            await _memoryService.UpsertMemoryAsync("key1", "apple fruit", "food", "healthy,red", false);
            await _memoryService.UpsertMemoryAsync("key2", "banana fruit", "food", "healthy,yellow", false);
            await _memoryService.UpsertMemoryAsync("key3", "car vehicle", "transport", "fast,metal", false);

            // Act
            var results = await _memoryService.SearchMemoriesAsync("fruit");

            // Assert
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results.All(m => m.Value.Contains("fruit")), Is.True);
        }

        [Test]
        public async Task SearchMemoriesAsync_ShouldSearchInAllFields()
        {
            // Arrange
            await _memoryService.UpsertMemoryAsync("search_key", "some value", "search_category", "search_tag", false);

            // Act - Search in key
            var keyResults = await _memoryService.SearchMemoriesAsync("search_key");
            // Search in value
            var valueResults = await _memoryService.SearchMemoriesAsync("some value");
            // Search in category
            var categoryResults = await _memoryService.SearchMemoriesAsync("search_category");
            // Search in tags
            var tagResults = await _memoryService.SearchMemoriesAsync("search_tag");

            // Assert
            Assert.That(keyResults, Has.Count.EqualTo(1));
            Assert.That(valueResults, Has.Count.EqualTo(1));
            Assert.That(categoryResults, Has.Count.EqualTo(1));
            Assert.That(tagResults, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetMemoriesByCategoryAsync_ShouldFilterByCategory()
        {
            // Arrange
            await _memoryService.UpsertMemoryAsync("key1", "value1", "personal", "tag1", false);
            await _memoryService.UpsertMemoryAsync("key2", "value2", "personal", "tag2", false);
            await _memoryService.UpsertMemoryAsync("key3", "value3", "work", "tag3", false);

            // Act
            var personalMemories = await _memoryService.GetMemoriesByCategoryAsync("personal");
            var workMemories = await _memoryService.GetMemoriesByCategoryAsync("work");

            // Assert
            Assert.That(personalMemories, Has.Count.EqualTo(2));
            Assert.That(workMemories, Has.Count.EqualTo(1));
            Assert.That(personalMemories.All(m => m.Category == "personal"), Is.True);
            Assert.That(workMemories.All(m => m.Category == "work"), Is.True);
        }

        [Test]
        public async Task GetCategoriesAsync_ShouldReturnUniqueCategories()
        {
            // Arrange
            await _memoryService.UpsertMemoryAsync("key1", "value1", "category1", "", false);
            await _memoryService.UpsertMemoryAsync("key2", "value2", "category2", "", false);
            await _memoryService.UpsertMemoryAsync("key3", "value3", "category1", "", false); // Duplicate category
            await _memoryService.UpsertMemoryAsync("key4", "value4", "", "", false); // Empty category

            // Act
            var categories = await _memoryService.GetCategoriesAsync();

            // Assert
            Assert.That(categories, Has.Count.EqualTo(2));
            Assert.That(categories, Does.Contain("category1"));
            Assert.That(categories, Does.Contain("category2"));
            Assert.That(categories, Is.Ordered);
        }

        [Test]
        public async Task GetTagsAsync_ShouldReturnUniqueTagsFromAllMemories()
        {
            // Arrange
            await _memoryService.UpsertMemoryAsync("key1", "value1", "cat", "tag1,tag2", false);
            await _memoryService.UpsertMemoryAsync("key2", "value2", "cat", "tag2,tag3", false);
            await _memoryService.UpsertMemoryAsync("key3", "value3", "cat", " tag4 , tag1 ", false); // Test trimming
            await _memoryService.UpsertMemoryAsync("key4", "value4", "cat", "", false); // Empty tags

            // Act
            var tags = await _memoryService.GetTagsAsync();

            // Assert
            Assert.That(tags, Has.Count.EqualTo(4));
            Assert.That(tags, Does.Contain("tag1"));
            Assert.That(tags, Does.Contain("tag2"));
            Assert.That(tags, Does.Contain("tag3"));
            Assert.That(tags, Does.Contain("tag4"));
            Assert.That(tags, Is.Ordered);
        }

        [Test]
        public async Task GetAllMemoriesAsync_ShouldReturnAllMemoriesOrderedByUpdateTime()
        {
            // Arrange
            var memory1 = new Memory { Key = "key1", Value = "value1" };
            var memory2 = new Memory { Key = "key2", Value = "value2" };
            
            await _memoryService.AddMemoryAsync(memory1);
            await Task.Delay(100); // Ensure different timestamps
            await _memoryService.AddMemoryAsync(memory2);

            // Act
            var allMemories = await _memoryService.GetAllMemoriesAsync();

            // Assert
            Assert.That(allMemories, Has.Count.EqualTo(2));
            Assert.That(allMemories[0].UpdatedAt, Is.GreaterThanOrEqualTo(allMemories[1].UpdatedAt));
        }

        [Test]
        public async Task MemoryService_ShouldHandleEmptyDatabase()
        {
            // Act & Assert
            var allMemories = await _memoryService.GetAllMemoriesAsync();
            Assert.That(allMemories, Is.Empty);

            var categories = await _memoryService.GetCategoriesAsync();
            Assert.That(categories, Is.Empty);

            var tags = await _memoryService.GetTagsAsync();
            Assert.That(tags, Is.Empty);

            var searchResults = await _memoryService.SearchMemoriesAsync("anything");
            Assert.That(searchResults, Is.Empty);
        }

        [Test]
        public async Task MemoryService_ShouldHandleSpecialCharacters()
        {
            // Arrange
            var memory = new Memory
            {
                Key = "special_key",
                Value = "Value with 'quotes' and \"double quotes\" and unicode: 🧠",
                Category = "special-category",
                Tags = "unicode🏷️,quotes'test"
            };

            // Act
            await _memoryService.AddMemoryAsync(memory);
            var retrieved = await _memoryService.GetMemoryByKeyAsync("special_key");

            // Assert
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.Value, Is.EqualTo(memory.Value));
            Assert.That(retrieved.Category, Is.EqualTo(memory.Category));
            Assert.That(retrieved.Tags, Is.EqualTo(memory.Tags));
        }

        [Test]
        public async Task MemoryService_ShouldHandleLongText()
        {
            // Arrange
            var longValue = new string('A', 10000); // 10k characters
            var memory = new Memory
            {
                Key = "long_key",
                Value = longValue,
                Category = "test"
            };

            // Act
            await _memoryService.AddMemoryAsync(memory);
            var retrieved = await _memoryService.GetMemoryByKeyAsync("long_key");

            // Assert
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.Value, Is.EqualTo(longValue));
            Assert.That(retrieved.Value.Length, Is.EqualTo(10000));
        }
    }
}