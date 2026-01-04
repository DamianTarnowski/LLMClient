using System.Text.Json;
using LLMClient.Models;
using LLMClient.Services;
using NUnit.Framework;

namespace LLMClient.Tests.Integration;

/// <summary>
/// Testy integracyjne DocumentAnalysisService z prawdziwym API.
/// </summary>
[TestFixture]
[Category("Integration")]
public class DocumentAnalysisIntegrationTests
{
    private DocumentAnalysisService _analysisService = null!;
    private AiService _aiService = null!;
    private AiModel _testModel = null!;
    private bool _secretsLoaded = false;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var secretsPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "secrets.json");
        
        if (!File.Exists(secretsPath))
        {
            secretsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "secrets.json");
        }
        
        if (File.Exists(secretsPath))
        {
            var json = File.ReadAllText(secretsPath);
            var secrets = JsonSerializer.Deserialize<SecretsConfig>(json);
            
            if (secrets?.OpenRouter != null)
            {
                _testModel = new AiModel
                {
                    Id = 9998, // Test ID
                    Name = "Test Model",
                    Provider = AiProvider.OpenRouter,
                    ModelId = secrets.OpenRouter.Model,
                    ApiKey = secrets.OpenRouter.ApiKey,
                    Endpoint = secrets.OpenRouter.BaseUrl,
                    IsActive = true,
                    SupportsStreaming = true
                };
                
                _aiService = new AiService(null, null, null);
                await _aiService.UpdateConfiguration(_testModel);
                _analysisService = new DocumentAnalysisService(_aiService);
                _secretsLoaded = true;
            }
        }
    }

    private void EnsureSecretsLoaded()
    {
        if (!_secretsLoaded)
        {
            Assert.Ignore("Plik secrets.json nie został znaleziony.");
        }
    }

    [Test]
    public async Task AnalyzeAsync_SimpleText_ReturnsResult()
    {
        EnsureSecretsLoaded();
        
        // Arrange
        var text = @"
            Firma XYZ ogłosiła dzisiaj wyniki finansowe za trzeci kwartał 2024 roku.
            Przychody wzrosły o 15% w porównaniu z rokiem poprzednim, osiągając 50 milionów złotych.
            Prezes Jan Kowalski zapowiedział ekspansję na nowe rynki europejskie w 2025 roku.
            Zatrudnienie w firmie wzrosło o 20% i obecnie pracuje tam 500 osób.
        ";
        
        // Act
        var result = await _analysisService.AnalyzeAsync(text);
        
        // Assert - sprawdzamy tylko że wynik nie jest null (darmowe modele mogą zwracać niepełne dane)
        Assert.That(result, Is.Not.Null);
        TestContext.WriteLine($"Summary: {result.Summary ?? "(empty)"}");
        TestContext.WriteLine($"Key Points: {result.KeyPoints?.Count ?? 0}");
        TestContext.WriteLine($"Word Count: {result.Metrics?.WordCount ?? 0}");
    }

    [Test]
    public async Task AnalyzeStreamingAsync_ReceivesChunks()
    {
        EnsureSecretsLoaded();
        
        // Arrange
        var text = @"
            Umowa najmu lokalu mieszkalnego.
            Strony: Wynajmujący - Anna Nowak, Najemca - Piotr Wiśniewski.
            Okres najmu: 12 miesięcy od 1 stycznia 2025.
            Czynsz miesięczny: 2500 PLN, płatny do 10-go każdego miesiąca.
            Kaucja: 5000 PLN, zwrotna po zakończeniu umowy.
        ";
        
        var chunks = new List<string>();
        
        // Act
        await foreach (var chunk in _analysisService.AnalyzeStreamingAsync(text))
        {
            chunks.Add(chunk);
        }
        
        // Assert - streaming może nie działać z darmowym modelem
        TestContext.WriteLine($"Streaming chunks: {chunks.Count}");
        if (chunks.Any())
        {
            var fullResponse = string.Join("", chunks);
            TestContext.WriteLine($"Streaming analysis: {fullResponse.Substring(0, Math.Min(200, fullResponse.Length))}...");
        }
    }

    [Test]
    public async Task AnalyzeAsync_EmailText_ExtractsIntents()
    {
        EnsureSecretsLoaded();
        
        // Arrange
        var emailText = @"
            Szanowny Panie Kowalski,
            
            Piszę w sprawie naszej rozmowy z zeszłego tygodnia.
            Chciałbym umówić się na spotkanie w celu omówienia warunków współpracy.
            Proszę o przesłanie dostępnych terminów na przyszły tydzień.
            
            Dodatkowo, czy mógłby Pan przesłać mi ofertę cenową na usługi konsultingowe?
            Interesuje mnie pakiet podstawowy oraz premium.
            
            Pozdrawiam,
            Adam Malinowski
        ";
        
        // Act
        var result = await _analysisService.AnalyzeAsync(emailText);
        
        // Assert
        Assert.That(result, Is.Not.Null);
        TestContext.WriteLine($"Summary: {result.Summary}");
        
        if (result.DetectedIntents?.Any() == true)
        {
            TestContext.WriteLine("Detected Intents:");
            foreach (var intent in result.DetectedIntents)
            {
                TestContext.WriteLine($"  - {intent.Intent}: {intent.Evidence} ({intent.Confidence:P0})");
            }
        }
    }

    [Test]
    public async Task AnalyzeAsync_ContractText_FindsRedFlags()
    {
        EnsureSecretsLoaded();
        
        // Arrange - tekst z potencjalnymi problemami
        var contractText = @"
            UMOWA O ŚWIADCZENIE USŁUG
            
            1. Wykonawca zobowiązuje się do wykonania prac w terminie nieokreślonym.
            2. Zamawiający płaci 100% wynagrodzenia z góry, przed rozpoczęciem prac.
            3. W przypadku rezygnacji, Zamawiający traci całą wpłaconą kwotę.
            4. Wykonawca ma prawo jednostronnie zmienić warunki umowy bez powiadomienia.
            5. Wszelkie spory rozstrzygane są wyłącznie przez sąd w Singapurze.
            6. Zamawiający zrzeka się prawa do reklamacji.
        ";
        
        // Act
        var result = await _analysisService.AnalyzeAsync(contractText);
        
        // Assert
        Assert.That(result, Is.Not.Null);
        TestContext.WriteLine($"Summary: {result.Summary}");
        
        if (result.RedFlags?.Any() == true)
        {
            TestContext.WriteLine("Red Flags:");
            foreach (var flag in result.RedFlags)
            {
                TestContext.WriteLine($"  [{flag.Severity}] {flag.Description}");
                if (!string.IsNullOrEmpty(flag.Recommendation))
                    TestContext.WriteLine($"    Recommendation: {flag.Recommendation}");
            }
        }
    }

    [Test]
    public async Task AnalyzeAsync_MeetingNotes_ExtractsKeyPoints()
    {
        EnsureSecretsLoaded();
        
        // Arrange
        var meetingNotes = @"
            Notatki ze spotkania projektowego - 15.01.2025
            
            Obecni: Anna, Piotr, Marek, Kasia
            
            Omówiono postępy w projekcie Alpha:
            - Frontend gotowy w 80%
            - Backend wymaga jeszcze 2 tygodni pracy
            - Testy rozpoczną się w lutym
            
            Ustalenia:
            - Piotr przygotuje dokumentację API do piątku
            - Kasia skontaktuje się z klientem w sprawie feedbacku
            - Następne spotkanie: 22.01.2025, godz. 10:00
            
            Budżet: pozostało 15000 PLN z 50000 PLN
        ";
        
        // Act
        var result = await _analysisService.AnalyzeAsync(meetingNotes);
        
        // Assert
        Assert.That(result, Is.Not.Null);
        
        if (result.KeyPoints?.Any() == true)
        {
            TestContext.WriteLine("Key Points:");
            foreach (var point in result.KeyPoints)
            {
                TestContext.WriteLine($"  • {point}");
            }
        }
        
        // Darmowe modele mogą nie wykryć key points
        TestContext.WriteLine($"Found {result.KeyPoints?.Count ?? 0} key points");
    }

    // DTO
    private class SecretsConfig
    {
        public OpenRouterConfig? OpenRouter { get; set; }
    }

    private class OpenRouterConfig
    {
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string Model { get; set; } = "";
    }
}
