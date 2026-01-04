using WPFLLM.Models;

namespace WPFLLM.Services;

public class SettingsService : ISettingsService
{
    private readonly IDatabaseService _database;
    private AppSettings? _cachedSettings;

    public event EventHandler<AppSettings>? SettingsChanged;

    public SettingsService(IDatabaseService database)
    {
        _database = database;
    }

    public async Task<AppSettings> GetSettingsAsync()
    {
        _cachedSettings ??= await _database.GetSettingsAsync();
        return _cachedSettings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        await _database.SaveSettingsAsync(settings);
        _cachedSettings = settings;
        SettingsChanged?.Invoke(this, settings);
    }
}
