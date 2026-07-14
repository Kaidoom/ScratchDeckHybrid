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
            Tabs = new()
            {
                new TabDocument
                {
                    ScratchBrushColor = "not-a-color",
                    ScratchBrushSize = double.PositiveInfinity
                }
            }
        };

        state.Normalize();

        Assert.Equal(ScratchPaletteService.SlotCount, state.ScratchPalette.Count);
        Assert.Equal(state.ScratchPalette[0], state.Tabs[0].ScratchBrushColor);
        Assert.Equal(ScratchPaletteService.DefaultBrushSize, state.Tabs[0].ScratchBrushSize);
    }
}
