using LLMClient.Models;

namespace LLMClient.Services
{
    public class OnboardingStep
    {
        public string Id { get; set; } = string.Empty;
        public string TitleKey { get; set; } = string.Empty;
        public string DescriptionKey { get; set; } = string.Empty;
        public string IconCode { get; set; } = string.Empty; // Material Design icon code
        public bool IsCompleted { get; set; }
        public bool RequiresLocalModel { get; set; } = false;
        public string OnboardingTopic { get; set; } = "general";
    }

    public interface IOnboardingService
    {
        Task<List<OnboardingStep>> GetOnboardingStepsAsync();
        Task<bool> IsOnboardingCompletedAsync();
        Task MarkStepAsCompletedAsync(string stepId);
        Task CompleteOnboardingAsync();
        Task ResetOnboardingAsync();
        Task<string> GetStepResponseAsync(OnboardingStep step, string userLanguage);
        bool ShouldShowOnboarding { get; }
    }

    public class OnboardingService : IOnboardingService
    {
        private readonly ILocalModelService? _localModelService;
        private readonly ILocalizationService _localizationService;
        private const string ONBOARDING_COMPLETED_KEY = "onboarding_completed";
        private const string ONBOARDING_STEPS_KEY = "onboarding_steps";

        public OnboardingService(ILocalizationService localizationService, ILocalModelService? localModelService = null)
        {
            _localizationService = localizationService;
            _localModelService = localModelService;
        }

        public bool ShouldShowOnboarding => !Preferences.Get(ONBOARDING_COMPLETED_KEY, false);

        public async Task<List<OnboardingStep>> GetOnboardingStepsAsync()
        {
            var steps = new List<OnboardingStep>
            {
                new OnboardingStep
                {
                    Id = "welcome",
                    TitleKey = "OnboardingWelcomeTitle",
                    DescriptionKey = "OnboardingWelcomeDescription",
                    IconCode = "&#xe7fd;", // waving_hand
                    OnboardingTopic = "general",
                    RequiresLocalModel = true
                },
                new OnboardingStep
                {
                    Id = "ai_setup",
                    TitleKey = "OnboardingAiSetupTitle", 
                    DescriptionKey = "OnboardingAiSetupDescription",
                    IconCode = "&#xe869;", // smart_toy
                    OnboardingTopic = "setup"
                },
                new OnboardingStep
                {
                    Id = "memory_system",
                    TitleKey = "OnboardingMemoryTitle",
                    DescriptionKey = "OnboardingMemoryDescription", 
                    IconCode = "&#xe322;", // psychology
                    OnboardingTopic = "memory",
                    RequiresLocalModel = true
                },
                new OnboardingStep
                {
                    Id = "semantic_search",
                    TitleKey = "OnboardingSearchTitle",
                    DescriptionKey = "OnboardingSearchDescription",
                    IconCode = "&#xe8b6;", // search
                    OnboardingTopic = "search",
                    RequiresLocalModel = true
                },
                new OnboardingStep
                {
                    Id = "multilingual",
                    TitleKey = "OnboardingLanguageTitle",
                    DescriptionKey = "OnboardingLanguageDescription",
                    IconCode = "&#xe894;", // translate
                    OnboardingTopic = "languages",
                    RequiresLocalModel = true
                },
                new OnboardingStep
                {
                    Id = "local_model",
                    TitleKey = "OnboardingLocalModelTitle",
                    DescriptionKey = "OnboardingLocalModelDescription",
                    IconCode = "&#xe1c3;", // offline_bolt
                    OnboardingTopic = "local_model"
                }
            };

            // Load completion status from preferences
            var completedSteps = Preferences.Get(ONBOARDING_STEPS_KEY, string.Empty);
            if (!string.IsNullOrEmpty(completedSteps))
            {
                var completedList = completedSteps.Split(',').ToHashSet();
                foreach (var step in steps)
                {
                    step.IsCompleted = completedList.Contains(step.Id);
                }
            }

            return steps;
        }

        public async Task<bool> IsOnboardingCompletedAsync()
        {
            return Preferences.Get(ONBOARDING_COMPLETED_KEY, false);
        }

