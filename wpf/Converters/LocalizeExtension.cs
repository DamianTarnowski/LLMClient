using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace WPFLLM.Converters;

/// <summary>
/// Markup extension for easy localization binding in XAML
/// Usage: Text="{loc:Localize Nav_Chat}"
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public class LocalizeExtension : MarkupExtension
{
    public string Key { get; set; }

    public LocalizeExtension() => Key = string.Empty;
    
    public LocalizeExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
            return string.Empty;

        var binding = new Binding($"[{Key}]")
        {
            Source = Application.Current.Resources,
            Mode = BindingMode.OneWay,
            FallbackValue = Key
        };

        return binding.ProvideValue(serviceProvider);
    }
}

/// <summary>
/// Value converter to get localized string by key
/// </summary>
public class LocalizationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (parameter is string key && Application.Current.Resources.Contains(key))
        {
            return Application.Current.Resources[key]?.ToString() ?? key;
        }
        return parameter?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
