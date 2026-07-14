using System.Collections.ObjectModel;
using Scratchdeck.Services;

namespace Scratchdeck.Models;

public sealed class WorkspaceState
{
    public const int CurrentSchemaVersion = 4;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ObservableCollection<TabDocument> Tabs { get; set; } = [];
    public int SelectedTabIndex { get; set; }
    public WindowPlacement Window { get; set; } = new();
    public bool Topmost { get; set; }
    public bool AutoWrap { get; set; } = true;
    public string AppThemeId { get; set; } = ThemeService.DefaultAppThemeId;
    public string CodeThemeId { get; set; } = ThemeService.DefaultCodeThemeId;
    public List<string> ScratchPalette { get; set; } = ScratchPaletteService.CreateDefaultPalette();

    public static WorkspaceState CreateDefault()
    {
        var state = new WorkspaceState();
        state.Tabs.Add(new TabDocument { Title = "QUICK NOTE" });
        return state;
    }

    public void Normalize()
    {
        if (Tabs.Count == 0)
        {
            Tabs.Add(new TabDocument { Title = "QUICK NOTE" });
        }

        SelectedTabIndex = Math.Clamp(SelectedTabIndex, 0, Tabs.Count - 1);
        AppThemeId = string.IsNullOrWhiteSpace(AppThemeId) ? ThemeService.DefaultAppThemeId : AppThemeId;
        CodeThemeId = string.IsNullOrWhiteSpace(CodeThemeId) ? ThemeService.DefaultCodeThemeId : CodeThemeId;
        ScratchPalette = ScratchPaletteService.Normalize(ScratchPalette);
        Window.Width = Math.Clamp(Window.Width, 420, 4000);
        Window.Height = Math.Clamp(Window.Height, 280, 2500);

        foreach (var tab in Tabs)
        {
            tab.Id = tab.Id == Guid.Empty ? Guid.NewGuid() : tab.Id;
            tab.Title = string.IsNullOrWhiteSpace(tab.Title) ? "Untitled" : tab.Title.Trim();
            tab.Content ??= string.Empty;
            tab.ScratchData ??= string.Empty;
            tab.ScratchBrushColor = ThemeService.IsValidColor(tab.ScratchBrushColor)
                ? tab.ScratchBrushColor.Trim().ToUpperInvariant()
                : ScratchPalette[0];
            tab.ScratchBrushSize = ScratchPaletteService.IsValidBrushSize(tab.ScratchBrushSize)
                ? tab.ScratchBrushSize
                : ScratchPaletteService.DefaultBrushSize;
            tab.SyntaxMode = SyntaxHighlightingService.IsKnownMode(tab.SyntaxMode)
                ? tab.SyntaxMode
                : "Plain Text";
        }
    }
}
