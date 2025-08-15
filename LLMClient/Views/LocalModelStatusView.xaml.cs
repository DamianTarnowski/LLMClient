using LLMClient.ViewModels;

namespace LLMClient.Views;

public partial class LocalModelStatusView : ContentView
{
    public LocalModelStatusView()
    {
        InitializeComponent();
    }
    
    public LocalModelStatusView(LocalModelStatusViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}