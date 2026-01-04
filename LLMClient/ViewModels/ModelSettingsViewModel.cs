using System.ComponentModel;
using System.Windows.Input;
using LLMClient.Services;
using LLMClient.Models;

namespace LLMClient.ViewModels
{
    public class ModelSettingsViewModel : INotifyPropertyChanged
    {
        private readonly ILocalModelService _localModelService;
        private readonly IAiService _aiService;
        private readonly DatabaseService _databaseService;
        
        private string _systemPrompt = "";
        private double _temperature = 0.3;
        private int _maxLength = 128;
        private double _repetitionPenalty = 1.8;
        private double _topP = 0.75;
        private bool _isLocalModelActive = false;
        private bool _includeMemoryInSystemPrompt = true;
        private string _currentModelName = "";
        private string _modelType = "";
        private EngineType _selectedEngine = EngineType.OnnxGenAI;

        public ModelSettingsViewModel(ILocalModelService localModelService, IAiService aiService, DatabaseService databaseService)
        {
            _localModelService = localModelService;
            _aiService = aiService;
            _databaseService = databaseService;
            
            SaveCommand = new Command(async () => await SaveSettingsAsync());
            ResetCommand = new Command(async () => await ResetToDefaultsAsync());
            
            // Subscribe to local model state changes
            _localModelService.StateChanged += OnLocalModelStateChanged;
            // Update view when engine changes elsewhere
            EngineSettings.EngineChanged += e =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SelectedEngine = e;
                    UpdateModelInfo();
                });
            };
            
            // Load initial settings
            Task.Run(async () => await LoadSettingsAsync());
        }

        public string SystemPrompt
        {
            get => _systemPrompt;
            set
            {
                _systemPrompt = value;
                OnPropertyChanged();
            }
        }

        public double Temperature
        {
            get => _temperature;
            set
            {
                _temperature = value;
                OnPropertyChanged();
            }
        }

        public int MaxLength
        {
            get => _maxLength;
            set
            {
                _maxLength = value;
                OnPropertyChanged();
            }
        }

        public double RepetitionPenalty
        {
            get => _repetitionPenalty;
            set
            {
                _repetitionPenalty = value;
                OnPropertyChanged();
            }
        }

        public double TopP
        {
            get => _topP;
            set
            {
                _topP = value;
                OnPropertyChanged();
            }
        }

        public bool IsLocalModelActive
        {
            get => _isLocalModelActive;
            set
            {
                _isLocalModelActive = value;
                OnPropertyChanged();
            }
        }

        public bool IncludeMemoryInSystemPrompt
        {
            get => _includeMemoryInSystemPrompt;
            set
            {
                _includeMemoryInSystemPrompt = value;
                OnPropertyChanged();
            }
        }

        public string CurrentModelName
        {
            get => _currentModelName;
            set
            {
                _currentModelName = value;
                OnPropertyChanged();
            }
        }

        public string ModelType
        {
            get => _modelType;
            set
            {
                _modelType = value;
                OnPropertyChanged();
            }
        }

        // Engine options: ONNX GenAI (CPU) + LLamaSharp (llama.cpp) + MediaPipe GenAI (Gemma)
#if ANDROID || IOS
        public EngineType[] EngineOptions { get; } = new[] { EngineType.OnnxGenAI, EngineType.LLamaSharp, EngineType.MediaPipeGenAI };
#else
        public EngineType[] EngineOptions { get; } = new[] { EngineType.OnnxGenAI, EngineType.LLamaSharp };
