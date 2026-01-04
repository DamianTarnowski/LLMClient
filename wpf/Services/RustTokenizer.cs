using System.Runtime.InteropServices;
using System.Text;

namespace WPFLLM.Services;

/// <summary>
/// P/Invoke bindings for the Rust HuggingFace tokenizer
/// </summary>
public static class RustTokenizer
{
    private const string DllName = "hf_tokenizer";
    private static bool _isInitialized;
    private static readonly object _lock = new();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int tokenizer_initialize(byte[] path);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int tokenizer_encode(byte[] text, int[] outIds, nuint maxLen);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void tokenizer_free();

    /// <summary>
    /// Initialize the tokenizer from a tokenizer.json file
    /// </summary>
    public static bool Initialize(string tokenizerPath)
    {
        lock (_lock)
        {
            if (_isInitialized)
                return true;

            var pathBytes = Encoding.UTF8.GetBytes(tokenizerPath + "\0");
            var result = tokenizer_initialize(pathBytes);
            
            if (result == 0)
            {
                _isInitialized = true;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Encode text to token IDs with special tokens (add_special_tokens=true)
    /// </summary>
    public static int[] Encode(string text, int maxLength = 512)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Tokenizer not initialized. Call Initialize() first.");

        var textBytes = Encoding.UTF8.GetBytes(text + "\0");
        var outputIds = new int[maxLength];
        
        var count = tokenizer_encode(textBytes, outputIds, (nuint)maxLength);
        
        if (count < 0)
            throw new Exception($"Tokenization failed with error code: {count}");

        return outputIds.Take(count).ToArray();
    }

    /// <summary>
    /// Check if the tokenizer is initialized
    /// </summary>
    public static bool IsInitialized => _isInitialized;
}
