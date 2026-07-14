namespace Scratchdeck.Models;

public sealed class ThemeCatalog
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<AppThemeDefinition> AppThemes { get; set; } = [];
    public List<CodeThemeDefinition> CodeThemes { get; set; } = [];
}

public sealed class AppThemeDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = "Untitled App Theme";
    public string FontFamily { get; set; } = "Segoe UI Variable Text, Segoe UI";
    public double FontSize { get; set; } = 11;
    public AppThemeColors Colors { get; set; } = new();

    public AppThemeDefinition Clone() => new()
    {
        Id = Id,
        Title = Title,
        FontFamily = FontFamily,
        FontSize = FontSize,
        Colors = Colors.Clone()
    };

    public override string ToString() => Title;
}

public sealed class CodeThemeDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = "Untitled Code Theme";
    public string FontFamily { get; set; } = "Cascadia Mono, Consolas";
    public double FontSize { get; set; } = 13.5;
    public CodeThemeColors Colors { get; set; } = new();

    public CodeThemeDefinition Clone() => new()
    {
        Id = Id,
        Title = Title,
        FontFamily = FontFamily,
        FontSize = FontSize,
        Colors = Colors.Clone()
    };

    public override string ToString() => Title;
}

public sealed class AppThemeColors
{
    public string Background { get; set; } = "#060910";
    public string Surface { get; set; } = "#0A1019";
    public string RaisedSurface { get; set; } = "#101925";
    public string Border { get; set; } = "#253549";
    public string OuterEdgeTop { get; set; } = "#19D9F0";
    public string OuterEdgeBottom { get; set; } = "#FFC247";
    public string PrimaryAccent { get; set; } = "#19D9F0";
    public string SecondaryAccent { get; set; } = "#FFC247";
    public string Text { get; set; } = "#EAF4F8";
    public string MutedText { get; set; } = "#91A5B8";
    public string SubtleText { get; set; } = "#586A7E";
    public string Danger { get; set; } = "#FF5B73";
    public string Success { get; set; } = "#FFC247";

    public AppThemeColors Clone() => (AppThemeColors)MemberwiseClone();
}

public sealed class CodeThemeColors
{
    public string Background { get; set; } = "#070C14";
    public string Foreground { get; set; } = "#E4EBF0";
    public string Selection { get; set; } = "#215C71";
    public string Keyword { get; set; } = "#50C9FF";
    public string Type { get; set; } = "#7AE7FF";
    public string String { get; set; } = "#E2D28A";
    public string Number { get; set; } = "#FFC247";
    public string Comment { get; set; } = "#68798E";
    public string LineNumber { get; set; } = "#586A7E";
    public string Caret { get; set; } = "#EAF4F8";

    public CodeThemeColors Clone() => (CodeThemeColors)MemberwiseClone();
}