        public async Task MarkStepAsCompletedAsync(string stepId)
        {
            var completedSteps = Preferences.Get(ONBOARDING_STEPS_KEY, string.Empty);
            var completedList = string.IsNullOrEmpty(completedSteps) 
                ? new HashSet<string>() 
                : completedSteps.Split(',').ToHashSet();

            completedList.Add(stepId);
            Preferences.Set(ONBOARDING_STEPS_KEY, string.Join(",", completedList));

            // Check if all steps are completed
            var allSteps = await GetOnboardingStepsAsync();
            if (allSteps.All(s => completedList.Contains(s.Id)))
            {
                await CompleteOnboardingAsync();
            }
        }

        public async Task CompleteOnboardingAsync()
        {
            Preferences.Set(ONBOARDING_COMPLETED_KEY, true);
            await Task.CompletedTask;
        }

        public async Task ResetOnboardingAsync()
        {
            Preferences.Remove(ONBOARDING_COMPLETED_KEY);
            Preferences.Remove(ONBOARDING_STEPS_KEY);
            await Task.CompletedTask;
        }

        public async Task<string> GetStepResponseAsync(OnboardingStep step, string userLanguage)
        {
            // Try to use local model if available and step requires it
            if (step.RequiresLocalModel && _localModelService?.IsLoaded == true)
            {
                try
                {
                    return await _localModelService.GenerateOnboardingResponseAsync(
                        userLanguage, 
                        step.OnboardingTopic);
                }
                catch (Exception)
                {
                    // Fallback to static response if local model fails
                }
            }

            // Fallback to static localized responses
            return GetStaticResponse(step, userLanguage);
        }

        private string GetStaticResponse(OnboardingStep step, string userLanguage)
        {
            return step.Id switch
            {
                "welcome" => GetWelcomeResponse(userLanguage),
                "ai_setup" => GetAiSetupResponse(userLanguage),
                "memory_system" => GetMemoryResponse(userLanguage),
                "semantic_search" => GetSearchResponse(userLanguage),
                "multilingual" => GetLanguageResponse(userLanguage),
                "local_model" => GetLocalModelResponse(userLanguage),
                _ => GetDefaultResponse(userLanguage)
            };
        }

        private string GetWelcomeResponse(string language)
        {
            return language.ToLower() switch
            {
                "pl" or "pl-pl" => "Witaj w LLMClient! 👋\n\nTo zaawansowany klient AI z systemem pamięci, wyszukiwaniem semantycznym i obsługą wielu języków. Pozwala na rozmowy z różnymi modelami AI oraz lokalne przetwarzanie na Twoim urządzeniu.",
                "de" or "de-de" => "Willkommen bei LLMClient! 👋\n\nDies ist ein fortgeschrittener AI-Client mit Gedächtnissystem, semantischer Suche und mehrsprachiger Unterstützung. Ermöglicht Gespräche mit verschiedenen AI-Modellen und lokale Verarbeitung auf Ihrem Gerät.",
                "es" or "es-es" => "¡Bienvenido a LLMClient! 👋\n\nEste es un cliente de IA avanzado con sistema de memoria, búsqueda semántica y soporte multiidioma. Permite conversaciones con diferentes modelos de IA y procesamiento local en tu dispositivo.",
                "fr" or "fr-fr" => "Bienvenue dans LLMClient ! 👋\n\nCeci est un client IA avancé avec système de mémoire, recherche sémantique et support multilingue. Permet des conversations avec différents modèles IA et un traitement local sur votre appareil.",
                _ => "Welcome to LLMClient! 👋\n\nThis is an advanced AI client with memory system, semantic search, and multi-language support. It enables conversations with different AI models and local processing on your device."
            };
        }

