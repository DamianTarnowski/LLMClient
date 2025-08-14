using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Reflection;

namespace LLMClient.Services
{
    public interface ILocalizationService : INotifyPropertyChanged
    {
        string this[string key] { get; }
        string GetString(string key);
        void SetCulture(string cultureName);
        string CurrentCulture { get; }
        List<LanguageOption> AvailableLanguages { get; }
    }

    public class LanguageOption
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string NativeName { get; set; } = string.Empty;
    }

    public class LocalizationService : ILocalizationService
    {
        private readonly ResourceManager _resourceManager;
        private CultureInfo _currentCulture;

        public event PropertyChangedEventHandler? PropertyChanged;

        public LocalizationService()
        {
            _resourceManager = new ResourceManager("LLMClient.Resources.Strings", Assembly.GetExecutingAssembly());
            
            // Detect system language or load saved preference
            var savedLanguage = Preferences.Get("AppLanguage", "");
            if (string.IsNullOrEmpty(savedLanguage))
            {
                // Auto-detect system language
                var systemCulture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
                var supportedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code.StartsWith(systemCulture));
                savedLanguage = supportedLanguage?.Code ?? "en-US"; // Fallback to English
            }
            
            _currentCulture = new CultureInfo(savedLanguage);
        }

        public string CurrentCulture => _currentCulture.Name;

        public List<LanguageOption> AvailableLanguages { get; } = new List<LanguageOption>
        {
            // Top 30 najpopularniejszych języków świata (LTR only)
            new() { Code = "en-US", DisplayName = "English", NativeName = "English" },
            new() { Code = "zh-CN", DisplayName = "Chinese (Simplified)", NativeName = "中文 (简体)" },
            new() { Code = "es-ES", DisplayName = "Spanish", NativeName = "Español" },
            new() { Code = "hi-IN", DisplayName = "Hindi", NativeName = "हिन्दी" },
            new() { Code = "pt-BR", DisplayName = "Portuguese", NativeName = "Português" },
            new() { Code = "ru-RU", DisplayName = "Russian", NativeName = "Русский" },
            new() { Code = "ja-JP", DisplayName = "Japanese", NativeName = "日本語" },
            new() { Code = "de-DE", DisplayName = "German", NativeName = "Deutsch" },
            new() { Code = "ko-KR", DisplayName = "Korean", NativeName = "한국어" },
            new() { Code = "fr-FR", DisplayName = "French", NativeName = "Français" },
            new() { Code = "tr-TR", DisplayName = "Turkish", NativeName = "Türkçe" },
            new() { Code = "it-IT", DisplayName = "Italian", NativeName = "Italiano" },
            new() { Code = "zh-TW", DisplayName = "Chinese (Traditional)", NativeName = "中文 (繁體)" },
            new() { Code = "pl-PL", DisplayName = "Polish", NativeName = "Polski" },
            new() { Code = "nl-NL", DisplayName = "Dutch", NativeName = "Nederlands" },
            new() { Code = "sv-SE", DisplayName = "Swedish", NativeName = "Svenska" },
            new() { Code = "da-DK", DisplayName = "Danish", NativeName = "Dansk" },
            new() { Code = "no-NO", DisplayName = "Norwegian", NativeName = "Norsk" },
            new() { Code = "fi-FI", DisplayName = "Finnish", NativeName = "Suomi" },
            new() { Code = "cs-CZ", DisplayName = "Czech", NativeName = "Čeština" },
            new() { Code = "hu-HU", DisplayName = "Hungarian", NativeName = "Magyar" },
            new() { Code = "ro-RO", DisplayName = "Romanian", NativeName = "Română" },
            new() { Code = "uk-UA", DisplayName = "Ukrainian", NativeName = "Українська" },
            new() { Code = "bg-BG", DisplayName = "Bulgarian", NativeName = "Български" },
            new() { Code = "hr-HR", DisplayName = "Croatian", NativeName = "Hrvatski" },
            new() { Code = "sk-SK", DisplayName = "Slovak", NativeName = "Slovenčina" },
            new() { Code = "sl-SI", DisplayName = "Slovenian", NativeName = "Slovenščina" },
            new() { Code = "et-EE", DisplayName = "Estonian", NativeName = "Eesti" },
            new() { Code = "lv-LV", DisplayName = "Latvian", NativeName = "Latviešu" },
            new() { Code = "lt-LT", DisplayName = "Lithuanian", NativeName = "Lietuvių" }
        };

        public string this[string key] => GetString(key);

        public string GetString(string key)
        {
            try
            {
                return _resourceManager.GetString(key, _currentCulture) ?? key;
            }
            catch
            {
                return key; // Return key if resource not found
            }
        }

        public void SetCulture(string cultureName)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalizationService] SetCulture called with: {cultureName}");
            System.Diagnostics.Debug.WriteLine($"[LocalizationService] Current culture: {_currentCulture.Name}");
            
            if (_currentCulture.Name == cultureName) 
            {
                System.Diagnostics.Debug.WriteLine($"[LocalizationService] Culture is already set to {cultureName}, skipping");
                return;
            }
            
            try
            {
                _currentCulture = new CultureInfo(cultureName);
                Preferences.Set("AppLanguage", cultureName);
                
                System.Diagnostics.Debug.WriteLine($"[LocalizationService] Culture set to: {_currentCulture.Name}");
                System.Diagnostics.Debug.WriteLine($"[LocalizationService] Testing translation - NewConversation: {GetString("NewConversation")}");
                
                // Notify all bindings that strings have changed
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
                
                // Force refresh of UI by triggering a broader property change
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
                
                System.Diagnostics.Debug.WriteLine($"[LocalizationService] Language changed successfully to: {cultureName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalizationService] Error setting culture {cultureName}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[LocalizationService] Exception: {ex}");
            }
        }
    }
}