using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Google;
using LLMClient.Models;
using System.Runtime.CompilerServices;

namespace LLMClient.Services
{
    public interface IAiService
    {
        Task<string> GetResponseAsync(string message, List<Message> conversationHistory, CancellationToken cancellationToken = default);
        Task<string> GetResponseAsync(string message, string? imageBase64, List<Message> conversationHistory, CancellationToken cancellationToken = default);
        IAsyncEnumerable<string> GetStreamingResponseAsync(string message, List<Message> conversationHistory, CancellationToken cancellationToken = default);
        IAsyncEnumerable<string> GetStreamingResponseAsync(string message, string? imageBase64, List<Message> conversationHistory, CancellationToken cancellationToken = default);
        bool IsConfigured { get; }
        Task UpdateConfiguration(AiModel model);
    }

    public class AiService : IAiService
    {
        private Kernel? _kernel;
        private IChatCompletionService? _chatService;
        private AiModel? _currentModel;
        private readonly IMemoryContextService? _memoryContextService;
        private readonly ILocalModelService? _localModelService;
        private readonly DatabaseService? _databaseService;

        public AiService(IMemoryContextService? memoryContextService = null, ILocalModelService? localModelService = null, DatabaseService? databaseService = null)
        {
            _memoryContextService = memoryContextService;
            _localModelService = localModelService;
            _databaseService = databaseService;
        }

        public bool IsConfigured => (_kernel != null && _chatService != null && _currentModel != null) || 
                                    (_currentModel?.IsLocalModel == true && _localModelService?.IsLoaded == true);
        
        public bool IsUsingLocalModel => _currentModel?.IsLocalModel == true && _localModelService?.IsLoaded == true;

        public async Task UpdateConfiguration(AiModel model)
        {
            _currentModel = model;

            var builder = Kernel.CreateBuilder();

            try
            {
                switch (model.Provider)
                {
                    case AiProvider.OpenAI:
                        builder.AddOpenAIChatCompletion(
                            modelId: model.ModelId,
                            apiKey: model.ApiKey);
                        break;

                    case AiProvider.Gemini:
                        builder.AddGoogleAIGeminiChatCompletion(
                            modelId: model.ModelId,
                            apiKey: model.ApiKey);
                        break;

                    case AiProvider.OpenAICompatible:
                        builder.AddOpenAIChatCompletion(
                            modelId: model.ModelId,
                            apiKey: model.ApiKey,
                            endpoint: new Uri(model.Endpoint));
                        break;

                    case AiProvider.LocalModel:
                        // For local models, we don't use Semantic Kernel
                        // Just validate that the local model service is available
                        if (_localModelService == null)
                        {
                            throw new InvalidOperationException("Local model service is not available");
                        }
                        
                        // Load the local model if not already loaded
                        if (!_localModelService.IsLoaded)
                        {
                            var loadResult = await _localModelService.LoadModelAsync();
                            if (!loadResult)
                            {
                                throw new InvalidOperationException("Failed to load local model");
                            }
                        }
                        
                        // Clear kernel and chat service for local models
                        _kernel = null;
                        _chatService = null;
                        return; // Exit early, no need to build kernel
                }

                _kernel = builder.Build();
                
                _chatService = _kernel.GetRequiredService<IChatCompletionService>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Nie udało się skonfigurować modelu {model.Name}: {ex.Message}", ex);
            }
        }

        public async Task<string> GetResponseAsync(string message, List<Message> conversationHistory, CancellationToken cancellationToken = default)
        {
            return await GetResponseAsync(message, null, conversationHistory, cancellationToken);
        }