        private string GetAiSetupResponse(string language)
        {
            return language.ToLower() switch
            {
                "pl" or "pl-pl" => "Konfiguracja AI 🤖\n\nMożesz dodać różne modele AI:\n• OpenAI (GPT-4, GPT-3.5)\n• Google Gemini\n• Lokalne modele (Phi-4-mini)\n• Kompatybilne z OpenAI API\n\nPrzejdź do ustawień aby dodać swój pierwszy model!",
                "de" or "de-de" => "AI-Konfiguration 🤖\n\nSie können verschiedene AI-Modelle hinzufügen:\n• OpenAI (GPT-4, GPT-3.5)\n• Google Gemini\n• Lokale Modelle (Phi-4-mini)\n• OpenAI API-kompatibel\n\nGehen Sie zu den Einstellungen, um Ihr erstes Modell hinzuzufügen!",
                "es" or "es-es" => "Configuración de IA 🤖\n\nPuedes agregar diferentes modelos de IA:\n• OpenAI (GPT-4, GPT-3.5)\n• Google Gemini\n• Modelos locales (Phi-4-mini)\n• Compatible con OpenAI API\n\n¡Ve a configuración para agregar tu primer modelo!",
                "fr" or "fr-fr" => "Configuration IA 🤖\n\nVous pouvez ajouter différents modèles IA :\n• OpenAI (GPT-4, GPT-3.5)\n• Google Gemini\n• Modèles locaux (Phi-4-mini)\n• Compatible OpenAI API\n\nAllez dans les paramètres pour ajouter votre premier modèle !",
                _ => "AI Configuration 🤖\n\nYou can add different AI models:\n• OpenAI (GPT-4, GPT-3.5)\n• Google Gemini\n• Local models (Phi-4-mini)\n• OpenAI API compatible\n\nGo to settings to add your first model!"
            };
        }

        private string GetMemoryResponse(string language)
        {
            return language.ToLower() switch
            {
                "pl" or "pl-pl" => "System Pamięci 🧠\n\nLLMClient zapamięta ważne informacje o Tobie:\n• Imię, preferencje, zainteresowania\n• Kontekst poprzednich rozmów\n• Automatyczne kategoryzowanie wspomnień\n\nTwoje dane są bezpiecznie zaszyfrowane lokalnie!",
                "de" or "de-de" => "Gedächtnissystem 🧠\n\nLLMClient merkt sich wichtige Informationen über Sie:\n• Name, Vorlieben, Interessen\n• Kontext vorheriger Gespräche\n• Automatische Kategorisierung von Erinnerungen\n\nIhre Daten sind sicher lokal verschlüsselt!",
                "es" or "es-es" => "Sistema de Memoria 🧠\n\nLLMClient recordará información importante sobre ti:\n• Nombre, preferencias, intereses\n• Contexto de conversaciones anteriores\n• Categorización automática de recuerdos\n\n¡Tus datos están cifrados de forma segura localmente!",
                "fr" or "fr-fr" => "Système de Mémoire 🧠\n\nLLMClient se souviendra d'informations importantes sur vous :\n• Nom, préférences, intérêts\n• Contexte des conversations précédentes\n• Catégorisation automatique des souvenirs\n\nVos données sont chiffrées en sécurité localement !",
                _ => "Memory System 🧠\n\nLLMClient will remember important information about you:\n• Name, preferences, interests\n• Context from previous conversations\n• Automatic memory categorization\n\nYour data is securely encrypted locally!"
            };
        }

        private string GetSearchResponse(string language)
        {
            return language.ToLower() switch
            {
                "pl" or "pl-pl" => "Wyszukiwanie Semantyczne 🔍\n\nZnajdź informacje w swoich rozmowach:\n• Wyszukiwanie według znaczenia, nie tylko słów\n• Filtrowanie po kategoriach i datach\n• Szybkie znajdowanie poprzednich odpowiedzi\n\nWpisz frazę w pole wyszukiwania aby spróbować!",
                "de" or "de-de" => "Semantische Suche 🔍\n\nFinden Sie Informationen in Ihren Gesprächen:\n• Suche nach Bedeutung, nicht nur nach Wörtern\n• Filterung nach Kategorien und Daten\n• Schnelles Finden vorheriger Antworten\n\nGeben Sie eine Phrase in das Suchfeld ein, um es auszuprobieren!",
                "es" or "es-es" => "Búsqueda Semántica 🔍\n\nEncuentra información en tus conversaciones:\n• Búsqueda por significado, no solo palabras\n• Filtrado por categorías y fechas\n• Encuentra rápidamente respuestas anteriores\n\n¡Escribe una frase en el campo de búsqueda para probarlo!",
                "fr" or "fr-fr" => "Recherche Sémantique 🔍\n\nTrouvez des informations dans vos conversations :\n• Recherche par signification, pas seulement par mots\n• Filtrage par catégories et dates\n• Recherche rapide des réponses précédentes\n\nTapez une phrase dans le champ de recherche pour l'essayer !",
                _ => "Semantic Search 🔍\n\nFind information in your conversations:\n• Search by meaning, not just words\n• Filter by categories and dates\n• Quickly find previous answers\n\nType a phrase in the search field to try it!"
            };
        }

