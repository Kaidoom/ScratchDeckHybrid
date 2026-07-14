using Scratchdeck.Models;
using Scratchdeck.Services;

namespace Scratchdeck.Tests;

public sealed class ThemeServiceTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "Scratchdeck.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Load_WithMissingCatalog_CreatesHardCodedFallbackCatalog()
    {
        var paths = new WorkspacePaths(_temporaryDirectory);
        var service = new ThemeService(paths);

        await service.LoadAsync();

        Assert.True(File.Exists(paths.ThemesFile));
        Assert.Contains(service.AppThemes, theme =>
            theme.Id == ThemeService.DefaultAppThemeId && theme.Title == "Cyberpunk");
        Assert.Contains(service.CodeThemes, theme =>
            theme.Id == ThemeService.DefaultCodeThemeId && theme.Title == "Cyberpunk");
    }

    [Fact]
    public async Task CustomAppAndCodeThemes_RoundTripThroughJsonCatalog()
    {
        var paths = new WorkspacePaths(_temporaryDirectory);
        var service = new ThemeService(paths);
        await service.LoadAsync();

        var appTheme = await service.UpsertAppThemeAsync(new AppThemeDefinition
        {
            Title = "Slate Glow",
            Colors = new AppThemeColors
            {
                Background = "#16191F",
                OuterEdgeTop = "#32D7FF",
                OuterEdgeBottom = "#FFBA43"
            }
        });
        var codeTheme = await service.UpsertCodeThemeAsync(new CodeThemeDefinition
        {
            Title = "Soft Contrast",
            Colors = new CodeThemeColors
            {
                Background = "#202226",
                Foreground = "#E8E9EA",
                Keyword = "#FFBA43"
            }
        });

        var reloaded = new ThemeService(paths);
        await reloaded.LoadAsync();

        Assert.Equal("slate-glow", appTheme.Id);
        Assert.Equal("soft-contrast", codeTheme.Id);
        Assert.Equal("#16191F", reloaded.FindAppTheme(appTheme.Id)!.Colors.Background);
        Assert.Equal("#FFBA43", reloaded.FindAppTheme(appTheme.Id)!.Colors.OuterEdgeBottom);
        Assert.Equal("#202226", reloaded.FindCodeTheme(codeTheme.Id)!.Colors.Background);
        Assert.Equal("#FFBA43", reloaded.FindCodeTheme(codeTheme.Id)!.Colors.Keyword);
    }

    [Fact]
    public async Task Load_WithMalformedPrimaryCatalog_RecoversBackup()
    {
        var paths = new WorkspacePaths(_temporaryDirectory);
        var service = new ThemeService(paths);
        await service.LoadAsync();

        await service.UpsertAppThemeAsync(new AppThemeDefinition
        {
            Title = "First Custom",
            Colors = new AppThemeColors()
        });
        await service.UpsertAppThemeAsync(new AppThemeDefinition
        {
            Title = "Backup Custom",
            Colors = new AppThemeColors()
        });
        await File.WriteAllTextAsync(paths.ThemesFile, "{ malformed");

        var recovered = new ThemeService(paths);
        await recovered.LoadAsync();

        Assert.NotNull(recovered.FindAppTheme("first-custom"));
        Assert.Null(recovered.FindAppTheme("backup-custom"));
        Assert.True(Directory.EnumerateFiles(paths.LogsDirectory, "*.log").Any());
    }

    [Fact]
    public async Task Load_NormalizesInvalidColorsWithoutDiscardingTheTheme()
    {
        var paths = new WorkspacePaths(_temporaryDirectory);
        Directory.CreateDirectory(paths.DataDirectory);
        await File.WriteAllTextAsync(paths.ThemesFile, """
            {
              "schemaVersion": 1,
              "appThemes": [
                {
                  "id": "custom",
                  "title": "Custom",
                  "colors": {
                    "background": "not-a-color",
                    "outerEdgeTop": "#123456",
                    "outerEdgeBottom": "#ABCDEF"
                  }
                }
              ],
              "codeThemes": []
            }
            """);
        var service = new ThemeService(paths);

        await service.LoadAsync();

        var custom = service.FindAppTheme("custom");
        Assert.NotNull(custom);
        Assert.Equal("#060910", custom.Colors.Background);
        Assert.Equal("#123456", custom.Colors.OuterEdgeTop);
        Assert.Equal(ThemeService.DefaultCodeThemeId, service.CodeThemes[0].Id);
    }

    [Theory]
    [InlineData("#12ABef", true)]
    [InlineData("#8012ABEF", true)]
    [InlineData("12ABEF", false)]
    [InlineData("#1234", false)]
    [InlineData("#GG0000", false)]
    public void IsValidColor_AcceptsOnlySupportedHexFormats(string value, bool expected)
    {
        Assert.Equal(expected, ThemeService.IsValidColor(value));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }
}
