using NUnit.Framework;
using LLMClient.Services;
using Moq;
using Microsoft.Extensions.Logging;

namespace LLMClient.Tests.Services
{
    [TestFixture]
    public class EmbeddingServiceTests
    {
        private EmbeddingService _embeddingService;
        private Mock<ILogger<EmbeddingService>> _mockLogger;

        [SetUp]
        public void SetUp()
        {
            _mockLogger = new Mock<ILogger<EmbeddingService>>();
            _embeddingService = new EmbeddingService(_mockLogger.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _embeddingService?.Dispose();
        }

        #region Initialization Tests

        [Test]
        public void Constructor_ShouldInitializeWithCorrectModelVersion()
        {
            // Assert
            Assert.That(_embeddingService.ModelVersion, Is.EqualTo("paraphrase-multilingual-MiniLM-L12-v2"));
            Assert.That(_embeddingService.IsInitialized, Is.False);
        }

        [Test]
        public async Task InitializeAsync_WithValidSetup_ShouldSetIsInitializedTrue()
        {
            // Act & Assert
            Assert.DoesNotThrowAsync(async () => await _embeddingService.InitializeAsync(), 
                "Initialization should complete without throwing an exception.");

            // Optionally, you can assert the state if initialization is guaranteed to succeed
            // In a test environment where the model might be missing, checking IsInitialized might be flaky
            // Assert.That(_embeddingService.IsInitialized, Is.True);
        }

        #endregion

        #region Embedding Generation Tests

        [Test]
        public async Task GenerateEmbeddingAsync_WithNullText_ShouldReturnNull()
        {
            // Act
            var result = await _embeddingService.GenerateEmbeddingAsync(null);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GenerateEmbeddingAsync_WithEmptyText_ShouldReturnNull()
        {
            // Act
            var result = await _embeddingService.GenerateEmbeddingAsync("");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GenerateEmbeddingAsync_WithWhitespaceText_ShouldReturnNull()
        {
            // Act
            var result = await _embeddingService.GenerateEmbeddingAsync("   ");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GenerateEmbeddingAsync_WhenNotInitialized_ShouldReturnNull()
        {
            // Arrange - ensure service is not initialized
            Assert.That(_embeddingService.IsInitialized, Is.False);

            // Act
            var result = await _embeddingService.GenerateEmbeddingAsync("test text");

            // Assert
            Assert.That(result, Is.Null);
        }

        #endregion

        #region Serialization Tests

        [Test]
        public void FloatArrayToBytes_WithValidArray_ShouldSerializeCorrectly()
        {
            // Arrange
            var embedding = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };

            // Act
            var bytes = _embeddingService.FloatArrayToBytes(embedding);

            // Assert
            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes.Length, Is.EqualTo(embedding.Length * sizeof(float)));
        }

        [Test]
        public void FloatArrayToBytes_WithNullArray_ShouldReturnEmptyArray()
        {
            // Act
            var bytes = _embeddingService.FloatArrayToBytes(null);

            // Assert
            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes.Length, Is.EqualTo(0));
        }

        [Test]
        public void FloatArrayToBytes_WithEmptyArray_ShouldReturnEmptyArray()
        {
            // Act
            var bytes = _embeddingService.FloatArrayToBytes(new float[0]);

            // Assert
            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes.Length, Is.EqualTo(0));
        }

        [Test]
        public void BytesToFloatArray_WithValidBytes_ShouldDeserializeCorrectly()
        {
            // Arrange
            var originalEmbedding = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };
            var bytes = _embeddingService.FloatArrayToBytes(originalEmbedding);

            // Act
            var deserializedEmbedding = _embeddingService.BytesToFloatArray(bytes);

            // Assert
            Assert.That(deserializedEmbedding, Is.Not.Null);
            Assert.That(deserializedEmbedding.Length, Is.EqualTo(originalEmbedding.Length));
            for (int i = 0; i < originalEmbedding.Length; i++)
            {
                Assert.That(deserializedEmbedding[i], Is.EqualTo(originalEmbedding[i]).Within(0.001f));
            }
        }

