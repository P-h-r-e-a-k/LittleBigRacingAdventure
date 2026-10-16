using System.IO;
using System.Windows;

namespace LBRAInstaller;

public partial class App : Application
{
    // LBHRA-Setup.exe --install <LBA1 folder> <LBA2 folder> <install folder>: the install without the window (tests, scripts), its log in
    // <install folder>\setup.log; exit code 0 when installed
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args is ["--install", var lba1, var lba2, var target])
        {
            Directory.CreateDirectory(target);
            using var log = new StreamWriter(Path.Combine(target, "setup.log")) { AutoFlush = true };
            var code = 0;
            try
            {
                GameInstall.Run(lba1, lba2, target, (f, what) => log.WriteLine($"[{f:P0}] {what}"), log.WriteLine);
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
        base.OnStartup(e);
        new MainWindow().Show();
    }
}
