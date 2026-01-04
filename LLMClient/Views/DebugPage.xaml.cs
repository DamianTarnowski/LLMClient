namespace LLMClient.Views;

public partial class DebugPage : ContentPage
{
    public DebugPage(ViewModels.DebugViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
