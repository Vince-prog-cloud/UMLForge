using System.Windows;

namespace UmlForge;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var settings = ProjectStore.LoadSettings();
        var autosaved = ProjectStore.LoadAutosave();
        var pick = Dialogs.StartupPicker(null, autosaved, settings.LastFile);
        if (pick == null)
        {
            Shutdown();
            return;
        }

        var win = new MainWindow(pick.Value.project, pick.Value.focusPkg, pick.Value.filePath, settings);
        MainWindow = win;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        win.Show();
    }
}