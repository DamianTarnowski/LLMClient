using LLMClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.Views;

public partial class SemanticSearchPage : ContentPage
{
    public SemanticSearchPage()
    {
        InitializeComponent();
    }

    public SemanticSearchPage(SemanticSearchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext == null)
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            var resolvedVm = services?.GetService<SemanticSearchViewModel>();
            if (resolvedVm != null)
            {
                BindingContext = resolvedVm;
            }
        }

        if (BindingContext is SemanticSearchViewModel vm)
        {
            await vm.OnAppearingAsync();
        }
    }
}