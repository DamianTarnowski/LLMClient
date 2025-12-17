using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace MlcModelTest;

/// <summary>
/// Test application for MLC LLM model download and validation.
/// Downloads Qwen2.5-1.5B (~950MB) which is supported by the prebuilt MLC Chat APK.
/// The APK has precompiled GPU kernels for: Qwen2.5-1.5B, Phi-3.5-mini, gemma-2-2b, Llama-3.2-3B, Mistral-7B
/// </summary>
class Program
{
    // Model ID that matches APK's precompiled kernel: qwen2_q4f16_1_2e221f430380225c03990ad24c3d030e
    private const string MODEL_HF_ID = "mlc-ai/Qwen2.5-1.5B-Instruct-q4f16_1-MLC";
    private const string MODEL_SHORT_NAME = "Qwen2.5-1.5B";
    private const string MODEL_LIB = "qwen2_q4f16_1_2e221f430380225c03990ad24c3d030e";

    static async Task Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger<Program>();

        Console.WriteLine("===========================================");
        Console.WriteLine("   MLC LLM Model Download & Validation Test");
        Console.WriteLine($"   Model: {MODEL_SHORT_NAME} (APK-compatible)");
        Console.WriteLine("===========================================\n");

        var tester = new MlcModelTester(loggerFactory.CreateLogger<MlcModelTester>());

        // Test 1: List available files from HuggingFace
        Console.WriteLine("[Test 1] Fetching model file list from HuggingFace...");
        var files = await tester.GetModelFilesAsync(MODEL_HF_ID);

        if (files.Count == 0)
        {
            Console.WriteLine("ERROR: Failed to get file list from HuggingFace!");
            return;
        }

        Console.WriteLine($"SUCCESS: Found {files.Count} files");
        Console.WriteLine("\nFiles to download:");
        long totalSize = 0;
        foreach (var file in files.OrderByDescending(f => f.Size))
        {
            var sizeStr = file.Size > 1024 * 1024
                ? $"{file.Size / 1024.0 / 1024.0:F1} MB"
                : $"{file.Size / 1024.0:F1} KB";
            Console.WriteLine($"  - {file.Filename} ({sizeStr})");
            totalSize += file.Size;
        }
        Console.WriteLine($"\nTotal download size: {totalSize / 1024.0 / 1024.0:F1} MB\n");

        // Test 2: Download model
        Console.WriteLine("[Test 2] Downloading model (this may take a few minutes)...");

        var modelPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MlcModelTest", "Models", MODEL_SHORT_NAME);

        Console.WriteLine($"Download path: {modelPath}\n");

        var success = await tester.DownloadModelAsync(
            MODEL_HF_ID,
            modelPath,
            progress =>
            {
                Console.Write($"\r  Progress: {progress.Progress * 100:F1}% - {progress.CurrentFile} ({progress.DownloadedMB:F1}/{progress.TotalMB:F1} MB)          ");
            });

        Console.WriteLine();

        if (!success)
        {
            Console.WriteLine("\nERROR: Download failed!");
            return;
        }

        Console.WriteLine("\nSUCCESS: Model downloaded!\n");

        // Test 3: Validate model structure
        Console.WriteLine("[Test 3] Validating model structure...");
        var validation = tester.ValidateModelStructure(modelPath);

        Console.WriteLine($"  Config files present: {(validation.HasConfig ? "YES" : "NO")}");
        Console.WriteLine($"  Tokenizer present: {(validation.HasTokenizer ? "YES" : "NO")}");
        Console.WriteLine($"  Model weights present: {(validation.HasWeights ? "YES" : "NO")}");
        Console.WriteLine($"  Weight shards count: {validation.WeightShardCount}");
        Console.WriteLine($"  Total size on disk: {validation.TotalSizeMB:F1} MB");

        if (validation.IsValid)
        {
            Console.WriteLine("\nSUCCESS: Model structure is valid!\n");
        }
        else
        {
            Console.WriteLine("\nERROR: Model structure is invalid!");
            Console.WriteLine($"Missing: {string.Join(", ", validation.MissingFiles)}");
            return;
        }

        // Test 4: Parse and display config
        Console.WriteLine("[Test 4] Parsing MLC config...");
        var config = await tester.ParseMlcConfigAsync(modelPath);

