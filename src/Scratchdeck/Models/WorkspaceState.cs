using System.Collections.ObjectModel;
using Scratchdeck.Services;

namespace Scratchdeck.Models;

public sealed class WorkspaceState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ObservableCollection<TabDocument> Tabs { get; set; } = [];
    public int SelectedTabIndex { get; set; }
    public WindowPlacement Window { get; set; } = new();
    public bool Topmost { get; set; }
    public string Theme { get; set; } = ThemeService.DefaultTheme;

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
        Theme = ThemeService.IsKnownTheme(Theme) ? Theme : ThemeService.DefaultTheme;
        Window.Width = Math.Clamp(Window.Width, 420, 4000);
        Window.Height = Math.Clamp(Window.Height, 280, 2500);

        foreach (var tab in Tabs)
        {
            tab.Id = tab.Id == Guid.Empty ? Guid.NewGuid() : tab.Id;
            tab.Title = string.IsNullOrWhiteSpace(tab.Title) ? "Untitled" : tab.Title.Trim();
            tab.Content ??= string.Empty;
            tab.SyntaxMode = SyntaxHighlightingService.IsKnownMode(tab.SyntaxMode)
                ? tab.SyntaxMode
                : "Plain Text";
        }
    }
}
