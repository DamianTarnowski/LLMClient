using System.Windows;

namespace WPFLLM.Services;

public class LocalizationService : ILocalizationService
{
    private ResourceDictionary? _currentDictionary;
    private string _currentLanguage = "en-US";
    
    public string CurrentLanguage => _currentLanguage;
    
    public IReadOnlyList<LanguageInfo> AvailableLanguages { get; } = new List<LanguageInfo>
    {
        new("en-US", "English", "English"),
        new("pl-PL", "Polski", "Polish"),
        new("de-DE", "Deutsch", "German"),
        new("fr-FR", "Français", "French"),
        new("es-ES", "Español", "Spanish"),
        new("it-IT", "Italiano", "Italian"),
        new("pt-PT", "Português", "Portuguese"),
        new("nl-NL", "Nederlands", "Dutch"),
        new("ru-RU", "Русский", "Russian"),
        new("uk-UA", "Українська", "Ukrainian"),
        new("zh-CN", "简体中文", "Chinese (Simplified)")
    };

    public event EventHandler? LanguageChanged;

    public LocalizationService()
    {
        // Load default language
        SetLanguage("en-US");
    }

    public void SetLanguage(string cultureCode)
    {
        if (_currentLanguage == cultureCode && _currentDictionary != null)
            return;

        var dict = new ResourceDictionary();
        
        try
        {
            dict.Source = new Uri($"pack://application:,,,/Resources/Strings.{cultureCode}.xaml");
        }
        catch
        {
            // Fallback to English
            dict.Source = new Uri("pack://application:,,,/Resources/Strings.en-US.xaml");
            cultureCode = "en-US";
        }

        // Remove old dictionary if exists
        if (_currentDictionary != null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(_currentDictionary);
        }

        // Add new dictionary
        Application.Current.Resources.MergedDictionaries.Add(dict);
        _currentDictionary = dict;
        _currentLanguage = cultureCode;

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetString(string key)
    {
        if (Application.Current.Resources.Contains(key))
        {
            return Application.Current.Resources[key]?.ToString() ?? key;
        }
        return key;
    }
}
