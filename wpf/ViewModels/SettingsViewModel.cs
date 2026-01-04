using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFLLM.Models;
using WPFLLM.Services;

namespace WPFLLM.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IOpenRouterService _openRouterService;
    private readonly IDatabaseService _databaseService;
    private readonly ILocalLlmService _localLlmService;
    private readonly ILocalizationService _localizationService;
    private readonly IEncryptionService _encryptionService;
    private List<OpenRouterModel> _allModels = [];
    private List<SavedModel> _savedModels = [];

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _apiEndpoint = "https://openrouter.ai/api/v1";

    [ObservableProperty]
    private string _model = "openai/gpt-4o-mini";

    [ObservableProperty]
    private double _temperature = 0.7;

    [ObservableProperty]
    private int _maxTokens = 4096;

    [ObservableProperty]
    private string _systemPrompt = "You are a helpful assistant.";

    [ObservableProperty]
    private bool _useRag;

    [ObservableProperty]
    private int _ragTopK = 3;

    [ObservableProperty]
    private double _ragMinSimilarity = 0.5;

    [ObservableProperty]
    private bool _useLocalLlm;

    [ObservableProperty]
    private string _localLlmModel = "phi-3-mini-4k-instruct";

    [ObservableProperty]
    private bool _isLocalLlmModelDownloaded;

    [ObservableProperty]
    private bool _isDownloadingModel;

    [ObservableProperty]
    private string _downloadProgress = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<OpenRouterModel> _filteredModels = [];

    [ObservableProperty]
    private OpenRouterModel? _selectedModel;

    [ObservableProperty]
    private string _modelSearchText = string.Empty;

    [ObservableProperty]
    private bool _isLoadingModels;

    [ObservableProperty]
    private ObservableCollection<string> _providers = [];

    [ObservableProperty]
    private string? _selectedProvider;

    [ObservableProperty]
    private bool _useOpenRouter = true;

    [ObservableProperty]
    private string _nativeProvider = "OpenAI";

    [ObservableProperty]
    private bool _showModelDetails;

    [ObservableProperty]
    private string _modelDetailsText = string.Empty;

    [ObservableProperty]
    private string _providerDescription = string.Empty;

    [ObservableProperty]
    private string _providerKeyUrl = string.Empty;

    [ObservableProperty]
    private ObservableCollection<SavedModel> _favoriteModels = [];

    [ObservableProperty]
    private bool _isCurrentModelFavorite;

    [ObservableProperty]
    private string _selectedLanguage = "en-US";

    [ObservableProperty]
    private ObservableCollection<LanguageInfo> _availableLanguages = [];

    [ObservableProperty]
    private bool _encryptData;

    public ObservableCollection<string> NativeProviderNames { get; } = new(ApiProviders.GetProviderNames());

    public SettingsViewModel(ISettingsService settingsService, IOpenRouterService openRouterService, IDatabaseService databaseService, ILocalLlmService localLlmService, ILocalizationService localizationService, IEncryptionService encryptionService)
    {
        _settingsService = settingsService;
        _openRouterService = openRouterService;
        _databaseService = databaseService;
        _localLlmService = localLlmService;
        _localizationService = localizationService;
        _encryptionService = encryptionService;
        
        AvailableLanguages = new ObservableCollection<LanguageInfo>(_localizationService.AvailableLanguages);
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadSettingsAsync();
        await LoadSavedModelsAsync();
        await LoadModelsAsync();
        UpdateProviderInfo();
        await CheckLocalLlmModelAsync();
    }

    private async Task LoadSavedModelsAsync()
    {
        _savedModels = await _databaseService.GetSavedModelsAsync();
        FavoriteModels = new ObservableCollection<SavedModel>(_savedModels.Where(m => m.IsFavorite));
    }

    private async Task LoadSettingsAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        ApiKey = settings.ApiKey;
        ApiEndpoint = settings.ApiEndpoint;
        Model = settings.Model;
        UseOpenRouter = settings.UseOpenRouter;
        NativeProvider = settings.NativeProvider;
        Temperature = settings.Temperature;
        MaxTokens = settings.MaxTokens;
        SystemPrompt = settings.SystemPrompt;
        UseRag = settings.UseRag;
        RagTopK = settings.RagTopK;
        RagMinSimilarity = settings.RagMinSimilarity;
        UseLocalLlm = settings.UseLocalLlm;
        LocalLlmModel = settings.LocalLlmModel;
        SelectedLanguage = settings.Language;
        EncryptData = settings.EncryptData;
    }

    partial void OnEncryptDataChanged(bool value)
    {
        _encryptionService.SetEnabled(value);
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        _localizationService.SetLanguage(value);
    }

    [RelayCommand]
    private async Task LoadModelsAsync()
    {
        IsLoadingModels = true;
        StatusMessage = "Loading models from OpenRouter...";

        try
        {
            _allModels = await _openRouterService.GetModelsAsync();
            
            var providerList = _allModels
                .Select(m => m.Provider)
                .Distinct()
                .OrderBy(p => p)
                .ToList();
            
            Providers = new ObservableCollection<string>(["All Providers", .. providerList]);
            SelectedProvider = "All Providers";
            
            FilterModels();
            
            var currentModel = _allModels.FirstOrDefault(m => m.Id == Model);
            if (currentModel != null)
            {
                SelectedModel = currentModel;
            }

            StatusMessage = $"Loaded {_allModels.Count} models from {providerList.Count} providers";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load models: {ex.Message}";
        }
        finally
        {
            IsLoadingModels = false;
            await Task.Delay(3000);
            if (StatusMessage.StartsWith("Loaded") || StatusMessage.StartsWith("Failed"))
                StatusMessage = string.Empty;
        }
    }

    partial void OnModelSearchTextChanged(string value)
    {
        FilterModels();
    }

    partial void OnSelectedProviderChanged(string? value)
    {
        FilterModels();
    }

    partial void OnUseOpenRouterChanged(bool value)
    {
        if (value)
        {
            ApiEndpoint = ApiProviders.OpenRouterEndpoint;
        }
        else
        {
            ApiEndpoint = ApiProviders.GetEndpoint(NativeProvider);
        }
        UpdateProviderInfo();
        UpdateModelIdForCurrentMode();
    }

    partial void OnNativeProviderChanged(string value)
    {
        if (!UseOpenRouter)
        {
            ApiEndpoint = ApiProviders.GetEndpoint(value);
        }
        UpdateProviderInfo();
        UpdateModelIdForCurrentMode();
    }

    private void UpdateProviderInfo()
    {
        if (UseOpenRouter)
        {
            ProviderDescription = "Jeden klucz API do 400+ modeli od wszystkich dostawców";
            ProviderKeyUrl = "https://openrouter.ai/keys";
        }
        else if (ApiProviders.NativeProviders.TryGetValue(NativeProvider, out var info))
        {
            ProviderDescription = info.Description;
            ProviderKeyUrl = info.KeyUrl;
        }
    }

    private void UpdateModelIdForCurrentMode()
    {
        if (SelectedModel != null)
        {
            Model = UseOpenRouter ? SelectedModel.Id : SelectedModel.DirectApiId;
        }
    }

    partial void OnSelectedModelChanged(OpenRouterModel? value)
    {
        if (value != null)
        {
            Model = UseOpenRouter ? value.Id : value.DirectApiId;
            UpdateModelDetails(value);
            UpdateFavoriteStatus();
        }
    }

    private void UpdateModelDetails(OpenRouterModel model)
    {
        var details = $"""
            === {model.Name} ===
            
            Provider: {model.Provider}
            OpenRouter ID: {model.Id}
            Direct API ID: {model.DirectApiId}
            
            Context Length: {model.ContextLength:N0} tokens
            Pricing (OpenRouter): {model.PricingInfo}
            
            Description:
            {model.Description ?? "No description available."}
            """;
        
        ModelDetailsText = details;
    }

    [RelayCommand]
    private void ToggleModelDetails()
    {
        ShowModelDetails = !ShowModelDetails;
    }

    [RelayCommand]
    private void UseSelectedModel()
    {
        if (SelectedModel != null)
        {
            Model = UseOpenRouter ? SelectedModel.Id : SelectedModel.DirectApiId;
            StatusMessage = $"Selected: {SelectedModel.Name}";
        }
    }

    [RelayCommand]
    private void OpenKeyUrl()
    {
        if (!string.IsNullOrEmpty(ProviderKeyUrl))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = ProviderKeyUrl,
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteModelAsync()
    {
        if (SelectedModel == null) return;

        var existing = _savedModels.FirstOrDefault(m => m.ModelId == SelectedModel.Id);
        if (existing != null)
        {
            await _databaseService.ToggleFavoriteModelAsync(SelectedModel.Id);
            existing.IsFavorite = !existing.IsFavorite;
        }
        else
        {
            var savedModel = new SavedModel
            {
                ModelId = SelectedModel.Id,
                DisplayName = SelectedModel.Name,
                Provider = SelectedModel.Provider,
                Description = SelectedModel.Description,
                ContextLength = SelectedModel.ContextLength,
                PricingInfo = SelectedModel.PricingInfo,
                IsFavorite = true,
                CreatedAt = DateTime.UtcNow
            };
            await _databaseService.SaveModelAsync(savedModel);
            _savedModels.Add(savedModel);
        }

        await LoadSavedModelsAsync();
        UpdateFavoriteStatus();
        StatusMessage = existing?.IsFavorite == false ? "Removed from favorites" : "Added to favorites!";
    }

    [RelayCommand]
    private async Task UseFavoriteModelAsync(SavedModel? model)
    {
        if (model == null) return;
        
        Model = UseOpenRouter ? model.ModelId : model.ModelId.Split('/').LastOrDefault() ?? model.ModelId;
        await _databaseService.UpdateModelUsageAsync(model.ModelId);
        StatusMessage = $"Using: {model.DisplayName}";
    }

    private void UpdateFavoriteStatus()
    {
        IsCurrentModelFavorite = SelectedModel != null && 
            _savedModels.Any(m => m.ModelId == SelectedModel.Id && m.IsFavorite);
    }

    private void FilterModels()
    {
        var filtered = _allModels.AsEnumerable();

        if (!string.IsNullOrEmpty(SelectedProvider) && SelectedProvider != "All Providers")
        {
            filtered = filtered.Where(m => m.Provider == SelectedProvider);
        }

        if (!string.IsNullOrWhiteSpace(ModelSearchText))
        {
            var search = ModelSearchText.ToLowerInvariant();
            filtered = filtered.Where(m => 
                m.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                m.Id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (m.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        FilteredModels = new ObservableCollection<OpenRouterModel>(filtered);
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var settings = new AppSettings
        {
            ApiKey = ApiKey,
            ApiEndpoint = ApiEndpoint,
            Model = Model,
            UseOpenRouter = UseOpenRouter,
            NativeProvider = NativeProvider,
            Temperature = Temperature,
            MaxTokens = MaxTokens,
            SystemPrompt = SystemPrompt,
            UseRag = UseRag,
            RagTopK = RagTopK,
            RagMinSimilarity = RagMinSimilarity,
            UseLocalLlm = UseLocalLlm,
            LocalLlmModel = LocalLlmModel,
            Language = SelectedLanguage,
            EncryptData = EncryptData
        };

        await _settingsService.SaveSettingsAsync(settings);
        StatusMessage = "Settings saved!";

        await Task.Delay(2000);
        StatusMessage = string.Empty;
    }

    private async Task CheckLocalLlmModelAsync()
    {
        IsLocalLlmModelDownloaded = await _localLlmService.IsModelDownloadedAsync(LocalLlmModel);
    }

    [RelayCommand]
    private async Task DownloadLocalLlmModelAsync()
    {
        if (IsDownloadingModel) return;
        
        IsDownloadingModel = true;
        DownloadProgress = "Starting download...";
        
        try
        {
            var progress = new Progress<(long downloaded, long total, string status)>(p =>
            {
                if (p.total > 0)
                {
                    var percent = (double)p.downloaded / p.total * 100;
                    var mb = p.downloaded / 1024.0 / 1024.0;
                    var totalMb = p.total / 1024.0 / 1024.0;
                    DownloadProgress = $"{p.status}: {mb:F1}/{totalMb:F1} MB ({percent:F0}%)";
                }
                else
                {
                    DownloadProgress = p.status;
                }
            });

            await _localLlmService.DownloadModelAsync(LocalLlmModel, progress);
            IsLocalLlmModelDownloaded = true;
            DownloadProgress = "Download complete!";
        }
        catch (Exception ex)
        {
            DownloadProgress = "Error: " + ex.Message;
        }
        finally
        {
            IsDownloadingModel = false;
        }
    }
}
