using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public static class TokenizerNative
{
#if WINDOWS
    const string DllName = "tokenizer_rust.dll";
#elif ANDROID
    const string DllName = "libtokenizer_rust.so";
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
        Task.Run(() => tokenizer_init(path));

    public static Task<int> EncodeAsync(string text, int[] out_ids, int maxLen) =>
        Task.Run(() => tokenizer_encode(text, out_ids, (UIntPtr)maxLen));

    public static Task<string> DecodeAsync(int[] ids, int len) =>
        Task.Run(() =>
        {
            var ptr = tokenizer_decode(ids, (UIntPtr)len);
            if (ptr == IntPtr.Zero) return null;
            var str = Marshal.PtrToStringAnsi(ptr);
            // Marshal.FreeHGlobal(ptr); // Rust zarządza pamięcią
            return str;
        });

    public static void Cleanup() => tokenizer_cleanup();
} 