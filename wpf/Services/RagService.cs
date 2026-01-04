using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using WPFLLM.Models;

namespace WPFLLM.Services;

public class RagService : IRagService
{
    private readonly IDatabaseService _database;
    private readonly ILlmService _llmService;
    
    // Chunking parameters - optimized for E5 embeddings (max 512 tokens ≈ 2000 chars)
    private const int MaxChunkChars = 1500;
    private const int ChunkOverlapChars = 200;
    private const int MinChunkChars = 100;

    public RagService(IDatabaseService database, ILlmService llmService)
    {
        _database = database;
        _llmService = llmService;
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
            _ => throw new NotSupportedException($"Unsupported file type: {extension}. Supported: .txt, .md, .csv, .json, .xml, .html, .pdf, .docx")
        };
        
        // Clean up content
        content = CleanText(content);
        
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("File is empty or contains no readable text.");
        
        var document = await _database.AddDocumentAsync(fileName, content);
        var chunks = ChunkText(content);
        await _database.AddChunksAsync(document.Id, chunks);
        
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
        // Remove excessive whitespace
        text = Regex.Replace(text, @"\r\n|\r|\n", "\n");
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    public Task<List<RagDocument>> GetDocumentsAsync() => _database.GetDocumentsAsync();

    public Task DeleteDocumentAsync(long documentId) => _database.DeleteDocumentAsync(documentId);

    public async Task<string> GetRelevantContextAsync(string query, int topK = 3, double minSimilarity = 0.75)
    {
        var queryEmbedding = await _llmService.GetEmbeddingAsync(query);
        if (queryEmbedding.Length == 0) return string.Empty;

        var allChunks = await _database.GetAllChunksAsync();
        var chunksWithEmbeddings = allChunks
            .Where(c => !string.IsNullOrEmpty(c.Embedding))
            .ToList();

        if (chunksWithEmbeddings.Count == 0) return string.Empty;

        var scored = new List<(RagChunk Chunk, double Score)>();
        foreach (var chunk in chunksWithEmbeddings)
        {
            var embedding = JsonSerializer.Deserialize<float[]>(chunk.Embedding!);
            if (embedding != null)
            {
                var score = CosineSimilarity(queryEmbedding, embedding);
                // Filter by minimum similarity threshold
                if (score >= minSimilarity)
                {
                    scored.Add((chunk, score));
                }
            }
        }

        var topChunks = scored
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Chunk.Content);

