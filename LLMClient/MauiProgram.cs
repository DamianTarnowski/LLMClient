using Microsoft.Extensions.Logging;
using LLMClient.Services;
using LLMClient.ViewModels;
using LLMClient.Views;
using LLMClient.Models;


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
            builder.Services.AddSingleton<IDatabaseService>(provider => provider.GetRequiredService<DatabaseService>());

            builder.Services.AddSingleton<ILocalizationService, LocalizationService>();
            builder.Services.AddSingleton<ISecureApiKeyService, SecureApiKeyService>();
            builder.Services.AddSingleton<IStreamingBatchService, StreamingBatchService>();
            builder.Services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
            builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
            builder.Services.AddSingleton<IEmbeddingPipelineService, EmbeddingPipelineService>();
            // Concrete local model engines as singletons
            builder.Services.AddSingleton<RobustLocalModelService>(provider =>
            {
                var onnxLogger = provider.GetRequiredService<ILogger<RobustLocalModelService>>();
                var errorHandling = provider.GetService<IErrorHandlingService>();
                var databaseService = provider.GetService<DatabaseService>();
                return new RobustLocalModelService(onnxLogger, errorHandling, databaseService);
            });

#if WINDOWS
            // LlamaSharp only supported on Windows - no Android native libraries available
            builder.Services.AddSingleton<LlamaSharpLocalModelService>(provider =>
            {
                var llamaLogger = provider.GetRequiredService<ILogger<LlamaSharpLocalModelService>>();
                return new LlamaSharpLocalModelService(llamaLogger);
            });

            // Switchable service to allow runtime engine switching (Windows only)
            builder.Services.AddSingleton<SwitchableLocalModelService>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<SwitchableLocalModelService>>();
                return new SwitchableLocalModelService(
                    logger,
                    () => provider.GetRequiredService<RobustLocalModelService>(),
                    () => provider.GetRequiredService<LlamaSharpLocalModelService>());
            });

            // Expose ILocalModelService via safety wrapper around the switchable service
            builder.Services.AddSingleton<ILocalModelService>(provider =>
            {
                var errorHandling = provider.GetService<IErrorHandlingService>();
                var wrapperLogger = provider.GetRequiredService<ILogger<SafeLocalModelWrapper>>();
                var switchable = provider.GetRequiredService<SwitchableLocalModelService>();
                return new SafeLocalModelWrapper(wrapperLogger, switchable, errorHandling);
            });
#elif ANDROID
            // Android: LLamaSharp (llama.cpp) + MediaPipe GenAI (Gemma)
            builder.Services.AddSingleton<LlamaSharpLocalModelService>(provider =>
            {
                var llamaLogger = provider.GetRequiredService<ILogger<LlamaSharpLocalModelService>>();
                return new LlamaSharpLocalModelService(llamaLogger);
            });

            // MediaPipe GenAI for Gemma models (Google AI Edge)
            builder.Services.AddSingleton<MediaPipeLocalModelService>(provider =>
            {
                var mpLogger = provider.GetRequiredService<ILogger<MediaPipeLocalModelService>>();
                return new MediaPipeLocalModelService(mpLogger);
            });

            // Switchable service: ONNX + LLamaSharp + MediaPipe
            builder.Services.AddSingleton<SwitchableLocalModelService>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<SwitchableLocalModelService>>();
                return new SwitchableLocalModelService(
                    logger,
                    () => provider.GetRequiredService<RobustLocalModelService>(),      // ONNX
                    () => provider.GetRequiredService<LlamaSharpLocalModelService>(),  // LLamaSharp (llama.cpp)
                    () => provider.GetRequiredService<MediaPipeLocalModelService>());  // MediaPipe (Gemma)
            });

            // Expose ILocalModelService via safety wrapper around the switchable service
            builder.Services.AddSingleton<ILocalModelService>(provider =>
            {
                var errorHandling = provider.GetService<IErrorHandlingService>();
                var wrapperLogger = provider.GetRequiredService<ILogger<SafeLocalModelWrapper>>();
                var switchable = provider.GetRequiredService<SwitchableLocalModelService>();
                return new SafeLocalModelWrapper(wrapperLogger, switchable, errorHandling);
            });
#elif IOS
            // iOS: MediaPipe GenAI for Gemma models (Google AI Edge)
            builder.Services.AddSingleton<MediaPipeLocalModelService>(provider =>
            {
                var mpLogger = provider.GetRequiredService<ILogger<MediaPipeLocalModelService>>();
                return new MediaPipeLocalModelService(mpLogger);
            });

            // Switchable service for iOS: ONNX + MediaPipe
            builder.Services.AddSingleton<SwitchableLocalModelService>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<SwitchableLocalModelService>>();
                return new SwitchableLocalModelService(
                    logger,
                    () => provider.GetRequiredService<RobustLocalModelService>(),
                    () => provider.GetRequiredService<RobustLocalModelService>(),  // No LLamaSharp on iOS
                    () => provider.GetRequiredService<MediaPipeLocalModelService>());
            });

            // Expose ILocalModelService via safety wrapper around the switchable service
            builder.Services.AddSingleton<ILocalModelService>(provider =>
            {
                var errorHandling = provider.GetService<IErrorHandlingService>();
                var wrapperLogger = provider.GetRequiredService<ILogger<SafeLocalModelWrapper>>();
                var switchable = provider.GetRequiredService<SwitchableLocalModelService>();
                return new SafeLocalModelWrapper(wrapperLogger, switchable, errorHandling);
            });
