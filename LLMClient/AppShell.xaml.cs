using LLMClient.Views;

namespace LLMClient
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(ModelConfigurationPage), typeof(ModelConfigurationPage));
        }
    }
}
