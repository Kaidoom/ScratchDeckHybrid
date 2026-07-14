# Scratchdeck

Scratchdeck is a compact, native Windows scratchpad for notes, commands, IDs, and code snippets. It combines a tabbed AvalonEdit workspace with automatic local persistence, optional per-tab DPAPI protection, persistent line wrapping, a custom dark WPF shell, and four live-switching themes.

![Scratchdeck preview](docs/Scratchdeck-preview.png)

## Requirements

- Windows 10 or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) for building
- Visual Studio 2026 with the **.NET desktop development** workload is optional

No installer is required. The only runtime NuGet dependency is AvalonEdit.

## Build and run

From the repository root:

```powershell
dotnet restore Scratchdeck.sln
dotnet build Scratchdeck.sln -c Release
dotnet run --project src/Scratchdeck/Scratchdeck.csproj -c Release
```

Run the tests with:

```powershell
dotnet test Scratchdeck.sln -c Release
```

For a framework-dependent publish that can later be packaged in MSIX:

```powershell
dotnet publish src/Scratchdeck/Scratchdeck.csproj -c Release -r win-x64 --self-contained false -o publish/win-x64
```

## How it works

The solution is deliberately small:

- `Models/` contains the observable tab model, window placement, and workspace state.
- `Services/WorkspacePersistenceService.cs` maps the live model to a disk DTO, encrypts protected content, performs atomic replacement, rotates one backup, and recovers malformed workspaces.
- `Services/DpapiProtectionService.cs` uses Windows DPAPI with `CurrentUser` scope. Protected text can only be decrypted by the same Windows user profile.
- `Services/SingleInstanceService.cs` uses a per-session mutex and named pipe. A second launch tells the existing window to restore and activate.
- `Services/SyntaxHighlightingService.cs` builds lightweight AvalonEdit definitions from the active theme palette, validates them against loaded content, and falls back to plain text if a definition is unsafe.
- `Themes/` centralizes all interface and syntax colours in WPF resource dictionaries.
- `MainWindow.xaml` and its focused code-behind own the view interactions: tabs, drag reordering, inline rename, search, window chrome, and the 400 ms autosave debounce.

Workspace data is stored at:

```text
%LOCALAPPDATA%\Scratchdeck\workspace.json
```

The previous valid workspace is kept as `workspace.backup.json`, and recoverable I/O or parse errors are logged under `%LOCALAPPDATA%\Scratchdeck\logs\`.

Important: normal tabs are stored as plain text in `workspace.json`. Turn on **LOCK** for a tab to store its content as Windows DPAPI ciphertext. This protects data at rest for other Windows users, but it is not a password vault and does not defend against software running as the same signed-in user.

## Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+T` | Create a tab |
| `Ctrl+W` | Close the active tab |
| `Ctrl+Tab` | Select the next tab |
| `Ctrl+Shift+Tab` | Select the previous tab |
| `Ctrl+F` | Open search for the active tab |
| `Enter` / `Shift+Enter` | Next / previous search match |
| `F3` / `Shift+F3` | Next / previous search match |
| `Escape` | Close search or cancel a tab rename |
| `Ctrl+Shift+P` | Toggle always-on-top |
| `Ctrl+Z`, `Ctrl+Y`, `Ctrl+X`, `Ctrl+C`, `Ctrl+V`, `Ctrl+A` | Standard AvalonEdit commands |

Double-click a tab title to rename it. Drag a tab to reorder it. Use **WRAP** beside **PIN** to keep long lines inside the editor; the setting persists for the workspace. Closing the application never asks for confirmation; closing a tab only asks when that tab contains content.

## Themes and syntax modes

The built-in themes are Cyberpunk (default), Amber Terminal, Matrix, and Nord Dark. Cyberpunk uses cyan for primary active states and restrained amber for secondary controls, status, and emphasis. Theme changes apply immediately to chrome, controls, editor selection, syntax colours, focus states, scrollbars, and status indicators.

Each tab can use Plain Text, C++, C#, JSON, XML, HTML, PowerShell, Python, JavaScript, Markdown, SQL, Go, or INI highlighting. Text remains plain and is preserved exactly as entered. Unexpected mixed content is validated before a highlighter is activated, so a problematic definition degrades to stable plain-text editing rather than taking down the window.
