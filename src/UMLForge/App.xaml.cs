using System.Windows;
using System.Windows.Threading;

namespace UmlForge;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            try { ProjectStore.Autosave(MainWindow is MainWindow mw ? mw.Editor.Project : null); } catch { }
            MessageBox.Show("An unexpected error occurred. Your work has been autosaved and you can continue.\n\n" + args.Exception.Message,
                "UmlForge — recovered", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };
        var win = new MainWindow();
        if (e.Args.Length > 0 && System.IO.File.Exists(e.Args[0])) win.OpenFile(e.Args[0]);
        win.Show();
    }
}