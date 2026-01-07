using LLMClient.Core.Models;

namespace LLMClient.Tests.Models;

/// <summary>
/// Tests for DocumentAnalysis model and related classes
/// </summary>
[TestFixture]
public class DocumentAnalysisResultTests
{
    [Test]
    public void DocumentAnalysisResult_CreateNew_HasDefaultValues()
    {
        var result = new DocumentAnalysisResult();
        
        Assert.That(result.Summary, Is.Empty);
        Assert.That(result.KeyPoints, Is.Not.Null);
        Assert.That(result.DetectedIntents, Is.Not.Null);
        Assert.That(result.RedFlags, Is.Not.Null);
        Assert.That(result.ComplianceChecklist, Is.Not.Null);
        Assert.That(result.SuggestedResponse, Is.Empty);
        Assert.That(result.Metrics, Is.Not.Null);
    }

    [Test]
    public void DocumentAnalysisResult_SetSummary_UpdatesProperty()
    {
        var result = new DocumentAnalysisResult
        {
            Summary = "This document discusses contract terms and conditions."
        };
        
        Assert.That(result.Summary, Does.Contain("contract"));
    }

    [Test]
    public void DocumentAnalysisResult_AddKeyPoints_TracksAll()
    {
        var result = new DocumentAnalysisResult();
        result.KeyPoints.Add("Payment due in 30 days");
        result.KeyPoints.Add("Automatic renewal clause");
        result.KeyPoints.Add("Confidentiality agreement");
        
        Assert.That(result.KeyPoints.Count, Is.EqualTo(3));
        Assert.That(result.KeyPoints, Does.Contain("Payment due in 30 days"));
    }

    [Test]
    public void DocumentAnalysisResult_AddDetectedIntents_TracksAll()
    {
        var result = new DocumentAnalysisResult();
        result.DetectedIntents.Add(new DetectedIntent 
        { 
            Intent = "Request for proposal",
            Confidence = 0.95,
            Evidence = "We are looking for vendors..."
        });
        
        Assert.That(result.DetectedIntents.Count, Is.EqualTo(1));
        Assert.That(result.DetectedIntents[0].Confidence, Is.EqualTo(0.95));
    }

    [Test]
    public void DocumentAnalysisResult_AddRedFlags_TracksAll()
    {
        var result = new DocumentAnalysisResult();
        result.RedFlags.Add(new RedFlag
        {
            Severity = RedFlagSeverity.High,
            Description = "Unlimited liability clause",
            Quote = "Party agrees to unlimited liability...",
            Recommendation = "Negotiate a liability cap"
        });
        
        Assert.That(result.RedFlags.Count, Is.EqualTo(1));
        Assert.That(result.RedFlags[0].Severity, Is.EqualTo(RedFlagSeverity.High));
    }
}

[TestFixture]
public class DetectedIntentTests
{
    [Test]
    public void DetectedIntent_CreateNew_HasDefaultValues()
    {
        var intent = new DetectedIntent();
        
        Assert.That(intent.Intent, Is.Empty);
        Assert.That(intent.Confidence, Is.EqualTo(0));
        Assert.That(intent.Evidence, Is.Empty);
    }

    [Test]
    public void DetectedIntent_SetValues_UpdatesProperties()
    {
        var intent = new DetectedIntent
        {
            Intent = "Question about pricing",
            Confidence = 0.87,
            Evidence = "How much does the premium plan cost?"
        };
        
        Assert.That(intent.Intent, Is.EqualTo("Question about pricing"));
        Assert.That(intent.Confidence, Is.EqualTo(0.87).Within(0.01));
        Assert.That(intent.Evidence, Does.Contain("premium plan"));
    }

    [Test]
    public void DetectedIntent_ConfidenceRange_IsValid()
    {
        var lowConfidence = new DetectedIntent { Confidence = 0.1 };
        var highConfidence = new DetectedIntent { Confidence = 0.99 };
        
        Assert.That(lowConfidence.Confidence, Is.GreaterThanOrEqualTo(0));
        Assert.That(highConfidence.Confidence, Is.LessThanOrEqualTo(1));
    }
}

