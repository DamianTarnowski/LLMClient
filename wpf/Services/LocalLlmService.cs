using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using Microsoft.ML.OnnxRuntimeGenAI;
using WPFLLM.Models;

namespace WPFLLM.Services;

public class LocalLlmService : ILocalLlmService, IDisposable
{
    private readonly HttpClient _httpClient;
    private Model? _model;
    private Tokenizer? _tokenizer;
    private string? _currentModelId;
    private bool _disposed;

    public LocalLlmService(HttpClient httpClient) => _httpClient = httpClient;
    public Task<bool> IsAvailableAsync() => Task.FromResult(_model != null && _tokenizer != null);

    public Task<bool> IsModelDownloadedAsync(string modelId)
    {
        if (!LocalLlmModels.Available.TryGetValue(modelId, out var info)) return Task.FromResult(false);
        var path = LocalLlmModels.GetModelPath(modelId);
        if (!Directory.Exists(path)) return Task.FromResult(false);
        return Task.FromResult(info.RequiredFiles.All(f => File.Exists(Path.Combine(path, f))));
    }

    public async Task InitializeAsync(string modelId)
    {
        if (_currentModelId == modelId && _model != null) return;
        DisposeModel();
        if (!await IsModelDownloadedAsync(modelId)) throw new InvalidOperationException("Model not downloaded");
        var modelPath = LocalLlmModels.GetModelPath(modelId);
        _model = new Model(modelPath);
        _tokenizer = new Tokenizer(_model);
        _currentModelId = modelId;
    }

    public async IAsyncEnumerable<string> GenerateStreamAsync(string prompt, string? systemPrompt = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_model == null || _tokenizer == null) throw new InvalidOperationException("Not initialized");
        var fullPrompt = FormatPhi3Prompt(prompt, systemPrompt ?? "You are a helpful assistant.");
        using var inputSequences = _tokenizer.Encode(fullPrompt);
        using var genParams = new GeneratorParams(_model);
        genParams.SetSearchOption("max_length", 2048);
        genParams.SetSearchOption("temperature", 0.7);
        genParams.SetSearchOption("top_p", 0.9);
        genParams.SetSearchOption("repetition_penalty", 1.1);
        using var generator = new Generator(_model, genParams);
        generator.AppendTokenSequences(inputSequences);
        using var stream = _tokenizer.CreateStream();
        while (!generator.IsDone())
        {
            ct.ThrowIfCancellationRequested();
            generator.GenerateNextToken();
            var newTokens = generator.GetNextTokens();
            if (newTokens.Length > 0)
            {
                var text = stream.Decode(newTokens[0]);
                if (!string.IsNullOrEmpty(text)) yield return text;
            }
            await Task.Yield();
        }
    }

    private static string FormatPhi3Prompt(string user, string system)
    {
        var nl = "\n";
        return "<|system|>" + nl + system + "<|end|>" + nl + "<|user|>" + nl + user + "<|end|>" + nl + "<|assistant|>" + nl;
    }

    public async Task DownloadModelAsync(string modelId, IProgress<(long downloaded, long total, string status)>? progress = null, CancellationToken ct = default)
    {
        if (!LocalLlmModels.Available.TryGetValue(modelId, out var info)) throw new ArgumentException("Unknown model");
        var modelPath = LocalLlmModels.GetModelPath(modelId);
        Directory.CreateDirectory(modelPath);
        var baseUrl = "https://huggingface.co/" + info.HuggingFaceRepo + "/resolve/main/cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/";
        foreach (var file in info.RequiredFiles)
        {
            var filePath = Path.Combine(modelPath, file);
            if (File.Exists(filePath)) continue;
            progress?.Report((0, 0, "Downloading: " + file));
            using var response = await _httpClient.GetAsync(baseUrl + file, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 0;
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await using var fs = File.Create(filePath);
            var buffer = new byte[81920];
            long dl = 0; int r;
            while ((r = await stream.ReadAsync(buffer, ct)) > 0) { await fs.WriteAsync(buffer.AsMemory(0, r), ct); dl += r; progress?.Report((dl, total, file)); }
        }
        progress?.Report((0, 0, "Done!"));
    }

    private void DisposeModel()
    {
        _tokenizer?.Dispose();
        _model?.Dispose();
        _tokenizer = null;
        _model = null;
        _currentModelId = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        DisposeModel();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
