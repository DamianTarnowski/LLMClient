using System.Diagnostics;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace LocalLlmTest;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("=== Comprehensive Local LLM Test ===");
        Console.WriteLine();

        var modelPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WPFLLM", "models", "phi-3-mini-4k-instruct");

        Console.WriteLine($"Model path: {modelPath}");

        if (!Directory.Exists(modelPath))
        {
            Console.WriteLine("ERROR: Model not downloaded.");
            return;
        }

        Console.WriteLine("Loading model...");
        var sw = Stopwatch.StartNew();

        try
        {
            using var model = new Model(modelPath);
            using var tokenizer = new Tokenizer(model);
            
            Console.WriteLine($"Model loaded in {sw.ElapsedMilliseconds}ms");
            Console.WriteLine();

            // Test 1: Basic math
            Console.WriteLine("=== TEST 1: Basic Math ===");
            await Test(model, tokenizer, "What is 25 * 4 + 10? Give only the number.", "You are a calculator. Answer with just the number.", 64);

            // Test 2: Factual knowledge
            Console.WriteLine("=== TEST 2: Factual Knowledge ===");
            await Test(model, tokenizer, "What is the capital of Poland?", "You are a helpful assistant. Be brief.", 64);

            // Test 3: Polish language
            Console.WriteLine("=== TEST 3: Polish Language ===");
            await Test(model, tokenizer, "Co to jest sztuczna inteligencja? Odpowiedz krotko.", "Jestes pomocnym asystentem. Odpowiadaj po polsku.", 128);

            // Test 4: Code generation
            Console.WriteLine("=== TEST 4: Code Generation ===");
            await Test(model, tokenizer, "Write a C# function to reverse a string. Just the code, no explanation.", "You are a C# programmer. Output only code.", 256);

            // Test 5: Reasoning
            Console.WriteLine("=== TEST 5: Reasoning ===");
            await Test(model, tokenizer, "If all roses are flowers and some flowers fade quickly, can we conclude that some roses fade quickly?", "You are a logician. Explain briefly.", 128);

            // Test 6: Creative writing
            Console.WriteLine("=== TEST 6: Creative Writing ===");
            await Test(model, tokenizer, "Write a haiku about programming.", "You are a poet.", 64);

            // Test 7: Instruction following
            Console.WriteLine("=== TEST 7: Instruction Following ===");
            await Test(model, tokenizer, "List exactly 3 programming languages. Number them 1-3.", "You follow instructions exactly.", 64);

            // Test 8: Polish conversation
            Console.WriteLine("=== TEST 8: Polish Conversation ===");
            await Test(model, tokenizer, "Jakie sa najwieksze jeziora w Polsce?", "Jestes ekspertem od geografii Polski. Odpowiadaj po polsku.", 128);

            // Test 9: Longer generation
            Console.WriteLine("=== TEST 9: Longer Generation ===");
            await Test(model, tokenizer, "Explain how RAG (Retrieval Augmented Generation) works in 3 sentences.", "You are an AI expert. Be concise.", 192);

            Console.WriteLine();
            Console.WriteLine("=== ALL TESTS COMPLETED ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    static async Task Test(Model model, Tokenizer tokenizer, string prompt, string system, int maxLen)
    {
        Console.WriteLine($"Q: {prompt}");
        Console.Write("A: ");
        
        var nl = "\n";
        var fullPrompt = "<|system|>" + nl + system + "<|end|>" + nl + "<|user|>" + nl + prompt + "<|end|>" + nl + "<|assistant|>" + nl;
        
        using var sequences = tokenizer.Encode(fullPrompt);
        using var genParams = new GeneratorParams(model);
        genParams.SetSearchOption("max_length", maxLen);
        genParams.SetSearchOption("temperature", 0.7);
        genParams.SetSearchOption("top_p", 0.9);
        genParams.SetSearchOption("repetition_penalty", 1.1);
        
        using var generator = new Generator(model, genParams);
        generator.AppendTokenSequences(sequences);
        
        using var stream = tokenizer.CreateStream();
        var sw = Stopwatch.StartNew();
        var tokenCount = 0;
        
        while (!generator.IsDone())
        {
            generator.GenerateNextToken();
            var tokens = generator.GetNextTokens();
            if (tokens.Length > 0)
            {
                var text = stream.Decode(tokens[0]);
                Console.Write(text);
                tokenCount++;
            }
            await Task.Yield();
        }
        
        var elapsed = sw.Elapsed.TotalSeconds;
        var tokPerSec = tokenCount / (elapsed > 0 ? elapsed : 1);
        Console.WriteLine();
        Console.WriteLine($"[{tokenCount} tokens, {elapsed:F2}s, {tokPerSec:F1} tok/s]");
        Console.WriteLine();
    }
}
