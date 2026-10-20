using System.IO;
using System.Windows;

namespace LBRAInstaller;

public partial class App : Application
{
    // LBRA-Setup.exe --install <LBA1 folder> <LBA2 folder> <install folder>: the install without the window (tests, scripts), its log in
    // <install folder>\setup.log; exit code 0 when installed.
    // LBRA-Setup.exe --check-update <install folder> [--wait-pid <pid>] [--manual] [--update-url <http://localhost/...>]: the game's update
    // check, no window (the question "Install it now?" only); LBRA-Setup.exe --update <install folder>: this setup installed over the game,
    // a small progress window (Updater.cs).
    protected override async void OnStartup(StartupEventArgs e)
    {
        // (--install <LBA1> <LBA2> <folder> [<LBA1 music folder> [<LBA2 menu music folder>]]: as the setup window's questions choose them;
        // "" for none)
        if (e.Args is ["--install", var lba1, var lba2, var target, ..] && e.Args.Length <= 6)
        {
            Directory.CreateDirectory(target);
            using var log = new StreamWriter(Path.Combine(target, "setup.log")) { AutoFlush = true };
            var code = 0;
            try
            {
                GameInstall.Run(lba1, lba2, target, (f, what) => log.WriteLine($"[{f:P0}] {what}"), log.WriteLine, null, e.Args.Length >= 5 && e.Args[4].Length > 0 ? e.Args[4] : null, e.Args.Length == 6 && e.Args[5].Length > 0 ? e.Args[5] : null);
                log.WriteLine("INSTALLED");
            }
            catch (Exception error)
            {
                log.WriteLine("FAILED: " + error);
                code = 1;
            }
            Shutdown(code);
            return;
        }
        if (e.Args is ["--check-update", var game, ..])
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            int? pid = null;
            string? url = null;
            for (var i = 2; i < e.Args.Length; i++)
            {
                if (e.Args[i] == "--wait-pid" && i + 1 < e.Args.Length && int.TryParse(e.Args[i + 1], out var p)) pid = p;
                if (e.Args[i] == "--update-url" && i + 1 < e.Args.Length) url = e.Args[i + 1];
            }
            var code = await Updater.Check(game, pid, e.Args.Contains("--manual"), url);
            Shutdown(code);
            return;
        }
        if (e.Args is ["--update", var into, ..])
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var (code, window) = await Updater.Update(into);
            if (window is null) { Shutdown(code); return; }
            // (the update handed to the setup window: the folders to choose again)
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow = window;
            window.Show();
            return;
        }
        base.OnStartup(e);
        new MainWindow().Show();
    }
}
