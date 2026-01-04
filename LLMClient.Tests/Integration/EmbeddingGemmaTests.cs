using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NUnit.Framework;
using System.Text.Json;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Kompleksowe testy EmbeddingGemma-300M - porównanie jakości embeddingów.
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("EmbeddingGemma")]
public class EmbeddingGemmaTests
{
    private InferenceSession? _session;
    private Dictionary<string, int>? _vocab;
    private bool _modelAvailable = false;
    
    // Hardcoded path that works
    private static readonly string ModelDir = @"C:\Users\hdtdt\AppData\Local\User Name\com.companyname.llmclient\Data\models\embeddinggemma-300m";

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var onnxPath = Path.Combine(ModelDir, "onnx", "model.onnx");
        var tokenizerPath = Path.Combine(ModelDir, "tokenizer.json");
        
        if (!File.Exists(onnxPath) || !File.Exists(tokenizerPath))
        {
            TestContext.WriteLine($"Model not found at {onnxPath}");
            return;
        }

        try
        {
            // Load ONNX model
            var options = new SessionOptions();
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            _session = new InferenceSession(onnxPath, options);
            
            // Load tokenizer vocabulary from SentencePiece format
            var tokenizerJson = File.ReadAllText(tokenizerPath);
            var tokenizer = JsonDocument.Parse(tokenizerJson);
            
            _vocab = new Dictionary<string, int>();
            
            // Get vocab from model.vocab (SentencePiece format)
            if (tokenizer.RootElement.TryGetProperty("model", out var modelElement) &&
                modelElement.TryGetProperty("vocab", out var vocabElement))
            {
                foreach (var item in vocabElement.EnumerateArray())
                {
                    var token = item[0].GetString()!;
                    var id = item[1].GetInt32();
                    _vocab[token] = id;
                }
            }
            
            _modelAvailable = true;
            TestContext.WriteLine($"EmbeddingGemma loaded: {_vocab.Count} tokens");
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"Failed to load model: {ex.Message}");
        }
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _session?.Dispose();
    }

    private void EnsureModelAvailable()
    {
        if (!_modelAvailable || _session == null)
            Assert.Ignore("EmbeddingGemma model not available");
    }

    /// <summary>
    /// Simple tokenization using vocabulary lookup.
    /// Note: This is simplified - real tokenizer uses BPE.
    /// </summary>
    private int[] SimpleTokenize(string text, int maxLength = 512)
    {
        // Add task prefix for EmbeddingGemma
        var prefixedText = $"title: none | text: {text}";
        
        // Simple word-based tokenization (simplified, real uses SentencePiece)
        var tokens = new List<int> { 2 }; // BOS token
        
        // Character-level fallback
        foreach (var word in prefixedText.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (_vocab!.TryGetValue(word, out var id))
            {
                tokens.Add(id);
            }
            else
            {
                // Try subword tokenization
                var remaining = word;
                while (remaining.Length > 0)
                {
                    bool found = false;
                    for (int len = Math.Min(remaining.Length, 20); len > 0; len--)
                    {
                        var subword = remaining.Substring(0, len);
                        var prefix = tokens.Count > 1 ? "▁" : "";
                        
                        if (_vocab.TryGetValue(prefix + subword, out id) || _vocab.TryGetValue(subword, out id))
                        {
                            tokens.Add(id);
                            remaining = remaining.Substring(len);
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        // Unknown token
                        tokens.Add(3); // UNK
                        remaining = remaining.Length > 1 ? remaining.Substring(1) : "";
                    }
                }
            }
            
            if (tokens.Count >= maxLength - 1) break;
        }
        
        tokens.Add(1); // EOS token
        
        // Pad to maxLength
        while (tokens.Count < maxLength)
            tokens.Add(0); // PAD
            
        return tokens.Take(maxLength).ToArray();
    }

    private float[] GenerateEmbedding(string text)
    {
        var inputIds = SimpleTokenize(text);
        var attentionMask = inputIds.Select(id => id != 0 ? 1L : 0L).ToArray();
        
        var inputIdsTensor = new DenseTensor<long>(inputIds.Select(i => (long)i).ToArray(), new[] { 1, inputIds.Length });
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });
        
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
        };
        
        using var results = _session!.Run(inputs);
        
        // Get sentence embedding (second output)
        var embeddingTensor = results.Last().AsTensor<float>();
        return embeddingTensor.ToArray();
    }

    private float CosineSimilarity(float[] a, float[] b)
    {
        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    #region Basic Tests

    [Test]
    public void EmbeddingGemma_ModelLoads_Successfully()
    {
        EnsureModelAvailable();
        
        Assert.That(_session, Is.Not.Null);
        Assert.That(_vocab, Is.Not.Null);
        Assert.That(_vocab!.Count, Is.GreaterThan(200000), "Vocab should have >200k tokens");
        
        TestContext.WriteLine($"Vocab size: {_vocab.Count}");
    }

    [Test]
    public void EmbeddingGemma_GeneratesEmbedding_768Dimensions()
    {
        EnsureModelAvailable();
        
        var text = "This is a test sentence.";
        var embedding = GenerateEmbedding(text);
        
        Assert.That(embedding.Length, Is.EqualTo(768), "EmbeddingGemma should produce 768-dim vectors");
        
        TestContext.WriteLine($"Embedding dimensions: {embedding.Length}");
        TestContext.WriteLine($"First 5 values: [{string.Join(", ", embedding.Take(5).Select(v => v.ToString("F4")))}]");
    }

    #endregion

    #region Polish Language Tests

    [Test]
    public void Polish_SimilarSentences_HighSimilarity()
    {
        EnsureModelAvailable();
        
        var pairs = new[]
        {
            ("Lubię programować w C#.", "Uwielbiam kodować w C#."),
            ("Dzisiaj jest piękna pogoda.", "Dziś mamy ładną pogodę."),
            ("Kot śpi na kanapie.", "Kotek drzemie na sofie."),
            ("Samochód jedzie szybko.", "Auto pędzi z dużą prędkością."),
            ("Jestem programistą.", "Pracuję jako developer.")
        };
        
        TestContext.WriteLine("=== POLISH SIMILAR SENTENCES ===");
        
        foreach (var (text1, text2) in pairs)
        {
            var emb1 = GenerateEmbedding(text1);
            var emb2 = GenerateEmbedding(text2);
            var similarity = CosineSimilarity(emb1, emb2);
            
            TestContext.WriteLine($"[{similarity:F4}] \"{text1}\" ↔ \"{text2}\"");
            
            Assert.That(similarity, Is.GreaterThan(0.7f), 
                $"Similar Polish sentences should have >0.7 similarity: {text1} ↔ {text2}");
        }
    }

    [Test]
    public void Polish_DifferentSentences_LowSimilarity()
    {
        EnsureModelAvailable();
        
        var pairs = new[]
        {
            ("Lubię programować w C#.", "Pierogi są pyszne."),
            ("Dzisiaj jest piękna pogoda.", "Matematyka jest trudna."),
            ("Kot śpi na kanapie.", "Samolot leci do Paryża."),
            ("Samochód jedzie szybko.", "Książka leży na stole."),
            ("Jestem programistą.", "Góry są wysokie.")
        };
        
        TestContext.WriteLine("\n=== POLISH DIFFERENT SENTENCES ===");
        
        foreach (var (text1, text2) in pairs)
        {
            var emb1 = GenerateEmbedding(text1);
            var emb2 = GenerateEmbedding(text2);
            var similarity = CosineSimilarity(emb1, emb2);
            
            TestContext.WriteLine($"[{similarity:F4}] \"{text1}\" ↔ \"{text2}\"");
            
            Assert.That(similarity, Is.LessThan(0.7f), 
                $"Different Polish sentences should have <0.7 similarity: {text1} ↔ {text2}");
        }
    }

    [Test]
    public void Polish_SynonymsAndAntonyms()
    {
        EnsureModelAvailable();
        
        var synonyms = new[]
        {
            ("duży", "wielki"),
            ("mały", "malutki"),
            ("szybki", "prędki"),
            ("piękny", "ładny"),
            ("inteligentny", "mądry")
        };
        
        var antonyms = new[]
        {
            ("duży", "mały"),
            ("szybki", "wolny"),
            ("gorący", "zimny"),
            ("dobry", "zły"),
            ("jasny", "ciemny")
        };
        
        TestContext.WriteLine("\n=== POLISH SYNONYMS ===");
        float avgSynonymSim = 0;
        foreach (var (word1, word2) in synonyms)
        {
            var emb1 = GenerateEmbedding(word1);
            var emb2 = GenerateEmbedding(word2);
            var similarity = CosineSimilarity(emb1, emb2);
            avgSynonymSim += similarity;
            TestContext.WriteLine($"[{similarity:F4}] {word1} ↔ {word2}");
        }
        avgSynonymSim /= synonyms.Length;
        
        TestContext.WriteLine("\n=== POLISH ANTONYMS ===");
        float avgAntonymSim = 0;
        foreach (var (word1, word2) in antonyms)
        {
            var emb1 = GenerateEmbedding(word1);
            var emb2 = GenerateEmbedding(word2);
            var similarity = CosineSimilarity(emb1, emb2);
            avgAntonymSim += similarity;
            TestContext.WriteLine($"[{similarity:F4}] {word1} ↔ {word2}");
        }
        avgAntonymSim /= antonyms.Length;
        
        TestContext.WriteLine($"\nAvg synonym similarity: {avgSynonymSim:F4}");
        TestContext.WriteLine($"Avg antonym similarity: {avgAntonymSim:F4}");
        TestContext.WriteLine($"Gap (synonym - antonym): {avgSynonymSim - avgAntonymSim:F4}");
        
        Assert.That(avgSynonymSim, Is.GreaterThan(avgAntonymSim), 
            "Synonyms should have higher similarity than antonyms");
    }

    #endregion

    #region English Language Tests

    [Test]
    public void English_SimilarSentences_HighSimilarity()
    {
        EnsureModelAvailable();
        
        var pairs = new[]
        {
            ("I love programming in C#.", "I enjoy coding in C#."),
            ("The weather is beautiful today.", "It's a nice day today."),
            ("The cat sleeps on the couch.", "The kitten is napping on the sofa."),
            ("The car drives fast.", "The vehicle moves quickly."),
            ("I am a programmer.", "I work as a software developer.")
        };
        
        TestContext.WriteLine("\n=== ENGLISH SIMILAR SENTENCES ===");
        
        foreach (var (text1, text2) in pairs)
        {
            var emb1 = GenerateEmbedding(text1);
            var emb2 = GenerateEmbedding(text2);
            var similarity = CosineSimilarity(emb1, emb2);
            
            TestContext.WriteLine($"[{similarity:F4}] \"{text1}\" ↔ \"{text2}\"");
            
            Assert.That(similarity, Is.GreaterThan(0.7f), 
                $"Similar English sentences should have >0.7 similarity");
        }
    }

    [Test]
    public void English_DifferentSentences_LowSimilarity()
    {
        EnsureModelAvailable();
        
        var pairs = new[]
        {
            ("I love programming in C#.", "Pizza is delicious."),
            ("The weather is beautiful today.", "Mathematics is difficult."),
            ("The cat sleeps on the couch.", "The airplane flies to Paris."),
            ("The car drives fast.", "The book lies on the table."),
            ("I am a programmer.", "Mountains are tall.")
        };
        
        TestContext.WriteLine("\n=== ENGLISH DIFFERENT SENTENCES ===");
        
        foreach (var (text1, text2) in pairs)
        {
            var emb1 = GenerateEmbedding(text1);
            var emb2 = GenerateEmbedding(text2);
            var similarity = CosineSimilarity(emb1, emb2);
            
            TestContext.WriteLine($"[{similarity:F4}] \"{text1}\" ↔ \"{text2}\"");
            
            Assert.That(similarity, Is.LessThan(0.7f), 
                $"Different English sentences should have <0.7 similarity");
        }
    }

    [Test]
    public void English_SynonymsAndAntonyms()
    {
        EnsureModelAvailable();
        
        var synonyms = new[]
        {
            ("big", "large"),
            ("small", "tiny"),
            ("fast", "quick"),
            ("beautiful", "pretty"),
            ("intelligent", "smart")
        };
        
        var antonyms = new[]
        {
            ("big", "small"),
            ("fast", "slow"),
            ("hot", "cold"),
            ("good", "bad"),
            ("light", "dark")
        };
        
        TestContext.WriteLine("\n=== ENGLISH SYNONYMS ===");
        float avgSynonymSim = 0;
        foreach (var (word1, word2) in synonyms)
        {
            var emb1 = GenerateEmbedding(word1);
            var emb2 = GenerateEmbedding(word2);
            var similarity = CosineSimilarity(emb1, emb2);
            avgSynonymSim += similarity;
            TestContext.WriteLine($"[{similarity:F4}] {word1} ↔ {word2}");
        }
        avgSynonymSim /= synonyms.Length;
        
        TestContext.WriteLine("\n=== ENGLISH ANTONYMS ===");
        float avgAntonymSim = 0;
        foreach (var (word1, word2) in antonyms)
        {
            var emb1 = GenerateEmbedding(word1);
            var emb2 = GenerateEmbedding(word2);
            var similarity = CosineSimilarity(emb1, emb2);
            avgAntonymSim += similarity;
            TestContext.WriteLine($"[{similarity:F4}] {word1} ↔ {word2}");
        }
        avgAntonymSim /= antonyms.Length;
        
        TestContext.WriteLine($"\nAvg synonym similarity: {avgSynonymSim:F4}");
        TestContext.WriteLine($"Avg antonym similarity: {avgAntonymSim:F4}");
        TestContext.WriteLine($"Gap (synonym - antonym): {avgSynonymSim - avgAntonymSim:F4}");
        
        Assert.That(avgSynonymSim, Is.GreaterThan(avgAntonymSim), 
            "Synonyms should have higher similarity than antonyms");
    }

    #endregion

    #region Cross-Lingual Tests

    [Test]
    public void CrossLingual_PolishEnglish_SameMeaning()
    {
        EnsureModelAvailable();
        
        var pairs = new[]
        {
            ("Sztuczna inteligencja zmienia świat.", "Artificial intelligence is changing the world."),
            ("Lubię programować.", "I like programming."),
            ("Kot jest zwierzęciem.", "A cat is an animal."),
            ("Warszawa jest stolicą Polski.", "Warsaw is the capital of Poland."),
            ("Uczenie maszynowe jest fascynujące.", "Machine learning is fascinating.")
        };
        
        TestContext.WriteLine("\n=== CROSS-LINGUAL PL-EN (Same meaning) ===");
        float avgSimilarity = 0;
        
        foreach (var (polish, english) in pairs)
        {
            var embPl = GenerateEmbedding(polish);
            var embEn = GenerateEmbedding(english);
            var similarity = CosineSimilarity(embPl, embEn);
            avgSimilarity += similarity;
            
            TestContext.WriteLine($"[{similarity:F4}] PL: \"{polish}\"");
            TestContext.WriteLine($"         EN: \"{english}\"");
        }
        
        avgSimilarity /= pairs.Length;
        TestContext.WriteLine($"\nAverage cross-lingual similarity: {avgSimilarity:F4}");
        
        Assert.That(avgSimilarity, Is.GreaterThan(0.75f), 
            "Cross-lingual same-meaning sentences should have >0.75 average similarity");
    }

    [Test]
    public void CrossLingual_PolishEnglish_DifferentMeaning()
    {
        EnsureModelAvailable();
        
        var pairs = new[]
        {
            ("Sztuczna inteligencja zmienia świat.", "Pizza is delicious."),
            ("Lubię programować.", "The mountain is high."),
            ("Kot jest zwierzęciem.", "Mathematics is difficult."),
            ("Warszawa jest stolicą Polski.", "The car drives fast."),
            ("Uczenie maszynowe jest fascynujące.", "I like coffee.")
        };
        
        TestContext.WriteLine("\n=== CROSS-LINGUAL PL-EN (Different meaning) ===");
        float avgSimilarity = 0;
        
        foreach (var (polish, english) in pairs)
        {
            var embPl = GenerateEmbedding(polish);
            var embEn = GenerateEmbedding(english);
            var similarity = CosineSimilarity(embPl, embEn);
            avgSimilarity += similarity;
            
            TestContext.WriteLine($"[{similarity:F4}] PL: \"{polish}\"");
            TestContext.WriteLine($"         EN: \"{english}\"");
        }
        
        avgSimilarity /= pairs.Length;
        TestContext.WriteLine($"\nAverage cross-lingual (different) similarity: {avgSimilarity:F4}");
        
        Assert.That(avgSimilarity, Is.LessThan(0.6f), 
            "Cross-lingual different-meaning sentences should have <0.6 average similarity");
    }

    [Test]
    public void CrossLingual_PolishGerman_SameMeaning()
    {
        EnsureModelAvailable();
        
        var pairs = new[]
        {
            ("Sztuczna inteligencja zmienia świat.", "Künstliche Intelligenz verändert die Welt."),
            ("Lubię programować.", "Ich programmiere gerne."),
            ("Kot jest zwierzęciem.", "Eine Katze ist ein Tier."),
            ("Dzisiaj jest piękna pogoda.", "Das Wetter ist heute schön.")
        };
        
        TestContext.WriteLine("\n=== CROSS-LINGUAL PL-DE (Same meaning) ===");
        float avgSimilarity = 0;
        
        foreach (var (polish, german) in pairs)
        {
            var embPl = GenerateEmbedding(polish);
            var embDe = GenerateEmbedding(german);
            var similarity = CosineSimilarity(embPl, embDe);
            avgSimilarity += similarity;
            
            TestContext.WriteLine($"[{similarity:F4}] PL: \"{polish}\"");
            TestContext.WriteLine($"         DE: \"{german}\"");
        }
        
        avgSimilarity /= pairs.Length;
        TestContext.WriteLine($"\nAverage PL-DE similarity: {avgSimilarity:F4}");
        
        Assert.That(avgSimilarity, Is.GreaterThan(0.7f), 
            "Cross-lingual PL-DE same-meaning should have >0.7 similarity");
    }

    #endregion

    #region Semantic Similarity Gap Analysis

    [Test]
    public void SemanticGap_Polish_Analysis()
    {
        EnsureModelAvailable();
        
        // Related topics
        var techPairs = new[]
        {
            ("programowanie", "kodowanie"),
            ("komputer", "laptop"),
            ("internet", "sieć"),
            ("baza danych", "SQL"),
            ("algorytm", "funkcja")
        };
        
        // Unrelated topics
        var unrelatedPairs = new[]
        {
            ("programowanie", "kuchnia"),
            ("komputer", "drzewo"),
            ("internet", "góra"),
            ("baza danych", "pies"),
            ("algorytm", "słońce")
        };
        
        TestContext.WriteLine("\n=== SEMANTIC GAP ANALYSIS (Polish) ===");
        
        float avgRelated = 0;
        TestContext.WriteLine("Related pairs:");
        foreach (var (w1, w2) in techPairs)
        {
            var sim = CosineSimilarity(GenerateEmbedding(w1), GenerateEmbedding(w2));
            avgRelated += sim;
            TestContext.WriteLine($"  [{sim:F4}] {w1} ↔ {w2}");
        }
        avgRelated /= techPairs.Length;
        
        float avgUnrelated = 0;
        TestContext.WriteLine("\nUnrelated pairs:");
        foreach (var (w1, w2) in unrelatedPairs)
        {
            var sim = CosineSimilarity(GenerateEmbedding(w1), GenerateEmbedding(w2));
            avgUnrelated += sim;
            TestContext.WriteLine($"  [{sim:F4}] {w1} ↔ {w2}");
        }
        avgUnrelated /= unrelatedPairs.Length;
        
        var gap = avgRelated - avgUnrelated;
        
        TestContext.WriteLine($"\n--- RESULTS ---");
        TestContext.WriteLine($"Avg related similarity:   {avgRelated:F4}");
        TestContext.WriteLine($"Avg unrelated similarity: {avgUnrelated:F4}");
        TestContext.WriteLine($"SEMANTIC GAP:             {gap:F4}");
        
        Assert.That(gap, Is.GreaterThan(0.1f), 
            "Semantic gap between related and unrelated should be >0.1");
    }

    [Test]
    public void SemanticGap_English_Analysis()
    {
        EnsureModelAvailable();
        
        var techPairs = new[]
        {
            ("programming", "coding"),
            ("computer", "laptop"),
            ("internet", "network"),
            ("database", "SQL"),
            ("algorithm", "function")
        };
        
        var unrelatedPairs = new[]
        {
            ("programming", "kitchen"),
            ("computer", "tree"),
            ("internet", "mountain"),
            ("database", "dog"),
            ("algorithm", "sun")
        };
        
        TestContext.WriteLine("\n=== SEMANTIC GAP ANALYSIS (English) ===");
        
        float avgRelated = 0;
        TestContext.WriteLine("Related pairs:");
        foreach (var (w1, w2) in techPairs)
        {
            var sim = CosineSimilarity(GenerateEmbedding(w1), GenerateEmbedding(w2));
            avgRelated += sim;
            TestContext.WriteLine($"  [{sim:F4}] {w1} ↔ {w2}");
        }
        avgRelated /= techPairs.Length;
        
        float avgUnrelated = 0;
        TestContext.WriteLine("\nUnrelated pairs:");
        foreach (var (w1, w2) in unrelatedPairs)
        {
            var sim = CosineSimilarity(GenerateEmbedding(w1), GenerateEmbedding(w2));
            avgUnrelated += sim;
            TestContext.WriteLine($"  [{sim:F4}] {w1} ↔ {w2}");
        }
        avgUnrelated /= unrelatedPairs.Length;
        
        var gap = avgRelated - avgUnrelated;
        
        TestContext.WriteLine($"\n--- RESULTS ---");
        TestContext.WriteLine($"Avg related similarity:   {avgRelated:F4}");
        TestContext.WriteLine($"Avg unrelated similarity: {avgUnrelated:F4}");
        TestContext.WriteLine($"SEMANTIC GAP:             {gap:F4}");
        
        Assert.That(gap, Is.GreaterThan(0.1f), 
            "Semantic gap between related and unrelated should be >0.1");
    }

    #endregion

    #region RAG Retrieval Simulation

    [Test]
    public void RAG_RetrievalSimulation_Polish()
    {
        EnsureModelAvailable();
        
        // Simulate a document corpus
        var documents = new[]
        {
            "C# to nowoczesny język programowania stworzony przez Microsoft. Jest używany do tworzenia aplikacji Windows, webowych i mobilnych.",
            "Python jest popularnym językiem programowania znanym z prostej składni. Jest często używany w uczeniu maszynowym.",
            "Pierogi ruskie to tradycyjne polskie danie. Farsz składa się z ziemniaków, twarogu i smażonej cebuli.",
            "Warszawa jest stolicą Polski i największym miastem w kraju. Znajduje się nad Wisłą.",
            "Machine learning to dziedzina sztucznej inteligencji. Pozwala komputerom uczyć się z danych."
        };
        
        var queries = new[]
        {
            ("Jak programować w C#?", 0),           // Should match doc 0
            ("Jaka jest stolica Polski?", 3),       // Should match doc 3
            ("Co to jest uczenie maszynowe?", 4),   // Should match doc 4
            ("Jak zrobić pierogi?", 2)              // Should match doc 2
        };
        
        TestContext.WriteLine("\n=== RAG RETRIEVAL SIMULATION (Polish) ===");
        
        // Generate document embeddings
        var docEmbeddings = documents.Select(d => GenerateEmbedding(d)).ToArray();
        
        int correctTop1 = 0;
        
        foreach (var (query, expectedDocIndex) in queries)
        {
            var queryEmb = GenerateEmbedding(query);
            
            // Calculate similarities and rank
            var similarities = docEmbeddings
                .Select((emb, idx) => (idx, sim: CosineSimilarity(queryEmb, emb)))
                .OrderByDescending(x => x.sim)
                .ToList();
            
            var top1Index = similarities[0].idx;
            var isCorrect = top1Index == expectedDocIndex;
            if (isCorrect) correctTop1++;
            
            TestContext.WriteLine($"\nQuery: \"{query}\"");
            TestContext.WriteLine($"Expected: Doc {expectedDocIndex}");
            TestContext.WriteLine($"Top-1: Doc {top1Index} (sim: {similarities[0].sim:F4}) {(isCorrect ? "✓" : "✗")}");
            TestContext.WriteLine($"Rankings:");
            foreach (var (idx, sim) in similarities.Take(3))
            {
                TestContext.WriteLine($"  Doc {idx}: {sim:F4} - {documents[idx].Substring(0, Math.Min(50, documents[idx].Length))}...");
            }
        }
        
        var accuracy = (float)correctTop1 / queries.Length;
        TestContext.WriteLine($"\n--- RAG RESULTS ---");
        TestContext.WriteLine($"Top-1 Accuracy: {correctTop1}/{queries.Length} ({accuracy:P0})");
        
        Assert.That(accuracy, Is.GreaterThanOrEqualTo(0.5f), 
            "RAG retrieval should have at least 50% Top-1 accuracy");
    }

    #endregion

    #region Performance Tests

    [Test]
    public void Performance_InferenceTime()
    {
        EnsureModelAvailable();
        
        var text = "This is a test sentence for performance measurement.";
        
        // Warmup
        GenerateEmbedding("warmup");
        
        var times = new List<long>();
        var sw = new System.Diagnostics.Stopwatch();
        
        for (int i = 0; i < 10; i++)
        {
            sw.Restart();
            GenerateEmbedding(text);
            sw.Stop();
            times.Add(sw.ElapsedMilliseconds);
        }
        
        TestContext.WriteLine("\n=== PERFORMANCE ===");
        TestContext.WriteLine($"Avg inference time: {times.Average():F1}ms");
        TestContext.WriteLine($"Min: {times.Min()}ms");
        TestContext.WriteLine($"Max: {times.Max()}ms");
        
        Assert.That(times.Average(), Is.LessThan(1000), 
            "Average inference should be under 1000ms");
    }

    #endregion
}
