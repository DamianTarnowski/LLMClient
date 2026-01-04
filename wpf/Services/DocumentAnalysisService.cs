using System.Text.Json;
using System.Text.RegularExpressions;
using WPFLLM.Models;

namespace WPFLLM.Services;

public partial class DocumentAnalysisService : IDocumentAnalysisService
{
    private readonly ILlmService _llmService;
    private readonly ISettingsService _settingsService;

    public DocumentAnalysisService(ILlmService llmService, ISettingsService settingsService)
    {
        _llmService = llmService;
        _settingsService = settingsService;
    }

    public async Task<DocumentAnalysisResult> AnalyzeAsync(string text, CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new DocumentAnalysisResult
        {
            Metrics = new AnalysisMetrics
            {
                WordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                SentenceCount = SentenceRegex().Matches(text).Count
            }
        };

        var settings = await _settingsService.GetSettingsAsync();
        var prompt = BuildAnalysisPrompt(text, settings.Language);
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = prompt }
        };
        
        var response = new System.Text.StringBuilder();
        
        await foreach (var chunk in _llmService.StreamChatAsync(messages, null, cancellationToken))
        {
            response.Append(chunk);
        }

        ParseAnalysisResponse(response.ToString(), result);
        
        result.Metrics.AnalysisTimeMs = sw.ElapsedMilliseconds;
        return result;
    }

    public async IAsyncEnumerable<string> AnalyzeStreamingAsync(string text, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        var prompt = BuildAnalysisPrompt(text, settings.Language);
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = prompt }
        };
        
        await foreach (var chunk in _llmService.StreamChatAsync(messages, null, cancellationToken))
        {
            yield return chunk;
        }
    }

    private static string BuildAnalysisPrompt(string text, string language)
    {
        var languageInstruction = language switch
        {
            "pl-PL" => "IMPORTANT: Respond entirely in Polish (Polski).",
            "de-DE" => "IMPORTANT: Respond entirely in German (Deutsch).",
            "fr-FR" => "IMPORTANT: Respond entirely in French (Français).",
            "es-ES" => "IMPORTANT: Respond entirely in Spanish (Español).",
            "it-IT" => "IMPORTANT: Respond entirely in Italian (Italiano).",
            "pt-PT" => "IMPORTANT: Respond entirely in Portuguese (Português).",
            "nl-NL" => "IMPORTANT: Respond entirely in Dutch (Nederlands).",
            "ru-RU" => "IMPORTANT: Respond entirely in Russian (Русский).",
            "uk-UA" => "IMPORTANT: Respond entirely in Ukrainian (Українська).",
            "zh-CN" => "IMPORTANT: Respond entirely in Chinese (中文).",
            _ => "Respond in English."
        };

        return $"""
            {languageInstruction}
            
            Analyze the following document/transcript and provide a structured analysis.
            
            DOCUMENT:
            ---
            {text}
            ---
            
            Provide your analysis in the following format (use these exact headers, but write content in the specified language):
            
            ## SUMMARY
            [2-3 sentence summary of the main content]
            
            ## KEY POINTS
            - [Point 1]
            - [Point 2]
            - [Point 3]
            
            ## DETECTED INTENTS
            - Intent: [intent name] | Confidence: [high/medium/low] | Evidence: "[quote]"
            
            ## RED FLAGS
            - Severity: [low/medium/high/critical] | Issue: [description] | Quote: "[relevant quote]" | Recommendation: [action]
            (If none found, write "No red flags detected")
            
            ## COMPLIANCE CHECKLIST
            - [✓] or [✗] [Requirement]: [details]
            Common items to check:
            - Proper greeting/introduction
            - Customer identification verified
            - Privacy statement provided
            - Clear explanation given
            - Next steps communicated
            - Professional tone maintained
            
            ## SUGGESTED RESPONSE
            [Draft a professional response that addresses the main points/concerns]
            
            Be concise and professional. Focus on actionable insights.
            """;
    }

    private static void ParseAnalysisResponse(string response, DocumentAnalysisResult result)
    {
        var sections = response.Split("##", StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var section in sections)
        {
            var lines = section.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) continue;
            
            var header = lines[0].Trim().ToUpperInvariant();
            var content = string.Join("\n", lines.Skip(1)).Trim();
            
            switch (header)
            {
                case "SUMMARY":
                    result.Summary = content;
                    break;
                    
                case "KEY POINTS":
                    result.KeyPoints = ParseBulletPoints(content);
                    break;
                    
                case "DETECTED INTENTS":
                    result.DetectedIntents = ParseIntents(content);
                    break;
                    
                case "RED FLAGS":
                    result.RedFlags = ParseRedFlags(content);
                    break;
                    
                case "COMPLIANCE CHECKLIST":
                    result.ComplianceChecklist = ParseCompliance(content);
                    break;
                    
                case "SUGGESTED RESPONSE":
                    result.SuggestedResponse = content;
                    break;
            }
        }
    }

    private static List<string> ParseBulletPoints(string content)
    {
        return content.Split('\n')
            .Select(l => l.TrimStart('-', '*', ' ', '•'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
    }

    private static List<DetectedIntent> ParseIntents(string content)
    {
        var intents = new List<DetectedIntent>();
        var lines = content.Split('\n').Where(l => l.Contains("Intent:"));
        
        foreach (var line in lines)
        {
            var intent = new DetectedIntent();
            
            var intentMatch = IntentRegex().Match(line);
            if (intentMatch.Success) intent.Intent = intentMatch.Groups[1].Value.Trim();
            
            var confMatch = ConfidenceRegex().Match(line);
            if (confMatch.Success)
            {
                intent.Confidence = confMatch.Groups[1].Value.ToLowerInvariant() switch
                {
                    "high" => 0.9,
                    "medium" => 0.6,
                    "low" => 0.3,
                    _ => 0.5
                };
            }
            
            var evidenceMatch = EvidenceRegex().Match(line);
            if (evidenceMatch.Success) intent.Evidence = evidenceMatch.Groups[1].Value;
            
            if (!string.IsNullOrEmpty(intent.Intent))
                intents.Add(intent);
        }
        
        return intents;
    }

    private static List<RedFlag> ParseRedFlags(string content)
    {
        if (content.Contains("No red flags", StringComparison.OrdinalIgnoreCase))
            return [];
            
        var flags = new List<RedFlag>();
        var lines = content.Split('\n').Where(l => l.Contains("Severity:"));
        
        foreach (var line in lines)
        {
            var flag = new RedFlag();
            
            var sevMatch = SeverityRegex().Match(line);
            if (sevMatch.Success)
            {
                flag.Severity = sevMatch.Groups[1].Value.ToLowerInvariant() switch
                {
                    "critical" => RedFlagSeverity.Critical,
                    "high" => RedFlagSeverity.High,
                    "medium" => RedFlagSeverity.Medium,
                    _ => RedFlagSeverity.Low
                };
            }
            
            var issueMatch = IssueRegex().Match(line);
            if (issueMatch.Success) flag.Description = issueMatch.Groups[1].Value.Trim();
            
            var quoteMatch = QuoteRegex().Match(line);
            if (quoteMatch.Success) flag.Quote = quoteMatch.Groups[1].Value;
            
            var recMatch = RecommendationRegex().Match(line);
            if (recMatch.Success) flag.Recommendation = recMatch.Groups[1].Value.Trim();
            
            if (!string.IsNullOrEmpty(flag.Description))
                flags.Add(flag);
        }
        
        return flags;
    }

    private static List<ComplianceItem> ParseCompliance(string content)
    {
        var items = new List<ComplianceItem>();
        var lines = content.Split('\n').Where(l => l.Contains("[✓]") || l.Contains("[✗]") || l.Contains("[x]", StringComparison.OrdinalIgnoreCase));
        
        foreach (var line in lines)
        {
            var item = new ComplianceItem
            {
                IsMet = line.Contains("[✓]") || (line.Contains("[x]", StringComparison.OrdinalIgnoreCase) && !line.Contains("[✗]"))
            };
            
            var cleanLine = line.Replace("[✓]", "").Replace("[✗]", "").Replace("[x]", "").Replace("[X]", "").Trim();
            var parts = cleanLine.Split(':', 2);
            
            item.Requirement = parts[0].TrimStart('-', '*', ' ');
            item.Details = parts.Length > 1 ? parts[1].Trim() : "";
            
            if (!string.IsNullOrEmpty(item.Requirement))
                items.Add(item);
        }
        
        return items;
    }

    [GeneratedRegex(@"Intent:\s*([^|]+)")]
    private static partial Regex IntentRegex();
    
    [GeneratedRegex(@"Confidence:\s*(\w+)")]
    private static partial Regex ConfidenceRegex();
    
    [GeneratedRegex(@"Evidence:\s*""([^""]+)""")]
    private static partial Regex EvidenceRegex();
    
    [GeneratedRegex(@"Severity:\s*(\w+)")]
    private static partial Regex SeverityRegex();
    
    [GeneratedRegex(@"Issue:\s*([^|]+)")]
    private static partial Regex IssueRegex();
    
    [GeneratedRegex(@"Quote:\s*""([^""]+)""")]
    private static partial Regex QuoteRegex();
    
    [GeneratedRegex(@"Recommendation:\s*(.+)$")]
    private static partial Regex RecommendationRegex();
    
    [GeneratedRegex(@"[.!?]+")]
    private static partial Regex SentenceRegex();
}
