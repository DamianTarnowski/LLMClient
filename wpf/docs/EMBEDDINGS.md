# 🧠 Embedding System - Technical Documentation

> **Version**: 1.0.0 | **Last Updated**: January 2026

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Rust Tokenizer FFI](#rust-tokenizer-ffi)
- [E5 Prefixes](#e5-prefixes)
- [Mean Pooling](#mean-pooling)
- [Models](#models)
- [Production Test Results](#production-test-results)
- [Troubleshooting](#troubleshooting)

---

## Overview

WPFLLM uses local embedding models from the **multilingual-E5** family to generate semantic vectors. The system has been optimized for **high-quality discrimination** between semantically similar and dissimilar texts, achieving a **20x improvement** over standard .NET tokenizers.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    LocalEmbeddingService                     │
│   - ONNX model initialization                               │
│   - E5 prefixes (query:/passage:)                           │
│   - Mean pooling + L2 normalization                         │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│                    RustTokenizer (FFI)                       │
│   - HuggingFace Tokenizers (Rust)                           │
│   - add_special_tokens = true                               │
│   - Automatic <s> and </s> tokens                           │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│                    ONNX Runtime                              │
│   - model.onnx (+ model.onnx_data for large)                │
│   - GPU/CPU inference                                        │
└─────────────────────────────────────────────────────────────┘
```

---

## Rust Tokenizer FFI

### The Problem

.NET tokenization libraries (Microsoft.ML.Tokenizers, Tokenizers.DotNet) do not properly support the `add_special_tokens=true` parameter, which is **critical** for E5/XLM-RoBERTa models. Without special tokens `<s>` (BOS) and `</s>` (EOS), embeddings have very poor discrimination.

### The Solution

We implemented a native tokenizer in **Rust** using the official HuggingFace `tokenizers` library:

```rust
// TokenizerRust/src/lib.rs
#[no_mangle]
pub extern "C" fn tokenizer_encode(text: *const c_char, out_ids: *mut c_int, max_len: usize) -> c_int {
    // ...
    // CRITICAL: add_special_tokens = true
    let encoding = tokenizer.encode(text_str, true)?;
    // ...
}
```

### Results

| Metric | Before (SentencePiece .NET) | After (Rust HuggingFace) |
|--------|---------------------------|----------------------|
| Semantically similar | 83.9% | 85.4% |
| Semantically different | 83.2% | 70.9% |
| **GAP (discrimination)** | **0.7%** ❌ | **14.5%** ✅ |

**20x better discrimination!**

---

## Model Files

Each E5 model requires the following files:

```
%LOCALAPPDATA%\WPFLLM\models\multilingual-e5-{size}\
├── model.onnx           # ONNX model
├── model.onnx_data      # Weights (only for large, ~2GB)
└── tokenizer.json       # HuggingFace tokenizer
```

---

## E5 Prefixes

E5 models require special prefixes for optimal performance:

| Text Type | Prefix | Example |
|-----------|--------|---------|
| User query | `query: ` | `query: How to buy a car?` |
| Document/passage | `passage: ` | `passage: Tips for buying a car...` |

```csharp
private string PrepareE5Text(string text, bool isQuery)
{
    var prefix = isQuery ? "query: " : "passage: ";
    if (text.StartsWith("query:") || text.StartsWith("passage:"))
        return text;
    return prefix + text;
}
```

## Mean Pooling

E5 wymaga **mean pooling** (NIE CLS pooling):

```csharp
private static float[] MeanPooling(Tensor<float> lastHiddenState, long[] attentionMask)
{
    var embedding = new float[hiddenSize];
    var sumMask = 0f;
    
    for (int i = 0; i < seqLen; i++)
    {
        if (attentionMask[i] == 1)
        {
            for (int j = 0; j < hiddenSize; j++)
                embedding[j] += lastHiddenState[0, i, j];
            sumMask += 1f;
        }
    }
    
    // Średnia po wszystkich tokenach
    for (int i = 0; i < hiddenSize; i++)
        embedding[i] /= sumMask;
        
    return embedding;
}
```

## Normalizacja L2

Po mean pooling stosujemy normalizację L2 (krytyczne dla cosine similarity):

```csharp
private static float[] L2Normalize(float[] vector)
{
    var norm = (float)Math.Sqrt(vector.Sum(x => x * x));
    if (norm < 1e-12f) return vector;
    return vector.Select(x => x / norm).ToArray();
}
```

---

## Models

| Model | Dimensions | Size | Quality | RAM | Format |
|-------|------------|------|---------|-----|--------|
| multilingual-e5-small | 384 | ~470MB | ★★★☆☆ | 1-2 GB | query:/passage: |
| multilingual-e5-base | 768 | ~1.1GB | ★★★★☆ | 2-3 GB | query:/passage: |
| multilingual-e5-large | 1024 | ~2.2GB | ★★★★★ | 4-6 GB | query:/passage: |
| **multilingual-e5-large-instruct** ⭐ | 1024 | ~2.2GB | ★★★★★+ | 4-6 GB | Instruct:/Query: |

All models support **100+ languages** including Polish, English, German, French, and more.

### ⭐ Recommended: multilingual-e5-large-instruct

Best model for semantic search and RAG:

```
Query format:    Instruct: {task description}\nQuery: {query}
Document format: {text without prefix}
```

---

## Production Test Results

Tests conducted January 2026 with 22 test pairs (PL/EN/Cross-language):

### Summary

| Category | Average | Min | Max |
|----------|---------|-----|-----|
| 🟢 VERY SIMILAR | **87.4%** | 82.5% | 90.5% |
| 🟡 SIMILAR | **79.5%** | 74.8% | 85.2% |
| 🔴 DIFFERENT | **73.1%** | 69.6% | 77.1% |

**GAP (discrimination):**
- Very Similar vs Different: **14.4%** ✅
- Similar vs Different: **6.5%** ✅

### Results by Language

| Language | Very Similar | Similar | Different | GAP |
|----------|--------------|---------|-----------|-----|
| 🇵🇱 Polish | 88.7% | 82.8% | 76.1% | **12.6%** |
| 🇬🇧 English | 88.7% | 77.8% | 70.3% | **18.4%** |
| 🌐 Cross-lang | 83.5% | 74.8% | 72.4% | **11.1%** |

### Production Assessment

✅ **APPROVED FOR PRODUCTION**

- GAP 14.4% is sufficient for Top-K RAG retrieval
- Cross-language works (PL query → EN document)
- Clear separation of 3 similarity levels

---

## Building the Rust Tokenizer

```bash
cd TokenizerRust
cargo build --release
```

Output file: `target/release/hf_tokenizer.dll` (~3.7MB)

---

## Embedding Checklist

- [x] Tokenizer from `tokenizer.json` (not sentencepiece.bpe.model)
- [x] `add_special_tokens = true` (tokens `<s>` and `</s>`)
- [x] Prefixes `query:` / `passage:`
- [x] Mean pooling (not CLS)
- [x] L2 normalization
- [x] Max sequence length: 256 (recommended), 512 (max)

---

## Troubleshooting

### Poor Discrimination (GAP < 5%)
1. Check if tokenizer adds special tokens (ID 0 at start, ID 2 at end)
2. Ensure you're using `query:`/`passage:` prefixes
3. Verify L2 normalization

### DllNotFoundException: hf_tokenizer
1. Copy `hf_tokenizer.dll` to application directory
2. Or add to project with `CopyToOutputDirectory`

### Slow Inference
1. Use a smaller model (e5-small)
2. Reduce `MaxSequenceLength`
3. Consider GPU acceleration (ONNX Runtime CUDA)

---

## Related Documentation

- [Architecture](ARCHITECTURE.md) - System design and service layer
- [Contributing](../CONTRIBUTING.md) - How to contribute
- [Changelog](../CHANGELOG.md) - Version history
