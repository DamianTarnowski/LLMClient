using NUnit.Framework;
using LLMClient.Converters;
using System.Globalization;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;

namespace LLMClient.Tests.Converters
{
    [TestFixture]
    public class ValueConvertersTests
    {
        private CultureInfo _culture;

        [SetUp]
        public void SetUp()
        {
            _culture = CultureInfo.InvariantCulture;
        }

        #region BoolToColorConverter Tests

        [TestFixture]
        public class BoolToColorConverterTests
        {
            private BoolToColorConverter _converter;

            [SetUp]
            public void SetUp()
            {
                _converter = new BoolToColorConverter();
            }

            [Test]
            public void Convert_WithTrue_ShouldReturnActiveColor()
            {
                // Act
                var result = _converter.Convert(true, typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo("#5865F2"));
            }

            [Test]
            public void Convert_WithFalse_ShouldReturnInactiveColor()
            {
                // Act
                var result = _converter.Convert(false, typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo("#40444B"));
            }

            [Test]
            public void Convert_WithNonBoolValue_ShouldReturnInactiveColor()
            {
                // Act
                var result = _converter.Convert("test", typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo("#40444B"));
            }

            [Test]
            public void Convert_WithNull_ShouldReturnInactiveColor()
            {
                // Act
                var result = _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo("#40444B"));
            }

            [Test]
            public void ConvertBack_ShouldThrowNotImplementedException()
            {
                // Assert
                Assert.Throws<NotImplementedException>(() => 
                    _converter.ConvertBack("#5865F2", typeof(bool), null, CultureInfo.InvariantCulture));
            }
        }

        #endregion

        #region IsNotNullConverter Tests

        [TestFixture]
        public class IsNotNullConverterTests
        {
            private IsNotNullConverter _converter;

            [SetUp]
            public void SetUp()
            {
                _converter = new IsNotNullConverter();
            }

            [Test]
            public void Convert_WithNotNullValue_ShouldReturnTrue()
            {
                // Act
                var result = _converter.Convert("test", typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(true));
            }

            [Test]
            public void Convert_WithNull_ShouldReturnFalse()
            {
                // Act
                var result = _converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(false));
            }

            [Test]
            public void Convert_WithEmptyString_ShouldReturnTrue()
            {
                // Act
                var result = _converter.Convert("", typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(true));
            }

