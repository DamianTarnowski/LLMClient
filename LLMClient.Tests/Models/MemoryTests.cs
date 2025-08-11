using LLMClient.Models;
using NUnit.Framework;
using System.ComponentModel;

namespace LLMClient.Tests.Models
{
    [TestFixture]
    public class MemoryTests
    {
        [Test]
        public void Memory_ShouldImplementINotifyPropertyChanged()
        {
            // Arrange & Act
            var memory = new Memory();

            // Assert
            Assert.That(memory, Is.InstanceOf<INotifyPropertyChanged>());
        }

        [Test]
        public void Memory_ShouldHaveCorrectDefaultValues()
        {
            // Act
            var memory = new Memory();

            // Assert
            Assert.That(memory.Id, Is.EqualTo(0));
            Assert.That(memory.Key, Is.EqualTo(string.Empty));
            Assert.That(memory.Value, Is.EqualTo(string.Empty));
            Assert.That(memory.Category, Is.EqualTo(string.Empty));
            Assert.That(memory.Tags, Is.EqualTo(string.Empty));
            Assert.That(memory.IsImportant, Is.False);
            Assert.That(memory.CreatedAt, Is.EqualTo(default(DateTime)));
            Assert.That(memory.UpdatedAt, Is.EqualTo(default(DateTime)));
        }

        [Test]
        public void Memory_Key_ShouldRaisePropertyChanged()
        {
            // Arrange
            var memory = new Memory();
            var propertyChangedRaised = false;
            string changedPropertyName = null;

            memory.PropertyChanged += (sender, e) =>
            {
                propertyChangedRaised = true;
                changedPropertyName = e.PropertyName;
            };

            // Act
            memory.Key = "test_key";

            // Assert
            Assert.That(propertyChangedRaised, Is.True);
            Assert.That(changedPropertyName, Is.EqualTo("Key"));
            Assert.That(memory.Key, Is.EqualTo("test_key"));
        }

        [Test]
        public void Memory_Value_ShouldRaisePropertyChanged()
        {
            // Arrange
            var memory = new Memory();
            var propertyChangedRaised = false;
            string changedPropertyName = null;

            memory.PropertyChanged += (sender, e) =>
            {
                propertyChangedRaised = true;
                changedPropertyName = e.PropertyName;
            };

            // Act
            memory.Value = "test_value";

            // Assert
            Assert.That(propertyChangedRaised, Is.True);
            Assert.That(changedPropertyName, Is.EqualTo("Value"));
            Assert.That(memory.Value, Is.EqualTo("test_value"));
        }

        [Test]
        public void Memory_Category_ShouldRaisePropertyChanged()
        {
            // Arrange
            var memory = new Memory();
            var propertyChangedRaised = false;

            memory.PropertyChanged += (sender, e) =>
            {
                propertyChangedRaised = true;
            };

            // Act
            memory.Category = "test_category";

            // Assert
            Assert.That(propertyChangedRaised, Is.True);
            Assert.That(memory.Category, Is.EqualTo("test_category"));
        }

        [Test]
        public void Memory_Tags_ShouldRaisePropertyChanged()
        {
            // Arrange
            var memory = new Memory();
            var propertyChangedRaised = false;

            memory.PropertyChanged += (sender, e) =>
            {
                propertyChangedRaised = true;
            };

            // Act
            memory.Tags = "tag1,tag2,tag3";

            // Assert
            Assert.That(propertyChangedRaised, Is.True);
            Assert.That(memory.Tags, Is.EqualTo("tag1,tag2,tag3"));
        }

        [Test]
        public void Memory_IsImportant_ShouldRaisePropertyChanged()
        {
            // Arrange
            var memory = new Memory();
            var propertyChangedRaised = false;

            memory.PropertyChanged += (sender, e) =>
            {
                propertyChangedRaised = true;
            };

            // Act
            memory.IsImportant = true;

            // Assert
            Assert.That(propertyChangedRaised, Is.True);
            Assert.That(memory.IsImportant, Is.True);
        }

