# MLC-LLM Integration Issues & Resolution Plan

## Status: ⚠️ TEMPORARILY DISABLED (December 2024)

MLC-LLM integration has been temporarily disabled in the UI due to unresolved compatibility issues with modern Android devices.

---

## 🔴 Current Problems

### 1. Samsung S25 Ultra (Adreno 830) - OpenCL Not Available

**Symptoms:**
```
java.lang.UnsatisfiedLinkError: dlopen failed: library "libOpenCL.so" not found
```

**Root Cause:**
- Samsung removed OpenCL support from Galaxy S25 series
- Adreno 830 GPU technically supports OpenCL but Samsung disabled it at OS level
- Official MLC Chat app also fails on this device

**Affected Devices:**
- Samsung Galaxy S25 series (S25, S25+, S25 Ultra)
- Potentially other 2024+ Samsung flagships

---

### 2. Vulkan Backend - Build System Issues

**Symptoms:**
```
Device API vulkan is not enabled
```

**Root Cause:**
- TVM's `FindVulkan.cmake` doesn't properly propagate `Vulkan_FOUND` variable for Android
- Android NDK r27+ moved Vulkan headers to new location
- Build completes but Vulkan runtime sources are not compiled

**Technical Details:**

```cmake
# Problem in FindVulkan.cmake (line 53-60):
if(CMAKE_SYSTEM_NAME STREQUAL "Android")
    set(VULKAN_NDK_SRC ${CMAKE_ANDROID_NDK}/sources/third_party/vulkan/src)
    set(Vulkan_INCLUDE_DIRS ${VULKAN_NDK_SRC}/include)
    set(Vulkan_FOUND TRUE)  # <-- NOT propagated to parent scope!
    return()
endif()
```

**Fix Required:**
```cmake
set(Vulkan_FOUND TRUE CACHE BOOL "" FORCE)
set(Vulkan_INCLUDE_DIRS "${VULKAN_NDK_SRC}/include" CACHE PATH "" FORCE)
```

**NDK r27+ Vulkan Location:**
```
Old: ${NDK}/sources/third_party/vulkan/src/include/
New: ${NDK}/toolchains/llvm/prebuilt/linux-x86_64/sysroot/usr/include/vulkan/
```

---

### 3. TVM FFI API Version Mismatch

**Symptoms:**
```
No implementation found for int org.apache.tvm.LibInfo.tvmFuncGetGlobal
```

**Root Cause:**
- New TVM uses `tvmFFI*` API (e.g., `tvmFFIFunctionGetGlobal`)
- Old Java bindings use `tvmFunc*` API (e.g., `tvmFuncGetGlobal`)

**Resolution Applied:**
Updated all Java files to new TVM FFI API:
- `LibInfo.java` - Native method declarations
- `TypeIndex.java` - Type codes (kTVMFFIModule = 73, not 9)
- `TVMObject.java` - Use `tvmFFIObjectFree` instead of per-type free
- `Module.java` - Use `ffi.ModuleGetFunction` API
- `TensorBase.java` - Use unified object free

---

### 4. Model Library Name Mismatch

**Symptoms:**
```
Cannot find system lib with phi3_q4f16_0_5fe42298399a05eb2a1878fdc1c8c115
```

**Root Cause:**
- Model compiled with one hash, library expects different hash
- Hash is based on model config + quantization + compilation settings

**Resolution:**
Update `ModelLibMappings` in `MlcLlmBridge.cs` when rebuilding library:
```csharp
{ "Phi-3.5-mini", "phi3_q4f16_0_7e3edeb1a479d33c19bf5d3a2077d0b5" }
```

---

## 🟡 Partial Solutions Achieved

### What Works:
1. ✅ TVM runtime loads successfully
2. ✅ JSONFFIEngine initializes
3. ✅ Device.vulkan() is recognized (type=7)
4. ✅ Java ↔ Native JNI communication works
5. ✅ Model library name mapping works

### What Doesn't Work:
1. ❌ OpenCL backend (Samsung disabled it)
2. ❌ Vulkan backend (CMake doesn't compile Vulkan sources)
3. ❌ Model loading fails due to missing GPU runtime

---

## 🟢 Resolution Plan

### Option A: Fix Vulkan Build (Recommended)

**Steps:**
1. Patch `FindVulkan.cmake` to use CACHE FORCE
2. Create symlink for NDK r27+ Vulkan headers
3. Rebuild with Vulkan runtime sources included
4. Test on S25 Ultra

**Commands:**
```bash
# Create symlink for old NDK path
mkdir -p ~/android-ndk-r27/sources/third_party/vulkan/src
ln -sf ~/android-ndk-r27/toolchains/llvm/prebuilt/linux-x86_64/sysroot/usr/include \
       ~/android-ndk-r27/sources/third_party/vulkan/src/include

# Patch FindVulkan.cmake
sed -i 's/set(Vulkan_FOUND TRUE)/set(Vulkan_FOUND TRUE CACHE BOOL "" FORCE)/' \
    ~/mlc-llm-vulkan/3rdparty/tvm/cmake/utils/FindVulkan.cmake

# Rebuild
python3 -m mlc_llm package --package-config mlc-package-config.json ...
```

**Estimated Time:** 2-4 hours

---

### Option B: Use llama.cpp with Vulkan

**Advantages:**
- Better Vulkan support for mobile
- Active community
- Simpler build process

**Disadvantages:**
- Different API, requires new integration
- May need model conversion

**Estimated Time:** 1-2 days

---

### Option C: Wait for MLC-LLM Updates

**Track Issues:**
- https://github.com/mlc-ai/mlc-llm/issues
- Samsung OpenCL removal is known issue

---

## 📁 Files Modified During Investigation

### Java (TVM FFI API Update):
- `Platforms/Android/Java/org/apache/tvm/LibInfo.java`
- `Platforms/Android/Java/org/apache/tvm/TypeIndex.java`
- `Platforms/Android/Java/org/apache/tvm/TVMObject.java`
- `Platforms/Android/Java/org/apache/tvm/TensorBase.java`
- `Platforms/Android/Java/org/apache/tvm/Module.java`
- `Platforms/Android/Java/org/apache/tvm/Function.java`

### C# (Model Library Mapping):
- `Platforms/Android/MlcLlm/MlcLlmBridge.cs`

### Native Library:
- `Platforms/Android/Libs/arm64-v8a/libtvm4j_runtime_packed.so` (117MB with model)

---

## 🔧 Build Environment Used

```
OS: WSL2 Ubuntu on Windows 11
Android NDK: r27
CMake: 3.22+
Rust: 1.92.0
Python: 3.10
MLC-LLM: main branch (Dec 2024)
TVM: 0.23.dev0
```

---

## 📝 Notes

1. **Official MLC Chat also fails on S25 Ultra** - this is not just our implementation
2. **Vulkan 1.3 is supported by Adreno 830** - the issue is build system, not hardware
3. **CPU backend works** but is too slow for practical use
4. **ONNX GenAI** remains as alternative local model solution

---

## 🔄 Last Updated
December 16, 2024
