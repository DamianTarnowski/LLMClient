using LLMClient.ViewModels;

namespace LLMClient.Views;

public partial class DocumentAnalysisPage : ContentPage
{
    public DocumentAnalysisPage(DocumentAnalysisViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