        private string GetLanguageResponse(string language)
        {
            return language.ToLower() switch
            {
                "pl" or "pl-pl" => "Obsługa Wielojęzyczna 🌍\n\nLLMClient wspiera 13 języków:\n• Angielski, Polski, Niemiecki, Hiszpański\n• Francuski, Włoski, Japoński, Koreański\n• Chiński, Rosyjski, Turecki, Holenderski, Portugalski\n\nZmień język w menu górnym lub zostaw obecny!",
                "de" or "de-de" => "Mehrsprachige Unterstützung 🌍\n\nLLMClient unterstützt 13 Sprachen:\n• Englisch, Polnisch, Deutsch, Spanisch\n• Französisch, Italienisch, Japanisch, Koreanisch\n• Chinesisch, Russisch, Türkisch, Niederländisch, Portugiesisch\n\nÄndern Sie die Sprache im oberen Menü oder behalten Sie die aktuelle bei!",
                "es" or "es-es" => "Soporte Multiidioma 🌍\n\nLLMClient soporta 13 idiomas:\n• Inglés, Polaco, Alemán, Español\n• Francés, Italiano, Japonés, Coreano\n• Chino, Ruso, Turco, Holandés, Portugués\n\n¡Cambia el idioma en el menú superior o mantén el actual!",
                "fr" or "fr-fr" => "Support Multilingue 🌍\n\nLLMClient supporte 13 langues :\n• Anglais, Polonais, Allemand, Espagnol\n• Français, Italien, Japonais, Coréen\n• Chinois, Russe, Turc, Néerlandais, Portugais\n\nChangez la langue dans le menu supérieur ou gardez l'actuelle !",
                _ => "Multi-Language Support 🌍\n\nLLMClient supports 13 languages:\n• English, Polish, German, Spanish\n• French, Italian, Japanese, Korean\n• Chinese, Russian, Turkish, Dutch, Portuguese\n\nChange language in the top menu or keep current!"
            };
        }

        private string GetLocalModelResponse(string language)
        {
            return language.ToLower() switch
            {
                "pl" or "pl-pl" => "Model Lokalny ⚡\n\nPhi-4-mini zapewnia:\n• Prywatność - wszystko na Twoim urządzeniu\n• Szybkość - odpowiedzi w czasie rzeczywistym\n• Brak kosztów API\n• Działanie offline\n\nIdealny do onboardingu i pomocy z aplikacją!",
                "de" or "de-de" => "Lokales Modell ⚡\n\nPhi-4-mini bietet:\n• Privatsphäre - alles auf Ihrem Gerät\n• Geschwindigkeit - Echtzeitantworten\n• Keine API-Kosten\n• Offline-Betrieb\n\nIdeal für Onboarding und App-Hilfe!",
                "es" or "es-es" => "Modelo Local ⚡\n\nPhi-4-mini proporciona:\n• Privacidad - todo en tu dispositivo\n• Velocidad - respuestas en tiempo real\n• Sin costos de API\n• Funcionamiento offline\n\n¡Ideal para onboarding y ayuda de la app!",
                "fr" or "fr-fr" => "Modèle Local ⚡\n\nPhi-4-mini fournit :\n• Confidentialité - tout sur votre appareil\n• Vitesse - réponses en temps réel\n• Pas de coûts d'API\n• Fonctionnement hors ligne\n\nIdéal pour l'onboarding et l'aide de l'app !",
                _ => "Local Model ⚡\n\nPhi-4-mini provides:\n• Privacy - everything on your device\n• Speed - real-time responses\n• No API costs\n• Offline operation\n\nPerfect for onboarding and app help!"
            };
        }

        private string GetDefaultResponse(string language)
        {
            return language.ToLower() switch
            {
                "pl" or "pl-pl" => "Dziękujemy za skorzystanie z LLMClient! Mamy nadzieję, że aplikacja będzie dla Ciebie użyteczna. 🚀",
                "de" or "de-de" => "Vielen Dank, dass Sie LLMClient verwenden! Wir hoffen, die App wird für Sie nützlich sein. 🚀",
                "es" or "es-es" => "¡Gracias por usar LLMClient! Esperamos que la aplicación te sea útil. 🚀",
                "fr" or "fr-fr" => "Merci d'utiliser LLMClient ! Nous espérons que l'application vous sera utile. 🚀",
                _ => "Thank you for using LLMClient! We hope the app will be useful for you. 🚀"
            };
        }
    }
}