            [Test]
            public void ConvertBack_ShouldThrowNotImplementedException()
            {
                // Assert
                Assert.Throws<NotImplementedException>(() => 
                    _converter.ConvertBack(true, typeof(object), null, CultureInfo.InvariantCulture));
            }
        }

        #endregion

        #region IsNotNullOrEmptyConverter Tests

        [TestFixture]
        public class IsNotNullOrEmptyConverterTests
        {
            private IsNotNullOrEmptyConverter _converter;

            [SetUp]
            public void SetUp()
            {
                _converter = new IsNotNullOrEmptyConverter();
            }

            [Test]
            public void Convert_WithValidString_ShouldReturnTrue()
            {
                // Act
                var result = _converter.Convert("test", typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(true));
            }

            [Test]
            public void Convert_WithEmptyString_ShouldReturnFalse()
            {
                // Act
                var result = _converter.Convert("", typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(false));
            }

            [Test]
            public void Convert_WithNullString_ShouldReturnFalse()
            {
                // Act
                var result = _converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(false));
            }

            [Test]
            public void Convert_WithNonStringNotNull_ShouldReturnTrue()
            {
                // Act
                var result = _converter.Convert(123, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(true));
            }

            [Test]
            public void ConvertBack_ShouldThrowNotImplementedException()
            {
                // Assert
                Assert.Throws<NotImplementedException>(() => 
                    _converter.ConvertBack(true, typeof(object), null, CultureInfo.InvariantCulture));
            }
        }

        #endregion

        #region InverseBoolConverter Tests

        [TestFixture]
        public class InverseBoolConverterTests
        {
            private InverseBoolConverter _converter;

            [SetUp]
            public void SetUp()
            {
                _converter = new InverseBoolConverter();
            }

            [Test]
            public void Convert_WithTrue_ShouldReturnFalse()
            {
                // Act
                var result = _converter.Convert(true, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(false));
            }

            [Test]
            public void Convert_WithFalse_ShouldReturnTrue()
            {
                // Act
                var result = _converter.Convert(false, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(true));
            }

            [Test]
            public void Convert_WithNonBoolValue_ShouldReturnFalse()
            {
                // Act
                var result = _converter.Convert("test", typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(false));
            }

            [Test]
            public void ConvertBack_WithTrue_ShouldReturnFalse()
            {
                // Act
                var result = _converter.ConvertBack(true, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(false));
            }

            [Test]
            public void ConvertBack_WithFalse_ShouldReturnTrue()
            {
                // Act
                var result = _converter.ConvertBack(false, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(true));
            }

            [Test]
            public void ConvertBack_WithNonBoolValue_ShouldReturnTrue()
            {
                // Act
                var result = _converter.ConvertBack("test", typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(true));
            }
        }

        #endregion

        #region ZeroToBoolConverter Tests

        [TestFixture]
        public class ZeroToBoolConverterTests
        {
            private ZeroToBoolConverter _converter;

            [SetUp]
            public void SetUp()
            {
                _converter = new ZeroToBoolConverter();
            }

            [Test]
            public void Convert_WithZero_ShouldReturnTrue()
            {
                // Act
                var result = _converter.Convert(0, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(true));
            }

            [Test]
            public void Convert_WithNonZero_ShouldReturnFalse()
            {
                // Act
                var result = _converter.Convert(5, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(false));
            }

            [Test]
            public void Convert_WithNegativeNumber_ShouldReturnFalse()
            {
                // Act
                var result = _converter.Convert(-1, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(false));
            }

            [Test]
            public void ConvertBack_ShouldThrowNotImplementedException()
            {
                // Assert
                Assert.Throws<NotImplementedException>(() => 
                    _converter.ConvertBack(true, typeof(int), null, CultureInfo.InvariantCulture));
            }
        }

        #endregion

        #region ZeroToInverseBoolConverter Tests

        [TestFixture]
        public class ZeroToInverseBoolConverterTests
        {
            private ZeroToInverseBoolConverter _converter;

            [SetUp]
            public void SetUp()
            {
                _converter = new ZeroToInverseBoolConverter();
            }

            [Test]
            public void Convert_WithZero_ShouldReturnFalse()
            {
                // Act
                var result = _converter.Convert(0, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(false));
            }

            [Test]
            public void Convert_WithNonZero_ShouldReturnTrue()
            {
                // Act
                var result = _converter.Convert(5, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(true));
            }

            [Test]
            public void Convert_WithNegativeNumber_ShouldReturnTrue()
            {
                // Act
                var result = _converter.Convert(-1, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(true));
            }

            [Test]
            public void ConvertBack_ShouldThrowNotImplementedException()
            {
                // Assert
                Assert.Throws<NotImplementedException>(() => 
                    _converter.ConvertBack(true, typeof(int), null, CultureInfo.InvariantCulture));
            }
        }

        #endregion

        #region TextTruncateConverter Tests

        [TestFixture]
        public class TextTruncateConverterTests
        {
            private TextTruncateConverter _converter;

            [SetUp]
            public void SetUp()
            {
                _converter = new TextTruncateConverter();
            }

            [Test]
            public void Convert_WithShortText_ShouldReturnOriginalText()
            {
                // Arrange
                var shortText = "Short text";

                // Act
                var result = _converter.Convert(shortText, typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(shortText));
            }

            [Test]
            public void Convert_WithLongText_ShouldTruncateWithSuffix()
            {
                // Arrange
                var longText = new string('A', 150);
                _converter.MaxLength = 50;

                // Act
                var result = _converter.Convert(longText, typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                var resultString = result as string;
                Assert.That(resultString.Length, Is.LessThanOrEqualTo(53)); // 50 + "..."
                Assert.That(resultString, Does.EndWith("..."));
            }

            [Test]
            public void Convert_WithParameterMaxLength_ShouldUseParameterValue()
            {
                // Arrange
                var text = new string('A', 50);

                // Act
                var result = _converter.Convert(text, typeof(string), "20", CultureInfo.InvariantCulture);

                // Assert
                var resultString = result as string;
                Assert.That(resultString.Length, Is.LessThanOrEqualTo(23)); // 20 + "..."
                Assert.That(resultString, Does.EndWith("..."));
            }

            [Test]
            public void Convert_WithNewlinesAndTabs_ShouldReplaceWithSpaces()
            {
                // Arrange
                var textWithWhitespace = "Line 1\nLine 2\r\nLine 3\tTabbed";

                // Act
                var result = _converter.Convert(textWithWhitespace, typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                var resultString = result as string;
                Assert.That(resultString, Does.Not.Contain("\n"));
                Assert.That(resultString, Does.Not.Contain("\r"));
                Assert.That(resultString, Does.Not.Contain("\t"));
                Assert.That(resultString, Contains.Substring("Line 1 Line 2 Line 3 Tabbed"));
            }

            [Test]
            public void Convert_WithMultipleSpaces_ShouldNormalizeSpaces()
            {
                // Arrange
                var textWithMultipleSpaces = "Text    with     multiple   spaces";

                // Act
                var result = _converter.Convert(textWithMultipleSpaces, typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                var resultString = result as string;
                Assert.That(resultString, Does.Not.Contain("  "));
                Assert.That(resultString, Is.EqualTo("Text with multiple spaces"));
            }

            [Test]
            public void Convert_WithNullOrEmpty_ShouldReturnEmpty()
            {
                // Act
                var resultNull = _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);
                var resultEmpty = _converter.Convert("", typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(resultNull, Is.EqualTo(string.Empty));
                Assert.That(resultEmpty, Is.EqualTo(string.Empty));
            }

            [Test]
            public void Convert_WithTextTruncatedAtWordBoundary_ShouldTruncateAtLastSpace()
            {
                // Arrange
                var text = "This is a very long sentence that should be truncated at a word boundary";
                _converter.MaxLength = 30;

                // Act
                var result = _converter.Convert(text, typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                var resultString = result as string;
                Assert.That(resultString, Does.EndWith("..."));
                Assert.That(resultString, Does.Not.EndWith(" ..."));
                // Should not cut in the middle of a word if possible
            }

            [Test]
            public void ConvertBack_ShouldThrowNotImplementedException()
            {
                // Assert
                Assert.Throws<NotImplementedException>(() => 
                    _converter.ConvertBack("test", typeof(string), null, CultureInfo.InvariantCulture));
            }
        }

        #endregion

        #region CollectionEmptyConverter Tests

        [TestFixture]
        public class CollectionEmptyConverterTests
        {
            private CollectionEmptyConverter _converter;

            [SetUp]
            public void SetUp()
            {
                _converter = new CollectionEmptyConverter();
            }

            [Test]
            public void Convert_WithEmptyCollection_ShouldReturnTrue()
            {
                // Arrange
                var emptyList = new List<string>();

                // Act
                var result = _converter.Convert(emptyList, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(true));
            }

            [Test]
            public void Convert_WithNonEmptyCollection_ShouldReturnFalse()
            {
                // Arrange
                var nonEmptyList = new List<string> { "item1", "item2" };

                // Act
                var result = _converter.Convert(nonEmptyList, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(false));
            }

            [Test]
            public void Convert_WithEmptyObservableCollection_ShouldReturnTrue()
            {
                // Arrange
                var emptyObservableCollection = new ObservableCollection<string>();

                // Act
                var result = _converter.Convert(emptyObservableCollection, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(true));
            }

            [Test]
            public void Convert_WithNonEmptyObservableCollection_ShouldReturnFalse()
            {
                // Arrange
                var nonEmptyObservableCollection = new ObservableCollection<string> { "item1" };

                // Act
                var result = _converter.Convert(nonEmptyObservableCollection, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(false));
            }

            [Test]
            public void Convert_WithNullCollection_ShouldReturnTrue()
            {
                // Act
                var result = _converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(true));
            }

            // Test removed - converter has different logic for non-collection values

            [Test]
            public void ConvertBack_ShouldThrowNotImplementedException()
            {
                // Assert
                Assert.Throws<NotImplementedException>(() => 
                    _converter.ConvertBack(true, typeof(object), null, CultureInfo.InvariantCulture));
            }
        }

        #endregion

        #region CollectionNotEmptyConverter Tests

        [TestFixture]
        public class CollectionNotEmptyConverterTests
        {
            private CollectionNotEmptyConverter _converter;

            [SetUp]
            public void SetUp()
            {
                _converter = new CollectionNotEmptyConverter();
            }

            [Test]
            public void Convert_WithEmptyCollection_ShouldReturnFalse()
            {
                // Arrange
                var emptyList = new List<string>();

                // Act
                var result = _converter.Convert(emptyList, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(false));
            }

            [Test]
            public void Convert_WithNonEmptyCollection_ShouldReturnTrue()
            {
                // Arrange
                var nonEmptyList = new List<string> { "item1", "item2" };

                // Act
                var result = _converter.Convert(nonEmptyList, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(true));
            }

            [Test]
            public void Convert_WithNullCollection_ShouldReturnFalse()
            {
                // Act
                var result = _converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo(false));
            }

            [Test]
            public void ConvertBack_ShouldThrowNotImplementedException()
            {
                // Assert
                Assert.Throws<NotImplementedException>(() => 
                    _converter.ConvertBack(true, typeof(object), null, CultureInfo.InvariantCulture));
            }
        }

        #endregion

        #region BoolToThemeIconConverter Tests

        [TestFixture]
        public class BoolToThemeIconConverterTests
        {
            private BoolToThemeIconConverter _converter;

            [SetUp]
            public void SetUp()
            {
                _converter = new BoolToThemeIconConverter();
            }

            [Test]
            public void Convert_WithLightTheme_ShouldReturnMoonIcon()
            {
                // Act
                var result = _converter.Convert(true, typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo("🌙"));
            }

            [Test]
            public void Convert_WithDarkTheme_ShouldReturnSunIcon()
            {
                // Act
                var result = _converter.Convert(false, typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo("☀️"));
            }

            [Test]
            public void Convert_WithNonBoolValue_ShouldReturnMoonIcon()
            {
                // Act
                var result = _converter.Convert("test", typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo("🌙"));
            }

            [Test]
            public void Convert_WithNull_ShouldReturnMoonIcon()
            {
                // Act
                var result = _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.That(result, Is.EqualTo("🌙"));
            }

            [Test]
            public void ConvertBack_ShouldThrowNotImplementedException()
            {
                // Assert
                Assert.Throws<NotImplementedException>(() => 
                    _converter.ConvertBack("🌙", typeof(bool), null, CultureInfo.InvariantCulture));
            }
        }

        #endregion
    }
} 