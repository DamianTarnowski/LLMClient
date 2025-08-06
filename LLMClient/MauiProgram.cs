using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Services;
using LLMClient.Services;
using LLMClient.ViewModels;
using LLMClient.Views;


namespace LLMClient
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MaterialSymbolsOutlined.ttf", "MaterialSymbols");
                });

            SQLitePCL.Batteries_V2.Init();

#if DEBUG
    		builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

            // Rejestracja serwisów dla Dependency Injection
            builder.Services.AddSingleton<IAiService, AiService>();
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<ISecureApiKeyService, SecureApiKeyService>();
            builder.Services.AddSingleton<IStreamingBatchService, StreamingBatchService>();
            builder.Services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
            builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
            builder.Services.AddSingleton<IEmbeddingPipelineService, EmbeddingPipelineService>();
            builder.Services.AddSingleton<ISearchService>(provider =>
            {
                var database = provider.GetRequiredService<DatabaseService>();
                var embedding = provider.GetService<IEmbeddingService>();
                return new SearchService(database, embedding!);
            });
            builder.Services.AddSingleton<IExportService, ExportService>();

            // Rejestracja ViewModels
            builder.Services.AddTransient<MainPageViewModel>();
            builder.Services.AddTransient<ModelConfigurationViewModel>();
            builder.Services.AddTransient<SemanticSearchViewModel>(provider =>
            {
                var database = provider.GetRequiredService<DatabaseService>();
                var embedding = provider.GetService<IEmbeddingService>();
                var errorHandling = provider.GetRequiredService<IErrorHandlingService>();
                var embeddingPipeline = provider.GetRequiredService<IEmbeddingPipelineService>();
                var logger = provider.GetRequiredService<ILogger<SemanticSearchViewModel>>();
                return new SemanticSearchViewModel(database, embedding, errorHandling, embeddingPipeline, logger);
            });

            // Rejestracja Pages
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<ModelConfigurationPage>();
            builder.Services.AddTransient<SemanticSearchPage>();

            //Rejestracja Shell
            builder.Services.AddSingleton<AppShell>();

            return builder.Build();
        }
    }
}
