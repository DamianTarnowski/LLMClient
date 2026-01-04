using System.IO;
using System.Net.Http;
using System.Text.Json;
using WPFLLM.Models;

namespace WPFLLM.Services;

public interface IModelDownloadService
{
    Task<bool> IsModelDownloadedAsync(string modelId);
    Task<ModelDownloadStatus> GetDownloadStatusAsync(string modelId);
    Task DownloadModelAsync(string modelId, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default);
    Task CancelDownloadAsync(string modelId);
    Task DeleteModelAsync(string modelId);
    Task<long> GetDownloadedSizeAsync(string modelId);
}

public class ModelDownloadProgress
{
    public string FileName { get; set; } = string.Empty;
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public double ProgressPercent => TotalBytes > 0 ? (double)BytesDownloaded / TotalBytes * 100 : 0;
    public string Status { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public string? Error { get; set; }
}

public enum ModelDownloadStatus
{
    NotDownloaded,
    Downloading,
    PartiallyDownloaded,
    Downloaded,
    Error
}

public class ModelDownloadService : IModelDownloadService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Dictionary<string, CancellationTokenSource> _activeDownloads = new();
    private const string HuggingFaceBaseUrl = "https://huggingface.co";

    public ModelDownloadService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public Task<bool> IsModelDownloadedAsync(string modelId)
    {
        if (!EmbeddingModels.Available.TryGetValue(modelId, out var modelInfo))
            return Task.FromResult(false);

        var modelPath = EmbeddingModels.GetModelPath(modelId);
        if (!Directory.Exists(modelPath))
            return Task.FromResult(false);

        foreach (var file in modelInfo.RequiredFiles)
        {
            var filePath = Path.Combine(modelPath, file);
            if (!File.Exists(filePath))
                return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public async Task<ModelDownloadStatus> GetDownloadStatusAsync(string modelId)
    {
        if (_activeDownloads.ContainsKey(modelId))
            return ModelDownloadStatus.Downloading;

        if (await IsModelDownloadedAsync(modelId))
            return ModelDownloadStatus.Downloaded;

        var downloadedSize = await GetDownloadedSizeAsync(modelId);
        if (downloadedSize > 0)
            return ModelDownloadStatus.PartiallyDownloaded;

        return ModelDownloadStatus.NotDownloaded;
    }

    public async Task<long> GetDownloadedSizeAsync(string modelId)
    {
        var modelPath = EmbeddingModels.GetModelPath(modelId);
        if (!Directory.Exists(modelPath))
            return 0;

        long totalSize = 0;
        foreach (var file in Directory.GetFiles(modelPath, "*", SearchOption.AllDirectories))
        {
            var fileInfo = new FileInfo(file);
            totalSize += fileInfo.Length;
        }

        return totalSize;
    }

    public async Task DownloadModelAsync(string modelId, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!EmbeddingModels.Available.TryGetValue(modelId, out var modelInfo))
            throw new ArgumentException($"Unknown model: {modelId}");

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeDownloads[modelId] = cts;

        try
        {
            var modelPath = EmbeddingModels.GetModelPath(modelId);
            Directory.CreateDirectory(modelPath);

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromHours(2);

            // Download each required file
            var filesToDownload = await GetFilesToDownloadAsync(modelInfo, client);
            long totalSize = filesToDownload.Sum(f => f.Size);
            long downloadedTotal = 0;

            foreach (var (fileName, url, size) in filesToDownload)
            {
                var filePath = Path.Combine(modelPath, fileName);
                var tempPath = filePath + ".partial";

                long existingSize = 0;
                if (File.Exists(tempPath))
                {
                    existingSize = new FileInfo(tempPath).Length;
                    downloadedTotal += existingSize;
                }
                else if (File.Exists(filePath))
                {
                    var fileSize = new FileInfo(filePath).Length;
                    downloadedTotal += fileSize;
                    progress?.Report(new ModelDownloadProgress
                    {
                        FileName = fileName,
                        BytesDownloaded = downloadedTotal,
                        TotalBytes = totalSize,
                        Status = $"Pominięto {fileName} (już istnieje)"
                    });
                    continue;
                }

                progress?.Report(new ModelDownloadProgress
                {
                    FileName = fileName,
                    BytesDownloaded = downloadedTotal,
                    TotalBytes = totalSize,
                    Status = $"Pobieranie {fileName}..."
                });

                await DownloadFileWithResumeAsync(client, url, tempPath, existingSize, size, 
                    bytesDownloaded =>
                    {
                        progress?.Report(new ModelDownloadProgress
                        {
                            FileName = fileName,
                            BytesDownloaded = downloadedTotal + bytesDownloaded,
                            TotalBytes = totalSize,
                            Status = $"Pobieranie {fileName}..."
                        });
                    }, cts.Token);

                // Rename temp file to final
                if (File.Exists(filePath))
                    File.Delete(filePath);
                File.Move(tempPath, filePath);

                downloadedTotal += size - existingSize;
            }

            progress?.Report(new ModelDownloadProgress
            {
                FileName = string.Empty,
                BytesDownloaded = totalSize,
                TotalBytes = totalSize,
                Status = "Pobieranie zakończone!",
                IsComplete = true
            });
        }
        catch (OperationCanceledException)
        {
            progress?.Report(new ModelDownloadProgress
            {
                Status = "Pobieranie anulowane",
                Error = "Anulowano przez użytkownika"
            });
            throw;
        }
        catch (Exception ex)
        {
            progress?.Report(new ModelDownloadProgress
            {
                Status = "Błąd pobierania",
                Error = ex.Message
            });
            throw;
        }
        finally
        {
            _activeDownloads.Remove(modelId);
            cts.Dispose();
        }
    }

    private async Task<List<(string FileName, string Url, long Size)>> GetFilesToDownloadAsync(EmbeddingModelInfo modelInfo, HttpClient client)
    {
        var files = new List<(string, string, long)>();

        // Get file list from HuggingFace API
        var apiUrl = $"{HuggingFaceBaseUrl}/api/models/{modelInfo.HuggingFaceRepo}/tree/main/onnx";
        
        try
        {
            var response = await client.GetStringAsync(apiUrl);
            var fileList = JsonSerializer.Deserialize<JsonElement[]>(response);

            if (fileList != null)
            {
                foreach (var file in fileList)
                {
                    var path = file.GetProperty("path").GetString() ?? "";
                    var size = file.TryGetProperty("size", out var sizeEl) ? sizeEl.GetInt64() : 0;
                    var fileName = Path.GetFileName(path);

                    // Include model.onnx and tokenizer files
                    if (fileName.EndsWith(".onnx") || fileName.Contains("tokenizer") || fileName == "vocab.txt")
                    {
                        var downloadUrl = $"{HuggingFaceBaseUrl}/{modelInfo.HuggingFaceRepo}/resolve/main/{path}";
                        files.Add((fileName, downloadUrl, size));
                    }
                }
            }
        }
        catch
        {
            // Fallback to known files
            foreach (var fileName in modelInfo.RequiredFiles)
            {
                var downloadUrl = $"{HuggingFaceBaseUrl}/{modelInfo.HuggingFaceRepo}/resolve/main/onnx/{fileName}";
                files.Add((fileName, downloadUrl, modelInfo.SizeBytes / modelInfo.RequiredFiles.Length));
            }
        }

        return files;
    }

    private async Task DownloadFileWithResumeAsync(HttpClient client, string url, string tempPath, long existingSize, long totalSize, Action<long> onProgress, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        
        if (existingSize > 0)
        {
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingSize, null);
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var fileMode = existingSize > 0 ? FileMode.Append : FileMode.Create;
        await using var fileStream = new FileStream(tempPath, fileMode, FileAccess.Write, FileShare.None, 81920, true);
        await using var downloadStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var buffer = new byte[81920];
        long totalBytesRead = existingSize;
        int bytesRead;

        while ((bytesRead = await downloadStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;
            onProgress(totalBytesRead);
        }
    }

    public Task CancelDownloadAsync(string modelId)
    {
        if (_activeDownloads.TryGetValue(modelId, out var cts))
        {
            cts.Cancel();
        }
        return Task.CompletedTask;
    }

    public Task DeleteModelAsync(string modelId)
    {
        var modelPath = EmbeddingModels.GetModelPath(modelId);
        if (Directory.Exists(modelPath))
        {
            Directory.Delete(modelPath, true);
        }
        return Task.CompletedTask;
    }
}
