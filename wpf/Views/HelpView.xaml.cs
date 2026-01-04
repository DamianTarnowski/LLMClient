using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace WPFLLM.Views;

public partial class HelpView : UserControl
{
    public HelpView()
    {
        InitializeComponent();
    }

    private void ReportBug_Click(object sender, RoutedEventArgs e)
    {
        var subject = System.Net.WebUtility.UrlEncode("WPFLLM - Bug Report / Feedback");
        var body = System.Net.WebUtility.UrlEncode("Please describe the issue or feedback:\n\n");
        
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"mailto:hdtdtr@gmail.com?subject={subject}&body={body}",
                UseShellExecute = true
            });
        }
        catch
        {
            // Fallback - copy email to clipboard
            Clipboard.SetText("hdtdtr@gmail.com");
            MessageBox.Show(
                Application.Current.TryFindResource("Help_EmailCopied") as string ?? "Email copied to clipboard: hdtdtr@gmail.com",
                "Contact",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