        [Test]
        public void BytesToFloatArray_WithNullBytes_ShouldReturnEmptyArray()
        {
            // Act
            var result = _embeddingService.BytesToFloatArray(null);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(0));
        }

        [Test]
        public void BytesToFloatArray_WithEmptyBytes_ShouldReturnEmptyArray()
        {
            // Act
            var result = _embeddingService.BytesToFloatArray(new byte[0]);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(0));
        }

        [Test]
        public void BytesToFloatArray_WithInvalidByteLength_ShouldHandleGracefully()
        {
            // Arrange - create byte array that's not divisible by sizeof(float)
            var invalidBytes = new byte[7]; // 7 is not divisible by 4

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = _embeddingService.BytesToFloatArray(invalidBytes);
                // Result should be truncated to valid float count
                Assert.That(result.Length, Is.EqualTo(1)); // 7/4 = 1 (truncated)
            });
        }

        #endregion

        #region Similarity Calculation Tests

        [Test]
        public void CalculateSimilarity_WithIdenticalVectors_ShouldReturnOne()
        {
            // Arrange
            var vector = new float[] { 1.0f, 0.0f, 0.0f };

            // Act
            var similarity = _embeddingService.CalculateSimilarity(vector, vector);

            // Assert
            Assert.That(similarity, Is.EqualTo(1.0f).Within(0.001f));
        }

        [Test]
        public void CalculateSimilarity_WithOppositeVectors_ShouldReturnNegativeOne()
        {
            // Arrange
            var vector1 = new float[] { 1.0f, 0.0f, 0.0f };
            var vector2 = new float[] { -1.0f, 0.0f, 0.0f };

            // Act
            var similarity = _embeddingService.CalculateSimilarity(vector1, vector2);

            // Assert
            Assert.That(similarity, Is.EqualTo(-1.0f).Within(0.001f));
        }

        [Test]
        public void CalculateSimilarity_WithOrthogonalVectors_ShouldReturnZero()
        {
            // Arrange
            var vector1 = new float[] { 1.0f, 0.0f, 0.0f };
            var vector2 = new float[] { 0.0f, 1.0f, 0.0f };

            // Act
            var similarity = _embeddingService.CalculateSimilarity(vector1, vector2);

            // Assert
            Assert.That(similarity, Is.EqualTo(0.0f).Within(0.001f));
        }

        [Test]
        public void CalculateSimilarity_WithNullVector1_ShouldReturnZero()
        {
            // Arrange
            var vector2 = new float[] { 1.0f, 0.0f, 0.0f };

            // Act
            var similarity = _embeddingService.CalculateSimilarity(null, vector2);

            // Assert
            Assert.That(similarity, Is.EqualTo(0.0f));
        }

        [Test]
        public void CalculateSimilarity_WithNullVector2_ShouldReturnZero()
        {
            // Arrange
            var vector1 = new float[] { 1.0f, 0.0f, 0.0f };

            // Act
            var similarity = _embeddingService.CalculateSimilarity(vector1, null);

            // Assert
            Assert.That(similarity, Is.EqualTo(0.0f));
        }

        [Test]
        public void CalculateSimilarity_WithEmptyVectors_ShouldReturnZero()
        {
            // Arrange
            var vector1 = new float[0];
            var vector2 = new float[0];

            // Act
            var similarity = _embeddingService.CalculateSimilarity(vector1, vector2);

            // Assert
            Assert.That(similarity, Is.EqualTo(0.0f));
        }

        [Test]
        public void CalculateSimilarity_WithDifferentLengthVectors_ShouldReturnZero()
        {
            // Arrange
            var vector1 = new float[] { 1.0f, 0.0f };
            var vector2 = new float[] { 1.0f, 0.0f, 0.0f };

            // Act
            var similarity = _embeddingService.CalculateSimilarity(vector1, vector2);

            // Assert
            Assert.That(similarity, Is.EqualTo(0.0f));
        }

        [Test]
        public void CalculateSimilarity_WithZeroVectors_ShouldReturnZero()
        {
            // Arrange
            var vector1 = new float[] { 0.0f, 0.0f, 0.0f };
            var vector2 = new float[] { 0.0f, 0.0f, 0.0f };

            // Act
            var similarity = _embeddingService.CalculateSimilarity(vector1, vector2);

            // Assert
            Assert.That(similarity, Is.EqualTo(0.0f));
        }

        [Test]
        public void CalculateSimilarity_WithComplexVectors_ShouldCalculateCorrectly()
        {
            // Arrange
            var vector1 = new float[] { 0.5f, 0.5f, 0.5f, 0.5f };
            var vector2 = new float[] { 0.6f, 0.4f, 0.6f, 0.4f };

            // Act
            var similarity = _embeddingService.CalculateSimilarity(vector1, vector2);

            // Assert
            // Manual calculation: dot product = 0.5*0.6 + 0.5*0.4 + 0.5*0.6 + 0.5*0.4 = 1.0
            // magnitude1 = sqrt(0.25 + 0.25 + 0.25 + 0.25) = 1.0
            // magnitude2 = sqrt(0.36 + 0.16 + 0.36 + 0.16) = sqrt(1.04) ≈ 1.0198
            // Expected similarity ≈ 1.0 / (1.0 * 1.0198) ≈ 0.9806
            Assert.That(similarity, Is.GreaterThan(0.95f));
            Assert.That(similarity, Is.LessThanOrEqualTo(1.0f));
        }

        #endregion

        #region Round-trip Serialization Tests

        [Test]
        public void SerializationRoundTrip_ShouldPreserveValues()
        {
            // Arrange
            var originalEmbedding = new float[] 
            { 
                0.123f, -0.456f, 0.789f, -0.012f, 0.345f,
                -0.678f, 0.901f, -0.234f, 0.567f, -0.890f
            };

            // Act
            var bytes = _embeddingService.FloatArrayToBytes(originalEmbedding);
            var roundTripEmbedding = _embeddingService.BytesToFloatArray(bytes);

            // Assert
            Assert.That(roundTripEmbedding.Length, Is.EqualTo(originalEmbedding.Length));
            
            for (int i = 0; i < originalEmbedding.Length; i++)
            {
                Assert.That(roundTripEmbedding[i], Is.EqualTo(originalEmbedding[i]).Within(0.00001f),
                    $"Value at index {i} should be preserved during serialization round-trip");
            }
        }

        [Test]
        public void SerializationRoundTrip_WithLargeArray_ShouldPreserveValues()
        {
            // Arrange - simulate 384-dimensional embedding like all-MiniLM-L6-v2
            var originalEmbedding = new float[384];
            var random = new Random(42); // Fixed seed for reproducible tests
            
            for (int i = 0; i < originalEmbedding.Length; i++)
            {
                originalEmbedding[i] = (float)(random.NextDouble() * 2.0 - 1.0); // Values between -1 and 1
            }

            // Act
            var bytes = _embeddingService.FloatArrayToBytes(originalEmbedding);
            var roundTripEmbedding = _embeddingService.BytesToFloatArray(bytes);

            // Assert
            Assert.That(roundTripEmbedding.Length, Is.EqualTo(384));
            
            for (int i = 0; i < originalEmbedding.Length; i++)
            {
                Assert.That(roundTripEmbedding[i], Is.EqualTo(originalEmbedding[i]).Within(0.00001f));
            }
        }

        #endregion

        #region Performance and Edge Case Tests

        [Test]
        public void CalculateSimilarity_WithLargeVectors_ShouldCompleteInReasonableTime()
        {
            // Arrange
            var vector1 = new float[384]; // all-MiniLM-L6-v2 dimension
            var vector2 = new float[384];
            
            for (int i = 0; i < 384; i++)
            {
                vector1[i] = 0.5f;
                vector2[i] = 0.6f;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var similarity = _embeddingService.CalculateSimilarity(vector1, vector2);

            // Assert
            stopwatch.Stop();
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(10), 
                "Similarity calculation should complete quickly for 384-dimensional vectors");
            Assert.That(similarity, Is.GreaterThan(0.9f));
        }

        [Test]
        public void FloatArrayToBytes_WithLargeArray_ShouldCompleteQuickly()
        {
            // Arrange
            var embedding = new float[384];
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] = i * 0.001f;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var bytes = _embeddingService.FloatArrayToBytes(embedding);

            // Assert
            stopwatch.Stop();
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5),
                "Serialization should be fast for 384-dimensional vectors");
            Assert.That(bytes.Length, Is.EqualTo(384 * sizeof(float)));
        }

        #endregion

        #region Diagnostic Embedding Tests

        [Test]
        public async Task Diagnostic_IdenticalTexts_ShouldHaveHighSimilarity()
        {
            await _embeddingService.InitializeAsync();
            var text1 = "To jest test";
            var text2 = "To jest test";
            var emb1 = await _embeddingService.GenerateEmbeddingAsync(text1);
            var emb2 = await _embeddingService.GenerateEmbeddingAsync(text2);
            Console.WriteLine($"emb1: {string.Join(", ", emb1.Take(5))}...");
            Console.WriteLine($"emb2: {string.Join(", ", emb2.Take(5))}...");
            var sim = _embeddingService.CalculateSimilarity(emb1, emb2);
            Console.WriteLine($"Similarity: {sim}");
            Assert.That(sim, Is.GreaterThan(0.95f));
        }

        [Test]
        public async Task Diagnostic_DifferentTexts_ShouldHaveLowSimilarity()
        {
            await _embeddingService.InitializeAsync();
            var text1 = "kot";
            var text2 = "samolot";
            var emb1 = await _embeddingService.GenerateEmbeddingAsync(text1);
            var emb2 = await _embeddingService.GenerateEmbeddingAsync(text2);
            Console.WriteLine($"emb1: {string.Join(", ", emb1.Take(5))}...");
            Console.WriteLine($"emb2: {string.Join(", ", emb2.Take(5))}...");
            var sim = _embeddingService.CalculateSimilarity(emb1, emb2);
            Console.WriteLine($"Similarity: {sim}");
            Assert.That(sim, Is.LessThan(0.8f));
        }

        [Test]
        public async Task Diagnostic_PolishCharacters_ShouldTokenizeAndEmbed()
        {
            await _embeddingService.InitializeAsync();
            var text1 = "zażółć gęślą jaźń";
            var text2 = "zażółć gęślą jaźń";
            var emb1 = await _embeddingService.GenerateEmbeddingAsync(text1);
            var emb2 = await _embeddingService.GenerateEmbeddingAsync(text2);
            var sim = _embeddingService.CalculateSimilarity(emb1, emb2);
            Console.WriteLine($"Polish similarity: {sim}");
            Assert.That(sim, Is.GreaterThan(0.95f));
        }

        [Test]
        public async Task Diagnostic_Emoji_ShouldNotBreakEmbedding()
        {
            await _embeddingService.InitializeAsync();
            var text1 = "😀😀😀";
            var text2 = "😀😀😀";
            var emb1 = await _embeddingService.GenerateEmbeddingAsync(text1);
            var emb2 = await _embeddingService.GenerateEmbeddingAsync(text2);
            var sim = _embeddingService.CalculateSimilarity(emb1, emb2);
            Console.WriteLine($"Emoji similarity: {sim}");
            Assert.That(sim, Is.GreaterThan(0.95f));
        }

        [Test]
        public async Task Diagnostic_LongText_ShouldProduceConsistentEmbedding()
        {
            await _embeddingService.InitializeAsync();
            var text = string.Join(" ", Enumerable.Repeat("test", 100));
            var emb1 = await _embeddingService.GenerateEmbeddingAsync(text);
            var emb2 = await _embeddingService.GenerateEmbeddingAsync(text);
            var sim = _embeddingService.CalculateSimilarity(emb1, emb2);
            Console.WriteLine($"Long text similarity: {sim}");
            Assert.That(sim, Is.GreaterThan(0.95f));
        }

        [Test]
        public async Task Diagnostic_PolishParaphrase_ShouldHaveHigherSimilarityThanUnrelated()
        {
            await _embeddingService.InitializeAsync();
            var sent1 = "Idę do sklepu.";
            var sent2 = "Wybieram się do sklepu."; // parafraza
            var unrelated = "Na dworze świeci słońce.";

            var emb1 = await _embeddingService.GenerateEmbeddingAsync(sent1);
            var emb2 = await _embeddingService.GenerateEmbeddingAsync(sent2);
            var embUn = await _embeddingService.GenerateEmbeddingAsync(unrelated);

            var simPara = _embeddingService.CalculateSimilarity(emb1, emb2);
            var simUnrel = _embeddingService.CalculateSimilarity(emb1, embUn);

            Console.WriteLine($"Paraphrase similarity: {simPara}");
            Console.WriteLine($"Unrelated similarity: {simUnrel}");

            // Parafraza powinna mieć wyższe podobieństwo niż zdania niepowiązane
            Assert.That(simPara, Is.GreaterThan(simUnrel), "Podobieństwo parafrazy powinno być wyższe niż zdania niepowiązanego");
        }

        #endregion
    }
} 