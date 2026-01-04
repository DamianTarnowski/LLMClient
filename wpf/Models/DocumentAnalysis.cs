namespace WPFLLM.Models;

public class DocumentAnalysisResult
{
    public string Summary { get; set; } = string.Empty;
    public List<string> KeyPoints { get; set; } = [];
    public List<DetectedIntent> DetectedIntents { get; set; } = [];
    public List<RedFlag> RedFlags { get; set; } = [];
    public List<ComplianceItem> ComplianceChecklist { get; set; } = [];
    public string SuggestedResponse { get; set; } = string.Empty;
    public AnalysisMetrics Metrics { get; set; } = new();
}

public class DetectedIntent
{
    public string Intent { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Evidence { get; set; } = string.Empty;
}

public class RedFlag
{
    public RedFlagSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Quote { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public enum RedFlagSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public class ComplianceItem
{
    public string Requirement { get; set; } = string.Empty;
    public bool IsMet { get; set; }
    public string Details { get; set; } = string.Empty;
}

public class AnalysisMetrics
{
    public int WordCount { get; set; }
    public int SentenceCount { get; set; }
    public long AnalysisTimeMs { get; set; }
    public string DocumentType { get; set; } = string.Empty;
}
