using WPFLLM.Models;

namespace WPFLLM.Services;

public interface IOpenRouterService
{
    Task<List<OpenRouterModel>> GetModelsAsync(CancellationToken cancellationToken = default);
}
