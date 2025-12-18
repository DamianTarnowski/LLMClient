package com.llmclient.mlcllm;

import android.content.Context;
import android.util.Log;

import org.json.JSONObject;
import org.json.JSONArray;
import org.json.JSONException;

import java.io.File;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.function.Consumer;

/**
 * MLC LLM Engine wrapper for Android
 * Provides high-performance GPU-accelerated LLM inference using OpenCL
 */
public class MlcLlmEngine {
    private static final String TAG = "MlcLlmEngine";

    // Native library handle
    private long engineHandle = 0;
    private boolean isLoaded = false;
    private String modelPath = "";
    private ExecutorService executor;

    // Callbacks
    public interface StreamCallback {
        void onToken(String token);
        void onComplete(String fullResponse);
        void onError(String error);
    }

    public interface ProgressCallback {
        void onProgress(double progress, String status);
    }

    static {
        try {
            // Load MLC LLM native libraries
            System.loadLibrary("mlc_llm");
            System.loadLibrary("tvm_runtime");
            Log.i(TAG, "MLC LLM native libraries loaded successfully");
        } catch (UnsatisfiedLinkError e) {
            Log.e(TAG, "Failed to load MLC LLM libraries: " + e.getMessage());
        }
    }

    public MlcLlmEngine() {
        executor = Executors.newSingleThreadExecutor();
    }

    /**
     * Initialize the engine with a model
     * @param context Android context
     * @param modelId Model identifier (e.g., "Qwen2-1.5B-Instruct-q4f16_1-MLC")
     * @param modelPath Path to model files
     */
    public void initialize(Context context, String modelId, String modelPath) {
        this.modelPath = modelPath;
        executor.execute(() -> {
            try {
                engineHandle = nativeCreateEngine(modelPath, getDefaultConfig());
                if (engineHandle != 0) {
                    isLoaded = true;
                    Log.i(TAG, "MLC LLM Engine initialized with model: " + modelId);
                } else {
                    Log.e(TAG, "Failed to create MLC LLM engine");
                }
            } catch (Exception e) {
                Log.e(TAG, "Error initializing engine: " + e.getMessage());
            }
        });
    }

    /**
     * Check if model is loaded and ready
     */
    public boolean isReady() {
        return isLoaded && engineHandle != 0;
    }

    /**
     * Generate response (non-streaming)
     */
    public String generate(String prompt, int maxTokens, double temperature) {
        if (!isReady()) {
            return "Error: Model not loaded";
        }

        try {
            String config = buildGenerationConfig(maxTokens, temperature);
            return nativeGenerate(engineHandle, prompt, config);
        } catch (Exception e) {
            Log.e(TAG, "Generation error: " + e.getMessage());
            return "Error: " + e.getMessage();
        }
    }

    /**
     * Generate response with streaming
     */
    public void generateStreaming(String prompt, int maxTokens, double temperature, StreamCallback callback) {
        if (!isReady()) {
            callback.onError("Model not loaded");
            return;
        }

        executor.execute(() -> {
            try {
                String config = buildGenerationConfig(maxTokens, temperature);
                nativeGenerateStreaming(engineHandle, prompt, config, new NativeStreamCallback() {
                    StringBuilder fullResponse = new StringBuilder();

                    @Override
                    public void onToken(String token) {
                        fullResponse.append(token);
                        callback.onToken(token);
                    }

                    @Override
                    public void onComplete() {
                        callback.onComplete(fullResponse.toString());
                    }

                    @Override
                    public void onError(String error) {
                        callback.onError(error);
                    }
                });
            } catch (Exception e) {
                callback.onError(e.getMessage());
            }
        });
    }

    /**
     * Build chat messages in OpenAI format
     */
    public String buildChatPrompt(String systemPrompt, String[] history, String userMessage) {
        try {
            JSONObject request = new JSONObject();
            JSONArray messages = new JSONArray();

            // System message
            if (systemPrompt != null && !systemPrompt.isEmpty()) {
                JSONObject sysMsg = new JSONObject();
                sysMsg.put("role", "system");
                sysMsg.put("content", systemPrompt);
                messages.put(sysMsg);
            }

            // History (alternating user/assistant)
            if (history != null) {
                for (int i = 0; i < history.length; i++) {
                    JSONObject msg = new JSONObject();
                    msg.put("role", i % 2 == 0 ? "user" : "assistant");
                    msg.put("content", history[i]);
                    messages.put(msg);
                }
            }

            // New user message
            JSONObject userMsg = new JSONObject();
            userMsg.put("role", "user");
            userMsg.put("content", userMessage);
            messages.put(userMsg);

            request.put("messages", messages);
            return request.toString();

        } catch (JSONException e) {
            Log.e(TAG, "Error building chat prompt: " + e.getMessage());
            return userMessage;
        }
    }

    /**
     * Unload model and free resources
     */
    public void unload() {
        if (engineHandle != 0) {
            nativeDestroyEngine(engineHandle);
            engineHandle = 0;
            isLoaded = false;
            Log.i(TAG, "MLC LLM Engine unloaded");
        }
    }

    /**
     * Get model info
     */
    public String getModelInfo() {
        if (engineHandle != 0) {
            return nativeGetModelInfo(engineHandle);
        }
        return "{}";
    }

    /**
     * Reset conversation/KV cache
     */
    public void resetChat() {
        if (engineHandle != 0) {
            nativeResetChat(engineHandle);
        }
    }

    private String getDefaultConfig() {
        try {
            JSONObject config = new JSONObject();
            config.put("device", "opencl");  // Use GPU via OpenCL
            config.put("model_lib", "");
            return config.toString();
        } catch (JSONException e) {
            return "{}";
        }
    }

    private String buildGenerationConfig(int maxTokens, double temperature) {
        try {
            JSONObject config = new JSONObject();
            config.put("max_tokens", maxTokens);
            config.put("temperature", temperature);
            config.put("top_p", 0.95);
            config.put("frequency_penalty", 0.0);
            config.put("presence_penalty", 0.0);
            config.put("stop", new JSONArray().put("<|endoftext|>").put("<|im_end|>"));
            return config.toString();
        } catch (JSONException e) {
            return "{}";
        }
    }

    public void dispose() {
        unload();
        if (executor != null) {
            executor.shutdown();
        }
    }

    // Native interface callback
    interface NativeStreamCallback {
        void onToken(String token);
        void onComplete();
        void onError(String error);
    }

    // Native methods (implemented in C++)
    private native long nativeCreateEngine(String modelPath, String config);
    private native void nativeDestroyEngine(long handle);
    private native String nativeGenerate(long handle, String prompt, String config);
    private native void nativeGenerateStreaming(long handle, String prompt, String config, NativeStreamCallback callback);
    private native String nativeGetModelInfo(long handle);
    private native void nativeResetChat(long handle);
}
