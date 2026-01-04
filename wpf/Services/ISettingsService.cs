using WPFLLM.Models;

namespace WPFLLM.Services;

public interface ISettingsService
{
    Task<AppSettings> GetSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    event EventHandler<AppSettings>? SettingsChanged;
}
