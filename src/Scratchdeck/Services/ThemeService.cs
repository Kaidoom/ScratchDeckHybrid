using System.Windows;

namespace Scratchdeck.Services;

public static class ThemeService
{
    public const string DefaultTheme = "Cyberpunk";

    public static IReadOnlyList<string> Themes { get; } =
        ["Cyberpunk", "Amber Terminal", "Matrix", "Nord Dark"];

    public static bool IsKnownTheme(string? theme) =>
        theme is not null && Themes.Contains(theme, StringComparer.Ordinal);

    public static void Apply(string theme)
    {
        if (!IsKnownTheme(theme))
        {
            theme = DefaultTheme;
        }

        var fileName = theme.Replace(" ", string.Empty, StringComparison.Ordinal) + ".xaml";
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("Themes/", StringComparison.OrdinalIgnoreCase) == true &&
            !dictionary.Source.OriginalString.EndsWith("Base.xaml", StringComparison.OrdinalIgnoreCase));

        var replacement = new ResourceDictionary
        {
            Source = new Uri($"/Scratchdeck;component/Themes/{fileName}", UriKind.RelativeOrAbsolute)
        };

        if (existing is null)
        {
            dictionaries.Add(replacement);
        }
        else
        {
            var index = dictionaries.IndexOf(existing);
            dictionaries[index] = replacement;
        }
    }
}
