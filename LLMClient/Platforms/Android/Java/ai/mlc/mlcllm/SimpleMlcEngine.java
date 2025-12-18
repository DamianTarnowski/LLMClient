package ai.mlc.mlcllm;

import android.util.Log;

/**
 * Simplified MLC LLM Engine wrapper for .NET MAUI integration.
 * This class provides a straightforward Java API that can be easily called from C# via JNI.
 */
public class SimpleMlcEngine {
    private static final String TAG = "SimpleMlcEngine";

    private JSONFFIEngine ffiEngine;
    private volatile boolean isInitialized = false;
    private volatile boolean isGenerating = false;
    private volatile String lastResponse = "";
    private volatile String lastError = "";

    // Standard Android callback for easier C# integration
    // Value format: "TOKEN:text", "DONE:full_text", "ERROR:message"
    private android.webkit.ValueCallback<String> streamCallback;
    private StringBuilder responseBuilder = new StringBuilder();

    private volatile boolean backgroundLoopsStarted = false;

    public SimpleMlcEngine() {
        Log.i(TAG, "SimpleMlcEngine created");
    }

    /**
     * Initialize the engine. Must be called on a background thread.
     */
    public boolean initialize() {
        try {
            Log.i(TAG, "Initializing JSONFFIEngine...");
            ffiEngine = new JSONFFIEngine();

            // Initialize with callback for streaming responses
            ffiEngine.initBackgroundEngine(new JSONFFIEngine.KotlinFunction() {
                @Override
                public void invoke(String jsonResponse) {
                    handleStreamResponse(jsonResponse);
                }
            });

            // Background loops are started after model load (reload) to avoid running the loop
            // when the engine is not yet configured.

            isInitialized = true;
            Log.i(TAG, "Engine initialized successfully");
            return true;
        } catch (Throwable t) {
            lastError = "Init failed: " + t.getMessage();
            Log.e(TAG, lastError, t);
            return false;
        }
    }

    private void startBackgroundLoopsIfNeeded() {
        if (backgroundLoopsStarted || ffiEngine == null) {
            return;
        }
        backgroundLoopsStarted = true;

        // Start background loops in separate threads
        new Thread(() -> {
            try {
                Thread.currentThread().setPriority(Thread.MAX_PRIORITY);
                ffiEngine.runBackgroundLoop();
            } catch (Throwable t) {
                lastError = "Background loop crashed (marker=cbret-fix-2025-12-15): " + t.getMessage();
                Log.e(TAG, lastError, t);
                if (streamCallback != null) streamCallback.onReceiveValue("ERROR:" + lastError);
            }
        }).start();

        new Thread(() -> {
            try {
                ffiEngine.runBackgroundStreamBackLoop();
            } catch (Throwable t) {
                lastError = "Stream-back loop crashed (marker=cbret-fix-2025-12-15): " + t.getMessage();
                Log.e(TAG, lastError, t);
                if (streamCallback != null) streamCallback.onReceiveValue("ERROR:" + lastError);
            }
        }).start();
    }

    /**
     * Load a model.
     * @param modelPath Path to the model directory
     * @param modelLib Model library identifier (e.g., "qwen2_q4f16_1_...")
     */
    public boolean loadModel(String modelPath, String modelLib) {
        if (!isInitialized || ffiEngine == null) {
            lastError = "Engine not initialized";
            return false;
        }

        try {
            Log.i(TAG, "Loading model: " + modelPath + " with lib: " + modelLib);

            String modelLibConfig;
            if (modelLib == null || modelLib.isEmpty()) {
                modelLibConfig = "";
            } else if (modelLib.contains("://") || modelLib.contains("/") || modelLib.endsWith(".so")) {
                // External library path/URI
                modelLibConfig = modelLib;
            } else {
                // Precompiled embedded system lib key
                modelLibConfig = "system://" + modelLib;
            }

            // Match upstream MLCEngine.kt format exactly - no "device" field
            String engineConfig = String.format(
                "{\"model\": \"%s\", \"model_lib\": \"%s\", \"mode\": \"interactive\"}",
                modelPath.replace("\\", "/"),
                modelLibConfig
            );

            startBackgroundLoopsIfNeeded();
            long t0 = System.currentTimeMillis();
            Log.i(TAG, "Reload start");
            ffiEngine.reload(engineConfig);
            Log.i(TAG, "Reload done in " + (System.currentTimeMillis() - t0) + " ms");
            Log.i(TAG, "Model loaded successfully");
            return true;
        } catch (Exception e) {
            lastError = "Load failed: " + e.getMessage();
            Log.e(TAG, lastError, e);
            return false;
        }
    }

