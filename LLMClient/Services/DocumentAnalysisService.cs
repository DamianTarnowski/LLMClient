using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using LLMClient.Models;

namespace LLMClient.Services;

public partial class DocumentAnalysisService : IDocumentAnalysisService
{
    private readonly IAiService _aiService;

    public DocumentAnalysisService(IAiService aiService)
    {
        _aiService = aiService;
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

        var prompt = BuildAnalysisPrompt(text);
        var response = new StringBuilder();

        await foreach (var chunk in _aiService.GetStreamingResponseAsync(prompt, [], cancellationToken))
        {
            response.Append(chunk);
        }

        ParseAnalysisResponse(response.ToString(), result);

        result.Metrics.AnalysisTimeMs = sw.ElapsedMilliseconds;
        return result;
    }

    public async IAsyncEnumerable<string> AnalyzeStreamingAsync(string text, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var prompt = BuildAnalysisPrompt(text);

        await foreach (var chunk in _aiService.GetStreamingResponseAsync(prompt, [], cancellationToken))
        {
            yield return chunk;
        }
    }

    private static string BuildAnalysisPrompt(string text)
    {
        return $"""
            IMPORTANT: Odpowiadaj po polsku.
            
            Przeanalizuj poniższy dokument/transkrypt i dostarcz strukturalną analizę.
            
            DOKUMENT:
            ---
            {text}
            ---
            
            Podaj analizę w następującym formacie (użyj dokładnie tych nagłówków):
            
            ## PODSUMOWANIE
            [2-3 zdania podsumowujące główną treść]
            
            ## KLUCZOWE PUNKTY
            - [Punkt 1]
            - [Punkt 2]
            - [Punkt 3]
            
            ## WYKRYTE INTENCJE
            - Intencja: [nazwa] | Pewność: [wysoka/średnia/niska] | Dowód: "[cytat]"
            
            ## CZERWONE FLAGI
            - Poziom: [niski/średni/wysoki/krytyczny] | Problem: [opis] | Cytat: "[fragment]" | Rekomendacja: [działanie]
            (Jeśli brak, napisz "Nie wykryto czerwonych flag")
            
            ## LISTA KONTROLNA ZGODNOŚCI
            - [✓] lub [✗] [Wymaganie]: [szczegóły]
            Sprawdź:
            - Poprawne powitanie/wprowadzenie
            - Weryfikacja tożsamości
            - Oświadczenie o prywatności
            - Jasne wyjaśnienie
            - Komunikacja następnych kroków
            - Profesjonalny ton
            
            ## SUGEROWANA ODPOWIEDŹ
            [Profesjonalna odpowiedź adresująca główne punkty/obawy]
            
            Bądź zwięzły i profesjonalny. Skup się na praktycznych wnioskach.
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
                case "PODSUMOWANIE" or "SUMMARY":
                    result.Summary = content;
                    break;

                case "KLUCZOWE PUNKTY" or "KEY POINTS":
                    result.KeyPoints = ParseBulletPoints(content);
                    break;

                case "WYKRYTE INTENCJE" or "DETECTED INTENTS":
                    result.DetectedIntents = ParseIntents(content);
                    break;

                case "CZERWONE FLAGI" or "RED FLAGS":
                    result.RedFlags = ParseRedFlags(content);
                    break;

                case "LISTA KONTROLNA ZGODNOŚCI" or "COMPLIANCE CHECKLIST":
                    result.ComplianceChecklist = ParseCompliance(content);
                    break;

                case "SUGEROWANA ODPOWIEDŹ" or "SUGGESTED RESPONSE":
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
        var lines = content.Split('\n').Where(l => l.Contains("Intencja:") || l.Contains("Intent:"));

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
                    "wysoka" or "high" => 0.9,
                    "średnia" or "medium" => 0.6,
                    "niska" or "low" => 0.3,
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
        if (content.Contains("Nie wykryto", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("No red flags", StringComparison.OrdinalIgnoreCase))
            return [];

        var flags = new List<RedFlag>();
        var lines = content.Split('\n').Where(l => l.Contains("Poziom:") || l.Contains("Severity:"));

        foreach (var line in lines)
        {
            var flag = new RedFlag();

            var sevMatch = SeverityRegex().Match(line);
            if (sevMatch.Success)
            {
                flag.Severity = sevMatch.Groups[1].Value.ToLowerInvariant() switch
                {
                    "krytyczny" or "critical" => RedFlagSeverity.Critical,
                    "wysoki" or "high" => RedFlagSeverity.High,
                    "średni" or "medium" => RedFlagSeverity.Medium,
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

    [GeneratedRegex(@"(?:Intencja|Intent):\s*([^|]+)")]
    private static partial Regex IntentRegex();

    [GeneratedRegex(@"(?:Pewność|Confidence):\s*(\w+)")]
    private static partial Regex ConfidenceRegex();

    [GeneratedRegex(@"(?:Dowód|Evidence):\s*""([^""]+)""")]
    private static partial Regex EvidenceRegex();

    [GeneratedRegex(@"(?:Poziom|Severity):\s*(\w+)")]
    private static partial Regex SeverityRegex();

    [GeneratedRegex(@"(?:Problem|Issue):\s*([^|]+)")]
    private static partial Regex IssueRegex();

    [GeneratedRegex(@"(?:Cytat|Quote):\s*""([^""]+)""")]
    private static partial Regex QuoteRegex();

    [GeneratedRegex(@"(?:Rekomendacja|Recommendation):\s*(.+)$")]
    private static partial Regex RecommendationRegex();

    [GeneratedRegex(@"[.!?]+")]
    private static partial Regex SentenceRegex();
}
