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
            SelectedFolderIndex = 1,
            FolderPanelWidth = 244,
            AppThemeId = "amber-terminal",
            CodeThemeId = "matrix-code",
            Topmost = true,
            AutoWrap = false,
            Window = new WindowPlacement
            {
                Left = 123,
                Top = 234,
                Width = 900,
                Height = 510,
                WasMaximized = true
            }
        };
        state.ScratchPalette[10] = "#123456";
        state.FolderPalette[10] = "#654321";
        var commands = new WorkspaceFolder
        {
            Title = "Commands",
            FolderColor = "#a855f7",
            SelectedTabIndex = 0
        };
        commands.Tabs.Add(new TabDocument
        {
            Title = "COMMANDS",
            Content = "dotnet test",
            SyntaxMode = "PowerShell",
            ShowLineNumbers = false,
            ScratchData = "unprotected-ink-payload",
            IsScratchMode = true,
            ScratchBrushColor = "#123456",
            ScratchBrushSize = 14
        });
        var privateNotes = new WorkspaceFolder
        {
            Title = "Private",
            FolderColor = "#654321",
            SelectedTabIndex = 1
        };
        privateNotes.Tabs.Add(new TabDocument
        {
            Title = "PRIVATE",
            Content = "classified-needle-9c7f",
            ScratchData = "classified-drawing-needle-4b2a",
            SyntaxMode = "JSON",
            IsProtected = true
        });
        privateNotes.Tabs.Add(new TabDocument
        {
            Title = "REFERENCE",
            Content = "visible"
        });
        state.Folders.Add(commands);
        state.Folders.Add(privateNotes);

        await service.SaveAsync(state);
        var json = await File.ReadAllTextAsync(paths.WorkspaceFile);
        var restored = await service.LoadAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.DoesNotContain("classified-needle-9c7f", json, StringComparison.Ordinal);
        Assert.DoesNotContain("classified-drawing-needle-4b2a", json, StringComparison.Ordinal);
        Assert.Contains("protectedContent", json, StringComparison.Ordinal);
        Assert.Contains("protectedScratchData", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"theme\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(WorkspaceState.CurrentSchemaVersion, root.GetProperty("schemaVersion").GetInt32());
        Assert.True(root.TryGetProperty("folders", out _));
        Assert.False(root.TryGetProperty("tabs", out _));
        Assert.False(root.TryGetProperty("selectedTabIndex", out _));
        Assert.Equal(2, restored.Folders.Count);
        Assert.Equal(1, restored.SelectedFolderIndex);
        Assert.Equal(244, restored.FolderPanelWidth);
        Assert.Equal("Commands", restored.Folders[0].Title);
        Assert.Equal("Private", restored.Folders[1].Title);
        Assert.Equal("#A855F7", restored.Folders[0].FolderColor);
        Assert.Equal("#654321", restored.Folders[1].FolderColor);
        Assert.Equal(commands.Id, restored.Folders[0].Id);
        Assert.Equal(privateNotes.Id, restored.Folders[1].Id);
        Assert.Equal(0, restored.Folders[0].SelectedTabIndex);
        Assert.Equal(1, restored.Folders[1].SelectedTabIndex);
        Assert.Equal("amber-terminal", restored.AppThemeId);
        Assert.Equal("matrix-code", restored.CodeThemeId);
        Assert.True(restored.Topmost);
        Assert.False(restored.AutoWrap);
        Assert.Equal("dotnet test", restored.Folders[0].Tabs[0].Content);
        Assert.Equal("unprotected-ink-payload", restored.Folders[0].Tabs[0].ScratchData);
        Assert.True(restored.Folders[0].Tabs[0].IsScratchMode);
        Assert.Equal("#123456", restored.Folders[0].Tabs[0].ScratchBrushColor);
        Assert.Equal(14, restored.Folders[0].Tabs[0].ScratchBrushSize);
        Assert.False(restored.Folders[0].Tabs[0].ShowLineNumbers);
        Assert.True(restored.Folders[1].Tabs[0].IsProtected);
        Assert.Equal("classified-needle-9c7f", restored.Folders[1].Tabs[0].Content);
        Assert.Equal("classified-drawing-needle-4b2a", restored.Folders[1].Tabs[0].ScratchData);
        Assert.Equal("#123456", restored.ScratchPalette[10]);
        Assert.Equal(ScratchPaletteService.SlotCount, restored.ScratchPalette.Count);
        Assert.Equal("#654321", restored.FolderPalette[10]);
        Assert.Equal(ScratchPaletteService.SlotCount, restored.FolderPalette.Count);
        Assert.Equal(900, restored.Window.Width);
        Assert.True(restored.Window.WasMaximized);
    }

    [Fact]
    public async Task Load_RecoversPreviousValidWorkspaceFromRotatingBackup()
    {
        var paths = new WorkspacePaths(_temporaryDirectory);
        var service = new WorkspacePersistenceService(paths, new DpapiProtectionService());
        var state = WorkspaceState.CreateDefault();
        state.Folders[0].Tabs[0].Title = "BACKUP VERSION";
        state.Folders[0].Tabs[0].Content = "known-good";
        await service.SaveAsync(state);

        state.Folders[0].Tabs[0].Title = "MIDDLE VERSION";
        state.Folders[0].Tabs[0].Content = "still-good";
        await service.SaveAsync(state);

        state.Folders[0].Tabs[0].Title = "CURRENT VERSION";
        state.Folders[0].Tabs[0].Content = "newest";
        await service.SaveAsync(state);
        await File.WriteAllTextAsync(paths.WorkspaceFile, "{ definitely-not-json");

        var recovered = await service.LoadAsync();

        Assert.Equal("MIDDLE VERSION", recovered.Folders[0].Tabs[0].Title);
        Assert.Equal("still-good", recovered.Folders[0].Tabs[0].Content);
        Assert.True(File.Exists(paths.BackupFile));
        Assert.True(Directory.EnumerateFiles(paths.LogsDirectory, "*.log").Any());
    }

    [Fact]
    public async Task Load_WithMissingFiles_ReturnsUsableDefaultWorkspace()
    {
        var paths = new WorkspacePaths(_temporaryDirectory);
        var service = new WorkspacePersistenceService(paths, new DpapiProtectionService());

        var state = await service.LoadAsync();

        Assert.Single(state.Folders);
        Assert.Equal("Default", state.Folders[0].Title);
        Assert.Single(state.Folders[0].Tabs);
        Assert.Equal("QUICK NOTE", state.Folders[0].Tabs[0].Title);
        Assert.Equal(ThemeService.DefaultAppThemeId, state.AppThemeId);
        Assert.Equal(ThemeService.DefaultCodeThemeId, state.CodeThemeId);
        Assert.True(state.AutoWrap);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsCompactFolderPanelWidth()
    {
        var paths = new WorkspacePaths(_temporaryDirectory);
        var service = new WorkspacePersistenceService(paths, new DpapiProtectionService());
        var state = WorkspaceState.CreateDefault();
        state.FolderPanelWidth = WorkspaceState.MinFolderPanelWidth;

        await service.SaveAsync(state);
        var restored = await service.LoadAsync();

        Assert.Equal(WorkspaceState.MinFolderPanelWidth, restored.FolderPanelWidth);
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
        Assert.Single(state.Folders);
        Assert.Equal("Default", state.Folders[0].Title);
        Assert.Equal("kept", state.Folders[0].Tabs[0].Content);
        Assert.Empty(state.Folders[0].Tabs[0].ScratchData);
        Assert.False(state.Folders[0].Tabs[0].IsScratchMode);
        Assert.Equal(ScratchPaletteService.SlotCount, state.ScratchPalette.Count);
        Assert.Equal(ThemeService.DefaultAppThemeId, state.AppThemeId);
        Assert.Equal(ThemeService.DefaultCodeThemeId, state.CodeThemeId);
    }

    [Fact]
    public async Task Load_SchemaFourWorkspace_MigratesFlatTabsAndNextSaveWritesOnlyFolders()
    {
        var paths = new WorkspacePaths(_temporaryDirectory);
        Directory.CreateDirectory(paths.DataDirectory);
        await File.WriteAllTextAsync(paths.WorkspaceFile, """
            {
              "schemaVersion": 4,
              "selectedTabIndex": 1,
              "autoWrap": false,
              "appThemeId": "matrix",
              "codeThemeId": "matrix-code",
              "tabs": [
                {
                  "id": "73ab1517-7626-412d-ab78-1a744c9ee345",
                  "title": "FIRST",
                  "content": "one",
                  "isProtected": false,
                  "syntaxMode": "Plain Text",
                  "showLineNumbers": true
                },
                {
                  "id": "660b77a4-252f-4300-ac96-f6305e58939f",
                  "title": "SECOND",
                  "content": "two",
                  "scratchData": "drawing",
                  "isScratchMode": true,
                  "isProtected": false,
                  "syntaxMode": "Markdown",
                  "showLineNumbers": false
                }
              ]
            }
            """);
        var service = new WorkspacePersistenceService(paths, new DpapiProtectionService());

        var state = await service.LoadAsync();

        Assert.Equal(WorkspaceState.CurrentSchemaVersion, state.SchemaVersion);
        Assert.Equal(0, state.SelectedFolderIndex);
        Assert.Single(state.Folders);
        Assert.Equal("Default", state.Folders[0].Title);
        Assert.Equal(1, state.Folders[0].SelectedTabIndex);
        Assert.Equal(2, state.Folders[0].Tabs.Count);
        Assert.Equal("two", state.Folders[0].Tabs[1].Content);
        Assert.Equal("drawing", state.Folders[0].Tabs[1].ScratchData);
        Assert.True(state.Folders[0].Tabs[1].IsScratchMode);
        Assert.Equal(WorkspaceState.DefaultFolderPanelWidth, state.FolderPanelWidth);
        Assert.Equal(WorkspaceFolder.DefaultFolderColor, state.Folders[0].FolderColor);
        Assert.Equal(ScratchPaletteService.SlotCount, state.FolderPalette.Count);

        await service.SaveAsync(state);
        using var saved = JsonDocument.Parse(await File.ReadAllTextAsync(paths.WorkspaceFile));
        var root = saved.RootElement;

        Assert.Equal(WorkspaceState.CurrentSchemaVersion, root.GetProperty("schemaVersion").GetInt32());
        Assert.True(root.TryGetProperty("folders", out var folders));
        Assert.Equal(2, folders[0].GetProperty("tabs").GetArrayLength());
        Assert.False(root.TryGetProperty("tabs", out _));
        Assert.False(root.TryGetProperty("selectedTabIndex", out _));
    }

    [Fact]
    public async Task Load_SchemaFiveWorkspace_DefaultsFolderColorsAndPalette()
    {
        var paths = new WorkspacePaths(_temporaryDirectory);
        Directory.CreateDirectory(paths.DataDirectory);
        await File.WriteAllTextAsync(paths.WorkspaceFile, """
            {
              "schemaVersion": 5,
              "selectedFolderIndex": 0,
              "folderPanelWidth": 180,
              "folders": [
                {
                  "id": "b5311375-f5cc-4d09-95d0-501a5b644be8",
                  "title": "Version Five",
                  "selectedTabIndex": 0,
                  "tabs": [
                    {
                      "id": "21b3cd86-b273-425d-b9f9-4b485448f2f0",
                      "title": "KEPT",
                      "content": "legacy folder content",
                      "isProtected": false,
                      "syntaxMode": "Plain Text",
                      "showLineNumbers": true
                    }
                  ]
                }
              ]
            }
            """);
        var service = new WorkspacePersistenceService(paths, new DpapiProtectionService());

        var state = await service.LoadAsync();

        Assert.Equal(WorkspaceState.CurrentSchemaVersion, state.SchemaVersion);
        Assert.Single(state.Folders);
        Assert.Equal("Version Five", state.Folders[0].Title);
        Assert.Equal("legacy folder content", state.Folders[0].Tabs[0].Content);
        Assert.Equal(WorkspaceFolder.DefaultFolderColor, state.Folders[0].FolderColor);
        Assert.Equal(ScratchPaletteService.CreateDefaultPalette(), state.FolderPalette);
    }

    [Fact]
    public async Task Load_FutureSchemaPrimary_RecoversCompatibleBackup()
    {
        var paths = new WorkspacePaths(_temporaryDirectory);
        var service = new WorkspacePersistenceService(paths, new DpapiProtectionService());
        var state = WorkspaceState.CreateDefault();
        state.Folders[0].Tabs[0].Content = "backup-content";
        await service.SaveAsync(state);

        state.Folders[0].Tabs[0].Content = "current-content";
        await service.SaveAsync(state);
        await File.WriteAllTextAsync(paths.WorkspaceFile, """
            {
              "schemaVersion": 999,
              "folders": []
            }
            """);

        var recovered = await service.LoadAsync();

        Assert.Equal("backup-content", recovered.Folders[0].Tabs[0].Content);
        Assert.True(Directory.EnumerateFiles(paths.LogsDirectory, "*.log").Any());
    }

    [Fact]
    public async Task Load_SchemaFourProtectedTab_DecryptsTextAndScratchInsideDefaultFolder()
    {
        var paths = new WorkspacePaths(_temporaryDirectory);
        Directory.CreateDirectory(paths.DataDirectory);
        var protection = new DpapiProtectionService();
        var legacy = new
        {
            schemaVersion = 4,
            selectedTabIndex = 0,
            tabs = new[]
            {
                new
                {
                    id = Guid.NewGuid(),
                    title = "PRIVATE",
                    protectedContent = protection.Protect("legacy-secret-text"),
                    protectedScratchData = protection.Protect("legacy-secret-drawing"),
                    isProtected = true,
                    syntaxMode = "Plain Text",
                    showLineNumbers = true
                }
            }
        };
        await File.WriteAllTextAsync(paths.WorkspaceFile, JsonSerializer.Serialize(legacy));
        var service = new WorkspacePersistenceService(paths, protection);

        var state = await service.LoadAsync();

        Assert.Single(state.Folders);
        Assert.True(state.Folders[0].Tabs[0].IsProtected);
        Assert.Equal("legacy-secret-text", state.Folders[0].Tabs[0].Content);
        Assert.Equal("legacy-secret-drawing", state.Folders[0].Tabs[0].ScratchData);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }
}
