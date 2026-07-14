using System.Windows;
using Scratchdeck.Services;

namespace Scratchdeck;

public partial class App : System.Windows.Application
{
    private SingleInstanceService? _singleInstance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.IsFirstInstance)
        {
            await _singleInstance.NotifyFirstInstanceAsync();
            Shutdown();
            return;
        }

        var paths = WorkspacePaths.CreateDefault();
        var themes = new ThemeService(paths);
        await themes.LoadAsync();
        var persistence = new WorkspacePersistenceService(paths, new DpapiProtectionService());
        var state = await persistence.LoadAsync();

        themes.NormalizeSelections(state);
        themes.ApplyAppTheme(state.AppThemeId);
        themes.ApplyCodeTheme(state.CodeThemeId);

        var window = new MainWindow(state, persistence, themes);
        MainWindow = window;
        _singleInstance.ActivationRequested += (_, _) => Dispatcher.Invoke(window.RestoreAndActivate);
        _singleInstance.StartListening();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
