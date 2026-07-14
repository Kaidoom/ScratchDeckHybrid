using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
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
            SelectedTabIndex = state.SelectedTabIndex,
            Window = new WindowPlacement
            {
                Left = double.IsFinite(state.Window.Left) ? state.Window.Left : 80,
                Top = double.IsFinite(state.Window.Top) ? state.Window.Top : 80,
                Width = double.IsFinite(state.Window.Width) ? state.Window.Width : 680,
                Height = double.IsFinite(state.Window.Height) ? state.Window.Height : 480,
                WasMaximized = state.Window.WasMaximized
            },
            Topmost = state.Topmost,
            Theme = state.Theme,
            Tabs = state.Tabs.Select(tab => new PersistedTab
            {
                Id = tab.Id,
                Title = tab.Title,
                SyntaxMode = tab.SyntaxMode,
                ShowLineNumbers = tab.ShowLineNumbers,
                IsProtected = tab.IsProtected,
                Content = tab.IsProtected ? null : tab.Content,
                ProtectedContent = tab.IsProtected ? _protection.Protect(tab.Content) : null
            }).ToList()
        };
    }

    private WorkspaceState FromPersisted(PersistedWorkspace persisted)
    {
        return new WorkspaceState
        {
            SchemaVersion = persisted.SchemaVersion,
            SelectedTabIndex = persisted.SelectedTabIndex,
            Window = persisted.Window ?? new WindowPlacement(),
            Topmost = persisted.Topmost,
            Theme = persisted.Theme ?? ThemeService.DefaultTheme,
            Tabs = new ObservableCollection<TabDocument>(persisted.Tabs.Select(tab => new TabDocument
            {
                Id = tab.Id,
                Title = tab.Title ?? "Untitled",
                SyntaxMode = tab.SyntaxMode ?? "Plain Text",
                ShowLineNumbers = tab.ShowLineNumbers,
                IsProtected = tab.IsProtected,
                Content = tab.IsProtected
                    ? _protection.Unprotect(tab.ProtectedContent ?? throw new JsonException("Protected content is missing."))
                    : tab.Content ?? string.Empty
            }))
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
        public int SchemaVersion { get; set; } = WorkspaceState.CurrentSchemaVersion;
        public List<PersistedTab> Tabs { get; set; } = [];
        public int SelectedTabIndex { get; set; }
        public WindowPlacement? Window { get; set; }
        public bool Topmost { get; set; }
        public string? Theme { get; set; }
    }

    private sealed class PersistedTab
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? ProtectedContent { get; set; }
        public bool IsProtected { get; set; }
        public string? SyntaxMode { get; set; }
        public bool ShowLineNumbers { get; set; } = true;
    }
}
