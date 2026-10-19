using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace LBRAInstaller;

public partial class MainWindow : Window
{
    private bool busy;
    // (opened by an update that couldn't install by itself -- the game's folders gone: the site's file it is, for install.json)
    private readonly Updater.Source? source;

    public MainWindow() : this(null, null, null, null) { }

    // The window filled in: the folders given (an update's), else guessed
    internal MainWindow(string? lba1, string? lba2, string? target, Updater.Source? source)
    {
        InitializeComponent();
        this.source = source;
        // (the usual places: GOG's, Steam's)
        Lba1Box.Text = lba1 ?? LBAAssembler.Terrain.Temple.TempleMusic.InstalledLba1Folders().FirstOrDefault(GameInstall.IsLba1Folder) ?? Guess("Little Big Adventure", GameInstall.IsLba1Folder);
        Lba2Box.Text = lba2 ?? Guess("Little Big Adventure 2", GameInstall.IsLba2Folder);
        TargetBox.Text = target ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", GameInstall.Title);
    }

    private static string Guess(string name, Func<string, bool> ok)
    {
        var roots = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).Select(d => d.RootDirectory.FullName);
        foreach (var root in roots)
            foreach (var where in new[] { "GOG Games", Path.Combine("Program Files (x86)", "GOG Galaxy", "Games"), Path.Combine("Program Files (x86)", "Steam", "steamapps", "common"),
                         Path.Combine("SteamLibrary", "steamapps", "common") })
            {
                var dir = Path.Combine(root, where, name);
                try { if (ok(dir)) return dir; } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
                // (Steam's copy keeps the data in a CommonClassic or its own subfolder)
                foreach (var sub in new[] { "CommonClassic", "Common", "LBA2", "LBA" })
                    try { if (ok(Path.Combine(dir, sub))) return Path.Combine(dir, sub); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
        return "";
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string boxName } || FindName(boxName) is not TextBox box) return;
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose the folder", InitialDirectory = Directory.Exists(box.Text) ? box.Text : null };
        if (dialog.ShowDialog(this) == true) box.Text = dialog.FolderName;
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        string lba1 = Lba1Box.Text.Trim(), lba2 = Lba2Box.Text.Trim(), target = TargetBox.Text.Trim();
        if (!GameInstall.IsLba1Folder(lba1)) { MessageBox.Show(this, "That isn't Little Big Adventure's folder: it needs SCENE.HQR, LBA_GRI.HQR, LBA_BLL.HQR, LBA_BRK.HQR and RESS.HQR.", Title); return; }
        if (!GameInstall.IsLba2Folder(lba2)) { MessageBox.Show(this, "That isn't Little Big Adventure 2's folder: it needs RESS.HQR, SCENE.HQR and BODY.HQR.", Title); return; }
        if (string.IsNullOrWhiteSpace(target)) { MessageBox.Show(this, "Choose a folder to install into.", Title); return; }
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any() && !File.Exists(Path.Combine(target, GameInstall.MenuFile))
            && MessageBox.Show(this, $"{target} isn't empty. Install into it anyway?", Title, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        busy = true; InstallButton.IsEnabled = false; PlayButton.IsEnabled = false;
        // (what the install did goes to <install folder>\setup.log, as a headless install's does -- the window shows only its progress)
        var lines = new List<string>();
        void Log(string line) { lock (lines) lines.Add(line); }
        void Report(double f, string what) => Dispatcher.Invoke(() => { Progress.Value = f; Status.Text = what; });
        var shortcut = ShortcutBox.IsChecked == true;
        try
        {
            try { await Task.Run(() => GameInstall.Run(lba1, lba2, target, Report, Log, source)); }
            finally { try { Directory.CreateDirectory(target); File.WriteAllLines(Path.Combine(target, "setup.log"), lines); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { } }
            if (shortcut) { try { Shortcut.Desktop(Path.Combine(target, GameInstall.GameExe), GameInstall.Title, Path.Combine(target, GameInstall.IconFile)); Log("a shortcut on the desktop"); } catch (Exception error) { Log("no shortcut: " + error.Message); } }
            Status.Text = "Installed. Happy Halloween!";
            PlayButton.IsEnabled = true;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            try { File.AppendAllText(Path.Combine(target, "setup.log"), "FAILED: " + error + "\n"); } catch (Exception again) when (again is IOException or UnauthorizedAccessException) { }
            Status.Text = "Not installed: " + error.Message;
            MessageBox.Show(this, "Not installed: " + error.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { busy = false; InstallButton.IsEnabled = true; }
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        var exe = Path.Combine(TargetBox.Text.Trim(), GameInstall.GameExe);
        if (File.Exists(exe)) Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe)!, UseShellExecute = true });
    }

    private void Close_Click(object sender, RoutedEventArgs e) { if (!busy) Close(); }
}
