using LLMClient.Views;

namespace LLMClient
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(ModelConfigurationPage), typeof(ModelConfigurationPage));
            Routing.RegisterRoute(nameof(MlcModelSelectorPage), typeof(MlcModelSelectorPage));

            // Hide MLC Model Manager on non-mobile platforms
#if !(ANDROID || IOS)
            MlcModelSelectorShellContent.IsVisible = false;
#endif
        }
    }
}
