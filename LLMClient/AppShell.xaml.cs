using LLMClient.Views;

namespace LLMClient
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(ModelConfigurationPage), typeof(ModelConfigurationPage));
            
            // MLC disabled - using only ONNX and LLamaSharp (GGUF)
            Routing.RegisterRoute(nameof(GgufModelManagerPage), typeof(GgufModelManagerPage));
            
            // Diagnostyka i Testy - dostępna dla użytkowników
            Routing.RegisterRoute(nameof(Views.DebugPage), typeof(Views.DebugPage));
            
            // Pomoc / Instrukcja
            Routing.RegisterRoute(nameof(Views.HelpPage), typeof(Views.HelpPage));
        }
    }
}
