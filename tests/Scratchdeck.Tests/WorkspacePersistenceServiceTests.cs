using System.Text.Json;
using Scratchdeck.Models;
using Scratchdeck.Services;

namespace Scratchdeck.Tests;

public sealed class WorkspacePersistenceServiceTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "Scratchdeck.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAndLoad_RoundTripsWorkspaceAndKeepsProtectedTextOutOfJson()
    {
        var paths = new WorkspacePaths(_temporaryDirectory);
        var service = new WorkspacePersistenceService(paths, new DpapiProtectionService());
        var state = new WorkspaceState
        {
            SelectedTabIndex = 1,
            AppThemeId = "amber-terminal",
            CodeThemeId = "matrix-code",
            Topmost = true,
            AutoWrap = false,
            Window = new WindowPlacement
            {
                Left = 123,
                Top = 234,
                Width = 720,
                Height = 510,
                WasMaximized = true
            }
        };
        state.Tabs.Add(new TabDocument
        {
            Title = "COMMANDS",
            Content = "dotnet test",
            SyntaxMode = "PowerShell",
            ShowLineNumbers = false
        });
        state.Tabs.Add(new TabDocument
        {
            Title = "PRIVATE",
            Content = "classified-needle-9c7f",
            SyntaxMode = "JSON",
            IsProtected = true
        });

        await service.SaveAsync(state);
        var json = await File.ReadAllTextAsync(paths.WorkspaceFile);
        var restored = await service.LoadAsync();

        Assert.DoesNotContain("classified-needle-9c7f", json, StringComparison.Ordinal);
        Assert.Contains("protectedContent", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"theme\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, restored.Tabs.Count);
        Assert.Equal(1, restored.SelectedTabIndex);
        Assert.Equal("amber-terminal", restored.AppThemeId);
        Assert.Equal("matrix-code", restored.CodeThemeId);
        Assert.True(restored.Topmost);
        Assert.False(restored.AutoWrap);
        Assert.Equal("dotnet test", restored.Tabs[0].Content);
        Assert.False(restored.Tabs[0].ShowLineNumbers);
        Assert.True(restored.Tabs[1].IsProtected);
        Assert.Equal("classified-needle-9c7f", restored.Tabs[1].Content);
        Assert.Equal(720, restored.Window.Width);
        Assert.True(restored.Window.WasMaximized);
    }

    [Fact]
    public async Task Load_RecoversPreviousValidWorkspaceFromRotatingBackup()
    {
        var paths = new WorkspacePaths(_temporaryDirectory);
        var service = new WorkspacePersistenceService(paths, new DpapiProtectionService());
        var state = WorkspaceState.CreateDefault();
        state.Tabs[0].Title = "BACKUP VERSION";
        state.Tabs[0].Content = "known-good";
        await service.SaveAsync(state);

        state.Tabs[0].Title = "MIDDLE VERSION";
        state.Tabs[0].Content = "still-good";
        await service.SaveAsync(state);

        state.Tabs[0].Title = "CURRENT VERSION";
        state.Tabs[0].Content = "newest";
        await service.SaveAsync(state);
        await File.WriteAllTextAsync(paths.WorkspaceFile, "{ definitely-not-json");

        var recovered = await service.LoadAsync();

        Assert.Equal("MIDDLE VERSION", recovered.Tabs[0].Title);
        Assert.Equal("still-good", recovered.Tabs[0].Content);
        Assert.True(File.Exists(paths.BackupFile));
        Assert.True(Directory.EnumerateFiles(paths.LogsDirectory, "*.log").Any());
    }

    [Fact]
    public async Task Load_WithMissingFiles_ReturnsUsableDefaultWorkspace()
    {
        var paths = new WorkspacePaths(_temporaryDirectory);
        var service = new WorkspacePersistenceService(paths, new DpapiProtectionService());

        var state = await service.LoadAsync();

        Assert.Single(state.Tabs);
        Assert.Equal("QUICK NOTE", state.Tabs[0].Title);
        Assert.Equal(ThemeService.DefaultAppThemeId, state.AppThemeId);
        Assert.Equal(ThemeService.DefaultCodeThemeId, state.CodeThemeId);
        Assert.True(state.AutoWrap);
    }

    [Fact]
    public async Task Load_LegacyWorkspaceWithoutAutoWrap_DefaultsToWrappingEnabled()
    {
        var paths = new WorkspacePaths(_temporaryDirectory);
        Directory.CreateDirectory(paths.DataDirectory);
        await File.WriteAllTextAsync(paths.WorkspaceFile, """
            {
              "schemaVersion": 1,
              "selectedTabIndex": 0,
              "topmost": false,
              "theme": "Cyberpunk",
              "tabs": [
                {
                  "id": "73ab1517-7626-412d-ab78-1a744c9ee345",
                  "title": "LEGACY",
                  "content": "kept",
                  "isProtected": false,
                  "syntaxMode": "Plain Text",
                  "showLineNumbers": true
                }
              ]
            }
            """);
        var service = new WorkspacePersistenceService(paths, new DpapiProtectionService());

        var state = await service.LoadAsync();

        Assert.True(state.AutoWrap);
        Assert.Equal("kept", state.Tabs[0].Content);
        Assert.Equal(ThemeService.DefaultAppThemeId, state.AppThemeId);
        Assert.Equal(ThemeService.DefaultCodeThemeId, state.CodeThemeId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }
}
