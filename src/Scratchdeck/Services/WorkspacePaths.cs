using System.IO;

namespace Scratchdeck.Services;

public sealed class WorkspacePaths
{
    public WorkspacePaths(string dataDirectory)
    {
        DataDirectory = dataDirectory;
    }

    public string DataDirectory { get; }
    public string WorkspaceFile => Path.Combine(DataDirectory, "workspace.json");
    public string BackupFile => Path.Combine(DataDirectory, "workspace.backup.json");
    public string TemporaryFile => Path.Combine(DataDirectory, "workspace.tmp");
    public string LogsDirectory => Path.Combine(DataDirectory, "logs");

    public static WorkspacePaths CreateDefault()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new WorkspacePaths(Path.Combine(localAppData, "Scratchdeck"));
    }
}
