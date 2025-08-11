using LLMClient.ViewModels;

namespace LLMClient.Views;

public partial class MemoryPage : ContentPage
{
    private readonly MemoryPageViewModel _viewModel;
    
    public MemoryPage(MemoryPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        
        // Inicjalizuj dane od razu po załadowaniu strony
        System.Diagnostics.Debug.WriteLine("[MemoryPage] Constructor - scheduling immediate initialization");
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), async () =>
        {
            System.Diagnostics.Debug.WriteLine("[MemoryPage] Delayed initialization starting");
            await _viewModel.InitializeAsync();
            System.Diagnostics.Debug.WriteLine("[MemoryPage] Delayed initialization completed");
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine("[MemoryPage] OnAppearing called - initializing ViewModel");
        await _viewModel.InitializeAsync();
        System.Diagnostics.Debug.WriteLine("[MemoryPage] OnAppearing completed");
    }
}