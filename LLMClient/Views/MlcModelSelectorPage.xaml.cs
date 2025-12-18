using LLMClient.ViewModels;
using System.Windows.Input;

namespace LLMClient.Views
{
    public partial class MlcModelSelectorPage : ContentPage
    {
        public ICommand GoBackCommand { get; }

        public MlcModelSelectorPage(MlcModelSelectorViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;

            GoBackCommand = new Command(async () =>
            {
                try
                {
                    if (Shell.Current != null)
                        await Shell.Current.GoToAsync("..");
                    else if (Navigation != null)
                        await Navigation.PopAsync();
                }
                catch { }
            });
        }
    }
}
