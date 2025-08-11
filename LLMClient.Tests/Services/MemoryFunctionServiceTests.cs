using LLMClient.Models;
using LLMClient.Services;
using Moq;
using NUnit.Framework;
using System.Threading.Tasks;

namespace LLMClient.Tests.Services
{
    [TestFixture]
    public class MemoryFunctionServiceTests
    {
        private Mock<IMemoryService> _mockMemoryService;
        private MemoryFunctionService _memoryFunctionService;

        [SetUp]
        public void SetUp()
        {
            _mockMemoryService = new Mock<IMemoryService>();
            _memoryFunctionService = new MemoryFunctionService(_mockMemoryService.Object);
        }

        [Test]
        public async Task RememberInformationAsync_WithValidData_ShouldSucceed()
        {
            // Arrange
            _mockMemoryService
                .Setup(x => x.UpsertMemoryAsync("user_name", "John", "personal", "identity", true))
                .ReturnsAsync(1);

            // Act
            var result = await _memoryFunctionService.RememberInformationAsync("user_name", "John", "personal", "identity", true);

            // Assert
            Assert.That(result, Does.Contain("✅ Zapamiętałem: user_name = John"));
            _mockMemoryService.Verify(x => x.UpsertMemoryAsync("user_name", "John", "personal", "identity", true), Times.Once);
        }

