using Scratchdeck.Models;
using Scratchdeck.Services;

namespace Scratchdeck.Tests;

public sealed class ScratchPaletteServiceTests
{
    [Fact]
    public void Normalize_AlwaysReturnsTwentyFiveValidSlots()
    {
        var palette = ScratchPaletteService.Normalize(
            ["#abcdef", "invalid", "#80123456"]);

        Assert.Equal(ScratchPaletteService.SlotCount, palette.Count);
        Assert.Equal("#ABCDEF", palette[0]);
        Assert.Equal("#FFC247", palette[1]);
        Assert.Equal("#80123456", palette[2]);
        Assert.All(palette, color => Assert.True(ThemeService.IsValidColor(color)));
    }

    [Fact]
    public void WorkspaceNormalize_RepairsScratchSettingsAndPalette()
    {
        var state = new WorkspaceState
        {
            ScratchPalette = ["bad"],
            FolderPalette = ["#abcdef", "bad"],
            Folders = new()
            {
                new WorkspaceFolder
                {
                    FolderColor = "not-a-color",
                    Tabs = new()
                    {
                        new TabDocument
                        {
                            ScratchBrushColor = "not-a-color",
                            ScratchBrushSize = double.PositiveInfinity
                        }
                    }
                }
            }
        };

        state.Normalize();

        Assert.Equal(ScratchPaletteService.SlotCount, state.ScratchPalette.Count);
        Assert.Equal(ScratchPaletteService.SlotCount, state.FolderPalette.Count);
        Assert.Equal("#ABCDEF", state.FolderPalette[0]);
        Assert.Equal("#FFC247", state.FolderPalette[1]);
        Assert.Equal(state.FolderPalette[0], state.Folders[0].FolderColor);
        Assert.Equal(state.ScratchPalette[0], state.Folders[0].Tabs[0].ScratchBrushColor);
        Assert.Equal(ScratchPaletteService.DefaultBrushSize, state.Folders[0].Tabs[0].ScratchBrushSize);
    }

    [Fact]
    public void WorkspaceNormalize_RepairsFolderStateAndKeepsIdsUnique()
    {
        var duplicateFolderId = Guid.NewGuid();
        var duplicateTabId = Guid.NewGuid();
        var state = new WorkspaceState
        {
            SelectedFolderIndex = 99,
            FolderPanelWidth = double.NaN,
            Window = new WindowPlacement { Width = 300 },
            Folders = new()
            {
                new WorkspaceFolder
                {
                    Id = duplicateFolderId,
                    Title = "  Notes  ",
                    SelectedTabIndex = 99,
                    Tabs = new()
                    {
                        new TabDocument { Id = duplicateTabId, Title = "  First  " }
                    }
                },
                new WorkspaceFolder
                {
                    Id = duplicateFolderId,
                    Title = " ",
                    Tabs = new()
                    {
                        new TabDocument { Id = duplicateTabId }
                    }
                },
                new WorkspaceFolder { Id = Guid.Empty, Tabs = new() }
            }
        };

        state.Normalize();

        Assert.Equal(WorkspaceState.CurrentSchemaVersion, state.SchemaVersion);
        Assert.Equal(2, state.SelectedFolderIndex);
        Assert.Equal(WorkspaceState.DefaultFolderPanelWidth, state.FolderPanelWidth);
        Assert.Equal(760, state.Window.Width);
        Assert.Equal("Notes", state.Folders[0].Title);
        Assert.Equal("Untitled Folder", state.Folders[1].Title);
        Assert.Single(state.Folders[2].Tabs);
        Assert.Equal("QUICK NOTE", state.Folders[2].Tabs[0].Title);
        Assert.Equal(0, state.Folders[0].SelectedTabIndex);
        Assert.Equal(state.Folders.Count, state.Folders.Select(folder => folder.Id).Distinct().Count());
        Assert.Equal(
            state.Folders.Sum(folder => folder.Tabs.Count),
            state.Folders.SelectMany(folder => folder.Tabs).Select(tab => tab.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(12, WorkspaceState.MinFolderPanelWidth)]
    [InlineData(WorkspaceState.MinFolderPanelWidth, WorkspaceState.MinFolderPanelWidth)]
    [InlineData(72, 72)]
    [InlineData(400, WorkspaceState.MaxFolderPanelWidth)]
    public void WorkspaceNormalize_AllowsAndClampsCompactFolderPanelWidths(
        double requested,
        double expected)
    {
        var state = WorkspaceState.CreateDefault();
        state.FolderPanelWidth = requested;

        state.Normalize();

        Assert.Equal(expected, state.FolderPanelWidth);
    }
}
