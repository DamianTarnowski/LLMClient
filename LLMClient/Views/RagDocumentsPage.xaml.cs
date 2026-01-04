using LLMClient.ViewModels;

namespace LLMClient.Views;

public partial class RagDocumentsPage : ContentPage
{
    public RagDocumentsPage(RagViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is RagViewModel vm)
        {
            vm.LoadDocumentsCommand.Execute(null);
        }
    }
}