    /**
     * Generate a response (blocking).
     */
    public String generate(String prompt, int maxTokens, double temperature) {
        if (!isInitialized || ffiEngine == null) {
            return "Error: Engine not initialized";
        }

        if (isGenerating) {
            return "Error: Generation already in progress";
        }

        try {
            isGenerating = true;
            responseBuilder.setLength(0);
            lastResponse = "";

            // Build OpenAI-compatible request JSON
            String requestJson = buildChatRequest(prompt, maxTokens, temperature);
            String requestId = "req_" + System.currentTimeMillis();

            Log.i(TAG, "Starting generation with request: " + requestId);
            ffiEngine.chatCompletion(requestJson, requestId);

            // Wait for completion (simple polling - could be improved with proper sync)
            int timeout = 60000; // 60 seconds
            int waited = 0;
            while (isGenerating && waited < timeout) {
                Thread.sleep(100);
                waited += 100;
            }

            return responseBuilder.toString();
        } catch (Exception e) {
            lastError = "Generate failed: " + e.getMessage();
            Log.e(TAG, lastError, e);
            return "Error: " + e.getMessage();
        } finally {
            isGenerating = false;
        }
    }

    /**
     * Generate with streaming callback using standard Android ValueCallback.
     * @param callback Receives strings prefixed with "TOKEN:", "DONE:", or "ERROR:"
     */
    public void generateStreaming(String prompt, int maxTokens, double temperature, android.webkit.ValueCallback<String> callback) {
        this.streamCallback = callback;

        if (!isInitialized || ffiEngine == null) {
            if (callback != null) callback.onReceiveValue("ERROR:Engine not initialized");
            return;
        }

        if (isGenerating) {
            if (callback != null) callback.onReceiveValue("ERROR:Generation already in progress");
            return;
        }

        new Thread(() -> {
            try {
                isGenerating = true;
                responseBuilder.setLength(0);

                String requestJson = buildChatRequest(prompt, maxTokens, temperature);
                String requestId = "req_" + System.currentTimeMillis();

                Log.i(TAG, "Starting streaming generation: " + requestId);
                ffiEngine.chatCompletion(requestJson, requestId);

            } catch (Exception e) {
                lastError = e.getMessage();
                if (callback != null) callback.onReceiveValue("ERROR:" + e.getMessage());
                isGenerating = false;
            }
        }).start();
    }

    /**
     * Build OpenAI-compatible chat completion request.
     */
    private String buildChatRequest(String prompt, int maxTokens, double temperature) {
        // Simple JSON building without dependencies
        return String.format(
            "{" +
            "\"messages\": [{\"role\": \"user\", \"content\": \"%s\"}]," +
            "\"max_tokens\": %d," +
            "\"temperature\": %f," +
            "\"stream\": true" +
            "}",
            escapeJson(prompt),
            maxTokens,
            temperature
        );
    }

    /**
     * Handle streaming response from FFI engine.
     */
    private void handleStreamResponse(String jsonResponse) {
        try {
            // Parse the JSON response array
            // Format: [{"id":"...","choices":[{"delta":{"content":"token"}}]}]

            if (jsonResponse == null || jsonResponse.isEmpty()) return;

            // Simple parsing - extract content from delta
            int contentStart = jsonResponse.indexOf("\"content\":\"");
            if (contentStart > 0) {
                contentStart += 11;
                int contentEnd = jsonResponse.indexOf("\"", contentStart);
                if (contentEnd > contentStart) {
                    String token = jsonResponse.substring(contentStart, contentEnd);
                    token = unescapeJson(token);

                    responseBuilder.append(token);

                    if (streamCallback != null) {
                        streamCallback.onReceiveValue("TOKEN:" + token);
                    }
                }
            }

            // Check for completion (usage field present means done)
            if (jsonResponse.contains("\"usage\":")) {
                String fullResponse = responseBuilder.toString();
                lastResponse = fullResponse;
                isGenerating = false;

                if (streamCallback != null) {
                    streamCallback.onReceiveValue("DONE:" + fullResponse);
                }
            }

        } catch (Exception e) {
            Log.e(TAG, "Error parsing response: " + e.getMessage());
        }
    }

    /**
     * Reset the conversation.
     */
    public void reset() {
        if (ffiEngine != null) {
            try {
                ffiEngine.reset();
                responseBuilder.setLength(0);
                Log.i(TAG, "Conversation reset");
            } catch (Exception e) {
                Log.e(TAG, "Reset failed: " + e.getMessage());
            }
        }
    }

    /**
     * Unload the model.
     */
    public void unload() {
        if (ffiEngine != null) {
            try {
                ffiEngine.unload();
                Log.i(TAG, "Model unloaded");
            } catch (Exception e) {
                Log.e(TAG, "Unload failed: " + e.getMessage());
            }
        }
    }

    /**
     * Check if engine is ready.
     */
    public boolean isReady() {
        return isInitialized && ffiEngine != null;
    }

    /**
     * Check if currently generating.
     */
    public boolean isGenerating() {
        return isGenerating;
    }

    /**
     * Get last error message.
     */
    public String getLastError() {
        return lastError;
    }

    /**
     * Get last response.
     */
    public String getLastResponse() {
        return lastResponse;
    }

    // Simple JSON escape
    private String escapeJson(String str) {
        return str
            .replace("\\", "\\\\")
            .replace("\"", "\\\"")
            .replace("\n", "\\n")
            .replace("\r", "\\r")
            .replace("\t", "\\t");
    }

    // Simple JSON unescape
    private String unescapeJson(String str) {
        return str
            .replace("\\n", "\n")
            .replace("\\r", "\r")
            .replace("\\t", "\t")
            .replace("\\\"", "\"")
            .replace("\\\\", "\\");
    }
}
