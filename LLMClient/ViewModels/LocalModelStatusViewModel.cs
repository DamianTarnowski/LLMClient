using System.ComponentModel;
using System.Windows.Input;
using LLMClient.Services;

namespace LLMClient.ViewModels
{
    public class LocalModelStatusViewModel : INotifyPropertyChanged
    {
        private readonly ILocalModelService _localModelService;
        private readonly ILocalizationService _localizationService;
        
        private bool _isVisible = false;
        private string _statusIcon = "🤖";
        private string _statusText = "";
        private string _actionButtonText = "";
        private string _actionButtonColor = "#5865F2";
        private bool _showActionButton = false;
        private bool _showProgressBar = false;
        private bool _showProgressText = false;
        private double _downloadProgress = 0.0;
        private string _progressText = "";

        public LocalModelStatusViewModel(ILocalModelService localModelService, ILocalizationService localizationService)
        {
            _localModelService = localModelService;
            _localizationService = localizationService;
            
            ActionCommand = new Command(async () => await ExecuteActionAsync(), () => ShowActionButton);
            
            // Subscribe to local model events
            _localModelService.StateChanged += OnLocalModelStateChanged;
            _localModelService.DownloadProgress += OnDownloadProgressChanged;
            _localModelService.ErrorOccurred += OnErrorOccurred;
            
            // Initialize status on startup
            Task.Run(async () => await UpdateStatusAsync());
        }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                OnPropertyChanged();
            }
        }

        public string StatusIcon
        {
            get => _statusIcon;
            set
            {
                _statusIcon = value;
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public string ActionButtonText
        {
            get => _actionButtonText;
            set
            {
                _actionButtonText = value;
                OnPropertyChanged();
            }
        }

        public string ActionButtonColor
        {
            get => _actionButtonColor;
            set
            {
                _actionButtonColor = value;
                OnPropertyChanged();
            }
        }

        public bool ShowActionButton
        {
            get => _showActionButton;
            set
            {
                _showActionButton = value;
                OnPropertyChanged();
                ((Command)ActionCommand).ChangeCanExecute();
            }
        }

        public bool ShowProgressBar
        {
            get => _showProgressBar;
            set
            {
                _showProgressBar = value;
                OnPropertyChanged();
            }
        }

        public bool ShowProgressText
        {
            get => _showProgressText;
            set
            {
                _showProgressText = value;
                OnPropertyChanged();
            }
        }

        public double DownloadProgress
        {
            get => _downloadProgress;
            set
            {
                _downloadProgress = value / 100.0; // Convert percentage to 0-1 range
                OnPropertyChanged();
            }
        }

        public string ProgressText
        {
            get => _progressText;
            set
            {
                _progressText = value;
                OnPropertyChanged();
            }
        }

        public ICommand ActionCommand { get; }

        private async void OnLocalModelStateChanged(LocalModelState state)
        {
            await UpdateStatusAsync();
        }

        private async void OnDownloadProgressChanged(double progress)
        {
            DownloadProgress = progress;
            ProgressText = GetProgressText(progress);
            
            if (progress < 100 && _localModelService.State == LocalModelState.Downloading)
            {
                ShowProgressBar = true;
                ShowProgressText = true;
            }
        }

        private async void OnErrorOccurred(string error)
        {
            StatusIcon = "❌";
            StatusText = GetLocalizedString("LocalModelError");
            ActionButtonText = GetLocalizedString("Retry");
            ActionButtonColor = "#ED4245";
            ShowActionButton = true;
            ShowProgressBar = false;
            ShowProgressText = false;
            IsVisible = true;
        }

        private async Task UpdateStatusAsync()
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var state = _localModelService.State;
                
                switch (state)
                {
                    case LocalModelState.NotDownloaded:
                        StatusIcon = "📥";
                        StatusText = GetLocalizedString("LocalModelNotDownloaded");
                        ActionButtonText = GetLocalizedString("Download");
                        ActionButtonColor = "#43B581";
                        ShowActionButton = true;
                        ShowProgressBar = false;
                        ShowProgressText = false;
                        IsVisible = true;
                        break;
                        
                    case LocalModelState.Downloading:
                        StatusIcon = "⬇️";
                        StatusText = GetLocalizedString("LocalModelDownloading");
                        ActionButtonText = GetLocalizedString("Cancel");
                        ActionButtonColor = "#ED4245";
                        ShowActionButton = true;
                        ShowProgressBar = true;
                        ShowProgressText = true;
                        IsVisible = true;
                        break;
                        
                    case LocalModelState.Downloaded:
                        StatusIcon = "✅";
                        StatusText = GetLocalizedString("LocalModelDownloaded");
                        ActionButtonText = GetLocalizedString("Load");
                        ActionButtonColor = "#5865F2";
                        ShowActionButton = true;
                        ShowProgressBar = false;
                        ShowProgressText = false;
                        IsVisible = true;
                        break;
                        
                    case LocalModelState.Loading:
                        StatusIcon = "⏳";
                        StatusText = GetLocalizedString("LocalModelLoading");
                        ShowActionButton = false;
                        ShowProgressBar = false;
                        ShowProgressText = false;
                        IsVisible = true;
                        break;
                        
                    case LocalModelState.Loaded:
                        StatusIcon = "🚀";
                        StatusText = GetLocalizedString("LocalModelReady");
                        ActionButtonText = GetLocalizedString("Unload");
                        ActionButtonColor = "#FFA500";
                        ShowActionButton = true;
                        ShowProgressBar = false;
                        ShowProgressText = false;
                        IsVisible = true;
                        break;
                        
                    case LocalModelState.Error:
                        StatusIcon = "❌";
                        StatusText = GetLocalizedString("LocalModelError");
                        ActionButtonText = GetLocalizedString("Retry");
                        ActionButtonColor = "#ED4245";
                        ShowActionButton = true;
                        ShowProgressBar = false;
                        ShowProgressText = false;
                        IsVisible = true;
                        break;
                        
                    default:
                        IsVisible = false;
                        break;
                }
            });
        }

        private async Task ExecuteActionAsync()
        {
            try
            {
                var state = _localModelService.State;
                
                switch (state)
                {
                    case LocalModelState.NotDownloaded:
                        await _localModelService.DownloadModelAsync(new Progress<double>(progress => 
                        {
                            DownloadProgress = progress;
                            ProgressText = GetProgressText(progress);
                        }));
                        break;
                        
                    case LocalModelState.Downloading:
                        // Cancel download - this would need to be implemented in the service
                        break;
                        
                    case LocalModelState.Downloaded:
                        var loadSuccess = await _localModelService.LoadModelAsync();
                        if (loadSuccess)
                        {
                            // Notify MainPageViewModel that local model is now active
                            MessagingCenter.Send(this, "LocalModelLoaded");
                        }
                        break;
                        
                    case LocalModelState.Loaded:
                        await _localModelService.UnloadModelAsync();
                        // Notify MainPageViewModel that local model is unloaded
                        MessagingCenter.Send(this, "LocalModelUnloaded");
                        break;
                        
                    case LocalModelState.Error:
                        // Retry by trying to download again
                        await _localModelService.DownloadModelAsync(new Progress<double>(progress => 
                        {
                            DownloadProgress = progress;
                            ProgressText = GetProgressText(progress);
                        }));
                        break;
                }
            }
            catch (Exception ex)
            {
                // Handle errors
                StatusIcon = "❌";
                StatusText = GetLocalizedString("ActionFailed");
                System.Diagnostics.Debug.WriteLine($"LocalModelStatusViewModel: Action failed - {ex.Message}");
            }
        }

        private string GetProgressText(double progress)
        {
            if (progress <= 0) return GetLocalizedString("Starting");
            if (progress >= 100) return GetLocalizedString("Completed");
            
            return string.Format(GetLocalizedString("ProgressFormat"), progress.ToString("F1"));
        }

        private string GetLocalizedString(string key)
        {
            try
            {
                return _localizationService[key];
            }
            catch
            {
                // Fallback to English if localization service fails
                return key switch
                {
                    "LocalModelNotDownloaded" => "Phi-4-mini model not downloaded",
                    "LocalModelDownloading" => "Downloading Phi-4-mini model...",
                    "LocalModelDownloaded" => "Phi-4-mini model ready to load",
                    "LocalModelLoading" => "Loading Phi-4-mini model...",
                    "LocalModelReady" => "Phi-4-mini model ready",
                    "LocalModelError" => "Local model error",
                    "Download" => "Download",
                    "Cancel" => "Cancel",
                    "Load" => "Load",
                    "Unload" => "Unload", 
                    "Retry" => "Retry",
                    "ActionFailed" => "Action failed",
                    "Starting" => "Starting...",
                    "Completed" => "Completed",
                    "ProgressFormat" => "{0}% downloaded",
                    _ => key
                };
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}