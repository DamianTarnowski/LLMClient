package ai.mlc.mlcllm;

import org.apache.tvm.Device;
import org.apache.tvm.Function;
import org.apache.tvm.Module;
import org.apache.tvm.TVMValue;
import android.util.Log;

public class JSONFFIEngine {
    private static final String TAG = "JSONFFIEngine";
    
    private Module jsonFFIEngine;
    private Function initBackgroundEngineFunc;
    private Function reloadFunc;
    private Function unloadFunc;
    private Function resetFunc;
    private Function chatCompletionFunc;
    private Function abortFunc;
    private Function getLastErrorFunc;
    private Function runBackgroundLoopFunc;
    private Function runBackgroundStreamBackLoopFunc;
    private Function exitBackgroundLoopFunc;
    private Function requestStreamCallback;

    public JSONFFIEngine() {
        Log.i(TAG, "Creating JSONFFIEngine... (marker=jsonffi-2025-12-15)");
        
        Function createFunc = Function.getFunction("mlc.json_ffi.CreateJSONFFIEngine");
        if (createFunc == null) {
            Log.e(TAG, "CRITICAL: mlc.json_ffi.CreateJSONFFIEngine function not found in native library!");
            Log.e(TAG, "Make sure libtvm4j_runtime_packed.so is loaded and contains MLC LLM symbols.");
            throw new RuntimeException("MLC LLM native function not found. Library may be incompatible.");
        }
        
        Log.i(TAG, "CreateJSONFFIEngine function found, invoking...");
        jsonFFIEngine = createFunc.invoke().asModule();
        
        if (jsonFFIEngine == null) {
            Log.e(TAG, "Failed to create JSON FFI Engine module");
            throw new RuntimeException("Failed to create MLC LLM engine module.");
        }
        
        Log.i(TAG, "JSON FFI Engine module created, getting functions...");
        initBackgroundEngineFunc = jsonFFIEngine.getFunction("init_background_engine");
        reloadFunc = jsonFFIEngine.getFunction("reload");
        unloadFunc = jsonFFIEngine.getFunction("unload");
        resetFunc = jsonFFIEngine.getFunction("reset");
        chatCompletionFunc = jsonFFIEngine.getFunction("chat_completion");
        abortFunc = jsonFFIEngine.getFunction("abort");
        getLastErrorFunc = jsonFFIEngine.getFunction("get_last_error");
        runBackgroundLoopFunc = jsonFFIEngine.getFunction("run_background_loop");
        runBackgroundStreamBackLoopFunc = jsonFFIEngine.getFunction("run_background_stream_back_loop");
        exitBackgroundLoopFunc = jsonFFIEngine.getFunction("exit_background_loop");
        
        Log.i(TAG, "JSONFFIEngine created successfully");
    }

    public void initBackgroundEngine(KotlinFunction callback) {
        // Try Vulkan backend - OpenCL not available on Samsung S25 Ultra
        // Adreno 830 GPU supports Vulkan 1.3
        Device device = Device.vulkan();
        Log.i(TAG, "Using Vulkan device: type=" + device.deviceType + ", id=" + device.deviceId);

        requestStreamCallback = Function.convertFunc(new Function.Callback() {
            @Override
            public Object invoke(TVMValue... args) {
                // Upstream MLC-LLM returns primitive int 1
                try {
                    final String chatCompletionStreamResponsesJSONStr = args[0].asString();
                    callback.invoke(chatCompletionStreamResponsesJSONStr);
                    return 1;
                } catch (Throwable t) {
                    Log.e(TAG, "Stream callback error: " + t.getMessage());
                    return 1;
                }
            }
        });

        initBackgroundEngineFunc.pushArg(device.deviceType).pushArg(device.deviceId).pushArg(requestStreamCallback)
                .invoke();
    }

    public void reload(String engineConfigJSONStr) {
        reloadFunc.pushArg(engineConfigJSONStr).invoke();
    }

    public void chatCompletion(String requestJSONStr, String requestId) {
        chatCompletionFunc.pushArg(requestJSONStr).pushArg(requestId).invoke();
    }

    public void runBackgroundLoop() {
        runBackgroundLoopFunc.invoke();
    }

    public void runBackgroundStreamBackLoop() {
        runBackgroundStreamBackLoopFunc.invoke();
    }

    public void exitBackgroundLoop() {
        exitBackgroundLoopFunc.invoke();
    }

    public void unload() {
        unloadFunc.invoke();
    }

    public interface KotlinFunction {
        void invoke(String arg);
    }

    public void reset() {
        resetFunc.invoke();
    }

}