#else
            // Other platforms: Only ONNX Runtime available for local models
            builder.Services.AddSingleton<ILocalModelService>(provider =>
            {
                var errorHandling = provider.GetService<IErrorHandlingService>();
                var wrapperLogger = provider.GetRequiredService<ILogger<SafeLocalModelWrapper>>();
                var onnx = provider.GetRequiredService<RobustLocalModelService>();
                return new SafeLocalModelWrapper(wrapperLogger, onnx, errorHandling);
            });
#endif
            builder.Services.AddSingleton<IOnboardingService, OnboardingService>();
            
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
            
            // Rejestracja AiService z dostępem do kontekstu pamięci i lokalnych modeli
            builder.Services.AddSingleton<IAiService>(provider =>
            {
                var memoryContextService = provider.GetService<IMemoryContextService>();
                var localModelService = provider.GetService<ILocalModelService>();
                var databaseService = provider.GetService<DatabaseService>();
                return new AiService(memoryContextService, localModelService, databaseService);
            });
            
            // Rejestracja serwisu wydobywania pamięci
            builder.Services.AddSingleton<IMemoryExtractionService>(provider =>
            {
                var memoryService = provider.GetRequiredService<IMemoryService>();
                var aiService = provider.GetRequiredService<IAiService>();
                return new MemoryExtractionService(memoryService, aiService);
            });
            // Rejestracja ApiEmbeddingService jako alternatywa dla lokalnych embeddingów
            builder.Services.AddSingleton<ApiEmbeddingService>();
            
            builder.Services.AddSingleton<ISearchService>(provider =>
            {
                var database = provider.GetRequiredService<DatabaseService>();
                var embedding = provider.GetService<IEmbeddingService>();
                return new SearchService(database, embedding!);
            });
            builder.Services.AddSingleton<IExportService, ExportService>();
            builder.Services.AddSingleton<IRagService>(provider =>
            {
                var database = provider.GetRequiredService<IDatabaseService>();
                var embedding = provider.GetService<IEmbeddingService>();
                return new RagService(database, embedding);
            });
            builder.Services.AddSingleton<IDocumentAnalysisService>(provider =>
            {
                var aiService = provider.GetRequiredService<IAiService>();
                return new DocumentAnalysisService(aiService);
            });
            builder.Services.AddSingleton<IIngestionService>(provider =>
            {
                var ragService = provider.GetRequiredService<IRagService>();
                return new IngestionService(ragService);
            });

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
                var localizationService = provider.GetRequiredService<ILocalizationService>();
                var localModelService = provider.GetRequiredService<ILocalModelService>();
                var memoryExtractionService = provider.GetService<IMemoryExtractionService>();
                
                return new MainPageViewModel(aiService, databaseService, streamingBatchService, errorHandlingService, searchService, exportService, embeddingService, localizationService, localModelService, memoryExtractionService);
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
            
            // Rejestracja LocalModelStatusViewModel
            builder.Services.AddTransient<LocalModelStatusViewModel>();
            
            // Rejestracja ModelSettingsViewModel
            builder.Services.AddTransient<ModelSettingsViewModel>();
            
            // Rejestracja RagViewModel
            builder.Services.AddTransient<RagViewModel>(provider =>
            {
                var ragService = provider.GetRequiredService<IRagService>();
                var ingestionService = provider.GetRequiredService<IIngestionService>();
                var embeddingService = provider.GetService<IEmbeddingService>();
                return new RagViewModel(ragService, ingestionService, embeddingService);
            });
            
            // Rejestracja DocumentAnalysisViewModel
            builder.Services.AddTransient<DocumentAnalysisViewModel>(provider =>
            {
                var analysisService = provider.GetRequiredService<IDocumentAnalysisService>();
                return new DocumentAnalysisViewModel(analysisService);
            });

            // Rejestracja Pages
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<ModelConfigurationPage>();
            builder.Services.AddTransient<SemanticSearchPage>();
            builder.Services.AddTransient<MemoryPage>();
            builder.Services.AddTransient<ModelSettingsPage>();
            builder.Services.AddTransient<RagDocumentsPage>();
            builder.Services.AddTransient<DocumentAnalysisPage>();

            // GGUF Model Manager Page (Windows/Android - LLamaSharp)
#if WINDOWS || ANDROID
            builder.Services.AddTransient<GgufModelManagerPage>();
#endif

            //Rejestracja Shell
            builder.Services.AddSingleton<AppShell>();

            return builder.Build();
        }
    }
}
