using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using WPFLLM.Models;

namespace WPFLLM.Services;

public interface ILocalEmbeddingService
{
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<float[]> GetEmbeddingAsync(string text, bool isQuery, CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync();
    Task InitializeAsync(string modelId);
    void Dispose();
    int GetDimensions();
}

public class LocalEmbeddingService : ILocalEmbeddingService, IDisposable
{
    private InferenceSession? _session;
    private string? _currentModelId;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private const int MaxSequenceLength = 256;

    public async Task InitializeAsync(string modelId)
    {
        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized && _currentModelId == modelId)
                return;

            Dispose();

            if (!EmbeddingModels.Available.TryGetValue(modelId, out var modelInfo))
                throw new ArgumentException($"Unknown model: {modelId}");

            var modelPath = EmbeddingModels.GetModelPath(modelId);
            var onnxPath = Path.Combine(modelPath, "model.onnx");
            var tokenizerPath = Path.Combine(modelPath, "tokenizer.json");

            if (!File.Exists(onnxPath))
                throw new FileNotFoundException($"Model file not found: {onnxPath}");

            if (!File.Exists(tokenizerPath))
                throw new FileNotFoundException($"Tokenizer file not found: {tokenizerPath}");

            // Load ONNX model
            var sessionOptions = new SessionOptions();
            sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            _session = new InferenceSession(onnxPath, sessionOptions);

            // Initialize Rust tokenizer (HuggingFace with add_special_tokens=true)
            if (!RustTokenizer.Initialize(tokenizerPath))
                throw new Exception($"Failed to initialize tokenizer from: {tokenizerPath}");

            _currentModelId = modelId;
            _isInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        return GetEmbeddingAsync(text, isQuery: true, cancellationToken);
    }

    public Task<float[]> GetEmbeddingAsync(string text, bool isQuery, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized || _session == null || !RustTokenizer.IsInitialized)
            throw new InvalidOperationException("Local embedding service not initialized");

        // E5 models require prefix - CRITICAL for good embeddings
        var prefixedText = PrepareE5Text(text, isQuery);

        // Tokenize with Rust HuggingFace tokenizer (add_special_tokens=true automatically!)
        var inputIds = RustTokenizer.Encode(prefixedText, MaxSequenceLength);

        // Create attention mask (all tokens are valid)
        var attentionMask = inputIds.Select(_ => 1L).ToArray();

        // Create tensors
        var inputIdsTensor = new DenseTensor<long>(inputIds.Select(x => (long)x).ToArray(), [1, inputIds.Length]);
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, [1, attentionMask.Length]);

        // Run inference
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
        };

        using var results = _session.Run(inputs);
        
        var output = results.FirstOrDefault(r => r.Name == "last_hidden_state") 
                  ?? results.First();

        var outputTensor = output.AsTensor<float>();
        
        // Mean pooling over sequence dimension (E5 requires mean pooling, NOT CLS)
        var embedding = MeanPooling(outputTensor, attentionMask);
        
        // L2 normalize - CRITICAL for cosine similarity
        var normalized = L2Normalize(embedding);
        
        return Task.FromResult(normalized);
    }

    private string PrepareE5Text(string text, bool isQuery)
    {
        // Get model info for instruct format check
        if (_currentModelId != null && EmbeddingModels.Available.TryGetValue(_currentModelId, out var modelInfo))
        {
            if (modelInfo.IsInstructModel)
            {
                // Instruct models: "Instruct: {task}\nQuery: {query}" for queries, no prefix for documents
                if (isQuery)
                {
                    if (text.StartsWith("Instruct:"))
                        return text;
                    return $"Instruct: {modelInfo.DefaultTaskInstruction}\nQuery: {text}";
                }
                else
                {
                    // Documents don't need prefix for instruct models
                    return text;
                }
            }
        }
        
        // Standard E5 models: query:/passage: prefixes
        var prefix = isQuery ? "query: " : "passage: ";
        
        // Don't double-prefix
        if (text.StartsWith("query:") || text.StartsWith("passage:"))
            return text;
            
        return prefix + text;
    }

    private static float[] MeanPooling(Tensor<float> lastHiddenState, long[] attentionMask)
    {
        var dimensions = lastHiddenState.Dimensions.ToArray();
        
        // Shape: [batch, seq_len, hidden_size]
        if (dimensions.Length == 2)
        {
            var result = new float[dimensions[1]];
            for (int i = 0; i < dimensions[1]; i++)
                result[i] = lastHiddenState[0, i];
            return result;
        }

        var seqLen = dimensions[1];
        var hiddenSize = dimensions[2];
        var embedding = new float[hiddenSize];
        var sumMask = 0f;

        for (int i = 0; i < seqLen; i++)
        {
            if (attentionMask[i] == 1)
            {
                for (int j = 0; j < hiddenSize; j++)
                    embedding[j] += lastHiddenState[0, i, j];
                sumMask += 1f;
            }
        }

        if (sumMask > 0)
            for (int i = 0; i < hiddenSize; i++)
                embedding[i] /= sumMask;

        return embedding;
    }

    private static float[] L2Normalize(float[] vector)
    {
        var norm = (float)Math.Sqrt(vector.Sum(x => x * x));
        if (norm < 1e-12f) return vector;
        return vector.Select(x => x / norm).ToArray();
    }

    public Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(_isInitialized && _session != null && RustTokenizer.IsInitialized);
    }

    public int GetDimensions()
    {
        if (_currentModelId != null && EmbeddingModels.Available.TryGetValue(_currentModelId, out var info))
            return info.Dimensions;
        return 0;
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
        _isInitialized = false;
        _currentModelId = null;
    }
}
