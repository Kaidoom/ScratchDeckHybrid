namespace Scratchdeck.Services;

public static class ScratchPaletteService
{
    public const int SlotCount = 25;
    public const int FirstCustomSlot = 10;
    public const double DefaultBrushSize = 6;
    public const double MinBrushSize = 1;
    public const double MaxBrushSize = 64;

    private static readonly string[] Defaults =
    [
        "#19D9F0", "#FFC247", "#FFFFFF", "#111827", "#EF4444",
        "#F97316", "#FACC15", "#22C55E", "#3B82F6", "#A855F7",
        "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFFFF",
        "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFFFF",
        "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFFFF"
    ];

    public static List<string> CreateDefaultPalette() => [.. Defaults];

    public static List<string> Normalize(IEnumerable<string>? colors)
    {
        var supplied = colors?.Take(SlotCount).ToList() ?? [];
        var normalized = new List<string>(SlotCount);
        for (var index = 0; index < SlotCount; index++)
        {
            var candidate = index < supplied.Count ? supplied[index] : null;
            normalized.Add(ThemeService.IsValidColor(candidate)
                ? candidate!.Trim().ToUpperInvariant()
                : Defaults[index]);
        }
        return normalized;
    }

    public static bool IsValidBrushSize(double value) =>
        double.IsFinite(value) && value >= MinBrushSize && value <= MaxBrushSize;
}
