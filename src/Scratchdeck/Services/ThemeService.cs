using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Scratchdeck.Models;

namespace Scratchdeck.Services;

public sealed class ThemeService
{
    public const string DefaultAppThemeId = "cyberpunk";
    public const string DefaultCodeThemeId = "cyberpunk-code";

    private readonly WorkspacePaths _paths;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    private ResourceDictionary? _appResources;
    private ResourceDictionary? _codeResources;

    public ThemeService(WorkspacePaths paths)
    {
        _paths = paths;
    }

    public ThemeCatalog Catalog { get; private set; } = CreateDefaultCatalog();
    public IReadOnlyList<AppThemeDefinition> AppThemes => Catalog.AppThemes;
    public IReadOnlyList<CodeThemeDefinition> CodeThemes => Catalog.CodeThemes;
    public string ThemesFilePath => _paths.ThemesFile;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.DataDirectory);

        if (!File.Exists(_paths.ThemesFile))
        {
            Catalog = CreateDefaultCatalog();
            try
            {
                await SaveCatalogAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await LogAsync("Could not create themes.json; using the hard-coded Cyberpunk fallback.", ex);
            }
            return;
        }

        var loaded = await TryLoadCatalogAsync(_paths.ThemesFile, cancellationToken)
            ?? await TryLoadCatalogAsync(_paths.ThemesBackupFile, cancellationToken);

        if (loaded is null)
        {
            Catalog = CreateDefaultCatalog();
            await LogAsync("No valid theme catalog could be loaded; using hard-coded themes.");
            return;
        }

        Catalog = NormalizeCatalog(loaded);
    }

    public void NormalizeSelections(WorkspaceState state)
    {
        if (FindAppTheme(state.AppThemeId) is null)
        {
            state.AppThemeId = AppThemes.FirstOrDefault(theme => theme.Id == DefaultAppThemeId)?.Id
                ?? AppThemes[0].Id;
        }

        if (FindCodeTheme(state.CodeThemeId) is null)
        {
            state.CodeThemeId = CodeThemes.FirstOrDefault(theme => theme.Id == DefaultCodeThemeId)?.Id
                ?? CodeThemes[0].Id;
        }
    }

    public AppThemeDefinition? FindAppTheme(string? id) =>
        AppThemes.FirstOrDefault(theme => theme.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public CodeThemeDefinition? FindCodeTheme(string? id) =>
        CodeThemes.FirstOrDefault(theme => theme.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public bool ApplyAppTheme(string? id)
    {
        var theme = FindAppTheme(id)
            ?? FindAppTheme(DefaultAppThemeId)
            ?? CreateCyberpunkAppTheme();

        if (System.Windows.Application.Current is null)
        {
            return false;
        }

        var colors = theme.Colors;
        var dictionary = new ResourceDictionary
        {
            ["WindowBackgroundBrush"] = Solid(colors.Background),
            ["SurfaceBrush"] = Solid(colors.Surface),
            ["SurfaceRaisedBrush"] = Solid(colors.RaisedSurface),
            ["BorderBrush"] = Solid(colors.Border),
            ["WindowFrameBrush"] = Gradient(colors.OuterEdgeTop, colors.OuterEdgeBottom),
            ["AccentBrush"] = Solid(colors.PrimaryAccent),
            ["AccentMutedBrush"] = Solid(colors.PrimaryAccent, 0.13),
            ["SecondaryAccentBrush"] = Solid(colors.SecondaryAccent),
            ["SecondaryAccentMutedBrush"] = Solid(colors.SecondaryAccent, 0.13),
            ["TextBrush"] = Solid(colors.Text),
            ["MutedTextBrush"] = Solid(colors.MutedText),
            ["SubtleTextBrush"] = Solid(colors.SubtleText),
            ["DangerBrush"] = Solid(colors.Danger),
            ["SuccessBrush"] = Solid(colors.Success),
            ["FocusBrush"] = Solid(colors.PrimaryAccent)
        };

        ReplaceRuntimeDictionary(ref _appResources, dictionary);
        return true;
    }

    public bool ApplyCodeTheme(string? id)
    {
        var theme = FindCodeTheme(id)
            ?? FindCodeTheme(DefaultCodeThemeId)
            ?? CreateCyberpunkCodeTheme();

        if (System.Windows.Application.Current is null)
        {
            return false;
        }

        var colors = theme.Colors;
        var dictionary = new ResourceDictionary
        {
            ["EditorBackgroundBrush"] = Solid(colors.Background),
            ["EditorForegroundBrush"] = Solid(colors.Foreground),
            ["SelectionBrush"] = Solid(colors.Selection),
            ["SyntaxKeywordBrush"] = Solid(colors.Keyword),
            ["SyntaxTypeBrush"] = Solid(colors.Type),
            ["SyntaxStringBrush"] = Solid(colors.String),
            ["SyntaxNumberBrush"] = Solid(colors.Number),
            ["SyntaxCommentBrush"] = Solid(colors.Comment),
            ["EditorLineNumberBrush"] = Solid(colors.LineNumber),
            ["EditorCaretBrush"] = Solid(colors.Caret)
        };

        ReplaceRuntimeDictionary(ref _codeResources, dictionary);
        return true;
    }

    public async Task<AppThemeDefinition> UpsertAppThemeAsync(
        AppThemeDefinition theme,
        CancellationToken cancellationToken = default)
    {
        Validate(theme.Title, theme.Colors);
        var clone = theme.Clone();
        clone.Id = string.IsNullOrWhiteSpace(clone.Id)
            ? CreateUniqueId(clone.Title, AppThemes.Select(item => item.Id), "app-theme")
            : clone.Id.Trim();

        var index = Catalog.AppThemes.FindIndex(item => item.Id.Equals(clone.Id, StringComparison.OrdinalIgnoreCase));
        var previous = index >= 0 ? Catalog.AppThemes[index] : null;
        if (index >= 0)
        {
            Catalog.AppThemes[index] = clone;
        }
        else
        {
            Catalog.AppThemes.Add(clone);
        }

        try
        {
            await SaveCatalogAsync(cancellationToken);
        }
        catch
        {
            if (index >= 0 && previous is not null)
            {
                Catalog.AppThemes[index] = previous;
            }
            else
            {
                Catalog.AppThemes.Remove(clone);
            }
            throw;
        }
        return clone;
    }

    public async Task<CodeThemeDefinition> UpsertCodeThemeAsync(
        CodeThemeDefinition theme,
        CancellationToken cancellationToken = default)
    {
        Validate(theme.Title, theme.Colors);
        var clone = theme.Clone();
        clone.Id = string.IsNullOrWhiteSpace(clone.Id)
            ? CreateUniqueId(clone.Title, CodeThemes.Select(item => item.Id), "code-theme")
            : clone.Id.Trim();

        var index = Catalog.CodeThemes.FindIndex(item => item.Id.Equals(clone.Id, StringComparison.OrdinalIgnoreCase));
        var previous = index >= 0 ? Catalog.CodeThemes[index] : null;
        if (index >= 0)
        {
            Catalog.CodeThemes[index] = clone;
        }
        else
        {
            Catalog.CodeThemes.Add(clone);
        }

        try
        {
            await SaveCatalogAsync(cancellationToken);
        }
        catch
        {
            if (index >= 0 && previous is not null)
            {
                Catalog.CodeThemes[index] = previous;
            }
            else
            {
                Catalog.CodeThemes.Remove(clone);
            }
            throw;
        }
        return clone;
    }

    public async Task SaveCatalogAsync(CancellationToken cancellationToken = default)
    {
        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_paths.DataDirectory);
            var json = JsonSerializer.Serialize(Catalog, _jsonOptions);
            await File.WriteAllTextAsync(_paths.ThemesTemporaryFile, json, cancellationToken);
            if (File.Exists(_paths.ThemesFile))
            {
                File.Replace(
                    _paths.ThemesTemporaryFile,
                    _paths.ThemesFile,
                    _paths.ThemesBackupFile,
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(_paths.ThemesTemporaryFile, _paths.ThemesFile);
            }
        }
        finally
        {
            TryDelete(_paths.ThemesTemporaryFile);
            _saveLock.Release();
        }
    }

    public static string LegacyAppThemeId(string? legacyTheme) => legacyTheme switch
    {
        "Amber Terminal" => "amber-terminal",
        "Matrix" => "matrix",
        "Nord Dark" => "nord-dark",
        _ => DefaultAppThemeId
    };

    public static string LegacyCodeThemeId(string? legacyTheme) => legacyTheme switch
    {
        "Amber Terminal" => "amber-code",
        "Matrix" => "matrix-code",
        "Nord Dark" => "nord-code",
        _ => DefaultCodeThemeId
    };

    public static bool IsValidColor(string? value)
    {
        var candidate = value?.Trim();
        if (candidate is null ||
            (candidate.Length != 7 && candidate.Length != 9) ||
            candidate[0] != '#' ||
            candidate.Skip(1).Any(character => !Uri.IsHexDigit(character)))
        {
            return false;
        }

        try
        {
            return ColorConverter.ConvertFromString(candidate) is Color;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task<ThemeCatalog?> TryLoadCatalogAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var catalog = JsonSerializer.Deserialize<ThemeCatalog>(json, _jsonOptions)
                ?? throw new JsonException("The theme catalog was empty.");
            if (catalog.SchemaVersion > ThemeCatalog.CurrentSchemaVersion)
            {
                throw new JsonException($"Unsupported theme schema {catalog.SchemaVersion}.");
            }
            return catalog;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await LogAsync($"Could not load theme catalog '{path}'.", ex);
            return null;
        }
    }

    private static ThemeCatalog NormalizeCatalog(ThemeCatalog catalog)
    {
        var fallback = CreateDefaultCatalog();
        var fallbackApp = fallback.AppThemes[0];
        var fallbackCode = fallback.CodeThemes[0];

        catalog.AppThemes = (catalog.AppThemes ?? [])
            .Where(theme => theme is not null)
            .GroupBy(theme => theme.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => Normalize(group.First(), fallbackApp))
            .ToList();
        catalog.CodeThemes = (catalog.CodeThemes ?? [])
            .Where(theme => theme is not null)
            .GroupBy(theme => theme.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => Normalize(group.First(), fallbackCode))
            .ToList();

        if (catalog.AppThemes.Count == 0)
        {
            catalog.AppThemes.Add(fallbackApp);
        }
        if (catalog.CodeThemes.Count == 0)
        {
            catalog.CodeThemes.Add(fallbackCode);
        }
        catalog.SchemaVersion = ThemeCatalog.CurrentSchemaVersion;
        return catalog;
    }

    private static AppThemeDefinition Normalize(AppThemeDefinition theme, AppThemeDefinition fallback)
    {
        theme.Id = string.IsNullOrWhiteSpace(theme.Id) ? Guid.NewGuid().ToString("N") : theme.Id.Trim();
        theme.Title = string.IsNullOrWhiteSpace(theme.Title) ? "Untitled App Theme" : theme.Title.Trim();
        theme.Colors ??= fallback.Colors.Clone();
        var colors = theme.Colors;
        var source = fallback.Colors;
        colors.Background = ValidOr(colors.Background, source.Background);
        colors.Surface = ValidOr(colors.Surface, source.Surface);
        colors.RaisedSurface = ValidOr(colors.RaisedSurface, source.RaisedSurface);
        colors.Border = ValidOr(colors.Border, source.Border);
        colors.OuterEdgeTop = ValidOr(colors.OuterEdgeTop, source.OuterEdgeTop);
        colors.OuterEdgeBottom = ValidOr(colors.OuterEdgeBottom, source.OuterEdgeBottom);
        colors.PrimaryAccent = ValidOr(colors.PrimaryAccent, source.PrimaryAccent);
        colors.SecondaryAccent = ValidOr(colors.SecondaryAccent, source.SecondaryAccent);
        colors.Text = ValidOr(colors.Text, source.Text);
        colors.MutedText = ValidOr(colors.MutedText, source.MutedText);
        colors.SubtleText = ValidOr(colors.SubtleText, source.SubtleText);
        colors.Danger = ValidOr(colors.Danger, source.Danger);
        colors.Success = ValidOr(colors.Success, source.Success);
        return theme;
    }

    private static CodeThemeDefinition Normalize(CodeThemeDefinition theme, CodeThemeDefinition fallback)
    {
        theme.Id = string.IsNullOrWhiteSpace(theme.Id) ? Guid.NewGuid().ToString("N") : theme.Id.Trim();
        theme.Title = string.IsNullOrWhiteSpace(theme.Title) ? "Untitled Code Theme" : theme.Title.Trim();
        theme.Colors ??= fallback.Colors.Clone();
        var colors = theme.Colors;
        var source = fallback.Colors;
        colors.Background = ValidOr(colors.Background, source.Background);
        colors.Foreground = ValidOr(colors.Foreground, source.Foreground);
        colors.Selection = ValidOr(colors.Selection, source.Selection);
        colors.Keyword = ValidOr(colors.Keyword, source.Keyword);
        colors.Type = ValidOr(colors.Type, source.Type);
        colors.String = ValidOr(colors.String, source.String);
        colors.Number = ValidOr(colors.Number, source.Number);
        colors.Comment = ValidOr(colors.Comment, source.Comment);
        colors.LineNumber = ValidOr(colors.LineNumber, source.LineNumber);
        colors.Caret = ValidOr(colors.Caret, source.Caret);
        return theme;
    }

    private static string ValidOr(string? value, string fallback) => IsValidColor(value) ? value! : fallback;

    private static void Validate(string title, AppThemeColors colors)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Theme title is required.", nameof(title));
        }

        ValidateColors(
            colors.Background, colors.Surface, colors.RaisedSurface, colors.Border,
            colors.OuterEdgeTop, colors.OuterEdgeBottom, colors.PrimaryAccent,
            colors.SecondaryAccent, colors.Text, colors.MutedText, colors.SubtleText,
            colors.Danger, colors.Success);
    }

    private static void Validate(string title, CodeThemeColors colors)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Theme title is required.", nameof(title));
        }

        ValidateColors(
            colors.Background, colors.Foreground, colors.Selection, colors.Keyword,
            colors.Type, colors.String, colors.Number, colors.Comment,
            colors.LineNumber, colors.Caret);
    }

    private static void ValidateColors(params string[] colors)
    {
        if (colors.Any(color => !IsValidColor(color)))
        {
            throw new ArgumentException("All theme colors must be valid #RRGGBB or #AARRGGBB values.");
        }
    }

    private static string CreateUniqueId(string title, IEnumerable<string> existingIds, string fallback)
    {
        var slug = new string(title.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }
        slug = slug.Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = fallback;
        }

        var used = existingIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = slug;
        var suffix = 2;
        while (used.Contains(candidate))
        {
            candidate = $"{slug}-{suffix++}";
        }
        return candidate;
    }

    private static SolidColorBrush Solid(string value, double opacity = 1)
    {
        var brush = new SolidColorBrush(ParseColor(value)) { Opacity = opacity };
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush Gradient(string top, string bottom)
    {
        var brush = new LinearGradientBrush(
            ParseColor(top),
            ParseColor(bottom),
            new Point(0.5, 0),
            new Point(0.5, 1));
        brush.Freeze();
        return brush;
    }

    private static Color ParseColor(string value) =>
        ColorConverter.ConvertFromString(value) is Color color ? color : Colors.Transparent;

    private static void ReplaceRuntimeDictionary(
        ref ResourceDictionary? current,
        ResourceDictionary replacement)
    {
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        if (current is null || !dictionaries.Contains(current))
        {
            dictionaries.Add(replacement);
        }
        else
        {
            dictionaries[dictionaries.IndexOf(current)] = replacement;
        }
        current = replacement;
    }

    private async Task LogAsync(string message, Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(_paths.LogsDirectory);
            var line = $"[{DateTimeOffset.Now:O}] {message}";
            if (exception is not null)
            {
                line += $" {exception.GetType().Name}: {exception.Message}";
            }
            await File.AppendAllTextAsync(
                Path.Combine(_paths.LogsDirectory, $"scratchdeck-{DateTime.UtcNow:yyyyMMdd}.log"),
                line + Environment.NewLine);
        }
        catch
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static ThemeCatalog CreateDefaultCatalog() => new()
    {
        AppThemes =
        [
            CreateCyberpunkAppTheme(),
            new AppThemeDefinition
            {
                Id = "amber-terminal", Title = "Amber Terminal",
                Colors = new AppThemeColors
                {
                    Background = "#0C0905", Surface = "#151008", RaisedSurface = "#1D160B",
                    Border = "#493519", OuterEdgeTop = "#FFB21D", OuterEdgeBottom = "#FF6B2D",
                    PrimaryAccent = "#FFB21D", SecondaryAccent = "#FF6B2D", Text = "#FFF3D6",
                    MutedText = "#B9A47A", SubtleText = "#716044", Danger = "#FF6254", Success = "#D4D954"
                }
            },
            new AppThemeDefinition
            {
                Id = "matrix", Title = "Matrix",
                Colors = new AppThemeColors
                {
                    Background = "#050A07", Surface = "#08120C", RaisedSurface = "#0D1B13",
                    Border = "#21442C", OuterEdgeTop = "#31E873", OuterEdgeBottom = "#16CDB1",
                    PrimaryAccent = "#31E873", SecondaryAccent = "#16CDB1", Text = "#E2F8E9",
                    MutedText = "#83AE90", SubtleText = "#4D7158", Danger = "#F05D6C", Success = "#31E873"
                }
            },
            new AppThemeDefinition
            {
                Id = "nord-dark", Title = "Nord Dark",
                Colors = new AppThemeColors
                {
                    Background = "#202630", Surface = "#252D38", RaisedSurface = "#2E3845",
                    Border = "#465365", OuterEdgeTop = "#88C0D0", OuterEdgeBottom = "#B48EAD",
                    PrimaryAccent = "#88C0D0", SecondaryAccent = "#B48EAD", Text = "#ECEFF4",
                    MutedText = "#A5B0C0", SubtleText = "#687589", Danger = "#BF616A", Success = "#A3BE8C"
                }
            }
        ],
        CodeThemes =
        [
            CreateCyberpunkCodeTheme(),
            new CodeThemeDefinition
            {
                Id = "amber-code", Title = "Amber Terminal",
                Colors = new CodeThemeColors
                {
                    Background = "#0B0906", Foreground = "#F4E7CA", Selection = "#684719",
                    Keyword = "#FF8A35", Type = "#F1B454", String = "#E5C965", Number = "#FFD457",
                    Comment = "#7B6C50", LineNumber = "#716044", Caret = "#FFF3D6"
                }
            },
            new CodeThemeDefinition
            {
                Id = "matrix-code", Title = "Matrix",
                Colors = new CodeThemeColors
                {
                    Background = "#050B07", Foreground = "#D8F4DF", Selection = "#185D32",
                    Keyword = "#35F394", Type = "#48DDBE", String = "#A5E075", Number = "#C8EE68",
                    Comment = "#557961", LineNumber = "#4D7158", Caret = "#E2F8E9"
                }
            },
            new CodeThemeDefinition
            {
                Id = "nord-code", Title = "Nord Dark",
                Colors = new CodeThemeColors
                {
                    Background = "#1D232C", Foreground = "#E5E9F0", Selection = "#3F5B72",
                    Keyword = "#B48EAD", Type = "#88C0D0", String = "#A3BE8C", Number = "#D08770",
                    Comment = "#78869A", LineNumber = "#687589", Caret = "#ECEFF4"
                }
            }
        ]
    };

    private static AppThemeDefinition CreateCyberpunkAppTheme() => new()
    {
        Id = DefaultAppThemeId,
        Title = "Cyberpunk",
        Colors = new AppThemeColors()
    };

    private static CodeThemeDefinition CreateCyberpunkCodeTheme() => new()
    {
        Id = DefaultCodeThemeId,
        Title = "Cyberpunk",
        Colors = new CodeThemeColors()
    };
}
