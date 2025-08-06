namespace LLMClient
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("App: Constructor called");
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            System.Diagnostics.Debug.WriteLine("App: CreateWindow called");
            try
            {
                var shell = new AppShell();
                System.Diagnostics.Debug.WriteLine("App: AppShell created successfully");
                
                var window = new Window(shell) 
                { 
                    Title = "LLM Client"
                };
                System.Diagnostics.Debug.WriteLine("App: Window created successfully");
                return window;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"App: ERROR in CreateWindow: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"App: Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}