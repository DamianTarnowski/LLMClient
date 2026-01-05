using LLMClient.Services;

namespace LLMClient.Helpers;

/// <summary>
/// XAML Markup Extension for localization.
/// Usage: Text="{helpers:Translate NewConversation}"
/// </summary>
[ContentProperty(nameof(Key))]
public class TranslateExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = string.Empty;

    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding
        {
            Mode = BindingMode.OneWay,
            Path = $"[{Key}]",
            Source = Application.Current?.Handler?.MauiContext?.Services.GetService<ILocalizationService>()
        };
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
    {
        return ProvideValue(serviceProvider);
    }
}
