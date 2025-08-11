using System.Globalization;

namespace LLMClient.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive ? "#5865F2" : "#40444B";
            }
            return "#40444B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IsNotNullConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class HighlightTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values?.Length != 2 || values[0] is not string text || values[1] is not string searchTerm)
                return CreatePlainFormattedString(values?[0]?.ToString() ?? string.Empty);

            if (string.IsNullOrWhiteSpace(searchTerm))
                return CreatePlainFormattedString(text);

            try
            {
                return CreateHighlightedFormattedString(text, searchTerm);
            }
            catch
            {
                return CreatePlainFormattedString(text);
            }
        }

        private static FormattedString CreatePlainFormattedString(string text)
        {
            var formattedString = new FormattedString();
            formattedString.Spans.Add(new Span { Text = text });
            return formattedString;
        }

        private static FormattedString CreateHighlightedFormattedString(string text, string searchTerm)
        {
            var formattedString = new FormattedString();
            
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchTerm))
            {
                formattedString.Spans.Add(new Span { Text = text ?? string.Empty });
                return formattedString;
            }

            var comparison = StringComparison.OrdinalIgnoreCase;
            var currentIndex = 0;
            var searchIndex = text.IndexOf(searchTerm, comparison);

            while (searchIndex >= 0)
            {
                // Dodaj tekst przed wyszukiwaną frazą
                if (searchIndex > currentIndex)
                {
                    var beforeText = text.Substring(currentIndex, searchIndex - currentIndex);
                    formattedString.Spans.Add(new Span { Text = beforeText });
                }

                // Dodaj podświetloną frazę
                var highlightedText = text.Substring(searchIndex, searchTerm.Length);
                formattedString.Spans.Add(new Span 
                { 
                    Text = highlightedText,
                    BackgroundColor = Colors.Yellow,
                    FontAttributes = FontAttributes.Bold
                });

                currentIndex = searchIndex + searchTerm.Length;
                searchIndex = text.IndexOf(searchTerm, currentIndex, comparison);
            }

            // Dodaj pozostały tekst
            if (currentIndex < text.Length)
            {
                var remainingText = text.Substring(currentIndex);
                formattedString.Spans.Add(new Span { Text = remainingText });
            }

            return formattedString;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IsNotNullOrEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return !string.IsNullOrEmpty(str);
            }
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;

            return true;
        }
    }

    public class ZeroToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)value == 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ZeroToInverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)value != 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TextTruncateConverter : IValueConverter
    {
        public int MaxLength { get; set; } = 100;
        public string Suffix { get; set; } = "...";

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string text || string.IsNullOrEmpty(text))
                return string.Empty;

            // Usuń znaki nowej linii i tabulatory - zastąp spacjami
            text = text.Replace("\n", " ").Replace("\r", " ").Replace("\t", " ");
            
            // Usuń wielokrotne spacje
            while (text.Contains("  "))
            {
                text = text.Replace("  ", " ");
            }
            
            text = text.Trim();

            // Jeśli parameter jest podany, użyj go jako MaxLength
            if (parameter is string paramStr && int.TryParse(paramStr, out int paramLength))
            {
                MaxLength = paramLength;
            }

            if (text.Length <= MaxLength)
                return text;

            // Obetnij na ostatnim słowie przed limitem
            var truncated = text.Substring(0, MaxLength);
            var lastSpaceIndex = truncated.LastIndexOf(' ');
            
            if (lastSpaceIndex > MaxLength * 0.7) // Jeśli ostatnia spacja jest w rozsądnej odległości
            {
                truncated = truncated.Substring(0, lastSpaceIndex);
            }

            return truncated + Suffix;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CollectionEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Collections.ICollection collection)
            {
                return collection.Count == 0;
            }
            if (value is System.Collections.IEnumerable enumerable)
            {
                return !enumerable.Cast<object>().Any();
            }
            return true; // Jeśli null, to traktujemy jako pustą kolekcję
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CollectionNotEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Collections.ICollection collection)
            {
                return collection.Count > 0;
            }
            if (value is System.Collections.IEnumerable enumerable)
            {
                return enumerable.Cast<object>().Any();
            }
            return false; // Jeśli null, to traktujemy jako pustą kolekcję
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToThemeIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isLightTheme)
            {
                return isLightTheme ? "🌙" : "☀️"; // Jeśli jasny motyw, pokaż księżyc (aby przełączyć na ciemny), jeśli ciemny, pokaż słońce
            }
            return "🌙";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CategoryToColorConverter : IValueConverter
    {
        private static readonly Dictionary<string, string> CategoryColors = new()
        {
            { "", "#6B7280" },
            { "osobiste", "#EF4444" },
            { "preferencje", "#F59E0B" },
            { "cele", "#10B981" },
            { "techniczne", "#3B82F6" },
            { "praca", "#8B5CF6" },
            { "hobby", "#EC4899" },
            { "zdrowie", "#06B6D4" },
            { "rodzina", "#84CC16" },
            { "finanse", "#F97316" }
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var category = value?.ToString() ?? string.Empty;
            var parameterCategory = parameter?.ToString() ?? category;
            
            if (CategoryColors.TryGetValue(parameterCategory.ToLower(), out var color))
                return color;
                
            // Generuj kolor na podstawie hash kodu kategorii
            var hash = parameterCategory.GetHashCode();
            var colors = new[] { "#EF4444", "#F59E0B", "#10B981", "#3B82F6", "#8B5CF6", "#EC4899", "#06B6D4", "#84CC16", "#F97316" };
            return colors[Math.Abs(hash) % colors.Length];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}