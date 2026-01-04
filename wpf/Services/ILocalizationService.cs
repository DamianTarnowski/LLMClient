using System.Globalization;

namespace WPFLLM.Services;

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    IReadOnlyList<LanguageInfo> AvailableLanguages { get; }
    
    void SetLanguage(string cultureCode);
    string GetString(string key);
    
    event EventHandler? LanguageChanged;
}

public record LanguageInfo(string Code, string NativeName, string EnglishName);
