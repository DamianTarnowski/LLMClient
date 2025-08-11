using LLMClient.Models;
using LLMClient.Services;
using LLMClient.ViewModels;
using Moq;
using NUnit.Framework;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace LLMClient.Tests.ViewModels
{
    [TestFixture]
    public class MemoryPageViewModelTests
    {
        private Mock<IMemoryService> _mockMemoryService;
        private MemoryPageViewModel _viewModel;
        private Mock<Microsoft.Maui.Controls.Page> _mockPage;

        [SetUp]
        public void SetUp()
        {
            _mockMemoryService = new Mock<IMemoryService>();
            _viewModel = new MemoryPageViewModel(_mockMemoryService.Object);

            // Mock Application.Current.MainPage for dialog operations
            _mockPage = new Mock<Microsoft.Maui.Controls.Page>();
            
            // Note: In real tests, you might want to use a test framework that properly mocks
            // Application.Current.MainPage, or inject a dialog service instead
        }

        [Test]
        public void MemoryPageViewModel_ShouldInitializeWithEmptyCollections()
        {
            // Assert
            Assert.That(_viewModel.Memories, Is.Not.Null);
            Assert.That(_viewModel.FilteredMemories, Is.Not.Null);
            Assert.That(_viewModel.Categories, Is.Not.Null);
            Assert.That(_viewModel.SearchTerm, Is.EqualTo(string.Empty));
            Assert.That(_viewModel.SelectedCategory, Is.EqualTo(string.Empty));
            Assert.That(_viewModel.IsEmpty, Is.False); // Default value
        }

        [Test]
        public void MemoryPageViewModel_ShouldHaveAllCommands()
        {
            // Assert
            Assert.That(_viewModel.BackToMainCommand, Is.Not.Null);
            Assert.That(_viewModel.RefreshCommand, Is.Not.Null);
            Assert.That(_viewModel.AddMemoryCommand, Is.Not.Null);
            Assert.That(_viewModel.EditMemoryCommand, Is.Not.Null);
            Assert.That(_viewModel.DeleteMemoryCommand, Is.Not.Null);
            Assert.That(_viewModel.FilterByCategoryCommand, Is.Not.Null);
        }

        [Test]
        public async Task InitializeAsync_ShouldCallRefresh()
        {
            // Arrange
            var memories = new List<Memory>
            {
                new Memory { Key = "key1", Value = "value1", Category = "cat1" },
                new Memory { Key = "key2", Value = "value2", Category = "cat2" }
            };

            var categories = new List<string> { "cat1", "cat2" };

            _mockMemoryService.Setup(x => x.GetAllMemoriesAsync()).ReturnsAsync(memories);
            _mockMemoryService.Setup(x => x.GetCategoriesAsync()).ReturnsAsync(categories);

            // Act
            await _viewModel.InitializeAsync();

            // Assert
            Assert.That(_viewModel.Memories.Count, Is.EqualTo(2));
            Assert.That(_viewModel.Categories.Count, Is.EqualTo(2));
            Assert.That(_viewModel.IsEmpty, Is.False);
            _mockMemoryService.Verify(x => x.GetAllMemoriesAsync(), Times.Once);
            _mockMemoryService.Verify(x => x.GetCategoriesAsync(), Times.Once);
        }

        [Test]
        public void SearchTerm_WhenChanged_ShouldApplyFilters()
        {
            // Arrange
            var memories = new List<Memory>
            {
                new Memory { Key = "apple", Value = "fruit", Category = "food" },
                new Memory { Key = "car", Value = "vehicle", Category = "transport" }
            };

            foreach (var memory in memories)
                _viewModel.Memories.Add(memory);

            // Act
            _viewModel.SearchTerm = "apple";

            // Assert
            Assert.That(_viewModel.FilteredMemories.Count, Is.EqualTo(1));
            Assert.That(_viewModel.FilteredMemories[0].Key, Is.EqualTo("apple"));
        }

        [Test]
        public void FilterByCategory_ShouldUpdateSelectedCategoryAndFilter()
        {
            // Arrange
            var memories = new List<Memory>
            {
                new Memory { Key = "key1", Value = "value1", Category = "personal" },
                new Memory { Key = "key2", Value = "value2", Category = "work" },
                new Memory { Key = "key3", Value = "value3", Category = "personal" }
            };

            foreach (var memory in memories)
                _viewModel.Memories.Add(memory);

            // Act
            if (_viewModel.FilterByCategoryCommand.CanExecute("personal"))
                _viewModel.FilterByCategoryCommand.Execute("personal");

            // Assert
            Assert.That(_viewModel.SelectedCategory, Is.EqualTo("personal"));
            Assert.That(_viewModel.FilteredMemories.Count, Is.EqualTo(2));
            Assert.That(_viewModel.FilteredMemories.All(m => m.Category == "personal"), Is.True);
        }

        [Test]
        public void ApplyFilters_ShouldFilterBySearchTermAndCategory()
        {
            // Arrange
            var memories = new List<Memory>
            {
                new Memory { Key = "apple", Value = "red fruit", Category = "food" },
                new Memory { Key = "banana", Value = "yellow fruit", Category = "food" },
                new Memory { Key = "car", Value = "red vehicle", Category = "transport" }
            };

            foreach (var memory in memories)
                _viewModel.Memories.Add(memory);

            // Act
            _viewModel.SearchTerm = "red";
            if (_viewModel.FilterByCategoryCommand.CanExecute("food"))
                _viewModel.FilterByCategoryCommand.Execute("food");

            // Assert
            Assert.That(_viewModel.FilteredMemories.Count, Is.EqualTo(1));
            Assert.That(_viewModel.FilteredMemories[0].Key, Is.EqualTo("apple"));
        }

        [Test]
        public void ApplyFilters_ShouldOrderByUpdatedAtDescending()
        {
            // Arrange
            var older = new Memory { Key = "old", Value = "value", UpdatedAt = DateTime.Now.AddDays(-1) };
            var newer = new Memory { Key = "new", Value = "value", UpdatedAt = DateTime.Now };

            _viewModel.Memories.Add(older);
            _viewModel.Memories.Add(newer);

            // Act
            _viewModel.SearchTerm = ""; // Trigger filter

            // Assert
            Assert.That(_viewModel.FilteredMemories.Count, Is.EqualTo(2));
            Assert.That(_viewModel.FilteredMemories[0].Key, Is.EqualTo("new")); // Newer first
            Assert.That(_viewModel.FilteredMemories[1].Key, Is.EqualTo("old"));
        }

        [Test]
        public void ApplyFilters_WithEmptyMemories_ShouldSetIsEmptyTrue()
        {
            // Arrange - No memories

            // Act
            _viewModel.SearchTerm = ""; // Trigger filter

            // Assert
            Assert.That(_viewModel.IsEmpty, Is.True);
            Assert.That(_viewModel.FilteredMemories.Count, Is.EqualTo(0));
        }

        [Test]
        public void ApplyFilters_WithMemories_ShouldSetIsEmptyFalse()
        {
            // Arrange
            _viewModel.Memories.Add(new Memory { Key = "key", Value = "value" });

            // Act
            _viewModel.SearchTerm = ""; // Trigger filter

            // Assert
            Assert.That(_viewModel.IsEmpty, Is.False);
            Assert.That(_viewModel.FilteredMemories.Count, Is.EqualTo(1));
        }

        [Test]
        public void SearchTerm_CaseInsensitiveSearch_ShouldWork()
        {
            // Arrange
            var memories = new List<Memory>
            {
                new Memory { Key = "Apple", Value = "FRUIT", Category = "Food" },
                new Memory { Key = "car", Value = "vehicle", Category = "transport" }
            };

            foreach (var memory in memories)
                _viewModel.Memories.Add(memory);

            // Act
            _viewModel.SearchTerm = "apple";

            // Assert
            Assert.That(_viewModel.FilteredMemories.Count, Is.EqualTo(1));
            Assert.That(_viewModel.FilteredMemories[0].Key, Is.EqualTo("Apple"));
        }

        [Test]
        public void SearchTerm_ShouldSearchInAllFields()
        {
            // Arrange
            var memories = new List<Memory>
            {
                new Memory { Key = "search_in_key", Value = "value", Category = "cat", Tags = "tag" },
                new Memory { Key = "key", Value = "search_in_value", Category = "cat", Tags = "tag" },
                new Memory { Key = "key", Value = "value", Category = "search_in_category", Tags = "tag" },
                new Memory { Key = "key", Value = "value", Category = "cat", Tags = "search_in_tags" },
                new Memory { Key = "no_match", Value = "nothing", Category = "empty", Tags = "none" }
            };

            foreach (var memory in memories)
                _viewModel.Memories.Add(memory);

            // Act
            _viewModel.SearchTerm = "search_in";

            // Assert
            Assert.That(_viewModel.FilteredMemories.Count, Is.EqualTo(4));
        }

        [Test]
        public void FilterByCategory_WithEmptyCategory_ShouldShowAllMemories()
        {
            // Arrange
            var memories = new List<Memory>
            {
                new Memory { Key = "key1", Value = "value1", Category = "personal" },
                new Memory { Key = "key2", Value = "value2", Category = "work" }
            };

            foreach (var memory in memories)
                _viewModel.Memories.Add(memory);

            // First filter by category
            if (_viewModel.FilterByCategoryCommand.CanExecute("personal"))
                _viewModel.FilterByCategoryCommand.Execute("personal");

            Assert.That(_viewModel.FilteredMemories.Count, Is.EqualTo(1));

            // Act - Clear filter
            if (_viewModel.FilterByCategoryCommand.CanExecute(""))
                _viewModel.FilterByCategoryCommand.Execute("");

            // Assert
            Assert.That(_viewModel.SelectedCategory, Is.EqualTo(string.Empty));
            Assert.That(_viewModel.FilteredMemories.Count, Is.EqualTo(2));
        }

        [Test]
        public void PropertyChanged_ShouldBeRaisedForAllProperties()
        {
            // Arrange
            var propertyNames = new List<string>();
            _viewModel.PropertyChanged += (sender, e) =>
            {
                propertyNames.Add(e.PropertyName);
            };

            // Act
            _viewModel.SearchTerm = "test";
            _viewModel.SelectedCategory = "category";
            _viewModel.IsEmpty = true;

            // Assert
            Assert.That(propertyNames, Does.Contain("SearchTerm"));
            Assert.That(propertyNames, Does.Contain("SelectedCategory"));
            Assert.That(propertyNames, Does.Contain("IsEmpty"));
        }

        [Test]
        public void ViewModel_ShouldHandleNullMemoryService()
        {
            // This test ensures the constructor doesn't throw with null service
            // In practice, you'd want to validate this or use dependency injection properly
            
            Assert.DoesNotThrow(() =>
            {
                var viewModel = new MemoryPageViewModel(null);
                Assert.That(viewModel, Is.Not.Null);
            });
        }

        [Test]
        public void MemoryPageViewModel_ShouldImplementINotifyPropertyChanged()
        {
            // Assert
            Assert.That(_viewModel, Is.InstanceOf<System.ComponentModel.INotifyPropertyChanged>());
        }

        [Test]
        public void BackToMainCommand_ShouldBeExecutable()
        {
            // Assert
            Assert.That(_viewModel.BackToMainCommand.CanExecute(null), Is.True);
        }
    }
}