#endif

        public EngineType SelectedEngine
        {
            get => _selectedEngine;
            set
            {
                if (_selectedEngine != value)
                {
                    _selectedEngine = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand GoBackCommand { get; } = new Command(async () =>
        {
            try
            {
                if (Shell.Current != null)
                {
                    // Navigate explicitly to MainPage to simulate Back when this page is a ShellContent root
                    await Shell.Current.GoToAsync("///MainPage");
                }
                else if (Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation != null)
                {
                    await Application.Current.Windows[0].Page!.Navigation.PopAsync();
                }
            }
            catch { }
        });

        private void OnLocalModelStateChanged(LocalModelState state)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsLocalModelActive = state == LocalModelState.Loaded;
                UpdateModelInfo();
            });
        }
        
        private void UpdateModelInfo()
        {
            if (IsLocalModelActive)
            {
                // Show friendly name based on selected local engine
                var engineName = SelectedEngine switch
                {
                    EngineType.LLamaSharp => "LLamaSharp (llama.cpp)",
                    EngineType.OnnxGenAI => "ONNX GenAI (Phi-4)",
                    EngineType.MediaPipeGenAI => "MediaPipe (Gemma)",
                    _ => "Local Model"
                };
                CurrentModelName = engineName;
                ModelType = "Local AI Model";
            }
            else
            {
                // Get current cloud model info from preferences or AI service
                CurrentModelName = Preferences.Get("LastCloudModel", "Cloud Model");
                ModelType = "Cloud AI Model";
            }
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                // Load settings from database
                var settings = await _databaseService.GetModelSettingsAsync();
                
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SystemPrompt = settings?.SystemPrompt ?? GetDefaultSystemPrompt();
                    IncludeMemoryInSystemPrompt = Preferences.Get("IncludeMemoryInSystemPrompt", true);
                    Temperature = settings?.Temperature ?? 0.6;
                    MaxLength = settings?.MaxLength ?? 512;
                    RepetitionPenalty = settings?.RepetitionPenalty ?? 1.15;
                    TopP = settings?.TopP ?? 0.85;
                    SelectedEngine = EngineSettings.LoadSelectedEngine();
                    
                    IsLocalModelActive = _localModelService.IsLoaded;
                    UpdateModelInfo();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelSettingsViewModel] Error loading settings: {ex.Message}");
                
                // Load defaults on error
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SystemPrompt = GetDefaultSystemPrompt();
                    UpdateModelInfo();
                });
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                var settings = new ModelSettings
                {
                    SystemPrompt = SystemPrompt,
                    Temperature = Temperature,
                    MaxLength = MaxLength,
                    RepetitionPenalty = RepetitionPenalty,
                    TopP = TopP,
                    UpdatedAt = DateTime.UtcNow
                };
                
                await _databaseService.SaveModelSettingsAsync(settings);
                Preferences.Set("IncludeMemoryInSystemPrompt", IncludeMemoryInSystemPrompt);
                EngineSettings.SaveSelectedEngine(SelectedEngine);
                
                // Apply settings to current model
                await ApplySettingsToCurrentModelAsync();
                
                var currentPage = Application.Current?.Windows.FirstOrDefault()?.Page;
                if (currentPage != null)
                {
                    await currentPage.DisplayAlertAsync(
                        "Sukces",
                        "Ustawienia zapisane.\n\nZmiana silnika lokalnego została zastosowana natychmiast. Jeśli chcesz używać nowego silnika, załaduj odpowiedni model w sekcji statusu lokalnego modelu.",
                        "OK");
                }

                // Po zapisie – wróć automatycznie do poprzedniego ekranu
                try
                {
                    if (Shell.Current != null)
                        await Shell.Current.GoToAsync("..");
                    else if (currentPage?.Navigation != null)
                        await currentPage.Navigation.PopAsync();
                }
                catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelSettingsViewModel] Error saving settings: {ex.Message}");
                var errorPage = Application.Current?.Windows.FirstOrDefault()?.Page;
                if (errorPage != null)
                    await errorPage.DisplayAlertAsync("Error", "Failed to save settings.", "OK");
            }
        }

        private async Task ResetToDefaultsAsync()
        {
            try
            {
                var currentPage = Application.Current?.Windows.FirstOrDefault()?.Page;
                var result = currentPage != null && await currentPage.DisplayAlertAsync(
                    "Reset Settings",
                    "Are you sure you want to reset all settings to defaults?",
                    "Yes", "No");
                
                if (result)
                {
                    SystemPrompt = GetDefaultSystemPrompt();
                    Temperature = 0.6;
                    MaxLength = 512;
                    RepetitionPenalty = 1.15;
                    TopP = 0.85;
                    
                    await SaveSettingsAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelSettingsViewModel] Error resetting settings: {ex.Message}");
            }
        }
        
        private async Task ApplySettingsToCurrentModelAsync()
        {
            try
            {
                if (IsLocalModelActive)
                {
                    // Apply settings to local model service
                    await ApplySettingsToLocalModelAsync();
                }
                // For cloud models, system prompt will be applied in AiService automatically
                // when it reads from database
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelSettingsViewModel] Error applying settings: {ex.Message}");
            }
        }
        
        private Task ApplySettingsToLocalModelAsync()
        {
            // This would require adding a method to ILocalModelService to update parameters
            // For now, the changes will take effect on next model reload
            System.Diagnostics.Debug.WriteLine($"[ModelSettingsViewModel] Local model settings will apply on next reload");
            return Task.CompletedTask;
        }

        private string GetDefaultSystemPrompt()
        {
            return "You are a helpful AI assistant. Respond in the same language the user writes in. Be concise, helpful, and avoid repetition.";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    public class ModelSettings
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        public int Id { get; set; }
        
        public string SystemPrompt { get; set; } = "";
        public double Temperature { get; set; } = 0.3;
        public int MaxLength { get; set; } = 128;
        public double RepetitionPenalty { get; set; } = 1.8;
        public double TopP { get; set; } = 0.75;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}