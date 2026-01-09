using System.ComponentModel;
using System.Windows.Input;
using LLMClient.Services;
using LLMClient.Models;
using CommunityToolkit.Mvvm.Messaging;
using LLMClient.Messaging;

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
        private string _warningText = "";
        private bool _showWarning = false;
        private string _estimatedTimeText = "";
        private DateTime _downloadStartTime;
        private long _lastBytesDownloaded = 0;
        private DateTime _lastSpeedCheck = DateTime.MinValue;
        private double _currentSpeedMBps = 0;
        
        // Model info (from ILocalModelService - works for both ONNX and LLamaSharp)
        private long _modelSizeBytes = 0;
        private string _modelName = "";
        private string _modelSize = "";
        private string _modelDescription = "";
        private bool _showModelInfo = false;

        public LocalModelStatusViewModel(ILocalModelService localModelService, ILocalizationService localizationService)
        {
            _localModelService = localModelService;
            _localizationService = localizationService;
            
            ActionCommand = new Command(async () => await ExecuteActionAsync(), () => ShowActionButton);
            SelectModelCommand = new Command(async () => await OpenModelSelectorAsync());
            
            // Subscribe to local model events
            _localModelService.StateChanged += OnLocalModelStateChanged;
            _localModelService.DownloadProgress += OnDownloadProgressChanged;
            _localModelService.ErrorOccurred += OnErrorOccurred;
            
            // Load selected model info (async)
            _ = LoadSelectedModelInfoAsync();
            
            // Subscribe to model selection changes (GGUF/LLamaSharp)
            WeakReferenceMessenger.Default.Register<GgufModelSelectedMessage>(this, (r, m) =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    System.Diagnostics.Debug.WriteLine($"[LocalModelStatus] Received GGUF model change: {m.Value}");
                    await LoadSelectedModelInfoAsync(); // Update wywołania na LoadSelectedModelInfoAsync
                });
            });
            
            // Initialize status on startup
            _ = UpdateStatusAsync();
        }
        
        private async Task LoadSelectedModelInfoAsync()
        {
            try
            {
                // Get model info from ILocalModelService (works for both ONNX and LLamaSharp)
                var modelInfo = await _localModelService.GetModelInfoAsync();
                
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ModelName = modelInfo.DisplayName ?? "Model lokalny";
                    _modelSizeBytes = modelInfo.SizeInMB * 1024L * 1024L;
                    ModelSize = modelInfo.SizeInMB > 0 
                        ? (modelInfo.SizeInMB < 1024 ? $"{modelInfo.SizeInMB} MB" : $"{modelInfo.SizeInMB / 1024.0:F1} GB")
                        : "";
                    ModelDescription = "";
                    ShowModelInfo = true;
                });
                
                System.Diagnostics.Debug.WriteLine($"[LocalModelStatus] Selected model: {ModelName} ({modelInfo.SizeInMB} MB)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalModelStatus] Error loading model info: {ex.Message}");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ModelName = "Model lokalny";
                    ModelSize = "";
                    ModelDescription = "";
                    ShowModelInfo = false;
                });
            }
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

        public string WarningText
        {
            get => _warningText;
            set
            {
                _warningText = value;
                OnPropertyChanged();
            }
        }

        public bool ShowWarning
        {
            get => _showWarning;
            set
            {
                _showWarning = value;
                OnPropertyChanged();
            }
        }

        public string EstimatedTimeText
        {
            get => _estimatedTimeText;
            set
            {
                _estimatedTimeText = value;
                OnPropertyChanged();
            }
        }

        public ICommand ActionCommand { get; }
        public ICommand SelectModelCommand { get; }
        
        public string ModelName
        {
            get => _modelName;
            set
            {
                _modelName = value;
                OnPropertyChanged();
            }
        }
        
        public string ModelSize
        {
            get => _modelSize;
            set
            {
                _modelSize = value;
                OnPropertyChanged();
            }
        }
        
        public string ModelDescription
        {
            get => _modelDescription;
            set
            {
                _modelDescription = value;
                OnPropertyChanged();
            }
        }
        
        public bool ShowModelInfo
        {
            get => _showModelInfo;
            set
            {
                _showModelInfo = value;
                OnPropertyChanged();
            }
        }
        
        public string ModelSizeBytes => _modelSizeBytes > 0 ? (_modelSizeBytes / (1024 * 1024)).ToString() : "";
        public string ModelLanguages => "";
        public string ModelCategory => "";

        private async void OnLocalModelStateChanged(LocalModelState state)
        {
            await UpdateStatusAsync();
        }

        private void OnDownloadProgressChanged(double progress)
        {
            DownloadProgress = progress;

            // Calculate speed and estimated time
            var now = DateTime.UtcNow;
            long totalBytes = _modelSizeBytes > 0 ? _modelSizeBytes : 1000L * 1024L * 1024L; // Use actual model size
            var downloadedBytes = (long)(totalBytes * progress / 100.0);

            if (_lastSpeedCheck != DateTime.MinValue && (now - _lastSpeedCheck).TotalSeconds >= 1)
            {
                var bytesDelta = downloadedBytes - _lastBytesDownloaded;
                var timeDelta = (now - _lastSpeedCheck).TotalSeconds;
                _currentSpeedMBps = (bytesDelta / (1024.0 * 1024.0)) / timeDelta;

                // Calculate remaining time
                if (_currentSpeedMBps > 0.1)
                {
                    var remainingBytes = totalBytes - downloadedBytes;
                    var remainingSeconds = remainingBytes / (_currentSpeedMBps * 1024 * 1024);

                    if (remainingSeconds < 60)
                        EstimatedTimeText = $"{_currentSpeedMBps:F1} MB/s • ~{remainingSeconds:F0}s left";
                    else if (remainingSeconds < 3600)
                        EstimatedTimeText = $"{_currentSpeedMBps:F1} MB/s • ~{remainingSeconds / 60:F0} min left";
                    else
                        EstimatedTimeText = $"{_currentSpeedMBps:F1} MB/s • ~{remainingSeconds / 3600:F1}h left";
                }

                _lastSpeedCheck = now;
                _lastBytesDownloaded = downloadedBytes;
            }
            else if (_lastSpeedCheck == DateTime.MinValue)
            {
                _lastSpeedCheck = now;
                _lastBytesDownloaded = downloadedBytes;
                EstimatedTimeText = "Calculating speed...";
            }

            ProgressText = GetProgressText(progress);

            if (progress < 100 && _localModelService.State == LocalModelState.Downloading)
            {
                ShowProgressBar = true;
                ShowProgressText = true;
            }
        }

        private void OnErrorOccurred(string error)
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
                        StatusText = GetModelStatusText("NotDownloaded");
                        ActionButtonText = GetLocalizedString("Download");
                        ActionButtonColor = "#43B581";
                        ShowActionButton = true;
                        ShowProgressBar = false;
                        ShowProgressText = false;
                        ShowModelInfo = true;
                        IsVisible = true;
                        break;
                        
                    case LocalModelState.Downloading:
                        StatusIcon = "⬇️";
                        StatusText = GetModelStatusText("Downloading");
                        ActionButtonText = GetLocalizedString("Cancel");
                        ActionButtonColor = "#ED4245";
                        ShowActionButton = true;
                        ShowProgressBar = true;
                        ShowProgressText = true;
                        ShowModelInfo = false;
                        IsVisible = true;
                        break;
                        
                    case LocalModelState.Downloaded:
                        StatusIcon = "✅";
                        StatusText = GetModelStatusText("Downloaded");
                        ActionButtonText = GetLocalizedString("Load");
                        ActionButtonColor = "#5865F2";
                        ShowActionButton = true;
                        ShowProgressBar = false;
                        ShowProgressText = false;
                        ShowModelInfo = true;
                        IsVisible = true;
                        break;
                        
                    case LocalModelState.Loading:
                        StatusIcon = "⏳";
                        StatusText = GetModelStatusText("Loading");
                        ShowActionButton = false;
                        ShowProgressBar = false;
                        ShowProgressText = false;
                        ShowModelInfo = false;
                        IsVisible = true;
                        break;
                        
                    case LocalModelState.Loaded:
                        StatusIcon = "🚀";
                        StatusText = GetModelStatusText("Ready");
                        ActionButtonText = GetLocalizedString("Unload");
                        ActionButtonColor = "#FFA500";
                        ShowActionButton = true;
                        ShowProgressBar = false;
                        ShowProgressText = false;
                        ShowModelInfo = true;
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
                        ShowModelInfo = true;
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
                        // Show confirmation dialog with model details
                        var confirmed = await ShowDownloadConfirmationAsync();
                        if (!confirmed) return;
                        
                        // Check device compatibility before download
                        await CheckAndShowCompatibilityWarningsAsync();

                        // Reset speed tracking
                        _lastSpeedCheck = DateTime.MinValue;
                        _lastBytesDownloaded = 0;
                        _downloadStartTime = DateTime.UtcNow;

                        await _localModelService.DownloadModelAsync(new Progress<double>(progress =>
                        {
                            DownloadProgress = progress;
                            ProgressText = GetProgressText(progress);
                        }));
                        break;
                        
                    case LocalModelState.Downloading:
                        // Cancel download
                        _localModelService.CancelDownload();
                        break;
                        
                    case LocalModelState.Downloaded:
                        var loadSuccess = await _localModelService.LoadModelAsync();
                        if (loadSuccess)
                        {
                            // Notify MainPageViewModel that local model is now active
                            WeakReferenceMessenger.Default.Send(new LocalModelLoadedMessage());
                        }
                        break;
                        
                    case LocalModelState.Loaded:
                        await _localModelService.UnloadModelAsync();
                        // Notify MainPageViewModel that local model is unloaded
                        WeakReferenceMessenger.Default.Send(new LocalModelUnloadedMessage());
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

        private string GetModelStatusText(string statusKey)
        {
            var modelName = !string.IsNullOrEmpty(_modelName) ? _modelName : "Model lokalny";
            return statusKey switch
            {
                "NotDownloaded" => $"{modelName} - nie pobrany",
                "Downloading" => $"Pobieranie {modelName}...",
                "Downloaded" => $"{modelName} - gotowy do uruchomienia",
                "Loading" => $"Ładowanie {modelName}...",
                "Ready" => $"{modelName} - aktywny",
                _ => modelName
            };
        }
        
        private string GetLocalizedString(string key)
        {
            try
            {
                return _localizationService[key];
            }
            catch
            {
                // Fallback to Polish
                return key switch
                {
                    "LocalModelError" => "Błąd modelu lokalnego",
                    "Download" => "Pobierz",
                    "Cancel" => "Anuluj",
                    "Load" => "Uruchom",
                    "Unload" => "Wyłącz",
                    "Retry" => "Ponów",
                    "ActionFailed" => "Akcja nie powiodła się",
                    "Starting" => "Rozpoczynanie...",
                    "Completed" => "Ukończono",
                    "ProgressFormat" => "Pobrano {0}%",
                    "Warning" => "Ostrzeżenie",
                    "Notice" => "Informacja",
                    "OK" => "OK",
                    "Yes" => "Tak",
                    "No" => "Nie",
                    "DownloadMayFail" => "Pobieranie może się nie powieść.",
                    "ConfirmDownload" => "Potwierdź pobieranie",
                    "ChangeModel" => "Zmień model",
                    _ => key
                };
            }
        }
        
        private async Task<bool> ShowDownloadConfirmationAsync()
        {
            try
            {
                var modelName = !string.IsNullOrEmpty(_modelName) ? _modelName : "Model";
                var modelSize = !string.IsNullOrEmpty(_modelSize) ? _modelSize : "?";
                var modelDesc = _modelDescription;
                var languages = "";
                var ramRequired = _modelSizeBytes > 0 ? (int)Math.Ceiling(_modelSizeBytes / (1024.0 * 1024.0 * 1024.0)) + 2 : 4;
                
                var message = $"📦 Model: {modelName}\n" +
                              $"📁 Rozmiar: {modelSize}\n" +
                              $"💾 Wymagana pamięć RAM: {ramRequired} GB\n" +
                              $"🌍 Języki: {languages}\n\n" +
                              $"{modelDesc}\n\n" +
                              $"Czy chcesz pobrać ten model?";
                
                var page = Application.Current?.Windows.FirstOrDefault()?.Page;
                if (page == null) return true;
                
                return await page.DisplayAlertAsync(
                    GetLocalizedString("ConfirmDownload"),
                    message,
                    GetLocalizedString("Yes"),
                    GetLocalizedString("No"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalModelStatus] Error showing confirmation: {ex.Message}");
                return true; // Proceed if dialog fails
            }
        }
        
        private async Task OpenModelSelectorAsync()
        {
            try
            {
                if (Shell.Current != null)
                {
                    // Używamy GgufModelManagerPage dla wszystkich platform (LLamaSharp/ONNX)
                    await Shell.Current.GoToAsync("GgufModelManagerPage");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalModelStatus] Error opening model selector: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Refresh model info (call after model selection changes)
        /// </summary>
        public async Task RefreshModelInfoAsync()
        {
            await LoadSelectedModelInfoAsync();
            await UpdateStatusAsync();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async Task CheckAndShowCompatibilityWarningsAsync()
        {
            try
            {
                // Try to get compatibility info from RobustLocalModelService
                if (_localModelService is LLMClient.Services.RobustLocalModelService robustService)
                {
                    var (canRun, warningMessage) = await robustService.CheckDeviceCompatibilityAsync();

                    if (!string.IsNullOrEmpty(warningMessage))
                    {
                        WarningText = warningMessage;
                        ShowWarning = true;

                        // Show alert dialog to user
                        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
                        if (page != null)
                        {
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                if (!canRun)
                                {
                                    await page.DisplayAlertAsync(
                                        GetLocalizedString("Warning"),
                                        warningMessage + "\n\n" + GetLocalizedString("DownloadMayFail"),
                                        GetLocalizedString("OK"));
                                }
                                else
                                {
                                    await page.DisplayAlertAsync(
                                        GetLocalizedString("Notice"),
                                        warningMessage,
                                        GetLocalizedString("OK"));
                                }
                            });
                        }

                        if (!canRun)
                        {
                            // Don't proceed with download
                            return;
                        }
                    }

                    // Show estimated download time
                    var estimatedTime = robustService.GetEstimatedDownloadTime();
                    EstimatedTimeText = $"Estimated: {estimatedTime}";
                }
                else if (_localModelService is LLMClient.Services.SafeLocalModelWrapper wrapper)
                {
                    // Try to access the inner service through reflection or just show generic warning
                    ShowWarning = false;
                    EstimatedTimeText = "~16 min (at 5 MB/s)";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking compatibility: {ex.Message}");
                // Don't block download on error
            }
        }
    }
}