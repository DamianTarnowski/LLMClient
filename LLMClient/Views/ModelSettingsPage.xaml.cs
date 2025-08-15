using LLMClient.ViewModels;

namespace LLMClient.Views;

public partial class ModelSettingsPage : ContentPage
{
    public ModelSettingsPage(ModelSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}