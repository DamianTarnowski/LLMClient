package com.llmclient.mlcllm;

import android.util.Log;

/**
 * Native helper to load shared libraries with RTLD_GLOBAL flag.
 * This is required so that external model libraries (e.g. libphi2_q4f16_1_.so)
 * can resolve symbols like TVMFFIFunctionCall from the TVM runtime.
 */
public class RtldGlobalLoader {
    private static final String TAG = "RtldGlobalLoader";
    private static boolean sNativeLoaded = false;

    static {
        try {
            System.loadLibrary("rtld_global_loader");
            sNativeLoaded = true;
            Log.i(TAG, "Native rtld_global_loader loaded");
        } catch (UnsatisfiedLinkError e) {
            Log.e(TAG, "Failed to load rtld_global_loader: " + e.getMessage());
        }
    }

    /**
     * Load a shared library with RTLD_NOW | RTLD_GLOBAL flags.
     * @param absolutePath Full path to the .so file
     * @return true if successful
     */
    public static boolean loadWithRtldGlobal(String absolutePath) {
        if (!sNativeLoaded) {
            Log.w(TAG, "Native loader not available, falling back to System.load");
            try {
                System.load(absolutePath);
                return true;
            } catch (UnsatisfiedLinkError e) {
                Log.e(TAG, "System.load fallback failed: " + e.getMessage());
                return false;
            }
        }
        return dlopenGlobal(absolutePath);
    }

    /**
     * Check if native loader is available
     */
    public static boolean isAvailable() {
        return sNativeLoaded;
    }

    private static native boolean dlopenGlobal(String path);
}
