using System.Net;
using System.Net.Sockets;

namespace LLMClient.Services
{
    public interface IErrorHandlingService
    {
        Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName, int maxRetries = 3);
        Task ExecuteWithRetryAsync(Func<Task> operation, string operationName, int maxRetries = 3);
        string GetUserFriendlyErrorMessage(Exception ex, string context);
        bool IsRetriableError(Exception ex);
    }

    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly TimeSpan[] _retryDelays = new[]
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5)
        };

        public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName, int maxRetries = 3)
        {
            Exception? lastException = null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Executing {operationName}, attempt {attempt + 1}");
                    return await operation();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    System.Diagnostics.Debug.WriteLine($"Error in {operationName}, attempt {attempt + 1}: {ex.Message}");

                    // Don't retry on the last attempt or if error is not retriable
                    if (attempt >= maxRetries || !IsRetriableError(ex))
                    {
                        break;
                    }

                    // Wait before retry
                    if (attempt < _retryDelays.Length)
                    {
                        await Task.Delay(_retryDelays[attempt]);
                    }
                }
            }

            throw new InvalidOperationException(
                GetUserFriendlyErrorMessage(lastException!, operationName),
                lastException);
        }

        public async Task ExecuteWithRetryAsync(Func<Task> operation, string operationName, int maxRetries = 3)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                await operation();
                return true; // Dummy return value
            }, operationName, maxRetries);
        }

        public string GetUserFriendlyErrorMessage(Exception ex, string context)
        {
            return ex switch
            {
                HttpRequestException httpEx when httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized") =>
                    "🔑 Nieprawidłowy klucz API. Sprawdź konfigurację modelu.",

                HttpRequestException httpEx when httpEx.Message.Contains("403") || httpEx.Message.Contains("Forbidden") =>
                    "🚫 Brak dostępu. Sprawdź uprawnienia API key lub czy masz wystarczające środki na koncie.",

                HttpRequestException httpEx when httpEx.Message.Contains("429") || httpEx.Message.Contains("Too Many Requests") =>
                    "⏰ Za dużo zapytań. Poczekaj chwilę i spróbuj ponownie.",

                HttpRequestException httpEx when httpEx.Message.Contains("500") || httpEx.Message.Contains("502") || httpEx.Message.Contains("503") =>
                    "🔧 Serwer AI ma problemy. Spróbuj ponownie za chwilę.",

                TaskCanceledException =>
                    "⏱️ Zapytanie przekroczyło limit czasu. Spróbuj z krótszą wiadomością.",

                SocketException =>
                    "🌐 Brak połączenia z internetem. Sprawdź połączenie sieciowe.",

                InvalidOperationException invalidOpEx when invalidOpEx.Message.Contains("API Key") =>
                    "🔑 Problem z kluczem API. Sprawdź konfigurację modelu.",

                InvalidOperationException invalidOpEx when invalidOpEx.Message.Contains("skonfigurowany") =>
                    "⚙️ Model AI nie jest skonfigurowany. Przejdź do ustawień i dodaj model.",

                ArgumentException =>
                    "📝 Nieprawidłowe dane wejściowe. Sprawdź wprowadzone informacje.",

                _ when context.Contains("streaming") =>
                    $"📡 Błąd podczas pobierania odpowiedzi: {GetSimpleErrorMessage(ex)}",

                _ when context.Contains("database") =>
                    $"💾 Błąd bazy danych: {GetSimpleErrorMessage(ex)}",

                _ when context.Contains("model test") =>
                    $"🧪 Test modelu nieudany: {GetSimpleErrorMessage(ex)}",

                _ => $"❌ Wystąpił błąd: {GetSimpleErrorMessage(ex)}"
            };
        }

        public bool IsRetriableError(Exception ex)
        {
            return ex switch
            {
                HttpRequestException httpEx => 
                    httpEx.Message.Contains("429") || // Rate limit
                    httpEx.Message.Contains("500") || // Server error
                    httpEx.Message.Contains("502") || // Bad gateway
                    httpEx.Message.Contains("503") || // Service unavailable
                    httpEx.Message.Contains("timeout"),

                TaskCanceledException => true, // Timeout
                SocketException => true,       // Network issues
                
                InvalidOperationException invalidOpEx when 
                    invalidOpEx.Message.Contains("network") ||
                    invalidOpEx.Message.Contains("connection") => true,

                _ => false // Don't retry auth errors, validation errors, etc.
            };
        }

        private static string GetSimpleErrorMessage(Exception ex)
        {
            // Extract the most relevant part of error message
            var message = ex.Message;
            
            // Remove technical details that users don't need to see
            if (message.Contains("HttpRequestException"))
                message = message.Substring(message.IndexOf(":") + 1).Trim();
            
            if (message.Length > 100)
                message = message.Substring(0, 97) + "...";
                
            return message;
        }
    }
} 