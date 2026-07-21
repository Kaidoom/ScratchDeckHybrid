using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Scratchdeck.Models;

namespace Scratchdeck.Services;

public sealed class WorkspacePersistenceService
{
    private readonly WorkspacePaths _paths;
    private readonly IProtectionService _protection;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public WorkspacePersistenceService(WorkspacePaths paths, IProtectionService protection)
    {
        _paths = paths;
        _protection = protection;
    }

    public async Task<WorkspaceState> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.DataDirectory);

        var primary = await TryLoadFileAsync(_paths.WorkspaceFile, cancellationToken);
        if (primary is not null)
        {
            return primary;
        }

        var backup = await TryLoadFileAsync(_paths.BackupFile, cancellationToken);
        if (backup is not null)
        {
            await LogAsync("Primary workspace was unavailable or invalid; loaded the rotating backup.");
            return backup;
        }

        return WorkspaceState.CreateDefault();
    }

    public async Task SaveAsync(WorkspaceState state, CancellationToken cancellationToken = default)
    {
        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_paths.DataDirectory);
            var persisted = ToPersisted(state);
            var json = JsonSerializer.Serialize(persisted, _jsonOptions);

            await File.WriteAllTextAsync(_paths.TemporaryFile, json, cancellationToken);

            if (File.Exists(_paths.WorkspaceFile))
            {
                File.Replace(
                    _paths.TemporaryFile,
                    _paths.WorkspaceFile,
                    _paths.BackupFile,
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(_paths.TemporaryFile, _paths.WorkspaceFile);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await LogAsync("Workspace save failed.", ex);
            throw;
        }
        finally
        {
            TryDeleteTemporaryFile();
            _saveLock.Release();
        }
    }

    private async Task<WorkspaceState?> TryLoadFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var persisted = JsonSerializer.Deserialize<PersistedWorkspace>(json, _jsonOptions)
                ?? throw new JsonException("The workspace document was empty.");

            if (persisted.SchemaVersion > WorkspaceState.CurrentSchemaVersion)
            {
                throw new JsonException($"Unsupported workspace schema {persisted.SchemaVersion}.");
            }

            var state = FromPersisted(persisted);
            state.Normalize();
            return state;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await LogAsync($"Could not load workspace file '{path}'.", ex);
            return null;
        }
    }

    private PersistedWorkspace ToPersisted(WorkspaceState state)
    {
        return new PersistedWorkspace
        {
            SchemaVersion = WorkspaceState.CurrentSchemaVersion,
            SelectedFolderIndex = state.SelectedFolderIndex,
            FolderPanelWidth = double.IsFinite(state.FolderPanelWidth)
                ? Math.Clamp(
                    state.FolderPanelWidth,
                    WorkspaceState.MinFolderPanelWidth,
                    WorkspaceState.MaxFolderPanelWidth)
                : WorkspaceState.DefaultFolderPanelWidth,
            Window = new WindowPlacement
            {
                Left = double.IsFinite(state.Window.Left) ? state.Window.Left : 80,
                Top = double.IsFinite(state.Window.Top) ? state.Window.Top : 80,
                Width = double.IsFinite(state.Window.Width) ? state.Window.Width : 860,
                Height = double.IsFinite(state.Window.Height) ? state.Window.Height : 480,
                WasMaximized = state.Window.WasMaximized
            },
            Topmost = state.Topmost,
            AutoWrap = state.AutoWrap,
            AppThemeId = state.AppThemeId,
            CodeThemeId = state.CodeThemeId,
            ScratchPalette = [.. state.ScratchPalette],
            LegacyTabs = null,
            Folders = state.Folders.Select(folder => new PersistedFolder
            {
                Id = folder.Id,
                Title = folder.Title,
                SelectedTabIndex = folder.SelectedTabIndex,
                Tabs = folder.Tabs.Select(ToPersistedTab).ToList()
            }).ToList()
        };
    }

    private WorkspaceState FromPersisted(PersistedWorkspace persisted)
    {
        ObservableCollection<WorkspaceFolder> folders;
        if (persisted.SchemaVersion <= 4)
        {
            var legacyTabs = persisted.LegacyTabs
                ?? throw new JsonException("The legacy tab collection was null.");
            folders =
            [
                new WorkspaceFolder
                {
                    Title = "Default",
                    SelectedTabIndex = persisted.LegacySelectedTabIndex ?? 0,
                    Tabs = new ObservableCollection<TabDocument>(legacyTabs.Select(FromPersistedTab))
                }
            ];
        }
        else
        {
            var persistedFolders = persisted.Folders
                ?? throw new JsonException("The folder collection was missing.");
            folders = new ObservableCollection<WorkspaceFolder>(persistedFolders.Select(FromPersistedFolder));
        }

        return new WorkspaceState
        {
            SchemaVersion = WorkspaceState.CurrentSchemaVersion,
            Folders = folders,
            SelectedFolderIndex = persisted.SchemaVersion <= 4 ? 0 : persisted.SelectedFolderIndex,
            FolderPanelWidth = persisted.FolderPanelWidth,
            Window = persisted.Window ?? new WindowPlacement(),
            Topmost = persisted.Topmost,
            AutoWrap = persisted.AutoWrap,
            AppThemeId = persisted.AppThemeId ?? ThemeService.LegacyAppThemeId(persisted.Theme),
            CodeThemeId = persisted.CodeThemeId ?? ThemeService.LegacyCodeThemeId(persisted.Theme),
            ScratchPalette = persisted.ScratchPalette ?? ScratchPaletteService.CreateDefaultPalette()
        };
    }

    private WorkspaceFolder FromPersistedFolder(PersistedFolder folder)
    {
        var tabs = folder.Tabs ?? throw new JsonException("A folder tab collection was null.");
        return new WorkspaceFolder
        {
            Id = folder.Id,
            Title = folder.Title ?? "Untitled Folder",
            SelectedTabIndex = folder.SelectedTabIndex,
            Tabs = new ObservableCollection<TabDocument>(tabs.Select(FromPersistedTab))
        };
    }

    private PersistedTab ToPersistedTab(TabDocument tab)
    {
        return new PersistedTab
        {
            Id = tab.Id,
            Title = tab.Title,
            SyntaxMode = tab.SyntaxMode,
            ShowLineNumbers = tab.ShowLineNumbers,
            IsProtected = tab.IsProtected,
            Content = tab.IsProtected ? null : tab.Content,
            ProtectedContent = tab.IsProtected ? _protection.Protect(tab.Content) : null,
            ScratchData = tab.IsProtected ? null : tab.ScratchData,
            ProtectedScratchData = tab.IsProtected && !string.IsNullOrEmpty(tab.ScratchData)
                ? _protection.Protect(tab.ScratchData)
                : null,
            IsScratchMode = tab.IsScratchMode,
            ScratchBrushColor = tab.ScratchBrushColor,
            ScratchBrushSize = tab.ScratchBrushSize
        };
    }

    private TabDocument FromPersistedTab(PersistedTab tab)
    {
        return new TabDocument
        {
            Id = tab.Id,
            Title = tab.Title ?? "Untitled",
            SyntaxMode = tab.SyntaxMode ?? "Plain Text",
            ShowLineNumbers = tab.ShowLineNumbers,
            IsProtected = tab.IsProtected,
            Content = tab.IsProtected
                ? _protection.Unprotect(tab.ProtectedContent ?? throw new JsonException("Protected content is missing."))
                : tab.Content ?? string.Empty,
            ScratchData = tab.IsProtected
                ? string.IsNullOrEmpty(tab.ProtectedScratchData)
                    ? string.Empty
                    : _protection.Unprotect(tab.ProtectedScratchData)
                : tab.ScratchData ?? string.Empty,
            IsScratchMode = tab.IsScratchMode,
            ScratchBrushColor = tab.ScratchBrushColor ?? "#19D9F0",
            ScratchBrushSize = tab.ScratchBrushSize
        };
    }

    private async Task LogAsync(string message, Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(_paths.LogsDirectory);
            var line = $"[{DateTimeOffset.Now:O}] {message}";
            if (exception is not null)
            {
                line += $" {exception.GetType().Name}: {exception.Message}";
            }

            await File.AppendAllTextAsync(
                Path.Combine(_paths.LogsDirectory, $"scratchdeck-{DateTime.UtcNow:yyyyMMdd}.log"),
                line + Environment.NewLine);
        }
        catch
        {
            // Logging must never make the editor unavailable.
        }
    }

    private void TryDeleteTemporaryFile()
    {
        try
        {
            if (File.Exists(_paths.TemporaryFile))
            {
                File.Delete(_paths.TemporaryFile);
            }
        }
        catch
        {
            // A stale temp file is harmless and will be overwritten on the next save.
        }
    }

    private sealed class PersistedWorkspace
    {
        public int SchemaVersion { get; set; } = 1;
        public List<PersistedFolder>? Folders { get; set; }
        public int SelectedFolderIndex { get; set; }
        public double FolderPanelWidth { get; set; } = WorkspaceState.DefaultFolderPanelWidth;
        [JsonPropertyName("tabs")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<PersistedTab>? LegacyTabs { get; set; } = [];
        [JsonPropertyName("selectedTabIndex")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? LegacySelectedTabIndex { get; set; }
        public WindowPlacement? Window { get; set; }
        public bool Topmost { get; set; }
        public bool AutoWrap { get; set; } = true;
        public string? AppThemeId { get; set; }
        public string? CodeThemeId { get; set; }
        public List<string>? ScratchPalette { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Theme { get; set; }
    }

    private sealed class PersistedFolder
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public List<PersistedTab>? Tabs { get; set; } = [];
        public int SelectedTabIndex { get; set; }
    }

    private sealed class PersistedTab
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? ProtectedContent { get; set; }
        public string? ScratchData { get; set; }
        public string? ProtectedScratchData { get; set; }
        public bool IsProtected { get; set; }
        public bool IsScratchMode { get; set; }
        public string? ScratchBrushColor { get; set; }
        public double ScratchBrushSize { get; set; } = ScratchPaletteService.DefaultBrushSize;
        public string? SyntaxMode { get; set; }
        public bool ShowLineNumbers { get; set; } = true;
    }
}
