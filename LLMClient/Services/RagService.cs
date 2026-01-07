using System.Text;
using System.Text.RegularExpressions;
using LLMClient.Models;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace LLMClient.Services;

public class RagService : IRagService
{
    private readonly IDatabaseService _databaseService;
    private readonly IEmbeddingService? _embeddingService;

    private const int MaxChunkChars = 1500;
    private const int ChunkOverlapChars = 200;
    private const int MinChunkChars = 100;

    public RagService(IDatabaseService databaseService, IEmbeddingService? embeddingService = null)
    {
        _databaseService = databaseService;
        _embeddingService = embeddingService;
    }

    public async Task<RagDocument> AddDocumentAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        var content = extension switch
        {
            ".txt" or ".md" or ".markdown" or ".csv" or ".json" or ".xml" or ".html" or ".htm"
                => await File.ReadAllTextAsync(filePath),
            ".pdf" => ExtractTextFromPdf(filePath),
            ".docx" => ExtractTextFromDocx(filePath),
            _ => throw new NotSupportedException($"Nieobsługiwany typ pliku: {extension}. Obsługiwane: .txt, .md, .csv, .json, .xml, .html, .pdf, .docx")
        };

        content = CleanText(content);

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Plik jest pusty lub nie zawiera czytelnego tekstu.");

