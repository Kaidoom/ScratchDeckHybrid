using System.Collections.ObjectModel;
using Scratchdeck.Services;

namespace Scratchdeck.Models;

public sealed class WorkspaceState
{
    public const int CurrentSchemaVersion = 5;
    public const double DefaultFolderPanelWidth = 176;
    public const double MinFolderPanelWidth = 132;
    public const double MaxFolderPanelWidth = 300;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ObservableCollection<WorkspaceFolder> Folders { get; set; } = [];
    public int SelectedFolderIndex { get; set; }
    public double FolderPanelWidth { get; set; } = DefaultFolderPanelWidth;
    public WindowPlacement Window { get; set; } = new();
    public bool Topmost { get; set; }
    public bool AutoWrap { get; set; } = true;
    public string AppThemeId { get; set; } = ThemeService.DefaultAppThemeId;
    public string CodeThemeId { get; set; } = ThemeService.DefaultCodeThemeId;
    public List<string> ScratchPalette { get; set; } = ScratchPaletteService.CreateDefaultPalette();

    public static WorkspaceState CreateDefault()
    {
        var state = new WorkspaceState();
        state.Folders.Add(CreateDefaultFolder());
        return state;
    }

    public void Normalize()
    {
        Folders ??= [];
        if (Folders.Count == 0)
        {
            Folders.Add(CreateDefaultFolder());
        }

        SelectedFolderIndex = Math.Clamp(SelectedFolderIndex, 0, Folders.Count - 1);
        FolderPanelWidth = double.IsFinite(FolderPanelWidth)
            ? Math.Clamp(FolderPanelWidth, MinFolderPanelWidth, MaxFolderPanelWidth)
            : DefaultFolderPanelWidth;
        AppThemeId = string.IsNullOrWhiteSpace(AppThemeId) ? ThemeService.DefaultAppThemeId : AppThemeId;
        CodeThemeId = string.IsNullOrWhiteSpace(CodeThemeId) ? ThemeService.DefaultCodeThemeId : CodeThemeId;
        ScratchPalette = ScratchPaletteService.Normalize(ScratchPalette);
        Window ??= new WindowPlacement();
        Window.Width = double.IsFinite(Window.Width) ? Math.Clamp(Window.Width, 760, 4000) : 860;
        Window.Height = double.IsFinite(Window.Height) ? Math.Clamp(Window.Height, 280, 2500) : 480;

        var folderIds = new HashSet<Guid>();
        var tabIds = new HashSet<Guid>();
        foreach (var folder in Folders)
        {
            if (folder.Id == Guid.Empty || !folderIds.Add(folder.Id))
            {
                folder.Id = CreateUniqueId(folderIds);
            }

            folder.Title = string.IsNullOrWhiteSpace(folder.Title) ? "Untitled Folder" : folder.Title.Trim();
            folder.Tabs ??= [];
            if (folder.Tabs.Count == 0)
            {
                folder.Tabs.Add(new TabDocument { Title = "QUICK NOTE" });
            }

            folder.SelectedTabIndex = Math.Clamp(folder.SelectedTabIndex, 0, folder.Tabs.Count - 1);
            foreach (var tab in folder.Tabs)
            {
                if (tab.Id == Guid.Empty || !tabIds.Add(tab.Id))
                {
                    tab.Id = CreateUniqueId(tabIds);
                }

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

        SchemaVersion = CurrentSchemaVersion;
    }

    private static WorkspaceFolder CreateDefaultFolder()
    {
        var folder = new WorkspaceFolder { Title = "Default" };
        folder.Tabs.Add(new TabDocument { Title = "QUICK NOTE" });
        return folder;
    }

    private static Guid CreateUniqueId(HashSet<Guid> usedIds)
    {
        Guid id;
        do
        {
            id = Guid.NewGuid();
        }
        while (!usedIds.Add(id));

        return id;
    }
}
