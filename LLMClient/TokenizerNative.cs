using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public static class TokenizerNative
{
#if WINDOWS
    const string DllName = "tokenizer_rust.dll";
#elif ANDROID
    const string DllName = "tokenizer_rust";
#elif IOS
    const string DllName = "__Internal";
#else
    const string DllName = "tokenizer_rust";
#endif

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int tokenizer_init([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int tokenizer_encode([MarshalAs(UnmanagedType.LPUTF8Str)] string text, int[] out_ids, UIntPtr max_len);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr tokenizer_decode(int[] ids, UIntPtr len);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void tokenizer_cleanup();

    public static Task<int> InitAsync(string path) =>
        Task.Run(() => {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[TokenizerNative] Próba inicjalizacji z: {path}");
                var result = tokenizer_init(path);
                System.Diagnostics.Debug.WriteLine($"[TokenizerNative] Rezultat inicjalizacji: {result}");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TokenizerNative] Błąd inicjalizacji: {ex.Message}");
                return -999; // Custom error code for exceptions
            }
        });

    public static Task<int> EncodeAsync(string text, int[] out_ids, int maxLen) =>
        Task.Run(() => {
            try
            {
                return tokenizer_encode(text, out_ids, (UIntPtr)maxLen);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TokenizerNative] Błąd encode: {ex.Message}");
                return -1;
            }
        });

    public static Task<string?> DecodeAsync(int[] ids, int len) =>
        Task.Run(() =>
        {
            try
            {
                var ptr = tokenizer_decode(ids, (UIntPtr)len);
                if (ptr == IntPtr.Zero) return null;
                var str = Marshal.PtrToStringAnsi(ptr);
                // Marshal.FreeHGlobal(ptr); // Rust zarządza pamięcią
                return str;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TokenizerNative] Błąd decode: {ex.Message}");
                return null;
            }
        });

    public static void Cleanup() => tokenizer_cleanup();

    // ============== MULTI-TOKENIZER API ==============

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int tokenizer_init_named(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int tokenizer_encode_named(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        int[] out_ids,
        UIntPtr max_len);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr tokenizer_decode_named(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        int[] ids,
        UIntPtr len);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int tokenizer_cleanup_named([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int tokenizer_count();

    /// <summary>
    /// Inicjalizuje tokenizer pod podaną nazwą (np. "e5", "gemma")
    /// </summary>
    public static Task<int> InitNamedAsync(string name, string path) =>
        Task.Run(() => {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[TokenizerNative] Init '{name}' from: {path}");
                return tokenizer_init_named(name, path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TokenizerNative] Error init '{name}': {ex.Message}");
                return -999;
            }
        });

    /// <summary>
    /// Koduje tekst używając tokenizera o podanej nazwie
    /// </summary>
    public static Task<int> EncodeNamedAsync(string name, string text, int[] out_ids, int maxLen) =>
        Task.Run(() => {
            try
            {
                return tokenizer_encode_named(name, text, out_ids, (UIntPtr)maxLen);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TokenizerNative] Error encode '{name}': {ex.Message}");
                return -1;
            }
        });

    /// <summary>
    /// Dekoduje tokeny używając tokenizera o podanej nazwie
    /// </summary>
    public static Task<string?> DecodeNamedAsync(string name, int[] ids, int len) =>
        Task.Run(() => {
            try
            {
                var ptr = tokenizer_decode_named(name, ids, (UIntPtr)len);
                if (ptr == IntPtr.Zero) return null;
                return Marshal.PtrToStringAnsi(ptr);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TokenizerNative] Error decode '{name}': {ex.Message}");
                return null;
            }
        });

    /// <summary>
    /// Zwraca liczbę załadowanych tokenizerów
    /// </summary>
    public static int GetTokenizerCount()
    {
        try { return tokenizer_count(); }
        catch { return 0; }
    }
}