        [Test]
        public void Memory_CreatedAt_ShouldRaisePropertyChanged()
        {
            // Arrange
            var memory = new Memory();
            var propertyChangedRaised = false;
            var testDate = DateTime.Now;

            memory.PropertyChanged += (sender, e) =>
            {
                propertyChangedRaised = true;
            };

            // Act
            memory.CreatedAt = testDate;

            // Assert
            Assert.That(propertyChangedRaised, Is.True);
            Assert.That(memory.CreatedAt, Is.EqualTo(testDate));
        }

        [Test]
        public void Memory_UpdatedAt_ShouldRaisePropertyChanged()
        {
            // Arrange
            var memory = new Memory();
            var propertyChangedRaised = false;
            var testDate = DateTime.Now;

            memory.PropertyChanged += (sender, e) =>
            {
                propertyChangedRaised = true;
            };

            // Act
            memory.UpdatedAt = testDate;

            // Assert
            Assert.That(propertyChangedRaised, Is.True);
            Assert.That(memory.UpdatedAt, Is.EqualTo(testDate));
        }

        [Test]
        public void Memory_ShouldNotRaisePropertyChangedWhenSettingSameValue()
        {
            // Arrange
            var memory = new Memory { Key = "test" };
            var propertyChangedCount = 0;

            memory.PropertyChanged += (sender, e) =>
            {
                propertyChangedCount++;
            };

            // Act
            memory.Key = "test"; // Same value

            // Assert
            Assert.That(propertyChangedCount, Is.EqualTo(0));
        }

        [Test]
        public void Memory_ShouldHandleNullValues()
        {
            // Arrange
            var memory = new Memory();

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() =>
            {
                memory.Key = null;
                memory.Value = null;
                memory.Category = null;
                memory.Tags = null;
            });

            // Values should be converted to empty strings by the setters
            Assert.That(memory.Key, Is.Not.Null);
            Assert.That(memory.Value, Is.Not.Null);
            Assert.That(memory.Category, Is.Not.Null);
            Assert.That(memory.Tags, Is.Not.Null);
        }

        [Test]
        public void Memory_ShouldHandleLongStrings()
        {
            // Arrange
            var memory = new Memory();
            var longString = new string('A', 10000);

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() =>
            {
                memory.Key = longString;
                memory.Value = longString;
                memory.Category = longString;
                memory.Tags = longString;
            });

            Assert.That(memory.Key, Is.EqualTo(longString));
            Assert.That(memory.Value, Is.EqualTo(longString));
            Assert.That(memory.Category, Is.EqualTo(longString));
            Assert.That(memory.Tags, Is.EqualTo(longString));
        }

        [Test]
        public void Memory_ShouldHandleSpecialCharacters()
        {
            // Arrange
            var memory = new Memory();
            var specialString = "Test with 'quotes' and \"double quotes\" and unicode: 🧠 and newlines\nand tabs\t";

            // Act
            memory.Key = specialString;
            memory.Value = specialString;
            memory.Category = specialString;
            memory.Tags = specialString;

            // Assert
            Assert.That(memory.Key, Is.EqualTo(specialString));
            Assert.That(memory.Value, Is.EqualTo(specialString));
            Assert.That(memory.Category, Is.EqualTo(specialString));
            Assert.That(memory.Tags, Is.EqualTo(specialString));
        }

        [Test]
        public void Memory_PropertyChanged_ShouldProvideCorrectSender()
        {
            // Arrange
            var memory = new Memory();
            object receivedSender = null;

            memory.PropertyChanged += (sender, e) =>
            {
                receivedSender = sender;
            };

            // Act
            memory.Key = "test";

            // Assert
            Assert.That(receivedSender, Is.SameAs(memory));
        }

        [Test]
        public void Memory_MultiplePropertyChanges_ShouldRaiseMultipleEvents()
        {
            // Arrange
            var memory = new Memory();
            var propertyNames = new List<string>();

            memory.PropertyChanged += (sender, e) =>
            {
                propertyNames.Add(e.PropertyName);
            };

            // Act
            memory.Key = "key";
            memory.Value = "value";
            memory.Category = "category";

            // Assert
            Assert.That(propertyNames, Has.Count.EqualTo(3));
            Assert.That(propertyNames, Does.Contain("Key"));
            Assert.That(propertyNames, Does.Contain("Value"));
            Assert.That(propertyNames, Does.Contain("Category"));
        }
    }
}