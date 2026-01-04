using System.IO;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WPFLLM.Models;
using WPFLLM.Services;

namespace WPFLLM.ViewModels;

public partial class DocumentAnalysisViewModel : ObservableObject
{
    private readonly IDocumentAnalysisService _analysisService;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _analysisCts;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _keyPoints = [];

    [ObservableProperty]
    private ObservableCollection<DetectedIntentViewModel> _detectedIntents = [];

    [ObservableProperty]
    private ObservableCollection<RedFlagViewModel> _redFlags = [];

    [ObservableProperty]
    private ObservableCollection<ComplianceItemViewModel> _complianceItems = [];

    [ObservableProperty]
    private string _suggestedResponse = string.Empty;

    [ObservableProperty]
    private string _streamingResponse = string.Empty;

    [ObservableProperty]
    private AnalysisMetrics? _metrics;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private string _warningMessage = string.Empty;

    [ObservableProperty]
    private bool _hasWarning;

    public DocumentAnalysisViewModel(IDocumentAnalysisService analysisService, ISettingsService settingsService)
    {
        _analysisService = analysisService;
        _settingsService = settingsService;
    }

    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            StatusText = "Please enter or import text to analyze.";
            return;
        }

        // Validate API configuration
        var settings = await _settingsService.GetSettingsAsync();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            WarningMessage = GetLocalizedString("Validation_NoApiKeyAnalysis");
            HasWarning = true;
            return;
        }

        HasWarning = false;
        WarningMessage = string.Empty;

        ClearResults();
        IsAnalyzing = true;
        StatusText = "Analyzing document...";
        _analysisCts = new CancellationTokenSource();

        var responseBuilder = new StringBuilder();

        try
        {
            await foreach (var chunk in _analysisService.AnalyzeStreamingAsync(InputText, _analysisCts.Token))
            {
                responseBuilder.Append(chunk);
                StreamingResponse = responseBuilder.ToString();
            }

            // Parse the complete response
            var result = await _analysisService.AnalyzeAsync(InputText, _analysisCts.Token);
            ApplyResults(result);
            
            HasResults = true;
            StatusText = $"Analysis complete in {result.Metrics.AnalysisTimeMs}ms";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Analysis cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
            _analysisCts?.Dispose();
            _analysisCts = null;
        }
    }

    [RelayCommand]
    private void StopAnalysis()
    {
        _analysisCts?.Cancel();
    }

    [RelayCommand]
    private void ImportFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Text files|*.txt;*.md;*.json;*.csv|All files|*.*",
            Title = "Import Document"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                InputText = File.ReadAllText(dialog.FileName);
                StatusText = $"Imported: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                StatusText = $"Error importing file: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void Clear()
    {
        InputText = string.Empty;
        ClearResults();
        StatusText = string.Empty;
    }

    [RelayCommand]
    private void CopyResponse()
    {
        if (!string.IsNullOrEmpty(SuggestedResponse))
        {
            System.Windows.Clipboard.SetText(SuggestedResponse);
            StatusText = "Response copied to clipboard!";
        }
    }

    private void ClearResults()
    {
        Summary = string.Empty;
        KeyPoints.Clear();
        DetectedIntents.Clear();
        RedFlags.Clear();
        ComplianceItems.Clear();
        SuggestedResponse = string.Empty;
        StreamingResponse = string.Empty;
        Metrics = null;
        HasResults = false;
    }

    private void ApplyResults(DocumentAnalysisResult result)
    {
        Summary = result.Summary;
        
        KeyPoints.Clear();
        foreach (var point in result.KeyPoints)
            KeyPoints.Add(point);

        DetectedIntents.Clear();
        foreach (var intent in result.DetectedIntents)
            DetectedIntents.Add(new DetectedIntentViewModel(intent));

        RedFlags.Clear();
        foreach (var flag in result.RedFlags)
            RedFlags.Add(new RedFlagViewModel(flag));

        ComplianceItems.Clear();
        foreach (var item in result.ComplianceChecklist)
            ComplianceItems.Add(new ComplianceItemViewModel(item));

        SuggestedResponse = result.SuggestedResponse;
        Metrics = result.Metrics;
    }

    private static string GetLocalizedString(string key)
    {
        return Application.Current.Resources[key] as string ?? key;
    }
}

public class DetectedIntentViewModel
{
    public DetectedIntent Intent { get; }
    public string Name => Intent.Intent;
    public string ConfidenceText => $"{Intent.Confidence:P0}";
    public string Evidence => Intent.Evidence;
    public double ConfidenceValue => Intent.Confidence;

    public DetectedIntentViewModel(DetectedIntent intent)
    {
        Intent = intent;
    }
}

public class RedFlagViewModel
{
    public RedFlag Flag { get; }
    public string Severity => Flag.Severity.ToString();
    public string Description => Flag.Description;
    public string Quote => Flag.Quote;
    public string Recommendation => Flag.Recommendation;
    
    public string SeverityIcon => Flag.Severity switch
    {
        RedFlagSeverity.Critical => "🔴",
        RedFlagSeverity.High => "🟠",
        RedFlagSeverity.Medium => "🟡",
        _ => "🟢"
    };

    public RedFlagViewModel(RedFlag flag)
    {
        Flag = flag;
    }
}

public class ComplianceItemViewModel
{
    public ComplianceItem Item { get; }
    public string Requirement => Item.Requirement;
    public string Details => Item.Details;
    public bool IsMet => Item.IsMet;
    public string StatusIcon => IsMet ? "✓" : "✗";
    public string StatusColor => IsMet ? "#22C55E" : "#EF4444";

    public ComplianceItemViewModel(ComplianceItem item)
    {
        Item = item;
    }
}
