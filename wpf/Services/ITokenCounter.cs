namespace WPFLLM.Services;

/// <summary>
/// Interface for counting tokens in text
/// </summary>
public interface ITokenCounter
{
    /// <summary>
    /// Count tokens in text
    /// </summary>
    int CountTokens(string text);
    
    /// <summary>
    /// Whether the count is approximate (estimation) or exact
    /// </summary>
    bool IsApproximate { get; }
    
    /// <summary>
    /// Name of the tokenizer/method used
    /// </summary>
    string TokenizerName { get; }
}

/// <summary>
/// Simple token counter using character estimation
/// Works reasonably well for English (~4 chars/token) and Polish (~3 chars/token)
/// </summary>
public class EstimationTokenCounter : ITokenCounter
{
    private readonly double _charsPerToken;
    
    public EstimationTokenCounter(double charsPerToken = 3.5)
    {
        _charsPerToken = charsPerToken;
    }
    
    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / _charsPerToken);
    }
    
    public bool IsApproximate => true;
    public string TokenizerName => $"Estimation (~{_charsPerToken} chars/token)";
}
