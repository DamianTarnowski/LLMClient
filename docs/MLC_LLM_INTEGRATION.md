# MLC LLM Integration Documentation

## Overview

MLC LLM (Machine Learning Compilation for Large Language Models) enables GPU-accelerated local model inference on mobile devices using OpenCL (Android) and Metal (iOS). This document describes the integration status, architecture, and next steps.

## Current Status: Android

### Completed

| Component | Status | Description |
|-----------|--------|-------------|
| Native Library | ✅ Done | `libtvm4j_runtime_packed.so` (103 MB) extracted from official MLC Chat APK |
| TVM Java Bindings | ✅ Done | 21 Java files from Apache TVM repo |
| MLC LLM FFI | ✅ Done | JSONFFIEngine.java from MLC LLM repo |
| SimpleMlcEngine | ✅ Done | Custom Java wrapper for .NET MAUI integration |
| C# Bridge | ✅ Done | MlcLlmBridge.cs with JNI reflection calls |
| Build Config | ✅ Done | csproj configured for Java compilation |
| APK Build | ✅ Done | 100 MB APK with all components |
| Test Model | ✅ Done | Qwen2.5-1.5B downloaded (838 MB) |

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    .NET MAUI (C#)                           │
├─────────────────────────────────────────────────────────────┤
│  MlcLlmLocalModelService                                    │
│    └── MlcLlmBridge.cs (JNI reflection)                     │
├─────────────────────────────────────────────────────────────┤
│                    Java Layer                               │
├─────────────────────────────────────────────────────────────┤
│  SimpleMlcEngine.java                                       │
│    └── JSONFFIEngine.java                                   │
│          └── org.apache.tvm.* (Function, Module, Device)    │
├─────────────────────────────────────────────────────────────┤
│                  Native Library                             │
├─────────────────────────────────────────────────────────────┤
│  libtvm4j_runtime_packed.so (103 MB)                        │
│    ├── TVM Runtime                                          │
│    ├── Precompiled GPU Kernels (OpenCL)                     │
│    └── JNI bindings for org.apache.tvm.LibInfo              │
└─────────────────────────────────────────────────────────────┘
```

### Supported Models (Precompiled in APK)

Only these 5 models have GPU kernels precompiled in `libtvm4j_runtime_packed.so`:

| Model | model_lib hash | VRAM |
|-------|---------------|------|
| Qwen2.5-1.5B-Instruct-q4f16_1-MLC | `qwen2_q4f16_1_2e221f430380225c03990ad24c3d030e` | ~4 GB |
| Phi-3.5-mini-instruct-q4f16_0-MLC | `phi3_q4f16_0_5fe42298399a05eb2a1878fdc1c8c115` | ~4 GB |
| gemma-2-2b-it-q4f16_1-MLC | `gemma2_q4f16_1_5cc7dbd3ae3d1040984d9720b2d7b7d4` | ~3 GB |
| Llama-3.2-3B-Instruct-q4f16_0-MLC | `llama_q4f16_0_2d32572d8a4ab2af20a1f587ef6c8c63` | ~4.7 GB |
| Mistral-7B-Instruct-v0.3-q4f16_1-MLC | `mistral_q4f16_1_c2cba77a6def4dd52f7e20b5d8576ab5` | ~4 GB |

### File Structure

```
LLMClient/
├── Platforms/
│   └── Android/
│       ├── libs/
│       │   └── arm64-v8a/
│       │       └── libtvm4j_runtime_packed.so  (103 MB)
│       ├── Java/
│       │   ├── org/apache/tvm/
│       │   │   ├── API.java
│       │   │   ├── APIInternal.java
│       │   │   ├── Base.java
│       │   │   ├── Device.java
│       │   │   ├── Function.java
│       │   │   ├── LibInfo.java
│       │   │   ├── Module.java
│       │   │   ├── NativeLibraryLoader.java
│       │   │   ├── Tensor.java
│       │   │   ├── TensorBase.java
│       │   │   ├── TVMObject.java
│       │   │   ├── TVMType.java
│       │   │   ├── TVMValue.java
│       │   │   ├── TVMValue*.java (6 files)
│       │   │   ├── TypeIndex.java
│       │   │   └── rpc/
│       │   │       └── RPC.java
│       │   └── ai/mlc/mlcllm/
│       │       ├── JSONFFIEngine.java
│       │       ├── OpenAIProtocol.kt
│       │       └── SimpleMlcEngine.java
│       └── MlcLlm/
│           └── MlcLlmBridge.cs
├── Services/
│   ├── MlcLlmLocalModelService.cs
│   ├── MlcModelDownloadService.cs
│   └── SwitchableLocalModelService.cs
├── Models/
│   └── MlcModelCatalog.cs
└── ViewModels/
    └── MlcModelSelectorViewModel.cs
```

### Key Classes

#### MlcLlmBridge.cs (C#)
```csharp
// Initialize engine with model
await bridge.InitializeAsync(modelPath, modelLib);

// Generate response
string response = await bridge.GenerateAsync(prompt, maxTokens, temperature);

// Streaming generation
await bridge.GenerateStreamingAsync(prompt, maxTokens, temperature,
    token => Console.Write(token));
```

#### SimpleMlcEngine.java
```java
// Initialize TVM FFI engine
boolean success = engine.initialize();

// Load model
engine.loadModel(modelPath, modelLib);

// Generate (blocking)
String response = engine.generate(prompt, maxTokens, temperature);

// Generate with streaming callback
engine.generateStreaming(prompt, maxTokens, temperature, callback);
```

---

## What Still Needs to Be Done

### Android

1. **Runtime Testing on Device**
   - Install APK on Samsung S25 Ultra
   - Copy Qwen2.5-1.5B model to device storage
   - Test TVM library loading
   - Test model initialization
   - Test inference with GPU acceleration
   - Measure performance (tokens/sec)

2. **Potential Issues to Debug**
   - TVM library may fail to load (missing OpenCL drivers)
   - Java class loading via reflection may need adjustments
   - Model path handling on Android file system
   - Memory management for large models

3. **Optimizations**
   - Add proper error handling and recovery
   - Implement conversation history management
   - Add model switching without app restart
   - Performance tuning for specific devices

### iOS

iOS integration requires a different approach because:
- Uses Metal instead of OpenCL for GPU
- Uses Swift/ObjC instead of Java
- Different native library format (.framework or .dylib)

---

## iOS Implementation Guide

### Step 1: Get MLC LLM iOS Library

**Option A: Build from source** (recommended for customization)
```bash
# Clone MLC LLM
git clone https://github.com/mlc-ai/mlc-llm.git
cd mlc-llm

# Install dependencies
pip install mlc-llm mlc-ai-nightly

# Build iOS library
cd ios
./prepare_libs.sh
```

**Option B: Extract from MLCChat iOS app**
1. Download MLCChat from App Store or TestFlight
2. Use tools like `ipatool` to get IPA
3. Extract `.framework` files from IPA

### Step 2: Add to MAUI Project

Create iOS native library structure:
```
LLMClient/
├── Platforms/
│   └── iOS/
│       ├── Frameworks/
│       │   └── MLCSwift.framework/
│       └── MlcLlm/
│           └── MlcLlmBridge.cs
```

Update csproj:
```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0-ios'">
  <NativeReference Include="Platforms\iOS\Frameworks\MLCSwift.framework">
    <Kind>Framework</Kind>
    <SmartLink>False</SmartLink>
  </NativeReference>
</ItemGroup>
```

### Step 3: Create iOS Bridge

```csharp
#if IOS
using Foundation;
using ObjCRuntime;

namespace LLMClient.Platforms.iOS.MlcLlm
{
    public class MlcLlmBridge : IDisposable
    {
        private NSObject? _engine;

        public async Task<bool> InitializeAsync(string modelPath, string modelLib)
        {
            // Use ObjC runtime to create MLCEngine instance
            var engineClass = Class.GetHandle("MLCEngine");
            // ... implementation
        }

        public async Task<string> GenerateAsync(string prompt, int maxTokens, double temperature)
        {
            // Call MLCEngine methods via ObjC runtime
        }
    }
}
#endif
```

### Step 4: iOS-Specific Considerations

1. **Metal GPU**: iOS uses Metal, kernels are precompiled differently
2. **Model Storage**: Use app's Documents directory
3. **Memory**: iOS has stricter memory limits than Android
4. **Background**: iOS may kill app when backgrounded during inference

---

## Testing on Samsung S25 Ultra

### Prerequisites

1. **Enable Developer Mode on S25 Ultra**
   - Settings → About phone → Software information
   - Tap "Build number" 7 times
   - Go back to Settings → Developer options
   - Enable "USB debugging"

2. **Connect to PC**
   - Use USB-C cable
   - Select "File transfer / Android Auto" on phone
   - PC should recognize device

### Step-by-Step Instructions

#### 1. Install APK

**Option A: Via ADB**
```bash
# Check device is connected
adb devices

# Install APK
adb install -r "C:\Users\hdtdt\source\repos\LLMClientFromGithub\LLMClient\bin\Release\net10.0-android\com.companyname.llmclient-Signed.apk"
```

**Option B: Copy and install manually**
1. Copy APK to phone via USB
2. Open file manager on phone
3. Tap APK file
4. Allow installation from unknown sources if prompted
5. Install

#### 2. Copy Model to Device

The model needs to be in a location accessible by the app.

**Option A: Via ADB (recommended)**
```bash
# Create model directory
adb shell mkdir -p /sdcard/Android/data/com.companyname.llmclient/files/Models/Qwen2.5-1.5B

# Copy model files (run from model directory)
cd "C:\Users\hdtdt\AppData\Local\MlcModelTest\Models\Qwen2.5-1.5B"
adb push . /sdcard/Android/data/com.companyname.llmclient/files/Models/Qwen2.5-1.5B/
```

**Option B: Via USB file transfer**
1. Connect phone to PC
2. Navigate to: `Internal storage/Android/data/com.companyname.llmclient/files/`
3. Create folder: `Models/Qwen2.5-1.5B/`
4. Copy all files from `C:\Users\hdtdt\AppData\Local\MlcModelTest\Models\Qwen2.5-1.5B\`

**Model files to copy** (838 MB total):
```
mlc-chat-config.json
ndarray-cache.json
tokenizer.json
tokenizer_config.json
params_shard_0.bin ... params_shard_29.bin (30 files)
```

#### 3. Run the App

1. Open LLMClient app on S25 Ultra
2. Go to "Model Manager" tab (if available)
3. Select MLC engine in settings
4. Select Qwen2.5-1.5B model
5. Try generating a response

#### 4. Check Logs for Debugging

```bash
# View app logs in real-time
adb logcat -s "MlcLlmBridge:*" "SimpleMlcEngine:*" "tvm4j:*"

# Or filter all app logs
adb logcat | grep -E "(MlcLlm|SimpleMlc|tvm|TVM)"
```

### Expected Log Output (Success)

```
I/MlcLlmBridge: Loading libtvm4j_runtime_packed.so...
I/MlcLlmBridge: Native library loaded successfully!
I/SimpleMlcEngine: SimpleMlcEngine created
I/SimpleMlcEngine: Initializing JSONFFIEngine...
I/SimpleMlcEngine: Engine initialized successfully
I/MlcLlmBridge: Loading model: /storage/.../Qwen2.5-1.5B with lib: qwen2_q4f16_1_...
I/SimpleMlcEngine: Model loaded successfully
```

### Potential Errors and Solutions

| Error | Cause | Solution |
|-------|-------|----------|
| `UnsatisfiedLinkError: libtvm4j_runtime_packed.so` | Library not in APK or wrong ABI | Rebuild APK, check arm64-v8a folder |
| `ClassNotFoundException: ai.mlc.mlcllm.SimpleMlcEngine` | Java classes not compiled | Check csproj AndroidJavaSource entries |
| `Model not supported` | Model not in precompiled list | Use one of 5 supported models |
| `OpenCL not available` | GPU drivers issue | Check if phone supports OpenCL |
| `Out of memory` | Model too large | Try smaller model or close other apps |

### Samsung S25 Ultra Specs (Relevant)

- **SoC**: Snapdragon 8 Elite (or Exynos 2500 in some regions)
- **RAM**: 12 GB
- **GPU**: Adreno 830 (Snapdragon) - supports OpenCL 3.0
- **Storage**: 256 GB+

The S25 Ultra should handle Qwen2.5-1.5B easily with ~4 GB VRAM requirement.

---

## Quick Reference Commands

```bash
# Build APK
dotnet build LLMClient/LLMClient.csproj -c Release -f net10.0-android

# Install on device
adb install -r "LLMClient/bin/Release/net10.0-android/com.companyname.llmclient-Signed.apk"

# Push model
adb push "C:\Users\hdtdt\AppData\Local\MlcModelTest\Models\Qwen2.5-1.5B" /sdcard/Android/data/com.companyname.llmclient/files/Models/

# View logs
adb logcat -s "MlcLlmBridge:*" "SimpleMlcEngine:*"

# Check if library loaded
adb logcat | grep "tvm4j_runtime_packed"
```

---

## References

- [MLC LLM Documentation](https://llm.mlc.ai/docs/)
- [MLC LLM Android Guide](https://llm.mlc.ai/docs/deploy/android.html)
- [Apache TVM Java](https://github.com/apache/tvm/tree/main/jvm)
- [MLC LLM GitHub](https://github.com/mlc-ai/mlc-llm)
- [Binary Releases](https://github.com/mlc-ai/binary-mlc-llm-libs/releases)
