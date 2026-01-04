using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using LLMClient.Models;
using LLMClient.Services;

namespace LLMClient.ViewModels;

public class DocumentAnalysisViewModel : INotifyPropertyChanged
{
    private readonly IDocumentAnalysisService _analysisService;

    private string _inputText = string.Empty;
    private string _streamingOutput = string.Empty;
    private bool _isAnalyzing;
    private string _statusMessage = string.Empty;
    private DocumentAnalysisResult? _analysisResult;
    private CancellationTokenSource? _cts;

    public string InputText
    {
        get => _inputText;
        set { _inputText = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanAnalyze)); }
    }

    public string StreamingOutput
    {
        get => _streamingOutput;
        set { _streamingOutput = value; OnPropertyChanged(); }
    }

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set { _isAnalyzing = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanAnalyze)); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public DocumentAnalysisResult? AnalysisResult
    {
        get => _analysisResult;
        set { _analysisResult = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasResult)); }
    }

    public bool HasResult => AnalysisResult != null;
    public bool CanAnalyze => !IsAnalyzing && !string.IsNullOrWhiteSpace(InputText);

    public ObservableCollection<string> KeyPoints { get; } = [];
    public ObservableCollection<DetectedIntent> Intents { get; } = [];
    public ObservableCollection<RedFlag> RedFlags { get; } = [];
    public ObservableCollection<ComplianceItem> ComplianceItems { get; } = [];

    public ICommand AnalyzeCommand { get; }
    public ICommand AnalyzeStreamingCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand PasteFromClipboardCommand { get; }

    public DocumentAnalysisViewModel(IDocumentAnalysisService analysisService)
    {
        _analysisService = analysisService;

        AnalyzeCommand = new Command(async () => await AnalyzeAsync(), () => CanAnalyze);
        AnalyzeStreamingCommand = new Command(async () => await AnalyzeStreamingAsync(), () => CanAnalyze);
        CancelCommand = new Command(Cancel, () => IsAnalyzing);
        ClearCommand = new Command(Clear);
        PasteFromClipboardCommand = new Command(async () => await PasteFromClipboardAsync());
    }

    private async Task AnalyzeAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        try
        {
            IsAnalyzing = true;
            _cts = new CancellationTokenSource();
            StatusMessage = "Analizowanie dokumentu...";
            StreamingOutput = string.Empty;
            ClearResults();

            var result = await _analysisService.AnalyzeAsync(InputText, _cts.Token);
            
            AnalysisResult = result;
            PopulateResults(result);

            StatusMessage = $"Analiza zakończona w {result.Metrics.AnalysisTimeMs}ms";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Analiza anulowana";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[DocumentAnalysisVM] Error: {ex}");
        }
        finally
        {
            IsAnalyzing = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task AnalyzeStreamingAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        try
        {
            IsAnalyzing = true;
            _cts = new CancellationTokenSource();
            StatusMessage = "Analizowanie (streaming)...";
            ClearResults();

            var sb = new StringBuilder();
            await foreach (var chunk in _analysisService.AnalyzeStreamingAsync(InputText, _cts.Token))
            {
                sb.Append(chunk);
                StreamingOutput = sb.ToString();
            }

            StatusMessage = "Streaming zakończony";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Analiza anulowana";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[DocumentAnalysisVM] Streaming error: {ex}");
        }
        finally
        {
            IsAnalyzing = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void Cancel()
    {
        _cts?.Cancel();
    }

    private void Clear()
    {
        InputText = string.Empty;
        StreamingOutput = string.Empty;
        StatusMessage = string.Empty;
        ClearResults();
        AnalysisResult = null;
    }

    private async Task PasteFromClipboardAsync()
    {
        try
        {
            var text = await Clipboard.Default.GetTextAsync();
            if (!string.IsNullOrEmpty(text))
            {
                InputText = text;
                StatusMessage = "Tekst wklejony ze schowka";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd wklejania: {ex.Message}";
        }
    }

    private void ClearResults()
    {
        KeyPoints.Clear();
        Intents.Clear();
        RedFlags.Clear();
        ComplianceItems.Clear();
    }

    private void PopulateResults(DocumentAnalysisResult result)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            foreach (var point in result.KeyPoints)
                KeyPoints.Add(point);

            foreach (var intent in result.DetectedIntents)
                Intents.Add(intent);

            foreach (var flag in result.RedFlags)
                RedFlags.Add(flag);

            foreach (var item in result.ComplianceChecklist)
                ComplianceItems.Add(item);
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
