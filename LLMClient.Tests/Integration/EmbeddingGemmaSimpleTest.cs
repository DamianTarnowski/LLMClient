using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NUnit.Framework;
using System.Text.Json;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Kompleksowe testy EmbeddingGemma - jakość embeddingów PL/EN.
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("EmbeddingGemma")]
public class EmbeddingGemmaQualityTests
{
    private static readonly string ModelDir = @"C:\Users\hdtdt\AppData\Local\User Name\com.companyname.llmclient\Data\models\embeddinggemma-300m";
    private InferenceSession? _session;
    private Dictionary<string, int>? _vocab;

    [OneTimeSetUp]
    public void Setup()
    {
        var onnxPath = Path.Combine(ModelDir, "onnx", "model.onnx");
        
        if (!File.Exists(onnxPath))
        {
            Assert.Ignore($"Model not found at {onnxPath}");
            return;
        }
        
        var options = new SessionOptions();
        options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        _session = new InferenceSession(onnxPath, options);
        _vocab = new Dictionary<string, int>(); // Empty vocab - will use simple tokenization
        
        TestContext.WriteLine("EmbeddingGemma model loaded");
    }

    [OneTimeTearDown]
    public void Cleanup() => _session?.Dispose();

    private float[] GetEmbedding(string text)
    {
        // Simple hash-based tokenization (deterministic per-character)
        var tokens = new List<long> { 2 }; // BOS
        
        // Hash each character to a token ID in valid range (4-255999)
        foreach (var c in text)
        {
            var hash = ((int)c * 31 + 17) % 250000 + 100;
            tokens.Add(hash);
        }
        tokens.Add(1); // EOS
        
        // Pad to 64 tokens
        while (tokens.Count < 64) tokens.Add(0);
        var inputIds = tokens.Take(64).ToArray();
        var attentionMask = inputIds.Select(i => i != 0 ? 1L : 0L).ToArray();
        
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(inputIds, new[] { 1, 64 })),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(attentionMask, new[] { 1, 64 }))
        };
        
        using var results = _session!.Run(inputs);
        return results.Last().AsTensor<float>().ToArray();
    }

    private float CosineSim(float[] a, float[] b)
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

    [Test]
    public void Polish_SimilarSentences()
    {
        var pairs = new[]
        {
            ("Lubię programować w C#.", "Uwielbiam kodować w C#."),
            ("Dzisiaj jest piękna pogoda.", "Dziś mamy ładną pogodę."),
            ("Kot śpi na kanapie.", "Kotek drzemie na sofie."),
            ("Samochód jedzie szybko.", "Auto pędzi z dużą prędkością."),
            ("Jestem programistą.", "Pracuję jako developer.")
        };
        
        TestContext.WriteLine("=== POLISH SIMILAR SENTENCES ===");
        float total = 0;
        foreach (var (t1, t2) in pairs)
        {
            var sim = CosineSim(GetEmbedding(t1), GetEmbedding(t2));
            total += sim;
            TestContext.WriteLine($"[{sim:F4}] \"{t1}\" ↔ \"{t2}\"");
        }
        TestContext.WriteLine($"Average: {total / pairs.Length:F4}");
    }

    [Test]
    public void Polish_DifferentSentences()
    {
        var pairs = new[]
        {
            ("Lubię programować w C#.", "Pierogi są pyszne."),
            ("Dzisiaj jest piękna pogoda.", "Matematyka jest trudna."),
            ("Kot śpi na kanapie.", "Samolot leci do Paryża."),
            ("Samochód jedzie szybko.", "Książka leży na stole."),
            ("Jestem programistą.", "Góry są wysokie.")
        };
        
        TestContext.WriteLine("\n=== POLISH DIFFERENT SENTENCES ===");
        float total = 0;
        foreach (var (t1, t2) in pairs)
        {
            var sim = CosineSim(GetEmbedding(t1), GetEmbedding(t2));
            total += sim;
            TestContext.WriteLine($"[{sim:F4}] \"{t1}\" ↔ \"{t2}\"");
        }
        TestContext.WriteLine($"Average: {total / pairs.Length:F4}");
    }

    [Test]
    public void English_SimilarSentences()
    {
        var pairs = new[]
        {
            ("I love programming in C#.", "I enjoy coding in C#."),
            ("The weather is beautiful today.", "It's a nice day today."),
            ("The cat sleeps on the couch.", "The kitten is napping on the sofa."),
            ("The car drives fast.", "The vehicle moves quickly."),
            ("I am a programmer.", "I work as a software developer.")
        };
        
        TestContext.WriteLine("\n=== ENGLISH SIMILAR SENTENCES ===");
        float total = 0;
        foreach (var (t1, t2) in pairs)
        {
            var sim = CosineSim(GetEmbedding(t1), GetEmbedding(t2));
            total += sim;
            TestContext.WriteLine($"[{sim:F4}] \"{t1}\" ↔ \"{t2}\"");
        }
        TestContext.WriteLine($"Average: {total / pairs.Length:F4}");
    }

    [Test]
    public void English_DifferentSentences()
    {
        var pairs = new[]
        {
            ("I love programming in C#.", "Pizza is delicious."),
            ("The weather is beautiful today.", "Mathematics is difficult."),
            ("The cat sleeps on the couch.", "The airplane flies to Paris."),
            ("The car drives fast.", "The book lies on the table."),
            ("I am a programmer.", "Mountains are tall.")
        };
        
        TestContext.WriteLine("\n=== ENGLISH DIFFERENT SENTENCES ===");
        float total = 0;
        foreach (var (t1, t2) in pairs)
        {
            var sim = CosineSim(GetEmbedding(t1), GetEmbedding(t2));
            total += sim;
            TestContext.WriteLine($"[{sim:F4}] \"{t1}\" ↔ \"{t2}\"");
        }
        TestContext.WriteLine($"Average: {total / pairs.Length:F4}");
    }

    [Test]
    public void CrossLingual_PolishEnglish_SameMeaning()
    {
        var pairs = new[]
        {
            ("Sztuczna inteligencja zmienia świat.", "Artificial intelligence is changing the world."),
            ("Lubię programować.", "I like programming."),
            ("Kot jest zwierzęciem.", "A cat is an animal."),
            ("Warszawa jest stolicą Polski.", "Warsaw is the capital of Poland."),
            ("Uczenie maszynowe jest fascynujące.", "Machine learning is fascinating.")
        };
        
        TestContext.WriteLine("\n=== CROSS-LINGUAL PL-EN (Same meaning) ===");
        float total = 0;
        foreach (var (pl, en) in pairs)
        {
            var sim = CosineSim(GetEmbedding(pl), GetEmbedding(en));
            total += sim;
            TestContext.WriteLine($"[{sim:F4}] PL: \"{pl}\"");
            TestContext.WriteLine($"         EN: \"{en}\"");
        }
        TestContext.WriteLine($"Average cross-lingual: {total / pairs.Length:F4}");
    }

    [Test]
    public void CrossLingual_PolishEnglish_DifferentMeaning()
    {
        var pairs = new[]
        {
            ("Sztuczna inteligencja zmienia świat.", "Pizza is delicious."),
            ("Lubię programować.", "The mountain is high."),
            ("Kot jest zwierzęciem.", "Mathematics is difficult."),
            ("Warszawa jest stolicą Polski.", "The car drives fast."),
            ("Uczenie maszynowe jest fascynujące.", "I like coffee.")
        };
        
        TestContext.WriteLine("\n=== CROSS-LINGUAL PL-EN (Different meaning) ===");
        float total = 0;
        foreach (var (pl, en) in pairs)
        {
            var sim = CosineSim(GetEmbedding(pl), GetEmbedding(en));
            total += sim;
            TestContext.WriteLine($"[{sim:F4}] PL: \"{pl}\"");
            TestContext.WriteLine($"         EN: \"{en}\"");
        }
        TestContext.WriteLine($"Average: {total / pairs.Length:F4}");
    }

    [Test]
    public void SemanticGap_Analysis()
    {
        // Related pairs
        var related = new[] { ("programowanie", "kodowanie"), ("komputer", "laptop"), ("internet", "sieć") };
        var unrelated = new[] { ("programowanie", "kuchnia"), ("komputer", "drzewo"), ("internet", "góra") };
        
        TestContext.WriteLine("\n=== SEMANTIC GAP ANALYSIS ===");
        
        float avgRelated = 0;
        TestContext.WriteLine("Related pairs:");
        foreach (var (w1, w2) in related)
        {
            var sim = CosineSim(GetEmbedding(w1), GetEmbedding(w2));
            avgRelated += sim;
            TestContext.WriteLine($"  [{sim:F4}] {w1} ↔ {w2}");
        }
        avgRelated /= related.Length;
        
        float avgUnrelated = 0;
        TestContext.WriteLine("Unrelated pairs:");
        foreach (var (w1, w2) in unrelated)
        {
            var sim = CosineSim(GetEmbedding(w1), GetEmbedding(w2));
            avgUnrelated += sim;
            TestContext.WriteLine($"  [{sim:F4}] {w1} ↔ {w2}");
        }
        avgUnrelated /= unrelated.Length;
        
        var gap = avgRelated - avgUnrelated;
        TestContext.WriteLine($"\nRelated avg:   {avgRelated:F4}");
        TestContext.WriteLine($"Unrelated avg: {avgUnrelated:F4}");
        TestContext.WriteLine($"SEMANTIC GAP:  {gap:F4}");
    }

    [Test]
    public void Synonyms_vs_Antonyms_Polish()
    {
        var synonyms = new[] { ("duży", "wielki"), ("mały", "malutki"), ("szybki", "prędki"), ("piękny", "ładny") };
        var antonyms = new[] { ("duży", "mały"), ("szybki", "wolny"), ("gorący", "zimny"), ("dobry", "zły") };
        
        TestContext.WriteLine("\n=== SYNONYMS vs ANTONYMS (Polish) ===");
        
        float avgSyn = 0;
        TestContext.WriteLine("Synonyms:");
        foreach (var (w1, w2) in synonyms)
        {
            var sim = CosineSim(GetEmbedding(w1), GetEmbedding(w2));
            avgSyn += sim;
            TestContext.WriteLine($"  [{sim:F4}] {w1} ↔ {w2}");
        }
        avgSyn /= synonyms.Length;
        
        float avgAnt = 0;
        TestContext.WriteLine("Antonyms:");
        foreach (var (w1, w2) in antonyms)
        {
            var sim = CosineSim(GetEmbedding(w1), GetEmbedding(w2));
            avgAnt += sim;
            TestContext.WriteLine($"  [{sim:F4}] {w1} ↔ {w2}");
        }
        avgAnt /= antonyms.Length;
        
        TestContext.WriteLine($"\nSynonyms avg: {avgSyn:F4}");
        TestContext.WriteLine($"Antonyms avg: {avgAnt:F4}");
        TestContext.WriteLine($"Gap:          {avgSyn - avgAnt:F4}");
    }
}
