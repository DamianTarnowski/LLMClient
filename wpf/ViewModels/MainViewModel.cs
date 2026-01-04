using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WPFLLM.Services;

namespace WPFLLM.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IStatusService _statusService;
    private readonly IEncryptionService _encryptionService;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isOfflineMode = true;

    [ObservableProperty]
    private bool _isEncrypted;

    [ObservableProperty]
    private int _networkCallCount;

    [ObservableProperty]
    private string _statusText = "Ready";

    public ChatViewModel ChatViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public RagViewModel RagViewModel { get; }
    public EmbeddingsViewModel EmbeddingsViewModel { get; }
    public DocumentAnalysisViewModel DocumentAnalysisViewModel { get; }

    public MainViewModel()
    {
        _statusService = App.Services.GetRequiredService<IStatusService>();
        _encryptionService = App.Services.GetRequiredService<IEncryptionService>();
        
        ChatViewModel = App.Services.GetRequiredService<ChatViewModel>();
        SettingsViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        RagViewModel = App.Services.GetRequiredService<RagViewModel>();
        EmbeddingsViewModel = App.Services.GetRequiredService<EmbeddingsViewModel>();
        DocumentAnalysisViewModel = App.Services.GetRequiredService<DocumentAnalysisViewModel>();
        CurrentView = ChatViewModel;

        // Initialize status
        IsEncrypted = _encryptionService.IsEnabled;
        
        // Subscribe to status changes
        _statusService.StatusChanged += (_, _) =>
        {
            IsOfflineMode = _statusService.IsOfflineMode;
            IsEncrypted = _statusService.IsEncrypted || _encryptionService.IsEnabled;
            NetworkCallCount = _statusService.NetworkCallCount;
            StatusText = _statusService.CurrentStatus;
        };
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        CurrentView = value switch
        {
            0 => ChatViewModel,
            1 => RagViewModel,
            2 => DocumentAnalysisViewModel,
            3 => EmbeddingsViewModel,
            4 => SettingsViewModel,
            _ => ChatViewModel
        };
    }
}
