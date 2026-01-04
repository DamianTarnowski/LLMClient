using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WPFLLM.ViewModels;

namespace WPFLLM.Views;

public partial class EmbeddingsView : UserControl
{
    public EmbeddingsView()
    {
        InitializeComponent();
    }

    private void Model_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && 
            element.DataContext is EmbeddingModelViewModel model &&
            DataContext is EmbeddingsViewModel vm)
        {
            vm.SelectedModel = model;
        }
    }
}