        public async Task<string> GetResponseAsync(string message, string? imageBase64, List<Message> conversationHistory, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("AI Service nie jest skonfigurowany");

            // Handle local model separately
            if (_currentModel?.IsLocalModel == true)
            {
                if (_localModelService == null)
                    throw new InvalidOperationException("Local model service is not available");

                // Local models don't support images yet
                if (!string.IsNullOrEmpty(imageBase64))
                    throw new NotSupportedException("Local models don't support images yet");

                return await _localModelService.GenerateResponseAsync(conversationHistory, message, cancellationToken);
            }

            var chatHistory = await CreateChatHistoryAsync(conversationHistory);
            
            // Dodaj wiadomość użytkownika z potencjalnym obrazkiem
            if (!string.IsNullOrEmpty(imageBase64))
            {
                var messageContent = new ChatMessageContentItemCollection();
                
                if (!string.IsNullOrEmpty(message))
                {
                    messageContent.Add(new TextContent(message));
                }
                
                var imageContent = new ImageContent(BinaryData.FromBytes(Convert.FromBase64String(imageBase64)), "image/jpeg");
                messageContent.Add(imageContent);
                
                chatHistory.Add(new ChatMessageContent(AuthorRole.User, messageContent));
            }
            else
            {
                chatHistory.AddUserMessage(message);
            }

            var result = await _chatService!.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: GetExecutionSettings(),
                kernel: _kernel,
                cancellationToken: cancellationToken);

            return result.Content ?? "Brak odpowiedzi";
        }

