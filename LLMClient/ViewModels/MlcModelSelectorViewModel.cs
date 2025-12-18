using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using LLMClient.Models;
using LLMClient.Services;
using LLMClient.Messaging;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace LLMClient.ViewModels
{
    public class MlcModelSelectorViewModel : INotifyPropertyChanged
    {
        private readonly MlcModelDownloadService _downloadService;
        private readonly ILogger<MlcModelSelectorViewModel> _logger;

        private MlcModelInfo? _selectedModel;
        private bool _isDownloading;
        private double _downloadProgress;
        private string _downloadStatus = "";
        private string _currentDownloadFile = "";
        private bool _showAllModels;
        private long _availableRamGB = 4;
        private long _totalDownloadedSize;
        private string _customModelPath = "";
        private string _customModelPathStatus = "";

        public ObservableCollection<ModelItemViewModel> Models { get; } = new();
        public ObservableCollection<ModelItemViewModel> DownloadedModels { get; } = new();

        public MlcModelSelectorViewModel(
            MlcModelDownloadService downloadService,
            ILogger<MlcModelSelectorViewModel> logger)
        {
            _downloadService = downloadService;
            _logger = logger;

            // Commands
            DownloadModelCommand = new Command<MlcModelInfo>(async m => await DownloadModelAsync(m));
            DeleteModelCommand = new Command<string>(async id => await DeleteModelAsync(id));
            CancelDownloadCommand = new Command(CancelDownload);
            RefreshCommand = new Command(async () => await RefreshModelsAsync());
            SelectModelCommand = new Command<MlcModelInfo>(SelectModel);
            SaveCustomModelPathCommand = new Command(async () => await SaveCustomModelPathAsync());
            ClearCustomModelPathCommand = new Command(async () => await ClearCustomModelPathAsync());

            // Subscribe to download events
            _downloadService.DownloadProgress += OnDownloadProgress;
            _downloadService.DownloadCompleted += OnDownloadCompleted;
            _downloadService.DownloadFailed += OnDownloadFailed;
            _downloadService.StatusChanged += status => DownloadStatus = status;

            // Detect available RAM
            DetectDeviceCapabilities();

            // Load models
            Task.Run(RefreshModelsAsync);
        }

        #region Properties

        public MlcModelInfo? SelectedModel
        {
            get => _selectedModel;
            set
            {
                _selectedModel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedModel));
                OnPropertyChanged(nameof(SelectedModelName));
            }
        }

        public bool HasSelectedModel => _selectedModel != null;
        public string SelectedModelName => _selectedModel?.DisplayName ?? "No model selected";

        public string CustomModelPath
        {
            get => _customModelPath;
            set
            {
                _customModelPath = value;
                OnPropertyChanged();
            }
        }

        public string CustomModelPathStatus
        {
            get => _customModelPathStatus;
            set
            {
                _customModelPathStatus = value;
                OnPropertyChanged();
            }
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set
            {
                _isDownloading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotDownloading));
            }
        }

        public bool IsNotDownloading => !_isDownloading;

        public double DownloadProgress
        {
            get => _downloadProgress;
            set
            {
                _downloadProgress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DownloadProgressPercent));
            }
        }

        public string DownloadProgressPercent => $"{DownloadProgress * 100:F1}%";

        public string DownloadStatus
        {
            get => _downloadStatus;
            set
            {
                _downloadStatus = value;
                OnPropertyChanged();
            }
        }

        public string CurrentDownloadFile
        {
            get => _currentDownloadFile;
            set
            {
                _currentDownloadFile = value;
                OnPropertyChanged();
            }
        }

        public bool ShowAllModels
        {
            get => _showAllModels;
            set
            {
                _showAllModels = value;
                OnPropertyChanged();
                _ = RefreshModelsAsync();
            }
        }

        public long AvailableRamGB
        {
            get => _availableRamGB;
            set
            {
                _availableRamGB = value;
                OnPropertyChanged();
            }
        }

        public string TotalDownloadedSizeText => $"{_totalDownloadedSize / 1024.0 / 1024.0 / 1024.0:F2} GB used";

        #endregion

        #region Commands

        public ICommand DownloadModelCommand { get; }
        public ICommand DeleteModelCommand { get; }
        public ICommand CancelDownloadCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SelectModelCommand { get; }
        public ICommand SaveCustomModelPathCommand { get; }
        public ICommand ClearCustomModelPathCommand { get; }

        #endregion

        #region Methods

        private void DetectDeviceCapabilities()
        {
#if ANDROID
            try
            {
                var activityManager = Android.App.Application.Context.GetSystemService(Android.Content.Context.ActivityService) as Android.App.ActivityManager;
                if (activityManager != null)
                {
                    var memInfo = new Android.App.ActivityManager.MemoryInfo();
                    activityManager.GetMemoryInfo(memInfo);
                    _availableRamGB = memInfo.TotalMem / 1024 / 1024 / 1024;
                }
            }
            catch { _availableRamGB = 4; }
#elif IOS
            _availableRamGB = 4; // Most iPhones have 4-6GB
#else
            _availableRamGB = 8;
#endif
            _logger.LogInformation("[MlcSelector] Detected RAM: {Ram}GB", _availableRamGB);
        }

        public async Task RefreshModelsAsync()
        {
            try
            {
                Models.Clear();

                // Get models based on settings
                var availableModels = ShowAllModels
                    ? MlcModelCatalog.AllModels
                    : MlcModelCatalog.GetModelsForRam((int)_availableRamGB);

                // Check download status for each
                foreach (var model in availableModels)
                {
                    var isDownloaded = await _downloadService.IsModelDownloadedAsync(model.Id);
                    var status = await _downloadService.GetModelStatusAsync(model.Id);

                    Models.Add(new ModelItemViewModel
                    {
                        Model = model,
                        IsDownloaded = isDownloaded,
                        DownloadedSize = status.DownloadedBytes,
                        IsCompatible = model.RecommendedRamGB <= _availableRamGB
                    });
                }

                // Update downloaded models list
                await RefreshDownloadedModelsAsync();

                // Update total size
                _totalDownloadedSize = _downloadService.GetTotalDownloadedSize();
                OnPropertyChanged(nameof(TotalDownloadedSizeText));

                // Load selected model from preferences
                var selectedId = Preferences.Get("MlcSelectedModelId", MlcModelCatalog.GetDefaultModel().Id);
                var selectedItem = Models.FirstOrDefault(m => m.Model.Id == selectedId);
                if (selectedItem != null)
                    SelectedModel = selectedItem.Model;

                LoadCustomModelPathForSelectedModel();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MlcSelector] Failed to refresh models");
            }
        }

        private void LoadCustomModelPathForSelectedModel()
        {
            try
            {
                if (SelectedModel == null)
                {
                    CustomModelPath = "";
                    CustomModelPathStatus = "";
                    return;
                }

                CustomModelPath = _downloadService.GetCustomModelPath(SelectedModel.Id) ?? "";
                CustomModelPathStatus = string.IsNullOrWhiteSpace(CustomModelPath)
                    ? ""
                    : "Używana jest własna ścieżka dla tego modelu.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[MlcSelector] Failed to load custom model path");
            }
        }

        private string? ResolveModelDirForValidation(string configuredPath, MlcModelInfo model)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
                return null;

            if (!Directory.Exists(configuredPath))
                return null;

            var directConfig = Path.Combine(configuredPath, "mlc-chat-config.json");
            if (File.Exists(directConfig))
                return configuredPath;

            var byId = Path.Combine(configuredPath, model.Id);
            if (Directory.Exists(byId) && File.Exists(Path.Combine(byId, "mlc-chat-config.json")))
                return byId;

            if (!string.IsNullOrWhiteSpace(model.HuggingFaceId))
            {
                var hfName = model.HuggingFaceId.Split('/').LastOrDefault();
                if (!string.IsNullOrWhiteSpace(hfName))
                {
                    var byHf = Path.Combine(configuredPath, hfName);
                    if (Directory.Exists(byHf) && File.Exists(Path.Combine(byHf, "mlc-chat-config.json")))
                        return byHf;
                }
            }

            return null;
        }

        private bool IsValidMlcModelDir(string modelDir)
        {
            if (!Directory.Exists(modelDir))
                return false;

            if (!File.Exists(Path.Combine(modelDir, "mlc-chat-config.json")))
                return false;

            if (!File.Exists(Path.Combine(modelDir, "tokenizer.json")))
                return false;

            if (!File.Exists(Path.Combine(modelDir, "tokenizer_config.json")))
                return false;

            var shards = Directory.GetFiles(modelDir, "params_shard_*.bin");
            return shards.Length > 0;
        }

        private async Task SaveCustomModelPathAsync()
        {
            try
            {
                if (SelectedModel == null)
                {
                    CustomModelPathStatus = "Najpierw wybierz model.";
                    return;
                }

                var configured = (CustomModelPath ?? "").Trim();
                if (string.IsNullOrWhiteSpace(configured))
                {
                    CustomModelPathStatus = "Ścieżka jest pusta.";
                    return;
                }

                var resolved = ResolveModelDirForValidation(configured, SelectedModel);
                if (resolved == null)
                {
                    CustomModelPathStatus = "Nie znaleziono pliku mlc-chat-config.json w tej ścieżce (ani w podfolderze z nazwą modelu).";
                    return;
                }

                if (!IsValidMlcModelDir(resolved))
                {
                    CustomModelPathStatus = "Folder wygląda na niekompletny (brak tokenizer/config lub params_shard_*.bin).";
                    return;
                }

                _downloadService.SetCustomModelPath(SelectedModel.Id, configured);
                CustomModelPathStatus = "Zapisano. Model będzie użyty z tej ścieżki.";
                await RefreshModelsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MlcSelector] Failed to save custom model path");
                CustomModelPathStatus = $"Błąd zapisu ścieżki: {ex.Message}";
            }
        }

        private async Task ClearCustomModelPathAsync()
        {
            try
            {
                if (SelectedModel == null)
                {
                    CustomModelPathStatus = "Najpierw wybierz model.";
                    return;
                }

                _downloadService.ClearCustomModelPath(SelectedModel.Id);
                CustomModelPath = "";
                CustomModelPathStatus = "Wyczyszczono. Aplikacja będzie używać domyślnych lokalizacji.";
                await RefreshModelsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MlcSelector] Failed to clear custom model path");
                CustomModelPathStatus = $"Błąd czyszczenia ścieżki: {ex.Message}";
            }
        }

        private async Task RefreshDownloadedModelsAsync()
        {
            DownloadedModels.Clear();
            var downloaded = await _downloadService.GetDownloadedModelsAsync();

            foreach (var (modelId, sizeBytes) in downloaded)
            {
                var model = MlcModelCatalog.GetModelById(modelId);
                if (model != null)
                {
                    DownloadedModels.Add(new ModelItemViewModel
                    {
                        Model = model,
                        IsDownloaded = true,
                        DownloadedSize = sizeBytes,
                        IsCompatible = true
                    });
                }
            }
        }

        private async Task DownloadModelAsync(MlcModelInfo model)
        {
            if (IsDownloading) return;

            try
            {
                IsDownloading = true;
                DownloadProgress = 0;
                DownloadStatus = $"Starting download: {model.DisplayName}";

                var progress = new Progress<DownloadProgressInfo>(p =>
                {
                    DownloadProgress = p.Progress;
                    CurrentDownloadFile = p.CurrentFile;
                    DownloadStatus = $"{p.ProgressText} - {p.SizeText}";
                });

                var success = await _downloadService.DownloadModelAsync(model, progress);

                if (success)
                {
                    // Auto-select downloaded model
                    SelectedModel = model;
                    Preferences.Set("MlcSelectedModelId", model.Id);

                    await RefreshModelsAsync();
                }
            }
            finally
            {
                IsDownloading = false;
            }
        }

        private async Task DeleteModelAsync(string modelId)
        {
            try
            {
                var success = await _downloadService.DeleteModelAsync(modelId);
                if (success)
                {
                    if (SelectedModel?.Id == modelId)
                    {
                        SelectedModel = null;
                    }
                    await RefreshModelsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MlcSelector] Failed to delete model");
            }
        }

        private void CancelDownload()
        {
            _downloadService.CancelDownload();
            IsDownloading = false;
            DownloadStatus = "Download cancelled";
        }

        private void SelectModel(MlcModelInfo model)
        {
            SelectedModel = model;
            Preferences.Set("MlcSelectedModelId", model.Id);

            LoadCustomModelPathForSelectedModel();
            
            // Notify other ViewModels about model selection change
            WeakReferenceMessenger.Default.Send(new MlcModelSelectedMessage(model.Id));
            _logger.LogInformation($"[MlcSelector] Model selected: {model.DisplayName}");
        }

        private void OnDownloadProgress(DownloadProgressInfo progress)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DownloadProgress = progress.Progress;
                CurrentDownloadFile = progress.CurrentFile;
                DownloadStatus = $"{progress.ProgressText} - {progress.FileText}";
            });
        }

        private void OnDownloadCompleted(string modelId)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                DownloadStatus = "Download complete!";
                await RefreshModelsAsync();
            });
        }

        private void OnDownloadFailed(string error)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DownloadStatus = $"Failed: {error}";
                IsDownloading = false;
            });
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ModelItemViewModel : INotifyPropertyChanged
    {
        public MlcModelInfo Model { get; set; } = null!;
        public bool IsDownloaded { get; set; }
        public long DownloadedSize { get; set; }
        public bool IsCompatible { get; set; }

        public string StatusText => IsDownloaded
            ? $"Downloaded ({DownloadedSize / 1024.0 / 1024.0:F0} MB)"
            : IsCompatible ? "Available" : "Requires more RAM";

        public Color StatusColor => IsDownloaded
            ? Colors.Green
            : IsCompatible ? Colors.Gray : Colors.Orange;

        public string SizeText => Model.SizeDisplay;
        public string RamText => $"{Model.RecommendedRamGB}GB RAM";

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