        if (config != null)
        {
            Console.WriteLine($"  Model type: {config.ModelType}");
            Console.WriteLine($"  Vocab size: {config.VocabSize}");
            Console.WriteLine($"  Context length: {config.MaxWindowSize}");
            Console.WriteLine($"  Quantization: {config.Quantization}");
            Console.WriteLine($"  Tensor parallel shards: {config.TensorParallelShards}");
            Console.WriteLine("\nSUCCESS: Config parsed!\n");
        }
        else
        {
            Console.WriteLine("WARNING: Could not parse config\n");
        }

        // Test 5: Verify tokenizer
        Console.WriteLine("[Test 5] Verifying tokenizer...");
        var tokenizerValid = await tester.VerifyTokenizerAsync(modelPath);

        if (tokenizerValid)
        {
            Console.WriteLine("SUCCESS: Tokenizer is valid!\n");
        }
        else
        {
            Console.WriteLine("WARNING: Tokenizer verification failed\n");
        }

        // Summary
        Console.WriteLine("===========================================");
        Console.WriteLine("                 SUMMARY");
        Console.WriteLine("===========================================");
        Console.WriteLine($"Model: {MODEL_SHORT_NAME}-Instruct (MLC q4f16_1)");
        Console.WriteLine($"Path: {modelPath}");
        Console.WriteLine($"Size: {validation.TotalSizeMB:F1} MB");
        Console.WriteLine($"Model Lib: {MODEL_LIB}");
        Console.WriteLine($"Status: {(validation.IsValid ? "READY FOR USE" : "INVALID")}");
        Console.WriteLine("===========================================\n");

        Console.WriteLine("NOTE: This model is COMPATIBLE with prebuilt MLC Chat APK!");
        Console.WriteLine("The C# bridge will load it using GPU acceleration (OpenCL on Android).\n");
        Console.WriteLine($"To use, call: engine.reload(\"{modelPath}\", \"{MODEL_LIB}\")\n");

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}

public class MlcModelTester
{
    private readonly ILogger<MlcModelTester> _logger;
    private readonly HttpClient _httpClient;

    private const string HF_API_BASE = "https://huggingface.co/api/models";
    private const string HF_RESOLVE_BASE = "https://huggingface.co";

    public MlcModelTester(ILogger<MlcModelTester> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(60)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MlcModelTest/1.0");
    }

