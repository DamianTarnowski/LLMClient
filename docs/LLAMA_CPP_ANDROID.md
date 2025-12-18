# LLamaSharp / llama.cpp Integration for Android

## Status: ✅ FULLY IMPLEMENTED (December 2024)

LLamaSharp (llama.cpp .NET wrapper) has been integrated for Android ARM64 as a replacement for MLC LLM which had OpenCL/Vulkan issues on Samsung S25 Ultra.

---

## 🟢 What Works

- **LLamaSharp 0.25.0** - Latest version with Qwen3, Gemma3, DeepSeek R1 support
- **Custom-built libllama.so** - Compiled from llama.cpp source for Android ARM64
- **CPU inference** - Optimized for mobile with reduced thread count and batch size
- **GGUF models** - Standard quantized model format (Q4_K_M, Q8_0, etc.)
- **Multi-model support** - UI to select, download, and switch between models
- **GgufModelManagerPage** - Dedicated page in Shell navigation for model management

---

## 📁 Native Libraries

Located in `Platforms/Android/Libs/arm64-v8a/`:

| Library | Size | Description |
|---------|------|-------------|
| `libllama.so` | ~28 MB | Main llama.cpp library |
| `libggml.so` | ~0.6 MB | GGML tensor library |
| `libggml-base.so` | ~5 MB | GGML base operations |
| `libggml-cpu.so` | ~4 MB | CPU backend |
| `libc++_shared.so` | ~1.8 MB | C++ runtime |

---

## 🔧 Build Instructions

### Prerequisites (WSL Ubuntu)

```bash
# Android NDK r27
export ANDROID_NDK=~/android-ndk-r27

# CMake, Ninja
sudo apt install cmake ninja-build
```

### Building libllama.so

```bash
# Clone llama.cpp
git clone --depth 1 https://github.com/ggml-org/llama.cpp ~/llama-cpp-android
cd ~/llama-cpp-android

# Configure for Android ARM64
mkdir build-android && cd build-android
cmake .. \
    -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK/build/cmake/android.toolchain.cmake \
    -DANDROID_ABI=arm64-v8a \
    -DANDROID_PLATFORM=android-28 \
    -DCMAKE_BUILD_TYPE=Release \
    -DBUILD_SHARED_LIBS=ON \
    -DGGML_OPENMP=OFF \
    -DLLAMA_CURL=OFF \
    -GNinja

# Build
ninja -j4 llama

# Output: build-android/bin/libllama.so + libggml*.so
```

### Copy to Project

```bash
cp ~/llama-cpp-android/build-android/bin/lib*.so \
   /mnt/c/Users/.../LLMClient/Platforms/Android/Libs/arm64-v8a/
```

---

## 📦 NuGet Configuration

```xml
<!-- LLMClient.csproj -->

<!-- LLamaSharp main package (Windows + Android) -->
<PackageReference Include="LLamaSharp" Version="0.25.0" 
    Condition="$(TargetFramework.Contains('windows')) OR $(TargetFramework.Contains('android'))" />

<!-- Windows backends -->
<PackageReference Include="LLamaSharp.Backend.Cpu" Version="0.25.0" 
    Condition="$(TargetFramework.Contains('windows'))" />

<!-- Android: Uses custom-built libllama.so (no NuGet backend) -->
<AndroidNativeLibrary Include="Platforms\Android\libs\arm64-v8a\libllama.so" Abi="arm64-v8a" />
<AndroidNativeLibrary Include="Platforms\Android\libs\arm64-v8a\libggml.so" Abi="arm64-v8a" />
<AndroidNativeLibrary Include="Platforms\Android\libs\arm64-v8a\libggml-base.so" Abi="arm64-v8a" />
<AndroidNativeLibrary Include="Platforms\Android\libs\arm64-v8a\libggml-cpu.so" Abi="arm64-v8a" />
```

---

## 🔧 Code Configuration

### NativeLibraryConfig (Android)

```csharp
// LlamaSharpLocalModelService.cs constructor
#if ANDROID
var nativeLibDir = Android.App.Application.Context.ApplicationInfo?.NativeLibraryDir;
if (!string.IsNullOrEmpty(nativeLibDir))
{
    var llamaPath = Path.Combine(nativeLibDir, "libllama.so");
    if (File.Exists(llamaPath))
    {
        NativeLibraryConfig.LLama.WithLibrary(llamaPath);
    }
}
#endif
```

### Model Parameters (Android-optimized)

```csharp
// Conservative settings for mobile
var parameters = new ModelParams(modelPath)
{
    ContextSize = 2048,        // Reduced for RAM
    BatchSize = 256,           // Smaller batches
    Threads = 4,               // Fewer threads to avoid overheating
    UseMemorymap = true,
    UseMemoryLock = false,     // Not supported on Android
    GpuLayerCount = 0          // CPU only (no GPU backend yet)
};
```

---

## 📱 Model Storage

Models are stored in external storage for easy management:

```
/storage/emulated/0/Download/LLMClient/Models/gguf/
├── gemma-3-1b-it-Q4_K_M.gguf
├── gemma-3-1b-it-Q4_K_M.gguf.complete
├── Qwen3-4B-Instruct-2507-Q4_K_M.gguf
└── Qwen3-4B-Instruct-2507-Q4_K_M.gguf.complete
```

---

## 📋 Available Models (Built-in Catalog)

| Model | Size | Description |
|-------|------|-------------|
| **Gemma 3 1B Instruct** ⭐ | ~700 MB | Recommended for mobile - fast, good quality |
| Gemma 3 4B Instruct | ~2.8 GB | Larger Gemma with multimodal support |
| Qwen3 1.7B Instruct | ~1.1 GB | Compact Qwen with good quality |
| Qwen3 4B Instruct | ~2.7 GB | Medium Qwen with latest improvements |
| Phi-3 Mini 3.8B | ~2.3 GB | Microsoft's compact model |

⭐ = Recommended for mobile devices

---

## ⚡ Performance Notes

| Device | Model | Tokens/sec (est.) |
|--------|-------|-------------------|
| S25 Ultra (Snapdragon 8 Elite) | Qwen3-4B Q4_K_M | ~5-10 t/s |
| S25 Ultra | Smaller 1-2B model | ~15-25 t/s |

**Recommendations:**
- Use Q4_K_M or Q4_K_S quantization for best speed/quality balance
- Smaller models (1-3B) recommended for mobile
- Consider Q8_0 for better quality if RAM allows

---

## 🔄 Future Improvements

1. **Vulkan GPU Backend** - Build llama.cpp with Vulkan for GPU acceleration
2. **Smaller Models** - Integrate Qwen3-1.5B or Phi-3-mini for faster inference
3. **Model Selection UI** - Allow users to choose between downloaded models

---

## 📝 Related Files

- `Services/LlamaSharpLocalModelService.cs` - Main service implementation
- `Models/EngineSettings.cs` - Engine selection (LLamaSharp default on Android)
- `MauiProgram.cs` - DI registration
- `docs/MLC_LLM_ISSUES.md` - Why MLC was disabled

---

## 🔄 Last Updated
December 16, 2024
