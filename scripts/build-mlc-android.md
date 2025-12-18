# Budowanie bibliotek MLC LLM dla Android

Ten dokument opisuje jak zbudować natywne biblioteki TVM z kernelami GPU dla MLC LLM.

## Wymagania

1. **Python 3.10+** z pip
2. **Android NDK r26b+** (zalecane r27)
3. **CMake 3.24+**
4. **Ninja build**
5. **MLC LLM** i **TVM** z source

## Instalacja MLC LLM

```bash
# Klonuj repozytorium
git clone --recursive https://github.com/mlc-ai/mlc-llm.git
cd mlc-llm

# Utwórz środowisko conda/venv
conda create -n mlc python=3.11
conda activate mlc

# Zainstaluj zależności
pip install -e ".[all]"

# Zbuduj TVM dla Androida
cd 3rdparty/tvm
mkdir build && cd build

# Konfiguracja dla Android (OpenCL)
cmake .. \
    -DCMAKE_BUILD_TYPE=Release \
    -DUSE_OPENCL=ON \
    -DUSE_LLVM=OFF \
    -DANDROID_ABI=arm64-v8a \
    -DANDROID_PLATFORM=android-26 \
    -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK/build/cmake/android.toolchain.cmake

ninja
```

## Kompilacja modeli

### Lista modeli do skompilowania

| Model | HuggingFace ID | Model Lib |
|-------|----------------|-----------|
| Qwen2.5-0.5B | mlc-ai/Qwen2.5-0.5B-Instruct-q4f16_1-MLC | qwen2_q4f16_1 |
| Qwen2.5-1.5B | mlc-ai/Qwen2.5-1.5B-Instruct-q4f16_1-MLC | qwen2_q4f16_1 |
| Llama-3.2-1B | mlc-ai/Llama-3.2-1B-Instruct-q4f16_1-MLC | llama_q4f16_1 |
| Llama-3.2-3B | mlc-ai/Llama-3.2-3B-Instruct-q4f16_1-MLC | llama_q4f16_1 |
| Phi-3-mini | mlc-ai/Phi-3-mini-4k-instruct-q4f16_1-MLC | phi3_q4f16_1 |
| Gemma-2-2B | mlc-ai/gemma-2-2b-it-q4f16_1-MLC | gemma2_q4f16_1 |

### Kompilacja dla Android (OpenCL GPU)

```bash
# Dla każdego modelu:
mlc_llm compile \
    --model mlc-ai/Qwen2.5-1.5B-Instruct-q4f16_1-MLC \
    --device android \
    --output libs/

# Lub użyj bundled compilation dla wielu modeli:
mlc_llm package \
    --model mlc-ai/Qwen2.5-0.5B-Instruct-q4f16_1-MLC \
    --model mlc-ai/Qwen2.5-1.5B-Instruct-q4f16_1-MLC \
    --model mlc-ai/Llama-3.2-1B-Instruct-q4f16_1-MLC \
    --model mlc-ai/Phi-3-mini-4k-instruct-q4f16_1-MLC \
    --model mlc-ai/gemma-2-2b-it-q4f16_1-MLC \
    --device android \
    --output bundle/
```

## Alternatywa: Użycie gotowych bibliotek z MLC releases

MLC LLM publikuje gotowe APK demo z bibliotekami. Można je wyekstrahować:

```bash
# Pobierz APK z https://github.com/mlc-ai/mlc-llm/releases
# Rozpakuj jako ZIP
unzip MLCChat.apk -d mlc_extracted

# Biblioteki są w:
# mlc_extracted/lib/arm64-v8a/libtvm4j_runtime_packed.so
```

## Integracja z projektem MAUI

1. Skopiuj `libtvm4j_runtime_packed.so` do:
   ```
   LLMClient/Platforms/Android/libs/arm64-v8a/
   ```

2. Upewnij się że w `.csproj` jest:
   ```xml
   <AndroidNativeLibrary Include="Platforms\Android\libs\arm64-v8a\libtvm4j_runtime_packed.so" Abi="arm64-v8a" />
   ```

3. Zaktualizuj `ModelLibMappings` w `MlcLlmBridge.cs` aby pasowały do skompilowanych kerneli.

## Weryfikacja

Sprawdź jakie model_lib są w bibliotece:

```bash
# Na Linuxie/Mac
nm -D libtvm4j_runtime_packed.so | grep -i "model_lib"

# Lub użyj mlc_llm:
mlc_llm chat --model-lib-path ./libtvm4j_runtime_packed.so --help
```

## Troubleshooting

### "Java engine not available"
- Sprawdź czy biblioteka .so jest prawidłowo załadowana (logcat)
- Upewnij się że klasy Java są skompilowane (AndroidJavaSource w csproj)
- Sprawdź czy model_lib mapping jest poprawny

### "Model not supported"
- Model lib ID musi dokładnie odpowiadać temu co jest skompilowane w .so
- Sprawdź mlc-chat-config.json w pobranym modelu dla prawidłowego model_lib

### OpenCL errors
- Nie wszystkie urządzenia wspierają OpenCL
- Starsze telefony mogą wymagać Vulkan backend zamiast OpenCL