    public async Task<List<HfFileInfo>> GetModelFilesAsync(string huggingFaceId)
    {
        try
        {
            var url = $"{HF_API_BASE}/{huggingFaceId}";
            var response = await _httpClient.GetStringAsync(url);
            var modelInfo = JsonSerializer.Deserialize<HfModelResponse>(response);

            if (modelInfo?.Siblings == null)
                return new List<HfFileInfo>();

            return modelInfo.Siblings
                .Where(s => IsRelevantFile(s.Rfilename))
                .Select(s => new HfFileInfo
                {
                    Filename = s.Rfilename,
                    Size = s.Size ?? 0,
                    DownloadUrl = $"{HF_RESOLVE_BASE}/{huggingFaceId}/resolve/main/{s.Rfilename}"
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get model files");
            return new List<HfFileInfo>();
        }
    }

    private bool IsRelevantFile(string filename)
    {
        return filename.EndsWith(".json") ||
               filename.EndsWith(".bin") ||
               filename.EndsWith(".model") ||
               filename == "tokenizer.model";
    }

    public async Task<bool> DownloadModelAsync(
        string huggingFaceId,
        string modelPath,
        Action<DownloadProgress>? onProgress = null)
    {
        try
        {
            Directory.CreateDirectory(modelPath);

            var files = await GetModelFilesAsync(huggingFaceId);
            if (files.Count == 0) return false;

            var totalSize = files.Sum(f => f.Size);
            var downloadedSize = 0L;

            foreach (var file in files)
            {
                var filePath = Path.Combine(modelPath, file.Filename);

                // Skip if already downloaded
                if (File.Exists(filePath))
                {
                    var existingSize = new FileInfo(filePath).Length;
                    if (existingSize == file.Size || file.Size == 0)
                    {
                        downloadedSize += existingSize;
                        onProgress?.Invoke(new DownloadProgress
                        {
                            CurrentFile = file.Filename,
                            Progress = totalSize > 0 ? (double)downloadedSize / totalSize : 0,
                            DownloadedMB = downloadedSize / 1024.0 / 1024.0,
                            TotalMB = totalSize / 1024.0 / 1024.0
                        });
                        continue;
                    }
                }

                // Download file
                using var response = await _httpClient.GetAsync(file.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var contentLength = response.Content.Headers.ContentLength ?? file.Size;
                var buffer = new byte[81920];
                var fileBytesRead = 0L;

                await using var contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true);

                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    fileBytesRead += bytesRead;

                    var totalDownloaded = downloadedSize + fileBytesRead;
                    onProgress?.Invoke(new DownloadProgress
                    {
                        CurrentFile = file.Filename,
                        Progress = totalSize > 0 ? (double)totalDownloaded / totalSize : 0,
                        DownloadedMB = totalDownloaded / 1024.0 / 1024.0,
                        TotalMB = totalSize / 1024.0 / 1024.0
                    });
                }

                downloadedSize += file.Size > 0 ? file.Size : fileBytesRead;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download failed");
            return false;
        }
    }

    public ModelValidation ValidateModelStructure(string modelPath)
    {
        var validation = new ModelValidation();

        if (!Directory.Exists(modelPath))
        {
            validation.MissingFiles.Add("Model directory");
            return validation;
        }

        // Check config
        var configPath = Path.Combine(modelPath, "mlc-chat-config.json");
        validation.HasConfig = File.Exists(configPath);
        if (!validation.HasConfig) validation.MissingFiles.Add("mlc-chat-config.json");

        // Check tokenizer
        var tokenizerPath = Path.Combine(modelPath, "tokenizer.json");
        var tokenizerModelPath = Path.Combine(modelPath, "tokenizer.model");
        validation.HasTokenizer = File.Exists(tokenizerPath) || File.Exists(tokenizerModelPath);
        if (!validation.HasTokenizer) validation.MissingFiles.Add("tokenizer.json or tokenizer.model");

        // Check weights
        var paramsFiles = Directory.GetFiles(modelPath, "params_shard_*.bin");
        validation.HasWeights = paramsFiles.Length > 0;
        validation.WeightShardCount = paramsFiles.Length;
        if (!validation.HasWeights) validation.MissingFiles.Add("params_shard_*.bin");

        // Calculate total size
        var allFiles = Directory.GetFiles(modelPath, "*", SearchOption.AllDirectories);
        validation.TotalSizeMB = allFiles.Sum(f => new FileInfo(f).Length) / 1024.0 / 1024.0;

        return validation;
    }

    public async Task<MlcConfig?> ParseMlcConfigAsync(string modelPath)
    {
        try
        {
            var configPath = Path.Combine(modelPath, "mlc-chat-config.json");
            if (!File.Exists(configPath)) return null;

            var json = await File.ReadAllTextAsync(configPath);
            return JsonSerializer.Deserialize<MlcConfig>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> VerifyTokenizerAsync(string modelPath)
    {
        try
        {
            var tokenizerPath = Path.Combine(modelPath, "tokenizer.json");
            if (!File.Exists(tokenizerPath)) return false;

            var json = await File.ReadAllTextAsync(tokenizerPath);
            var doc = JsonDocument.Parse(json);

            // Check for essential tokenizer fields
            return doc.RootElement.TryGetProperty("model", out _) ||
                   doc.RootElement.TryGetProperty("vocab", out _);
        }
        catch
        {
            return false;
        }
    }
}

public class HfFileInfo
{
    public string Filename { get; set; } = "";
    public long Size { get; set; }
    public string DownloadUrl { get; set; } = "";
}

public class HfModelResponse
{
    [JsonPropertyName("siblings")]
    public List<HfSibling>? Siblings { get; set; }
}

public class HfSibling
{
    [JsonPropertyName("rfilename")]
    public string Rfilename { get; set; } = "";

    [JsonPropertyName("size")]
    public long? Size { get; set; }
}

public class DownloadProgress
{
    public string CurrentFile { get; set; } = "";
    public double Progress { get; set; }
    public double DownloadedMB { get; set; }
    public double TotalMB { get; set; }
}

public class ModelValidation
{
    public bool HasConfig { get; set; }
    public bool HasTokenizer { get; set; }
    public bool HasWeights { get; set; }
    public int WeightShardCount { get; set; }
    public double TotalSizeMB { get; set; }
    public List<string> MissingFiles { get; set; } = new();

    public bool IsValid => HasConfig && HasTokenizer && HasWeights;
}

public class MlcConfig
{
    [JsonPropertyName("model_type")]
    public string ModelType { get; set; } = "";

    [JsonPropertyName("vocab_size")]
    public int VocabSize { get; set; }

    [JsonPropertyName("max_window_size")]
    public int MaxWindowSize { get; set; }

    [JsonPropertyName("quantization")]
    public string Quantization { get; set; } = "";

    [JsonPropertyName("tensor_parallel_shards")]
    public int TensorParallelShards { get; set; }
}
