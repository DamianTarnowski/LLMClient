using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using LLMClient.Services;
using LLMClient.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.Diagnostics
{
    /// <summary>
    /// Test diagnostyczny dla modelu lokalnego Phi-4-mini-instruct
    /// Uruchamia model i analizuje jego output żeby zdiagnozować problemy
    /// </summary>
    public class ModelDiagnosticTest
    {
        private readonly ILogger<ModelDiagnosticTest> _logger;
        private readonly RobustLocalModelService _modelService;

        public ModelDiagnosticTest()
        {
            // Setup logowania
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => 
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            
            var serviceProvider = serviceCollection.BuildServiceProvider();
            _logger = serviceProvider.GetRequiredService<ILogger<ModelDiagnosticTest>>();
            
            var modelLogger = serviceProvider.GetRequiredService<ILogger<RobustLocalModelService>>();
            _modelService = new RobustLocalModelService(modelLogger, null, null);
        }

        public async Task<bool> RunDiagnosticsAsync()
        {
            Console.WriteLine("🔍 URUCHAMIANIE TESTU DIAGNOSTYCZNEGO PHI-4-MINI-INSTRUCT");
            Console.WriteLine("=" + new string('=', 60));

            try
            {
                // Test 1: Sprawdź czy model jest pobrany
                await TestModelAvailability();
                
                // Test 2: Załaduj model
                await TestModelLoading();
                
                // Test 3: Proste testy generacji
                await TestSimpleGeneration();
                
                // Test 4: Test z różnymi promptami
                await TestDifferentPrompts();
                
                // Test 5: Test zapętlania
                await TestLoopingBehavior();
                
                // Test 6: Test tokenów końcowych
                await TestStopTokens();

                Console.WriteLine("✅ DIAGNOSTYKA ZAKOŃCZONA POMYŚLNIE");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ BŁĄD PODCZAS DIAGNOSTYKI: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
            finally
            {
                await _modelService.UnloadModelAsync();
            }
        }

        private async Task TestModelAvailability()
        {
            Console.WriteLine("\n📁 TEST 1: Dostępność modelu");
            Console.WriteLine("-" + new string('-', 30));
            
            var isDownloaded = await _modelService.IsModelDownloadedAsync();
            Console.WriteLine($"Model pobrany: {(isDownloaded ? "✅ TAK" : "❌ NIE")}");
            
            if (!isDownloaded)
            {
                throw new Exception("Model nie jest pobrany! Pobierz go najpierw przez aplikację.");
            }
            
            var modelInfo = await _modelService.GetModelInfoAsync();
            Console.WriteLine($"Model ID: {modelInfo.ModelId}");
            Console.WriteLine($"Nazwa: {modelInfo.DisplayName}");
            Console.WriteLine($"Rozmiar: {modelInfo.SizeInMB} MB");
        }

        private async Task TestModelLoading()
        {
            Console.WriteLine("\n🔄 TEST 2: Ładowanie modelu");
            Console.WriteLine("-" + new string('-', 30));
            
            var loadResult = await _modelService.LoadModelAsync();
            Console.WriteLine($"Ładowanie: {(loadResult ? "✅ SUKCES" : "❌ BŁĄD")}");
            Console.WriteLine($"Stan modelu: {_modelService.State}");
            Console.WriteLine($"Czy załadowany: {(_modelService.IsLoaded ? "✅ TAK" : "❌ NIE")}");
            
            if (!loadResult || !_modelService.IsLoaded)
            {
                throw new Exception("Nie udało się załadować modelu!");
            }
        }

        private async Task TestSimpleGeneration()
        {
            Console.WriteLine("\n💬 TEST 3: Prosta generacja");
            Console.WriteLine("-" + new string('-', 30));
            
            var testPrompts = new[]
            {
                "Cześć",
                "Jak się masz?", 
                "Co to jest AI?",
                "2+2=?"
            };

            foreach (var prompt in testPrompts)
            {
                Console.WriteLine($"\n🔸 Prompt: \"{prompt}\"");
                try
                {
                    var startTime = DateTime.UtcNow;
                    var response = await _modelService.GenerateResponseAsync(prompt);
                    var duration = DateTime.UtcNow - startTime;
                    
                    Console.WriteLine($"⏱️  Czas: {duration.TotalMilliseconds:F0}ms");
                    Console.WriteLine($"📝 Odpowiedź ({response.Length} znaków):");
                    Console.WriteLine($"   \"{response}\"");
                    
                    // Analiza odpowiedzi
                    AnalyzeResponse(response, prompt);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ BŁĄD: {ex.Message}");
                }
                
                await Task.Delay(1000); // Pauza między testami
            }
        }

        private async Task TestDifferentPrompts()
        {
            Console.WriteLine("\n🎯 TEST 4: Różne typy promptów");
            Console.WriteLine("-" + new string('-', 30));
            
            var conversationHistory = new List<Message>();
            
            var testCases = new[]
            {
                ("Krótkie pytanie", "Ile to 5+3?"),
                ("Instrukcja", "Wymień 3 kolory."),
                ("Rozmowa", "Opowiedz coś ciekawego."),
                ("Kontekst", "Jestem programistą. Jakie języki polecasz?")
            };

            foreach (var (category, prompt) in testCases)
            {
                Console.WriteLine($"\n🔹 Kategoria: {category}");
                Console.WriteLine($"🔸 Prompt: \"{prompt}\"");
                
                try
                {
                    var response = await _modelService.GenerateResponseAsync(conversationHistory, prompt);
                    Console.WriteLine($"📝 Odpowiedź: \"{response}\"");
                    
                    // Dodaj do historii
                    conversationHistory.Add(new Message { Content = prompt, IsUser = true, Timestamp = DateTime.UtcNow });
                    conversationHistory.Add(new Message { Content = response, IsUser = false, Timestamp = DateTime.UtcNow });
                    
                    AnalyzeResponse(response, prompt);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ BŁĄD: {ex.Message}");
                }
                
                await Task.Delay(1000);
            }
        }

        private async Task TestLoopingBehavior()
        {
            Console.WriteLine("\n🔄 TEST 5: Test zapętlania");
            Console.WriteLine("-" + new string('-', 30));
            
            var loopPrompts = new[]
            {
                "Explain how lasers work", // Znany problematyczny prompt
                "Tell me about yourself",
                "What can you do?",
                "Describe the weather"
            };

            foreach (var prompt in loopPrompts)
            {
                Console.WriteLine($"\n🔸 Test zapętlania z: \"{prompt}\"");
                try
                {
                    var startTime = DateTime.UtcNow;
                    var response = await _modelService.GenerateResponseAsync(prompt);
                    var duration = DateTime.UtcNow - startTime;
                    
                    Console.WriteLine($"⏱️  Czas: {duration.TotalSeconds:F1}s");
                    Console.WriteLine($"📝 Odpowiedź ({response.Length} znaków):");
                    Console.WriteLine($"   \"{response}\"");
                    
                    // Specjalna analiza zapętlania
                    AnalyzeForLooping(response);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ BŁĄD: {ex.Message}");
                }
            }
        }

        private async Task TestStopTokens()
        {
            Console.WriteLine("\n🛑 TEST 6: Test tokenów końcowych");
            Console.WriteLine("-" + new string('-', 30));
            
            var prompt = "List three fruits:";
            Console.WriteLine($"🔸 Prompt: \"{prompt}\"");
            
            try
            {
                var response = await _modelService.GenerateResponseAsync(prompt);
                Console.WriteLine($"📝 Odpowiedź: \"{response}\"");
                
                // Sprawdź czy zawiera problematyczne tokeny
                var problematicTokens = new[] { "<|im_start|>", "<|im_end|>", "<|im_sep|>", "<|endoftext|>" };
                
                foreach (var token in problematicTokens)
                {
                    if (response.Contains(token))
                    {
                        Console.WriteLine($"⚠️  ZNALEZIONO PROBLEMATYCZNY TOKEN: {token}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ BŁĄD: {ex.Message}");
            }
        }

        private void AnalyzeResponse(string response, string originalPrompt)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                Console.WriteLine("⚠️  PROBLEM: Pusta odpowiedź");
                return;
            }

            // Test repetycji
            var words = response.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 5)
            {
                var uniqueWords = new HashSet<string>(words).Count;
                var repetitionRatio = (double)uniqueWords / words.Length;
                if (repetitionRatio < 0.3)
                {
                    Console.WriteLine($"⚠️  PROBLEM: Wysoka repetycja słów ({repetitionRatio:P0})");
                }
            }

            // Test długości
            if (response.Length > 500)
            {
                Console.WriteLine($"⚠️  PROBLEM: Bardzo długa odpowiedź ({response.Length} znaków)");
            }

            // Test japońskich znaków
            if (System.Text.RegularExpressions.Regex.IsMatch(response, @"[\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF]"))
            {
                Console.WriteLine("⚠️  PROBLEM: Japońskie/chińskie znaki w odpowiedzi");
            }

            // Test kontekstu
            if (response.Length < originalPrompt.Length / 2)
            {
                Console.WriteLine("⚠️  UWAGA: Bardzo krótka odpowiedź względem promptu");
            }

            Console.WriteLine($"✅ Analiza: {words.Length} słów, {response.Length} znaków");
        }

        private void AnalyzeForLooping(string response)
        {
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // Sprawdź powtarzające się linie
            var lineCount = new Dictionary<string, int>();
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    lineCount[trimmed] = lineCount.GetValueOrDefault(trimmed, 0) + 1;
                }
            }

            var repeatedLines = lineCount.Where(kvp => kvp.Value > 1).ToList();
            if (repeatedLines.Any())
            {
                Console.WriteLine("⚠️  ZAPĘTLANIE WYKRYTE:");
                foreach (var (line, count) in repeatedLines)
                {
                    Console.WriteLine($"    \"{line}\" - powtórzone {count}x");
                }
            }
            else
            {
                Console.WriteLine("✅ Brak wykrytego zapętlania");
            }
        }
    }

    // Program główny do uruchomienia testu
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("PHI-4-MINI-INSTRUCT DIAGNOSTIC TOOL");
            Console.WriteLine("Uruchamianie diagnostyki modelu lokalnego...\n");

            var diagnostic = new ModelDiagnosticTest();
            var success = await diagnostic.RunDiagnosticsAsync();

            Console.WriteLine($"\n🏁 WYNIK DIAGNOSTYKI: {(success ? "✅ SUKCES" : "❌ BŁĄD")}");
            Console.WriteLine("\nNaciśnij Enter aby zamknąć...");
            Console.ReadLine();
        }
    }
}