        return string.Join("\n\n---\n\n", topChunks);
    }

    public async Task GenerateEmbeddingsAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var allChunks = await _database.GetAllChunksAsync();
        var chunksToProcess = allChunks.Where(c => string.IsNullOrEmpty(c.Embedding)).ToList();

        progress?.Report($"Processing {chunksToProcess.Count} chunks...");

        for (int i = 0; i < chunksToProcess.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var chunk = chunksToProcess[i];
            progress?.Report($"Embedding chunk {i + 1}/{chunksToProcess.Count}");

            var embedding = await _llmService.GetEmbeddingAsync(chunk.Content, isQuery: false, cancellationToken);
            if (embedding.Length > 0)
            {
                var embeddingJson = JsonSerializer.Serialize(embedding);
                await _database.UpdateChunkEmbeddingAsync(chunk.Id, embeddingJson);
            }

            await Task.Delay(100, cancellationToken); // Rate limiting
        }

        progress?.Report("Done!");
    }

    private static List<string> ChunkText(string text)
    {
        var chunks = new List<string>();
        
        // Split by paragraphs first (preserve semantic boundaries)
        var paragraphs = text.Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new StringBuilder();
        
        foreach (var para in paragraphs)
        {
            var trimmedPara = para.Trim();
            if (string.IsNullOrWhiteSpace(trimmedPara)) continue;
            
            // If adding this paragraph exceeds max, save current chunk and start new
            if (currentChunk.Length + trimmedPara.Length + 2 > MaxChunkChars && currentChunk.Length >= MinChunkChars)
            {
                chunks.Add(currentChunk.ToString().Trim());
                
                // Start new chunk with overlap from end of previous
                var overlapText = GetOverlapText(currentChunk.ToString(), ChunkOverlapChars);
                currentChunk.Clear();
                if (!string.IsNullOrEmpty(overlapText))
                {
                    currentChunk.Append(overlapText).Append(' ');
                }
            }
            
            // If single paragraph is too long, split by sentences
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
        
        // Don't forget the last chunk
        if (currentChunk.Length >= MinChunkChars)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }
        
        return chunks;
    }
    
    private static string GetOverlapText(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;
        
        // Try to break at sentence boundary
        var lastPart = text[^maxChars..];
        var sentenceEnd = lastPart.IndexOfAny(['.', '!', '?', '\n']);
        if (sentenceEnd > 0 && sentenceEnd < lastPart.Length - 20)
        {
            return lastPart[(sentenceEnd + 1)..].Trim();
        }
        return lastPart.Trim();
    }
    
    private static List<string> SplitIntoSentences(string text)
    {
        // Simple sentence splitting - handles . ! ? followed by space and capital letter
        var sentences = Regex.Split(text, @"(?<=[.!?])\s+(?=[A-ZĄĆĘŁŃÓŚŹŻ])");
        return sentences.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom == 0 ? 0 : dot / denom;
    }

    public async Task<RetrievalResult> RetrieveAsync(string query, int topK = 5, double minSimilarity = 0.7, 
        RetrievalMode mode = RetrievalMode.Hybrid, double rrfK = 60.0, double hybridBalance = 0.5)
    {
        var (result, _) = await RetrieveWithTraceAsync(query, topK, minSimilarity, mode, rrfK, hybridBalance);
        return result;
    }

    public async Task<(RetrievalResult Result, RagTrace Trace)> RetrieveWithTraceAsync(
        string query, 
        int topK = 5, 
        double minSimilarity = 0.7, 
        RetrievalMode mode = RetrievalMode.Hybrid,
        double rrfK = 60.0,
        double hybridBalance = 0.5,
        CancellationToken cancellationToken = default)
    {
        // Convert balance to weights: 0=keyword only, 0.5=balanced, 1=vector only
        var vectorWeight = hybridBalance * 2;
        var keywordWeight = (1 - hybridBalance) * 2;
        
        var trace = new RagTrace 
        { 
            Query = query,
            RetrievalMode = mode,
            FusionFormula = $"RRF(k={rrfK}) Balance:{hybridBalance:P0} (V:{vectorWeight:F1}/K:{keywordWeight:F1})"
        };
        
        var tokenCounter = new EstimationTokenCounter();
        
        var result = new RetrievalResult
        {
            Metrics = new RetrievalMetrics
            {
                Mode = mode,
                TopK = topK,
                MinSimilarity = minSimilarity
            }
        };

        var vectorResults = new List<(RagChunk Chunk, double Score)>();
        var keywordResults = new List<(RagChunk Chunk, double Score)>();
        float[] queryEmbedding = [];
        List<RagChunk> allChunks = [];
        
        // Vector search with timing
        if (mode is RetrievalMode.Vector or RetrievalMode.Hybrid)
        {
            queryEmbedding = await trace.MeasureAsync("EmbedQuery", async () => 
                await _llmService.GetEmbeddingAsync(query, isQuery: true, cancellationToken));
            
            if (queryEmbedding.Length > 0)
            {
                allChunks = await trace.MeasureAsync("LoadChunks", async () => 
                    await _database.GetAllChunksAsync());
                result.Metrics.TotalChunksSearched = allChunks.Count;
                
                trace.Measure("VectorSearch", () =>
                {
                    foreach (var chunk in allChunks.Where(c => !string.IsNullOrEmpty(c.Embedding)))
                    {
                        var embedding = JsonSerializer.Deserialize<float[]>(chunk.Embedding!);
                        if (embedding != null)
                        {
                            var score = CosineSimilarity(queryEmbedding, embedding);
                            vectorResults.Add((chunk, score));
                        }
                    }
                });
                result.Metrics.VectorMatches = vectorResults.Count(v => v.Score >= minSimilarity);
            }
        }

        // Keyword search (FTS5) with timing
        if (mode is RetrievalMode.Keyword or RetrievalMode.Hybrid)
        {
            keywordResults = await trace.MeasureAsync("KeywordSearch", async () => 
                await _database.SearchChunksFtsAsync(query, topK * 3));
            result.Metrics.KeywordMatches = keywordResults.Count;
            
            if (result.Metrics.TotalChunksSearched == 0)
            {
                allChunks = await _database.GetAllChunksAsync();
                result.Metrics.TotalChunksSearched = allChunks.Count;
            }
        }

        // Fuse results using RRF with timing
        List<RetrievedChunk> fusedResults = [];
        trace.Measure("Merge+Rerank", () =>
        {
            fusedResults = FuseResultsWithAllCandidates(vectorResults, keywordResults, mode, minSimilarity, rrfK, vectorWeight, keywordWeight);
        });
        
        // Sort and select top K
        var sortedResults = fusedResults
            .OrderByDescending(x => x.FusedScore)
            .ToList();
        
        var topResults = sortedResults.Take(topK).ToList();
        var selectedIds = topResults.Select(r => r.ChunkId).ToHashSet();

        // Build trace candidates (all evaluated chunks)
        trace.Measure("BuildTrace", () =>
        {
            int rank = 0;
            foreach (var item in sortedResults.Take(topK * 2)) // Show top 2x candidates
            {
                rank++;
                var preview = item.Content.Length > 400 
                    ? item.Content[..400] + "…" 
                    : item.Content;
                
                trace.Candidates.Add(new RagChunkCandidate(
                    ChunkId: item.ChunkId,
                    SourceName: item.DocumentName,
                    Section: null,
                    ChunkIndex: item.ChunkIndex,
                    VectorScore: (float)item.VectorScore,
                    KeywordScore: (float)item.KeywordScore,
                    FinalScore: (float)item.FusedScore,
                    TokenCount: tokenCounter.CountTokens(item.Content),
                    Included: selectedIds.Contains(item.ChunkId),
                    Preview: preview,
                    MatchedTerms: item.MatchedTerms
                ) { Rank = rank });
            }
        });

        // Enrich with document names
        foreach (var item in topResults)
        {
            item.DocumentName = await _database.GetDocumentNameAsync(item.DocumentId) ?? "Unknown";
        }

        // Update candidate source names after enrichment
        for (int i = 0; i < trace.Candidates.Count; i++)
        {
            var candidate = trace.Candidates[i];
            var matchingResult = topResults.FirstOrDefault(r => r.ChunkId == candidate.ChunkId);
            if (matchingResult != null && candidate.SourceName != matchingResult.DocumentName)
            {
                trace.Candidates[i] = candidate with { SourceName = matchingResult.DocumentName };
            }
        }

        result.Chunks = topResults;
        result.Metrics.FinalResults = topResults.Count;
        
        // Calculate timing metrics
        var embeddingTime = trace.Timings.FirstOrDefault(t => t.Name == "EmbedQuery")?.ElapsedMs ?? 0;
        result.Metrics.EmbeddingTimeMs = embeddingTime;
        result.Metrics.RetrievalTimeMs = trace.TotalTimeMs - embeddingTime;
        result.Metrics.TotalTimeMs = trace.TotalTimeMs;

        return (result, trace);
    }

    private static List<RetrievedChunk> FuseResultsWithAllCandidates(
        List<(RagChunk Chunk, double Score)> vectorResults,
        List<(RagChunk Chunk, double Score)> keywordResults,
        RetrievalMode mode,
        double minSimilarity,
        double rrfK = 60.0,
        double vectorWeight = 1.0,
        double keywordWeight = 1.0)
    {
        var fusedScores = new Dictionary<long, RetrievedChunk>();

        // Process vector results (include all for trace, filter later)
        if (mode is RetrievalMode.Vector or RetrievalMode.Hybrid)
        {
            var ranked = vectorResults.OrderByDescending(x => x.Score).ToList();
            for (int i = 0; i < ranked.Count; i++)
            {
                var (chunk, score) = ranked[i];
                
                // Only contribute to RRF if above threshold
                var rrfScore = score >= minSimilarity ? vectorWeight / (rrfK + i + 1) : 0;
                
                if (!fusedScores.TryGetValue(chunk.Id, out var existing))
                {
                    existing = new RetrievedChunk
                    {
                        ChunkId = chunk.Id,
                        DocumentId = chunk.DocumentId,
                        Content = chunk.Content,
                        ChunkIndex = chunk.ChunkIndex
                    };
                    fusedScores[chunk.Id] = existing;
                }
                
                existing.VectorScore = score;
                existing.FusedScore += rrfScore;
            }
        }

        // Process keyword results
        if (mode is RetrievalMode.Keyword or RetrievalMode.Hybrid)
        {
            var ranked = keywordResults.OrderByDescending(x => x.Score).ToList();
            for (int i = 0; i < ranked.Count; i++)
            {
                var (chunk, score) = ranked[i];
                var rrfScore = keywordWeight / (rrfK + i + 1);
                
                if (!fusedScores.TryGetValue(chunk.Id, out var existing))
                {
                    existing = new RetrievedChunk
                    {
                        ChunkId = chunk.Id,
                        DocumentId = chunk.DocumentId,
                        Content = chunk.Content,
                        ChunkIndex = chunk.ChunkIndex
                    };
                    fusedScores[chunk.Id] = existing;
                }
                
                existing.KeywordScore = score;
                existing.FusedScore += rrfScore;
            }
        }

        // Filter: must have positive fused score (at least one valid contribution)
        return fusedScores.Values
            .Where(x => x.FusedScore > 0)
            .ToList();
    }

    private static List<RetrievedChunk> FuseResults(
        List<(RagChunk Chunk, double Score)> vectorResults,
        List<(RagChunk Chunk, double Score)> keywordResults,
        RetrievalMode mode)
    {
        const double k = 60.0; // RRF constant
        var fusedScores = new Dictionary<long, RetrievedChunk>();

        // Process vector results
        if (mode is RetrievalMode.Vector or RetrievalMode.Hybrid)
        {
            var ranked = vectorResults.OrderByDescending(x => x.Score).ToList();
            for (int i = 0; i < ranked.Count; i++)
            {
                var (chunk, score) = ranked[i];
                var rrfScore = 1.0 / (k + i + 1);
                
                if (!fusedScores.TryGetValue(chunk.Id, out var existing))
                {
                    existing = new RetrievedChunk
                    {
                        ChunkId = chunk.Id,
                        DocumentId = chunk.DocumentId,
                        Content = chunk.Content,
                        ChunkIndex = chunk.ChunkIndex
                    };
                    fusedScores[chunk.Id] = existing;
                }
                
                existing.VectorScore = score;
                existing.FusedScore += rrfScore;
            }
        }

        // Process keyword results
        if (mode is RetrievalMode.Keyword or RetrievalMode.Hybrid)
        {
            var ranked = keywordResults.OrderByDescending(x => x.Score).ToList();
            for (int i = 0; i < ranked.Count; i++)
            {
                var (chunk, score) = ranked[i];
                var rrfScore = 1.0 / (k + i + 1);
                
                if (!fusedScores.TryGetValue(chunk.Id, out var existing))
                {
                    existing = new RetrievedChunk
                    {
                        ChunkId = chunk.Id,
                        DocumentId = chunk.DocumentId,
                        Content = chunk.Content,
                        ChunkIndex = chunk.ChunkIndex
                    };
                    fusedScores[chunk.Id] = existing;
                }
                
                existing.KeywordScore = score;
                existing.FusedScore += rrfScore;
            }
        }

        return fusedScores.Values.ToList();
    }
}
