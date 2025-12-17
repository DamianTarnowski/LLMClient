using LLMClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.Views;

public partial class ModelSettingsPage : ContentPage
{
    public ModelSettingsPage()
    {
        InitializeComponent();
    }

    public ModelSettingsPage(ModelSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext == null)
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            var vm = services?.GetService<ModelSettingsViewModel>();
            if (vm != null)
            {
                BindingContext = vm;
            }
        }
    }
}