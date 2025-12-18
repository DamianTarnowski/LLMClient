import Foundation
import Metal
import MetalPerformanceShaders

/// MLC LLM Engine wrapper for iOS
/// Provides high-performance GPU-accelerated LLM inference using Metal
@objc public class MlcLlmEngine: NSObject {

    private var enginePtr: UnsafeMutableRawPointer?
    private var isLoaded: Bool = false
    private var modelPath: String = ""
    private let queue = DispatchQueue(label: "com.llmclient.mlcllm", qos: .userInitiated)

    // Metal device for GPU acceleration
    private var metalDevice: MTLDevice?
    private var commandQueue: MTLCommandQueue?

    // Callbacks
    @objc public var onToken: ((String) -> Void)?
    @objc public var onComplete: ((String) -> Void)?
    @objc public var onError: ((String) -> Void)?
    @objc public var onProgress: ((Double, String) -> Void)?

    @objc public override init() {
        super.init()
        setupMetal()
    }

    private func setupMetal() {
        metalDevice = MTLCreateSystemDefaultDevice()
        if let device = metalDevice {
            commandQueue = device.makeCommandQueue()
            NSLog("[MlcLlmEngine] Metal GPU initialized: \(device.name)")
        } else {
            NSLog("[MlcLlmEngine] Metal not available, will use CPU")
        }
    }

    /// Initialize the engine with a model
    @objc public func initialize(modelId: String, modelPath: String) {
        self.modelPath = modelPath

        queue.async { [weak self] in
            guard let self = self else { return }

            do {
                let config = self.buildConfig()
                self.enginePtr = mlc_create_engine(modelPath, config)

                if self.enginePtr != nil {
                    self.isLoaded = true
                    NSLog("[MlcLlmEngine] Initialized with model: \(modelId)")
                } else {
                    self.onError?("Failed to create MLC engine")
                }
            } catch {
                NSLog("[MlcLlmEngine] Initialization error: \(error.localizedDescription)")
                self.onError?(error.localizedDescription)
            }
        }
    }

    /// Check if engine is ready
    @objc public var isReady: Bool {
        return isLoaded && enginePtr != nil
    }

    /// Generate response (non-streaming)
    @objc public func generate(prompt: String, maxTokens: Int, temperature: Double) -> String {
        guard isReady, let engine = enginePtr else {
            return "Error: Model not loaded"
        }

        let config = buildGenerationConfig(maxTokens: maxTokens, temperature: temperature)

        if let resultPtr = mlc_generate(engine, prompt, config) {
            let result = String(cString: resultPtr)
            mlc_free_string(resultPtr)
            return result
        }

        return "Error: Generation failed"
    }

    /// Generate response with streaming
    @objc public func generateStreaming(prompt: String, maxTokens: Int, temperature: Double) {
        guard isReady, let engine = enginePtr else {
            onError?("Model not loaded")
            return
        }

        queue.async { [weak self] in
            guard let self = self else { return }

            let config = self.buildGenerationConfig(maxTokens: maxTokens, temperature: temperature)
            var fullResponse = ""

            mlc_generate_streaming(engine, prompt, config) { tokenPtr in
                if let ptr = tokenPtr {
                    let token = String(cString: ptr)
                    fullResponse += token

                    DispatchQueue.main.async {
                        self.onToken?(token)
                    }
                }
            }

            DispatchQueue.main.async {
                self.onComplete?(fullResponse)
            }
        }
    }

    /// Build chat prompt with history
    @objc public func buildChatPrompt(systemPrompt: String?, history: [String]?, userMessage: String) -> String {
        var messages: [[String: String]] = []

        // System message
        if let sys = systemPrompt, !sys.isEmpty {
            messages.append(["role": "system", "content": sys])
        }

        // History
        if let hist = history {
            for (index, msg) in hist.enumerated() {
                let role = index % 2 == 0 ? "user" : "assistant"
                messages.append(["role": role, "content": msg])
            }
        }

        // User message
        messages.append(["role": "user", "content": userMessage])

        // Convert to JSON
        do {
            let json: [String: Any] = ["messages": messages]
            let data = try JSONSerialization.data(withJSONObject: json)
            return String(data: data, encoding: .utf8) ?? userMessage
        } catch {
            return userMessage
        }
    }

    /// Unload model
    @objc public func unload() {
        if let engine = enginePtr {
            mlc_destroy_engine(engine)
            enginePtr = nil
            isLoaded = false
            NSLog("[MlcLlmEngine] Engine unloaded")
        }
    }

    /// Reset conversation
    @objc public func resetChat() {
        if let engine = enginePtr {
            mlc_reset_chat(engine)
        }
    }

    /// Get model info
    @objc public func getModelInfo() -> String {
        guard let engine = enginePtr else {
            return "{}"
        }

        if let infoPtr = mlc_get_model_info(engine) {
            let info = String(cString: infoPtr)
            mlc_free_string(infoPtr)
            return info
        }

        return "{}"
    }

    /// Check if Metal GPU is available
    @objc public func isGpuAvailable() -> Bool {
        return metalDevice != nil
    }

    /// Get GPU name
    @objc public func getGpuName() -> String {
        return metalDevice?.name ?? "CPU"
    }

    private func buildConfig() -> String {
        let useGpu = metalDevice != nil
        let config: [String: Any] = [
            "device": useGpu ? "metal" : "cpu",
            "model_lib": ""
        ]

        do {
            let data = try JSONSerialization.data(withJSONObject: config)
            return String(data: data, encoding: .utf8) ?? "{}"
        } catch {
            return "{}"
        }
    }

    private func buildGenerationConfig(maxTokens: Int, temperature: Double) -> String {
        let config: [String: Any] = [
            "max_tokens": maxTokens,
            "temperature": temperature,
            "top_p": 0.95,
            "frequency_penalty": 0.0,
            "presence_penalty": 0.0,
            "stop": ["<|endoftext|>", "<|im_end|>"]
        ]

        do {
            let data = try JSONSerialization.data(withJSONObject: config)
            return String(data: data, encoding: .utf8) ?? "{}"
        } catch {
            return "{}"
        }
    }

    deinit {
        unload()
    }
}

// MARK: - Native C Interface (to be linked with MLC LLM library)
// These are placeholder declarations - actual implementation comes from libmlc_llm.a

@_silgen_name("mlc_create_engine")
func mlc_create_engine(_ modelPath: UnsafePointer<CChar>, _ config: UnsafePointer<CChar>) -> UnsafeMutableRawPointer?

@_silgen_name("mlc_destroy_engine")
func mlc_destroy_engine(_ engine: UnsafeMutableRawPointer)

@_silgen_name("mlc_generate")
func mlc_generate(_ engine: UnsafeMutableRawPointer, _ prompt: UnsafePointer<CChar>, _ config: UnsafePointer<CChar>) -> UnsafeMutablePointer<CChar>?

@_silgen_name("mlc_generate_streaming")
func mlc_generate_streaming(_ engine: UnsafeMutableRawPointer, _ prompt: UnsafePointer<CChar>, _ config: UnsafePointer<CChar>, _ callback: @escaping (UnsafePointer<CChar>?) -> Void)

@_silgen_name("mlc_reset_chat")
func mlc_reset_chat(_ engine: UnsafeMutableRawPointer)

@_silgen_name("mlc_get_model_info")
func mlc_get_model_info(_ engine: UnsafeMutableRawPointer) -> UnsafeMutablePointer<CChar>?

@_silgen_name("mlc_free_string")
func mlc_free_string(_ str: UnsafeMutablePointer<CChar>)
