namespace LLMClient.Views;

public partial class HelpPage : ContentPage
{
    private const string PrivacyPolicyUrl = "https://github.com/DamianTarnowski/LLMClient/blob/main/docs/PRIVACY_POLICY.md";
    
    public HelpPage()
    {
        InitializeComponent();
    }
    
    private async void OnLicensesClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(LicensesPage));
    }
    
    private async void OnPrivacyPolicyClicked(object sender, EventArgs e)
    {
        try
        {
            await Browser.Default.OpenAsync(PrivacyPolicyUrl, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open privacy policy: {ex.Message}");
            await DisplayAlertAsync("Error", "Could not open the privacy policy link.", "OK");
        }
    }
}