        public async IAsyncEnumerable<string> GetStreamingResponseAsync(
            string message,
            List<Message> conversationHistory,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var chunk in GetStreamingResponseAsync(message, null, conversationHistory, cancellationToken))
            {
                yield return chunk;
            }
        }

        public async IAsyncEnumerable<string> GetStreamingResponseAsync(
            string message,
            string? imageBase64,
            List<Message> conversationHistory,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("AI Service nie jest skonfigurowany");

            // Handle local model streaming separately
            if (_currentModel?.IsLocalModel == true)
            {
                if (_localModelService == null)
                    throw new InvalidOperationException("Local model service is not available");

                // Local models don't support images yet
                if (!string.IsNullOrEmpty(imageBase64))
                    throw new NotSupportedException("Local models don't support images yet");

                // Build conversation prompt using Phi-4-mini-instruct chat template
                var promptBuilder = new System.Text.StringBuilder();

                // Load user-defined system prompt for local models (no memory context)
                var systemPrompt = await GetSystemPromptAsync();

                // Use the same template as LocalModelService: <|im_start|>role<|im_sep|> ... <|im_end|>
                if (!string.IsNullOrWhiteSpace(systemPrompt))
                {
                    promptBuilder.AppendLine("<|im_start|>system<|im_sep|>");
                    promptBuilder.AppendLine(systemPrompt);
                    promptBuilder.AppendLine("<|im_end|>");
                }

                // Add last few messages as chat turns
                foreach (var msg in conversationHistory.TakeLast(3))
                {
                    if (msg.IsUser)
                    {
                        promptBuilder.AppendLine("<|im_start|>user<|im_sep|>");
                        promptBuilder.AppendLine(msg.Content);
                        promptBuilder.AppendLine("<|im_end|>");
                    }
                    else
                    {
                        promptBuilder.AppendLine("<|im_start|>assistant<|im_sep|>");
                        promptBuilder.AppendLine(msg.Content);
                        promptBuilder.AppendLine("<|im_end|>");
                    }
                }

                // Current user turn, then start assistant turn (no end token)
                promptBuilder.AppendLine("<|im_start|>user<|im_sep|>");
                promptBuilder.AppendLine(message);
                promptBuilder.AppendLine("<|im_end|>");
                promptBuilder.AppendLine("<|im_start|>assistant<|im_sep|>");

                await foreach (var chunk in _localModelService.GenerateStreamingResponseAsync(promptBuilder.ToString(), cancellationToken))
                {
                    yield return chunk;
                }
                yield break;
            }

            var chatHistory = await CreateChatHistoryAsync(conversationHistory);
            
            // Dodaj wiadomość użytkownika z potencjalnym obrazkiem
            if (!string.IsNullOrEmpty(imageBase64))
            {
                var messageContent = new ChatMessageContentItemCollection();
                
                if (!string.IsNullOrEmpty(message))
                {
                    messageContent.Add(new TextContent(message));
                }
                
                var imageContent = new ImageContent(BinaryData.FromBytes(Convert.FromBase64String(imageBase64)), "image/jpeg");
                messageContent.Add(imageContent);
                
                chatHistory.Add(new ChatMessageContent(AuthorRole.User, messageContent));
            }
            else
            {
                chatHistory.AddUserMessage(message);
            }

            var response = _chatService!.GetStreamingChatMessageContentsAsync(
                chatHistory,
                executionSettings: GetExecutionSettings(),
                kernel: _kernel,
                cancellationToken: cancellationToken);

            await foreach (var content in response.WithCancellation(cancellationToken))
            {
                if (!string.IsNullOrEmpty(content.Content))
                {
                    yield return content.Content;
                }
            }
        }

        private async Task<ChatHistory> CreateChatHistoryAsync(List<Message> conversationHistory)
        {
            var chatHistory = new ChatHistory();

            // Load system prompt from DB (editable in UI). If empty, omit the system message.
            var systemMessage = await GetSystemPromptAsync();
            
            // Dodaj kontekst pamięci tylko dla modeli chmurowych (i gdy włączone w ustawieniach)
            if (_currentModel?.IsLocalModel != true && _memoryContextService != null && Preferences.Get("IncludeMemoryInSystemPrompt", true))
            {
                System.Diagnostics.Debug.WriteLine("[AiService] Loading memory context");
                var memoryContext = await _memoryContextService.GenerateMemoryContextAsync();
                
                if (!string.IsNullOrWhiteSpace(memoryContext))
                {
                    systemMessage += "\n\n" + memoryContext;
                    systemMessage += "\n\nUżywaj tej pamięci do personalizacji odpowiedzi. Kiedy użytkownik poda nowe informacje o sobie, zapamiętaj je (ale nie pokazuj procesu zapamiętywania - po prostu używaj informacji w przyszłych rozmowach).";
                    System.Diagnostics.Debug.WriteLine($"[AiService] Memory context added to system message ({memoryContext.Length} chars)");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[AiService] No memory context available");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[AiService] No memory context service available");
            }
            
            if (!string.IsNullOrWhiteSpace(systemMessage))
            {
                chatHistory.AddSystemMessage(systemMessage);
            }

            // Dodaj historię konwersacji
            foreach (var msg in conversationHistory.Take(20)) // Ogranicz do ostatnich 20 wiadomości
            {
                if (msg.IsUser)
                {
                    if (msg.HasImage && !string.IsNullOrEmpty(msg.ImageBase64))
                    {
                        // Wiadomość z obrazkiem
                        var messageContent = new ChatMessageContentItemCollection();
                        
                        if (!string.IsNullOrEmpty(msg.Content))
                        {
                            messageContent.Add(new TextContent(msg.Content));
                        }
                        
                        // Dodaj obrazek - format depends on the model
                        var imageContent = new ImageContent(BinaryData.FromBytes(Convert.FromBase64String(msg.ImageBase64)), "image/jpeg");
                        messageContent.Add(imageContent);
                        
                        chatHistory.Add(new ChatMessageContent(AuthorRole.User, messageContent));
                    }
                    else
                    {
                        chatHistory.AddUserMessage(msg.Content);
                    }
                }
                else
                {
                    chatHistory.AddAssistantMessage(msg.Content);
                }
            }

            return chatHistory;
        }

        private PromptExecutionSettings GetExecutionSettings()
        {
            var settings = _currentModel?.Provider switch
            {
                AiProvider.OpenAI or AiProvider.OpenAICompatible => new OpenAIPromptExecutionSettings
                {
                    //MaxTokens = 2000,
                    //Temperature = 0.7,
                    //TopP = 1.0,
                    //FrequencyPenalty = 0.0,
                    //PresencePenalty = 0.0
                },
                AiProvider.Gemini => new GeminiPromptExecutionSettings
                {
                    //MaxTokens = 2000,
                    //Temperature = 0.7,
                    //TopP = 1.0
                },
                _ => new PromptExecutionSettings
                {
                    ExtensionData = new Dictionary<string, object>
                    {
                        ["max_tokens"] = 2000,
                        ["temperature"] = 0.7
                    }
                }
            };
            
            System.Diagnostics.Debug.WriteLine($"[AiService] Execution settings configured for {_currentModel?.Provider}");
            return settings;
        }

        private async Task<string> GetSystemPromptAsync()
        {
            try
            {
                if (_databaseService != null)
                {
                    var settings = await _databaseService.GetModelSettingsAsync();
                    return settings?.SystemPrompt?.Trim() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AiService] Failed to load system prompt from DB: {ex.Message}");
            }
            return string.Empty;
        }
    }
}