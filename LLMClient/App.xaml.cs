using LLMClient.Services;

namespace LLMClient
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("App: Constructor called");
            
            // Initialize crash reporting
            CrashReportingService.Instance.Initialize();
            
            // Check if app crashed on last run
            if (CrashReportingService.Instance.DidCrashOnLastRun())
            {
                System.Diagnostics.Debug.WriteLine("App: Detected crash on last run");
                // Crash report will be available via GetLastCrashReport()
                // Could show a dialog to user asking to send crash report
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(2000); // Wait for app to fully load
                    await ShowCrashReportDialogAsync();
                });
            }
        }
        
        private async Task ShowCrashReportDialogAsync()
        {
            try
            {
                var lastCrash = CrashReportingService.Instance.GetLastCrashReport();
                if (lastCrash == null) return;
                
                var result = await Current!.Windows[0].Page!.DisplayAlertAsync(
                    "Wykryto awarię / Crash Detected",
                    "Aplikacja zakończyła się nieprawidłowo podczas ostatniego uruchomienia. Czy chcesz wyeksportować raport błędu?\n\nThe app crashed on the last run. Would you like to export the crash report?",
                    "Tak / Yes",
                    "Nie / No");
                
                if (result)
                {
                    var exportPath = await CrashReportingService.Instance.ExportCrashLogsAsync();
                    if (exportPath != null)
                    {
                        await Share.Default.RequestAsync(new ShareFileRequest
                        {
                            Title = "LLMClient Crash Report",
                            File = new ShareFile(exportPath)
                        });
                    }
                }
                
                CrashReportingService.Instance.ClearLastCrash();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"App: Error showing crash dialog: {ex.Message}");
            }
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