using LLMClient.ViewModels;

namespace LLMClient.Views;

public partial class SemanticSearchPage : ContentPage
{
    public SemanticSearchPage(SemanticSearchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SemanticSearchViewModel vm)
        {
            await vm.RefreshAsync();
        }
    }
} 