[TestFixture]
public class RedFlagTests
{
    [Test]
    public void RedFlag_CreateNew_HasDefaultValues()
    {
        var flag = new RedFlag();
        
        Assert.That(flag.Severity, Is.EqualTo(RedFlagSeverity.Low));
        Assert.That(flag.Description, Is.Empty);
        Assert.That(flag.Quote, Is.Empty);
        Assert.That(flag.Recommendation, Is.Empty);
    }

    [Test]
    public void RedFlag_SetValues_UpdatesProperties()
    {
        var flag = new RedFlag
        {
            Severity = RedFlagSeverity.Critical,
            Description = "Automatic data sharing with third parties",
            Quote = "We may share your data with our partners...",
            Recommendation = "Request opt-out option or negotiate removal"
        };
        
        Assert.That(flag.Severity, Is.EqualTo(RedFlagSeverity.Critical));
        Assert.That(flag.Description, Does.Contain("data sharing"));
        Assert.That(flag.Quote, Does.Contain("partners"));
    }

    [Test]
    public void RedFlagSeverity_HasCorrectOrder()
    {
        Assert.That((int)RedFlagSeverity.Low, Is.LessThan((int)RedFlagSeverity.Medium));
        Assert.That((int)RedFlagSeverity.Medium, Is.LessThan((int)RedFlagSeverity.High));
        Assert.That((int)RedFlagSeverity.High, Is.LessThan((int)RedFlagSeverity.Critical));
    }

    [Test]
    public void RedFlagSeverity_AllValuesAreDefined()
    {
        var values = Enum.GetValues<RedFlagSeverity>();
        Assert.That(values.Length, Is.EqualTo(4));
    }
}

[TestFixture]
public class ComplianceItemTests
{
    [Test]
    public void ComplianceItem_CreateNew_HasDefaultValues()
    {
        var item = new ComplianceItem();
        
        Assert.That(item.IsMet, Is.False);
        Assert.That(item.Requirement, Is.Empty);
        Assert.That(item.Details, Is.Empty);
    }

    [Test]
    public void ComplianceItem_SetValues_UpdatesProperties()
    {
        var item = new ComplianceItem
        {
            IsMet = true,
            Requirement = "GDPR Article 17 - Right to erasure",
            Details = "Document includes data deletion procedure"
        };
        
        Assert.That(item.IsMet, Is.True);
        Assert.That(item.Requirement, Does.Contain("GDPR"));
        Assert.That(item.Details, Does.Contain("deletion"));
    }

    [Test]
    public void ComplianceItem_Checklist_CanTrackMultipleItems()
    {
        var checklist = new List<ComplianceItem>
        {
            new() { IsMet = true, Requirement = "Data encryption" },
            new() { IsMet = true, Requirement = "Access logging" },
            new() { IsMet = false, Requirement = "Two-factor authentication" }
        };
        
        var metCount = checklist.Count(c => c.IsMet);
        var notMetCount = checklist.Count(c => !c.IsMet);
        
        Assert.That(metCount, Is.EqualTo(2));
        Assert.That(notMetCount, Is.EqualTo(1));
    }
}

[TestFixture]
public class AnalysisMetricsTests
{
    [Test]
    public void AnalysisMetrics_CreateNew_HasDefaultValues()
    {
        var metrics = new AnalysisMetrics();
        
        Assert.That(metrics.WordCount, Is.EqualTo(0));
        Assert.That(metrics.SentenceCount, Is.EqualTo(0));
        Assert.That(metrics.AnalysisTimeMs, Is.EqualTo(0));
    }

    [Test]
    public void AnalysisMetrics_SetValues_UpdatesProperties()
    {
        var metrics = new AnalysisMetrics
        {
            WordCount = 1500,
            SentenceCount = 75,
            AnalysisTimeMs = 2500
        };
        
        Assert.That(metrics.WordCount, Is.EqualTo(1500));
        Assert.That(metrics.SentenceCount, Is.EqualTo(75));
        Assert.That(metrics.AnalysisTimeMs, Is.EqualTo(2500));
    }

    [Test]
    public void AnalysisMetrics_CalculateAverageWordsPerSentence()
    {
        var metrics = new AnalysisMetrics
        {
            WordCount = 1500,
            SentenceCount = 75
        };
        
        var avgWords = metrics.WordCount / (double)metrics.SentenceCount;
        Assert.That(avgWords, Is.EqualTo(20.0));
    }
}