        return await AddDocumentFromContentAsync(fileName, content);
    }

    public async Task<RagDocument> AddDocumentFromContentAsync(string fileName, string content)
    {
        var document = new RagDocument
        {
            FileName = fileName,
            Content = content,
            CreatedAt = DateTime.Now
        };

        await _databaseService.SaveRagDocumentAsync(document);

        var chunks = ChunkText(content);
        await _databaseService.SaveRagChunksAsync(document.Id, chunks);

        document.ChunkCount = chunks.Count;
        System.Diagnostics.Debug.WriteLine($"[RagService] Added document '{fileName}' with {chunks.Count} chunks");

        return document;
    }

    private static string ExtractTextFromPdf(string filePath)
    {
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(filePath);

        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return sb.ToString();
    }

    private static string ExtractTextFromDocx(string filePath)
    {
        var sb = new StringBuilder();
        using var doc = WordprocessingDocument.Open(filePath, false);

        var body = doc.MainDocumentPart?.Document.Body;
        if (body == null) return string.Empty;

        foreach (var para in body.Elements<Paragraph>())
        {
            var text = para.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine(text);
            }
        }

        return sb.ToString();
    }

    private static string CleanText(string text)
    {
        text = Regex.Replace(text, @"\r\n|\r|\n", "\n");
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    public async Task<List<RagDocument>> GetDocumentsAsync()
    {
        return await _databaseService.GetRagDocumentsAsync();
    }

    public async Task DeleteDocumentAsync(int documentId)
    {
        await _databaseService.DeleteRagDocumentAsync(documentId);
    }

    public async Task<string> GetRelevantContextAsync(string query, int topK = 3, float minSimilarity = 0.5f, RetrievalMode mode = RetrievalMode.Hybrid)
    {
        if (_embeddingService == null || !_embeddingService.IsInitialized)
        {
            System.Diagnostics.Debug.WriteLine("[RagService] Embedding service not available, using keyword search only");
            mode = RetrievalMode.Keyword;
        }

        // OPTYMALIZACJA: Pre-filtrowanie keyword zamiast ładowania wszystkich chunków
        var queryTerms = query.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2) // Ignoruj krótkie słowa
            .Take(5) // Maksymalnie 5 słów kluczowych
            .ToArray();
        
        // Pobierz tylko chunki pasujące do słów kluczowych (limit 100)
        var candidateChunks = await _databaseService.GetRagChunksWithKeywordFilterAsync(queryTerms, limit: 500);
        
        if (candidateChunks.Count == 0)
        {
            // Fallback: pobierz wszystkie chunki z embeddingami
            candidateChunks = await _databaseService.GetAllRagChunksAsync();
        }
        
        if (candidateChunks.Count == 0) return string.Empty;
        
        System.Diagnostics.Debug.WriteLine($"[RagService] Pre-filtered to {candidateChunks.Count} candidate chunks");

        // Użyj Dictionary dla bezpiecznej modyfikacji i lepszej wydajności O(1)
        var scoredDict = new Dictionary<int, (RagChunk Chunk, float Score)>();

        if (mode == RetrievalMode.Vector || mode == RetrievalMode.Hybrid)
        {
            var queryEmbedding = await _embeddingService!.GenerateEmbeddingAsync(query, isQuery: true);
            if (queryEmbedding != null)
            {
                foreach (var chunk in candidateChunks.Where(c => c.Embedding != null))
                {
                    var chunkEmbedding = BytesToFloatArray(chunk.Embedding!);
                    var similarity = CosineSimilarity(queryEmbedding, chunkEmbedding);
                    if (similarity >= minSimilarity)
                    {
                        scoredDict[chunk.Id] = (chunk, similarity);
                    }
                }
            }
        }

        if (mode == RetrievalMode.Keyword || mode == RetrievalMode.Hybrid)
        {
            foreach (var chunk in candidateChunks)
            {
                var contentLower = chunk.Content.ToLowerInvariant();
                var matchCount = queryTerms.Count(t => contentLower.Contains(t));
                if (matchCount > 0)
                {
                    var keywordScore = (float)matchCount / Math.Max(queryTerms.Length, 1);
                    if (scoredDict.TryGetValue(chunk.Id, out var existing))
                    {
                        var hybridScore = (existing.Score * 0.7f) + (keywordScore * 0.3f);
                        scoredDict[chunk.Id] = (chunk, hybridScore);
                    }
                    else if (keywordScore >= 0.3f)
                    {
                        scoredDict[chunk.Id] = (chunk, keywordScore * 0.5f);
                    }
                }
            }
        }

        var topChunks = scoredDict.Values
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Chunk.Content);

        return string.Join("\n\n---\n\n", topChunks);
    }

    public async Task<RetrievalResult> GetRelevantContextWithTraceAsync(string query, int topK = 3, float minSimilarity = 0.5f, RetrievalMode mode = RetrievalMode.Hybrid)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var trace = new RagTrace
        {
            Query = query,
            RetrievalMode = mode
        };

        if (_embeddingService == null || !_embeddingService.IsInitialized)
        {
            mode = RetrievalMode.Keyword;
            trace.RetrievalMode = mode;
        }

        // OPTYMALIZACJA: Pre-filtrowanie keyword zamiast ładowania wszystkich chunków
        var queryTerms = query.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2)
            .Take(5)
            .ToArray();
        
        var candidateChunks = await _databaseService.GetRagChunksWithKeywordFilterAsync(queryTerms, limit: 500);
        if (candidateChunks.Count == 0)
        {
            candidateChunks = await _databaseService.GetAllRagChunksAsync();
        }
        
        var documents = await _databaseService.GetRagDocumentsAsync();
        var docLookup = documents.ToDictionary(d => d.Id, d => d.FileName);
        
        trace.Timings.Add(new RagTiming("PreFilter", 0)); // Placeholder for timing

        if (candidateChunks.Count == 0)
        {
            return new RetrievalResult { Trace = trace };
        }

        // Użyj Dictionary dla bezpiecznej modyfikacji i lepszej wydajności O(1)
        var scoredDict = new Dictionary<int, (RagChunk Chunk, float VectorScore, float KeywordScore, float FinalScore)>();

        // Vector search
        var vectorSw = System.Diagnostics.Stopwatch.StartNew();
        if (mode == RetrievalMode.Vector || mode == RetrievalMode.Hybrid)
        {
            var queryEmbedding = await _embeddingService!.GenerateEmbeddingAsync(query, isQuery: true);
            if (queryEmbedding != null)
            {
                foreach (var chunk in candidateChunks.Where(c => c.Embedding != null))
                {
                    var chunkEmbedding = BytesToFloatArray(chunk.Embedding!);
                    var similarity = CosineSimilarity(queryEmbedding, chunkEmbedding);
                    scoredDict[chunk.Id] = (chunk, similarity, 0f, similarity);
                }
            }
        }
        trace.Timings.Add(new RagTiming("VectorSearch", vectorSw.ElapsedMilliseconds));

        // Keyword search
        var keywordSw = System.Diagnostics.Stopwatch.StartNew();
        if (mode == RetrievalMode.Keyword || mode == RetrievalMode.Hybrid)
        {
            foreach (var chunk in candidateChunks)
            {
                var contentLower = chunk.Content.ToLowerInvariant();
                var matchCount = queryTerms.Count(t => contentLower.Contains(t));
                if (matchCount > 0)
                {
                    var keywordScore = (float)matchCount / Math.Max(queryTerms.Length, 1);
                    if (scoredDict.TryGetValue(chunk.Id, out var existing))
                    {
                        var hybridScore = (existing.VectorScore * 0.7f) + (keywordScore * 0.3f);
                        scoredDict[chunk.Id] = (chunk, existing.VectorScore, keywordScore, hybridScore);
                    }
                    else
                    {
                        scoredDict[chunk.Id] = (chunk, 0f, keywordScore, keywordScore * 0.5f);
                    }
                }
            }
        }
        trace.Timings.Add(new RagTiming("KeywordSearch", keywordSw.ElapsedMilliseconds));

        // Filter and rank
        var filtered = scoredDict.Values.Where(s => s.FinalScore >= minSimilarity).OrderByDescending(s => s.FinalScore).ToList();
        var topResults = filtered.Take(topK).ToList();

        // Build candidates for trace
        int rank = 1;
        foreach (var item in filtered)
        {
            var docName = docLookup.TryGetValue(item.Chunk.DocumentId, out var name) ? name : "Unknown";
            var candidate = new RagChunkCandidate(
                item.Chunk.Id,
                docName,
                item.Chunk.Section,
                item.Chunk.ChunkIndex,
                item.VectorScore,
                item.KeywordScore,
                item.FinalScore,
                item.Chunk.Content.Length / 4, // Approximate token count
                topResults.Any(t => t.Chunk.Id == item.Chunk.Id),
                item.Chunk.Content.Length > 100 ? item.Chunk.Content[..100] + "..." : item.Chunk.Content
            ) { Rank = rank++ };
            trace.Candidates.Add(candidate);
        }

        // Build result
        var retrievedChunks = topResults.Select(t => new RetrievedChunk
        {
            ChunkId = t.Chunk.Id,
            DocumentId = t.Chunk.DocumentId,
            DocumentName = docLookup.TryGetValue(t.Chunk.DocumentId, out var n) ? n : "Unknown",
            Content = t.Chunk.Content,
            Score = t.FinalScore,
            ChunkIndex = t.Chunk.ChunkIndex
        }).ToList();

        sw.Stop();
        trace.Timings.Add(new RagTiming("Total", sw.ElapsedMilliseconds));

        return new RetrievalResult
        {
            Context = string.Join("\n\n---\n\n", topResults.Select(t => t.Chunk.Content)),
            Chunks = retrievedChunks,
            Trace = trace,
            TotalChunksEvaluated = candidateChunks.Count,
            RetrievalTimeMs = sw.ElapsedMilliseconds
        };
    }

    public async Task GenerateEmbeddingsAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_embeddingService == null || !_embeddingService.IsInitialized)
        {
            progress?.Report("Embedding service nie jest dostępny");
            return;
        }

        var allChunks = await _databaseService.GetAllRagChunksAsync();
        var chunksToProcess = allChunks.Where(c => c.Embedding == null).ToList();

        progress?.Report($"Przetwarzanie {chunksToProcess.Count} chunków...");

        for (int i = 0; i < chunksToProcess.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunk = chunksToProcess[i];
            progress?.Report($"Embedding chunk {i + 1}/{chunksToProcess.Count}");

            var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Content, isQuery: false);
            if (embedding != null)
            {
                chunk.Embedding = FloatArrayToBytes(embedding);
                chunk.EmbeddingVersion = 1;
                await _databaseService.UpdateRagChunkEmbeddingAsync(chunk);
            }

            await Task.Delay(10, cancellationToken); // Zmniejszono z 50ms dla lepszej wydajności
        }

        progress?.Report("Zakończono!");
    }

    public async Task<int> GetPendingChunksCountAsync()
    {
        var allChunks = await _databaseService.GetAllRagChunksAsync();
        return allChunks.Count(c => c.Embedding == null);
    }

    private static List<string> ChunkText(string text)
    {
        var chunks = new List<string>();
        var paragraphs = text.Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new StringBuilder();

        foreach (var para in paragraphs)
        {
            var trimmedPara = para.Trim();
            if (string.IsNullOrWhiteSpace(trimmedPara)) continue;

            if (currentChunk.Length + trimmedPara.Length + 2 > MaxChunkChars && currentChunk.Length >= MinChunkChars)
            {
                chunks.Add(currentChunk.ToString().Trim());
                var overlapText = GetOverlapText(currentChunk.ToString(), ChunkOverlapChars);
                currentChunk.Clear();
                if (!string.IsNullOrEmpty(overlapText))
                {
                    currentChunk.Append(overlapText).Append(' ');
                }
            }

            if (trimmedPara.Length > MaxChunkChars)
            {
                var sentences = SplitIntoSentences(trimmedPara);
                foreach (var sentence in sentences)
                {
                    if (currentChunk.Length + sentence.Length + 1 > MaxChunkChars && currentChunk.Length >= MinChunkChars)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        var overlapText = GetOverlapText(currentChunk.ToString(), ChunkOverlapChars);
                        currentChunk.Clear();
                        if (!string.IsNullOrEmpty(overlapText))
                        {
                            currentChunk.Append(overlapText).Append(' ');
                        }
                    }
                    currentChunk.Append(sentence).Append(' ');
                }
            }
            else
            {
                currentChunk.Append(trimmedPara).Append("\n\n");
            }
        }

        if (currentChunk.Length >= MinChunkChars)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        return chunks;
    }

    private static string GetOverlapText(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;
        var lastSpaceIndex = text.LastIndexOf(' ', text.Length - 1, Math.Min(maxChars, text.Length));
        return lastSpaceIndex > 0 ? text[(text.Length - lastSpaceIndex)..] : text[^maxChars..];
    }

    private static List<string> SplitIntoSentences(string text)
    {
        return Regex.Split(text, @"(?<=[.!?])\s+").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denom = (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        return denom > 0 ? dot / denom : 0;
    }

    private static byte[] FloatArrayToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] BytesToFloatArray(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    public async Task ClearAllEmbeddingsAsync()
    {
        var allChunks = await _databaseService.GetAllRagChunksAsync();
        foreach (var chunk in allChunks)
        {
            chunk.Embedding = null;
            chunk.EmbeddingVersion = 0;
            await _databaseService.UpdateRagChunkEmbeddingAsync(chunk);
        }
        System.Diagnostics.Debug.WriteLine($"[RagService] Cleared embeddings for {allChunks.Count} chunks");
    }
}