        [Test]
        public async Task RememberInformationAsync_WithEmptyKey_ShouldReturnError()
        {
            // Act
            var result = await _memoryFunctionService.RememberInformationAsync("", "value", "", "", false);

            // Assert
            Assert.That(result, Does.Contain("Błąd: Klucz i wartość nie mogą być puste"));
            _mockMemoryService.Verify(x => x.UpsertMemoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public async Task RememberInformationAsync_WithEmptyValue_ShouldReturnError()
        {
            // Act
            var result = await _memoryFunctionService.RememberInformationAsync("key", "", "", "", false);

            // Assert
            Assert.That(result, Does.Contain("Błąd: Klucz i wartość nie mogą być puste"));
            _mockMemoryService.Verify(x => x.UpsertMemoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public async Task RememberInformationAsync_WhenServiceFails_ShouldReturnErrorMessage()
        {
            // Arrange
            _mockMemoryService
                .Setup(x => x.UpsertMemoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(0);

            // Act
            var result = await _memoryFunctionService.RememberInformationAsync("key", "value", "", "", false);

            // Assert
            Assert.That(result, Does.Contain("❌ Nie udało się zapamiętać informacji"));
        }

        [Test]
        public async Task RecallInformationAsync_WithDirectMatch_ShouldReturnMemory()
        {
            // Arrange
            var memory = new Memory
            {
                Key = "user_name",
                Value = "John",
                Category = "personal",
                Tags = "identity",
                IsImportant = true,
                UpdatedAt = DateTime.Now
            };

            _mockMemoryService
                .Setup(x => x.GetMemoryByKeyAsync("user_name"))
                .ReturnsAsync(memory);

            // Act
            var result = await _memoryFunctionService.RecallInformationAsync("user_name");

            // Assert
            Assert.That(result, Does.Contain("🔑 **user_name**: John"));
            Assert.That(result, Does.Contain("📂 Kategoria: personal"));
            Assert.That(result, Does.Contain("🏷️ Tagi: identity"));
            Assert.That(result, Does.Contain("⭐ Ważne"));
        }

        [Test]
        public async Task RecallInformationAsync_WithSearchResults_ShouldReturnMultipleMatches()
        {
            // Arrange
            var memories = new List<Memory>
            {
                new Memory { Key = "hobby1", Value = "reading", Category = "interests", UpdatedAt = DateTime.Now },
                new Memory { Key = "hobby2", Value = "gaming", Category = "interests", UpdatedAt = DateTime.Now.AddMinutes(-1) }
            };

            _mockMemoryService
                .Setup(x => x.GetMemoryByKeyAsync("hobby"))
                .ReturnsAsync((Memory)null);

            _mockMemoryService
                .Setup(x => x.SearchMemoriesAsync("hobby"))
                .ReturnsAsync(memories);

            // Act
            var result = await _memoryFunctionService.RecallInformationAsync("hobby");

            // Assert
            Assert.That(result, Does.Contain("🔍 Znaleziono 2 informacji dla 'hobby'"));
            Assert.That(result, Does.Contain("🔑 **hobby1**: reading"));
            Assert.That(result, Does.Contain("🔑 **hobby2**: gaming"));
        }

        [Test]
        public async Task RecallInformationAsync_WithNoResults_ShouldReturnNotFound()
        {
            // Arrange
            _mockMemoryService
                .Setup(x => x.GetMemoryByKeyAsync("nonexistent"))
                .ReturnsAsync((Memory)null);

            _mockMemoryService
                .Setup(x => x.SearchMemoriesAsync("nonexistent"))
                .ReturnsAsync(new List<Memory>());

            // Act
            var result = await _memoryFunctionService.RecallInformationAsync("nonexistent");

            // Assert
            Assert.That(result, Does.Contain("❌ Nie znalazłem informacji związanych z 'nonexistent'"));
        }

        [Test]
        public async Task RecallInformationAsync_WithEmptySearchTerm_ShouldReturnError()
        {
            // Act
            var result = await _memoryFunctionService.RecallInformationAsync("");

            // Assert
            Assert.That(result, Does.Contain("Błąd: Podaj klucz lub frazę do wyszukania"));
            _mockMemoryService.Verify(x => x.GetMemoryByKeyAsync(It.IsAny<string>()), Times.Never);
            _mockMemoryService.Verify(x => x.SearchMemoriesAsync(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ListMemoriesAsync_WithNoCategory_ShouldReturnAllMemories()
        {
            // Arrange
            var memories = new List<Memory>
            {
                new Memory { Key = "key1", Value = "value1", Category = "cat1", UpdatedAt = DateTime.Now },
                new Memory { Key = "key2", Value = "value2", Category = "cat2", UpdatedAt = DateTime.Now.AddMinutes(-1) }
            };

            _mockMemoryService
                .Setup(x => x.GetAllMemoriesAsync())
                .ReturnsAsync(memories);

            // Act
            var result = await _memoryFunctionService.ListMemoriesAsync("");

            // Assert
            Assert.That(result, Does.Contain("📝 Wszystkie zapamiętane informacje (2)"));
            Assert.That(result, Does.Contain("🔑 **key1**: value1"));
            Assert.That(result, Does.Contain("🔑 **key2**: value2"));
        }

        [Test]
        public async Task ListMemoriesAsync_WithCategory_ShouldReturnFilteredMemories()
        {
            // Arrange
            var memories = new List<Memory>
            {
                new Memory { Key = "key1", Value = "value1", Category = "personal", UpdatedAt = DateTime.Now }
            };

            _mockMemoryService
                .Setup(x => x.GetMemoriesByCategoryAsync("personal"))
                .ReturnsAsync(memories);

            // Act
            var result = await _memoryFunctionService.ListMemoriesAsync("personal");

            // Assert
            Assert.That(result, Does.Contain("📝 Wspomnienia w kategorii 'personal' (1)"));
            Assert.That(result, Does.Contain("🔑 **key1**: value1"));
        }

        [Test]
        public async Task ListMemoriesAsync_WithNoMemories_ShouldReturnEmptyMessage()
        {
            // Arrange
            _mockMemoryService
                .Setup(x => x.GetAllMemoriesAsync())
                .ReturnsAsync(new List<Memory>());

            // Act
            var result = await _memoryFunctionService.ListMemoriesAsync("");

            // Assert
            Assert.That(result, Does.Contain("📝 Nie mam jeszcze żadnych wspomnień"));
        }

        [Test]
        public async Task UpdateMemoryAsync_WithValidData_ShouldSucceed()
        {
            // Arrange
            var existingMemory = new Memory { Key = "test_key", Value = "old_value" };

            _mockMemoryService
                .Setup(x => x.GetMemoryByKeyAsync("test_key"))
                .ReturnsAsync(existingMemory);

            _mockMemoryService
                .Setup(x => x.UpsertMemoryAsync("test_key", "new_value", "category", "tags", true))
                .ReturnsAsync(1);

            // Act
            var result = await _memoryFunctionService.UpdateMemoryAsync("test_key", "new_value", "category", "tags", true);

            // Assert
            Assert.That(result, Does.Contain("✅ Zaktualizowałem informację: test_key = new_value"));
            _mockMemoryService.Verify(x => x.UpsertMemoryAsync("test_key", "new_value", "category", "tags", true), Times.Once);
        }

        [Test]
        public async Task UpdateMemoryAsync_WithNonExistentKey_ShouldReturnError()
        {
            // Arrange
            _mockMemoryService
                .Setup(x => x.GetMemoryByKeyAsync("nonexistent"))
                .ReturnsAsync((Memory)null);

            // Act
            var result = await _memoryFunctionService.UpdateMemoryAsync("nonexistent", "value", "", "", false);

            // Assert
            Assert.That(result, Does.Contain("❌ Nie znaleziono informacji o kluczu 'nonexistent'"));
            _mockMemoryService.Verify(x => x.UpsertMemoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public async Task ForgetInformationAsync_WithValidKey_ShouldSucceed()
        {
            // Arrange
            var memory = new Memory { Id = 1, Key = "forget_key", Value = "value" };

            _mockMemoryService
                .Setup(x => x.GetMemoryByKeyAsync("forget_key"))
                .ReturnsAsync(memory);

            _mockMemoryService
                .Setup(x => x.DeleteMemoryAsync(1))
                .ReturnsAsync(1);

            // Act
            var result = await _memoryFunctionService.ForgetInformationAsync("forget_key");

            // Assert
            Assert.That(result, Does.Contain("✅ Zapomniałem informację: forget_key"));
            _mockMemoryService.Verify(x => x.DeleteMemoryAsync(1), Times.Once);
        }

        [Test]
        public async Task ForgetInformationAsync_WithNonExistentKey_ShouldReturnError()
        {
            // Arrange
            _mockMemoryService
                .Setup(x => x.GetMemoryByKeyAsync("nonexistent"))
                .ReturnsAsync((Memory)null);

            // Act
            var result = await _memoryFunctionService.ForgetInformationAsync("nonexistent");

            // Assert
            Assert.That(result, Does.Contain("❌ Nie znaleziono informacji o kluczu 'nonexistent'"));
            _mockMemoryService.Verify(x => x.DeleteMemoryAsync(It.IsAny<int>()), Times.Never);
        }

        [Test]
        public async Task GetMemoryCategoriesAsync_ShouldReturnCategories()
        {
            // Arrange
            var categories = new List<string> { "personal", "work", "hobbies" };

            _mockMemoryService
                .Setup(x => x.GetCategoriesAsync())
                .ReturnsAsync(categories);

            // Act
            var result = await _memoryFunctionService.GetMemoryCategoriesAsync();

            // Assert
            Assert.That(result, Does.Contain("📂 Dostępne kategorie (3): personal, work, hobbies"));
        }

        [Test]
        public async Task GetMemoryCategoriesAsync_WithNoCategories_ShouldReturnEmptyMessage()
        {
            // Arrange
            _mockMemoryService
                .Setup(x => x.GetCategoriesAsync())
                .ReturnsAsync(new List<string>());

            // Act
            var result = await _memoryFunctionService.GetMemoryCategoriesAsync();

            // Assert
            Assert.That(result, Does.Contain("📂 Nie ma jeszcze żadnych kategorii"));
        }

        [Test]
        public async Task MemoryFunctionService_ShouldHandleExceptions()
        {
            // Arrange
            _mockMemoryService
                .Setup(x => x.UpsertMemoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _memoryFunctionService.RememberInformationAsync("key", "value", "", "", false);

            // Assert
            Assert.That(result, Does.Contain("❌ Błąd podczas zapisywania pamięci: Database error"));
        }

        [Test]
        public void MemoryFunctionService_ShouldFormatMemoryResultCorrectly()
        {
            // This test would be for the private FormatMemoryResult method
            // Since it's private, we test it indirectly through RecallInformationAsync

            // Arrange
            var memory = new Memory
            {
                Key = "test_key",
                Value = "test_value",
                Category = "test_category",
                Tags = "tag1,tag2",
                IsImportant = true,
                UpdatedAt = new DateTime(2024, 1, 1, 12, 0, 0)
            };

            _mockMemoryService
                .Setup(x => x.GetMemoryByKeyAsync("test_key"))
                .ReturnsAsync(memory);

            // Act
            var result = _memoryFunctionService.RecallInformationAsync("test_key").Result;

            // Assert
            Assert.That(result, Does.Contain("🔑 **test_key**: test_value"));
            Assert.That(result, Does.Contain("📂 Kategoria: test_category"));
            Assert.That(result, Does.Contain("🏷️ Tagi: tag1,tag2"));
            Assert.That(result, Does.Contain("⭐ Ważne"));
            Assert.That(result, Does.Contain("📅 Zaktualizowano: 2024-01-01 12:00"));
        }
    }
}