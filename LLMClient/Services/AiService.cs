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
        void UpdateConfiguration(AiModel model);
    }

    public class AiService : IAiService
    {
        private Kernel? _kernel;
        private IChatCompletionService? _chatService;
        private AiModel? _currentModel;

        public bool IsConfigured => _kernel != null && _chatService != null && _currentModel != null;

        public void UpdateConfiguration(AiModel model)
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

            var chatHistory = CreateChatHistory(conversationHistory);
            
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

            var chatHistory = CreateChatHistory(conversationHistory);
            
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
                cancellationToken: cancellationToken);

            await foreach (var content in response.WithCancellation(cancellationToken))
            {
                if (!string.IsNullOrEmpty(content.Content))
                {
                    yield return content.Content;
                }
            }
        }

        private ChatHistory CreateChatHistory(List<Message> conversationHistory)
        {
            var chatHistory = new ChatHistory();

            // Dodaj system message
            chatHistory.AddSystemMessage("Jesteś pomocnym asystentem AI. Odpowiadaj w języku polskim, chyba że użytkownik poprosi o inny język. Jeśli otrzymasz obrazek, opisz go szczegółowo.");

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
            return _currentModel?.Provider switch
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
        }
    }
}