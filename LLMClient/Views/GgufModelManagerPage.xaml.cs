#if WINDOWS || ANDROID
using System.ComponentModel;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using LLMClient.Services;

namespace LLMClient.Views;

public partial class GgufModelManagerPage : ContentPage
{
    private LlamaSharpLocalModelService? _llamaService;
    private bool _isDownloading = false;

    public GgufModelManagerPage()
    {
        InitializeComponent();
    }

    public GgufModelManagerPage(LlamaSharpLocalModelService llamaService)
    {
        InitializeComponent();
        _llamaService = llamaService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_llamaService == null)
        {
            try
            {
                _llamaService = TryResolveLlamaService();
            }
            catch
            {
                _llamaService = null;
            }
        }

        if (_llamaService == null)
        {
            CurrentModelLabel.Text = "Błąd inicjalizacji";
            ModelStatusLabel.Text = "Brak serwisu LLamaSharp (DI).";
            LoadModelButton.IsEnabled = false;
            return;
        }

        _llamaService.StateChanged -= OnStateChanged;
        _llamaService.DownloadProgress -= OnDownloadProgress;
        _llamaService.StateChanged += OnStateChanged;
        _llamaService.DownloadProgress += OnDownloadProgress;

        _ = LoadModelsAsync();
    }

    private async Task LoadModelsAsync()
    {
        try
        {
            if (_llamaService == null)
            {
                return;
            }
            var downloadedModels = await _llamaService.GetDownloadedModelsAsync();
            var models = _llamaService.GetAvailableModels();
            var selectedModel = _llamaService.SelectedModel;
            
            var viewModels = models.Select(m => new GgufModelViewModel
            {
                Id = m.Id,
                DisplayName = m.DisplayName,
                Description = m.Description,
                SizeInMB = m.SizeInMB,
                IsRecommended = m.IsRecommended,
                IsDownloaded = downloadedModels.GetValueOrDefault(m.Id, false),
                IsSelected = m.Id == selectedModel.Id,
                ActionCommand = new Command(async () => await OnModelActionAsync(m.Id))
            }).ToList();
            
            foreach (var vm in viewModels)
            {
                vm.UpdateButtonState();
            }
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ModelsCollectionView.ItemsSource = viewModels;
                CurrentModelLabel.Text = selectedModel.DisplayName;
                ModelStatusLabel.Text = _llamaService.IsLoaded ? "Załadowany" : 
                                        downloadedModels.GetValueOrDefault(selectedModel.Id, false) ? "Pobrany" : "Nie pobrany";
                LoadModelButton.IsEnabled = !_llamaService.IsLoaded && downloadedModels.GetValueOrDefault(selectedModel.Id, false);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GgufModelManager] Error loading models: {ex.Message}");
        }
    }

    private async Task OnModelActionAsync(string modelId)
    {
        if (_isDownloading) return;

        if (_llamaService == null)
        {
            return;
        }
        
        try
        {
            var downloadedModels = await _llamaService.GetDownloadedModelsAsync();
            var isDownloaded = downloadedModels.GetValueOrDefault(modelId, false);
            
            if (isDownloaded)
            {
                // Select this model
                await _llamaService.SelectModelAsync(modelId);
                await LoadModelsAsync();
            }
            else
            {
                // Download this model
                await _llamaService.SelectModelAsync(modelId);
                _isDownloading = true;
                
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ProgressSection.IsVisible = true;
                    DownloadProgressBar.Progress = 0;
                    ProgressLabel.Text = "0%";
                });
                
                var success = await _llamaService.DownloadModelAsync(new Progress<double>(p =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        DownloadProgressBar.Progress = p / 100.0;
                        ProgressLabel.Text = $"{p:F1}%";
                    });
                }));
                
                _isDownloading = false;
                
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ProgressSection.IsVisible = false;
                });
                
                if (success)
                {
                    await DisplayAlert("Sukces", "Model pobrany pomyślnie!", "OK");
                }
                else
                {
                    await DisplayAlert("Błąd", "Nie udało się pobrać modelu.", "OK");
                }
                
                await LoadModelsAsync();
            }
        }
        catch (Exception ex)
        {
            _isDownloading = false;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ProgressSection.IsVisible = false;
            });
            await DisplayAlert("Błąd", $"Wystąpił błąd: {ex.Message}", "OK");
        }
    }

    private void OnStateChanged(LocalModelState state)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await LoadModelsAsync();
        });
    }

    private void OnDownloadProgress(double progress)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DownloadProgressBar.Progress = progress / 100.0;
            ProgressLabel.Text = $"{progress:F1}%";
        });
    }

    private async void OnLoadModelClicked(object sender, EventArgs e)
    {
        try
        {
            if (_llamaService == null)
            {
                await DisplayAlert("Błąd", "Brak serwisu LLamaSharp.", "OK");
                return;
            }
            LoadModelButton.IsEnabled = false;
            LoadModelButton.Text = "Ładowanie...";
            
            var success = await _llamaService.LoadModelAsync();
            
            if (success)
            {
                await DisplayAlert("Sukces", "Model załadowany i gotowy do użycia!", "OK");
            }
            else
            {
                await DisplayAlert("Błąd", "Nie udało się załadować modelu.", "OK");
            }
            
            await LoadModelsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", $"Wystąpił błąd: {ex.Message}", "OK");
        }
        finally
        {
            LoadModelButton.Text = "Załaduj Wybrany Model";
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        try
        {
            if (Shell.Current != null)
                await Shell.Current.GoToAsync("..");
            else if (Navigation != null)
                await Navigation.PopAsync();
        }
        catch { }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_llamaService != null)
        {
            _llamaService.StateChanged -= OnStateChanged;
            _llamaService.DownloadProgress -= OnDownloadProgress;
        }
    }

    private static LlamaSharpLocalModelService? TryResolveLlamaService()
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        if (services == null)
            return null;

        return services.GetService<LlamaSharpLocalModelService>();
    }
}

public class GgufModelViewModel : INotifyPropertyChanged
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public long SizeInMB { get; set; }
    public bool IsRecommended { get; set; }
    public bool IsDownloaded { get; set; }
    public bool IsSelected { get; set; }
    public ICommand ActionCommand { get; set; } = null!;
    
    public string ButtonText { get; private set; } = "Pobierz";
    public Color ButtonColor { get; private set; } = Colors.Green;
    
    public void UpdateButtonState()
    {
        if (IsSelected && IsDownloaded)
        {
            ButtonText = "Wybrany";
            ButtonColor = Color.FromArgb("#6366F1"); // Primary
        }
        else if (IsDownloaded)
        {
            ButtonText = "Wybierz";
            ButtonColor = Color.FromArgb("#10B981"); // Secondary
        }
        else
        {
            ButtonText = "Pobierz";
            ButtonColor = Color.FromArgb("#F59E0B"); // Warning/Orange
        }
        
        OnPropertyChanged(nameof(ButtonText));
        OnPropertyChanged(nameof(ButtonColor));
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
#endif
