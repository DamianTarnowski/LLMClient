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
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<ISecureApiKeyService, SecureApiKeyService>();
            builder.Services.AddSingleton<IStreamingBatchService, StreamingBatchService>();
            builder.Services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
            builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
            builder.Services.AddSingleton<IEmbeddingPipelineService, EmbeddingPipelineService>();
            
            // Rejestracja serwisu pamięci - używa tej samej bazy co reszta aplikacji
            builder.Services.AddSingleton<IMemoryService>(provider =>
            {
                var databaseService = provider.GetRequiredService<DatabaseService>();
                return new DatabaseMemoryService(databaseService);
            });
            
            // Rejestracja serwisu kontekstu pamięci
            builder.Services.AddSingleton<IMemoryContextService>(provider =>
            {
                var memoryService = provider.GetRequiredService<IMemoryService>();
                var lazyAiService = new Lazy<IAiService?>(() => provider.GetService<IAiService>());
                return new MemoryContextService(memoryService, lazyAiService);
            });
            
            // Rejestracja AiService z dostępem do kontekstu pamięci
            builder.Services.AddSingleton<IAiService>(provider =>
            {
                var memoryContextService = provider.GetService<IMemoryContextService>();
                return new AiService(memoryContextService);
            });
            
            // Rejestracja serwisu wydobywania pamięci
            builder.Services.AddSingleton<IMemoryExtractionService>(provider =>
            {
                var memoryService = provider.GetRequiredService<IMemoryService>();
                var aiService = provider.GetRequiredService<IAiService>();
                return new MemoryExtractionService(memoryService, aiService);
            });
            builder.Services.AddSingleton<ISearchService>(provider =>
            {
                var database = provider.GetRequiredService<DatabaseService>();
                var embedding = provider.GetService<IEmbeddingService>();
                return new SearchService(database, embedding!);
            });
            builder.Services.AddSingleton<IExportService, ExportService>();

            // Rejestracja ViewModels
            builder.Services.AddTransient<MainPageViewModel>(provider =>
            {
                var aiService = provider.GetRequiredService<IAiService>();
                var databaseService = provider.GetRequiredService<DatabaseService>();
                var streamingBatchService = provider.GetRequiredService<IStreamingBatchService>();
                var errorHandlingService = provider.GetRequiredService<IErrorHandlingService>();
                var searchService = provider.GetRequiredService<ISearchService>();
                var exportService = provider.GetRequiredService<IExportService>();
                var embeddingService = provider.GetRequiredService<IEmbeddingService>();
                var memoryExtractionService = provider.GetService<IMemoryExtractionService>();
                
                return new MainPageViewModel(aiService, databaseService, streamingBatchService, errorHandlingService, searchService, exportService, embeddingService, memoryExtractionService);
            });
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
            
            // Rejestracja MemoryPageViewModel
            builder.Services.AddTransient<MemoryPageViewModel>();

            // Rejestracja Pages
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<ModelConfigurationPage>();
            builder.Services.AddTransient<SemanticSearchPage>();
            builder.Services.AddTransient<MemoryPage>();

            //Rejestracja Shell
            builder.Services.AddSingleton<AppShell>();

            return builder.Build();
        }
    }
}
