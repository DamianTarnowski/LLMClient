using System;
using System.Threading.Tasks;
using System.IO;

/// <summary>
/// Szybki test diagnostyczny modelu - sprawdza podstawowe informacje
/// </summary>
public class QuickModelTest
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🔍 SZYBKA DIAGNOSTYKA MODELU PHI-4-MINI-INSTRUCT");
        Console.WriteLine("=" + new string('=', 50));

        try
        {
            // Test 1: Sprawdź ścieżki modeli
            await CheckModelPaths();
            
            // Test 2: Sprawdź pliki modelu
            await CheckModelFiles();
            
            // Test 3: Sprawdź rozmiary plików
            await CheckFileSizes();

            Console.WriteLine("\n✅ DIAGNOSTYKA ZAKOŃCZONA");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ BŁĄD: {ex.Message}");
        }

        Console.WriteLine("\nNaciśnij Enter aby zakmnąć...");
        Console.ReadLine();
    }

    private static async Task CheckModelPaths()
    {
        Console.WriteLine("\n📁 TEST 1: Sprawdzanie ścieżek modeli");
        Console.WriteLine("-" + new string('-', 30));

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var modelsPath = Path.Combine(localAppData, "LLMClient", "Models");
        
        Console.WriteLine($"Ścieżka modeli: {modelsPath}");
        Console.WriteLine($"Katalog istnieje: {Directory.Exists(modelsPath)}");

        if (Directory.Exists(modelsPath))
        {
            var directories = Directory.GetDirectories(modelsPath);
            Console.WriteLine($"Znalezione modele ({directories.Length}):");
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                var fileCount = Directory.GetFiles(dir).Length;
                Console.WriteLine($"  ✅ {dirName} ({fileCount} plików)");
            }
        }
    }

    private static async Task CheckModelFiles()
    {
        Console.WriteLine("\n📄 TEST 2: Sprawdzanie plików modelu");
        Console.WriteLine("-" + new string('-', 30));

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var instructPath = Path.Combine(localAppData, "LLMClient", "Models", "phi-4-mini-instruct");
        
        if (!Directory.Exists(instructPath))
        {
            Console.WriteLine("❌ Folder phi-4-mini-instruct nie istnieje!");
            return;
        }

        var requiredFiles = new[]
        {
            "model.onnx",
            "model.onnx.data", 
            "tokenizer.json",
            "config.json",
            "genai_config.json",
            "tokenizer_config.json",
            "special_tokens_map.json"
        };

        Console.WriteLine($"Sprawdzanie w: {instructPath}");
        
        foreach (var fileName in requiredFiles)
        {
            var filePath = Path.Combine(instructPath, fileName);
            var exists = File.Exists(filePath);
            var size = exists ? new FileInfo(filePath).Length : 0;
            
            Console.WriteLine($"  {(exists ? "✅" : "❌")} {fileName} ({FormatBytes(size)})");
        }
    }

    private static async Task CheckFileSizes()
    {
        Console.WriteLine("\n📊 TEST 3: Analiza rozmiarów plików");
        Console.WriteLine("-" + new string('-', 30));

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var instructPath = Path.Combine(localAppData, "LLMClient", "Models", "phi-4-mini-instruct");
        
        if (!Directory.Exists(instructPath))
        {
            Console.WriteLine("❌ Folder nie istnieje!");
            return;
        }

        // Sprawdź główne pliki modelu
        var modelOnnx = Path.Combine(instructPath, "model.onnx");
        var modelData = Path.Combine(instructPath, "model.onnx.data");
        
        if (File.Exists(modelOnnx))
        {
            var size = new FileInfo(modelOnnx).Length;
            var expectedSize = 52118230; // ~52.1MB
            var diff = Math.Abs(size - expectedSize);
            var isCorrect = diff < expectedSize * 0.05; // 5% tolerancja
            
            Console.WriteLine($"model.onnx: {FormatBytes(size)} (oczekiwane: {FormatBytes(expectedSize)})");
            Console.WriteLine($"  {(isCorrect ? "✅" : "⚠️")} Rozmiar {(isCorrect ? "poprawny" : "niepoprawny")}");
        }

        if (File.Exists(modelData))
        {
            var size = new FileInfo(modelData).Length;
            var expectedSize = 4860000000L; // ~4.86GB
            var diff = Math.Abs(size - expectedSize);
            var isCorrect = diff < expectedSize * 0.05; // 5% tolerancja
            
            Console.WriteLine($"model.onnx.data: {FormatBytes(size)} (oczekiwane: {FormatBytes(expectedSize)})");
            Console.WriteLine($"  {(isCorrect ? "✅" : "⚠️")} Rozmiar {(isCorrect ? "poprawny" : "niepoprawny")}");
        }

        // Sprawdź config.json
        var configPath = Path.Combine(instructPath, "config.json");
        if (File.Exists(configPath))
        {
            try
            {
                var configContent = await File.ReadAllTextAsync(configPath);
                Console.WriteLine($"\nconfig.json zawartość (pierwsze 200 znaków):");
                Console.WriteLine($"  {configContent.Substring(0, Math.Min(200, configContent.Length))}...");
                
                // Sprawdź czy to model instruct
                if (configContent.Contains("\"model_type\""))
                {
                    Console.WriteLine("  ✅ Zawiera model_type");
                }
                if (configContent.Contains("instruct") || configContent.Contains("Instruct"))
                {
                    Console.WriteLine("  ✅ Zawiera 'instruct' w konfiguracji");
                }
                else
                {
                    Console.WriteLine("  ⚠️  NIE zawiera 'instruct' w konfiguracji - może być bazowy model!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Błąd odczytu config.json: {ex.Message}");
            }
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;
        
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }
        
        return $"{size:F1} {suffixes[suffixIndex]}";
    }
}