using LLMClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.Views;

public partial class MemoryPage : ContentPage
{
    private MemoryPageViewModel? _viewModel;
    private bool _initialized;

    public MemoryPage()
    {
        InitializeComponent();
    }
    
    public MemoryPage(MemoryPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel == null)
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            _viewModel = services?.GetService<MemoryPageViewModel>();
            if (_viewModel != null)
            {
                BindingContext = _viewModel;
            }
        }

        if (_viewModel == null)
            return;

        if (_initialized)
            return;

        _initialized = true;

        System.Diagnostics.Debug.WriteLine("[MemoryPage] OnAppearing called - initializing ViewModel");
        await _viewModel.InitializeAsync();
        System.Diagnostics.Debug.WriteLine("[MemoryPage] OnAppearing completed");
    }
}