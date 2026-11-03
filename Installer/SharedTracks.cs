using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LBAAssembler.Terrain;
using Microsoft.Win32;

namespace LBRAInstaller;

// Shared tracks (the user, 2026-10-31: "LBAAssembler allows a user to create custom tracks. Let's support them exporting a track, sharing
// it, then loading a track into LBRA possibly for them to race a friend"). A track drawn in LBA Assembler is shared as a .lbrtrack file
// (its race track window's "Share for LBRA...": RaceTrackShare) -- the route and the island it is on, nothing of the game's. The player
// keeps the ones they race in User\LBA2\Tracks; every install builds them into the game from the player's own LBA2, each on a copy of its
// island, beside the island's own track (RaceTrackIsland.Shared), raced from the game's Racing > Shared tracks (RACEGAME.CPP: shared=).
// The game's "Add or remove tracks..." starts this setup's copy beside it:
//     LBRA-Setup.exe --shared-tracks "<install folder>"   -- the tracks there, added and taken out; built in, the game closing for it
// Two players racing each other online need the same shared tracks: the game's key for the races it can share is made from its files.
internal static class SharedTracks
{
    public const string FolderName = "Tracks";
    // (the copies' scenes from 400: past every island's own and the race tracks' -- the Long Table's from 350)
    public const int FirstScene = 400;
    // (the most the game lists: the online vote's page holds Track select's fifteen and these, RACEGAME.CPP VotePage)
    public const int Most = 8;
    // (Update\shared_now.txt: what the game's page waits for -- "build", the game closes for it; "closed", nothing to do)
    private const string NowFile = "shared_now.txt";

    public static string Folder(string installDir) => Path.Combine(installDir, "User", "LBA2", FolderName);

    // A file of the folder: its track, or why it can't be raced
    public sealed record Entry(string File, RaceTrackShare.Track? Track, string? Problem);

    public static List<Entry> List(string installDir)
    {
        var dir = Folder(installDir);
        if (!Directory.Exists(dir)) return new();
        return Directory.GetFiles(dir, "*" + RaceTrackShare.Extension).OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).Select(f =>
        {
            try { return new Entry(f, RaceTrackShare.Read(f), null); }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { return new Entry(f, null, e.Message); }
        }).ToList();
    }

    // The shared tracks an install builds: each one's island made and given to the build (RaceTrackIsland.SharedIslands), its scenes after
    // the one before's; the copies an earlier install made of tracks since taken out deleted
    public static List<(RaceTrackShare.Track Track, RaceTrackIsland Island, string File)> Prepare(string target, Action<string> log)
    {
        RaceTrackIsland.SharedIslands.Clear();
        // (and their race files: share01.txt, share01_trial.txt, share01_racepath.txt ...)
        var tracksDir = Path.Combine(target, GameInstall.TracksFolder);
        var raceFiles = Directory.Exists(tracksDir)
            ? Directory.GetFiles(tracksDir, "share*.txt").Where(f => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(f), @"^share\d\d(_.*)?\.txt$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            : Enumerable.Empty<string>();
        foreach (var stale in Directory.GetFiles(target, "SHARE??.ILE").Concat(Directory.GetFiles(target, "SHARE??.OBL")).Concat(raceFiles))
            try { File.Delete(stale); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { log($"{stale} not removed: {e.Message}"); }
        var tracks = new List<(RaceTrackShare.Track, RaceTrackIsland, string)>();
        var scene = FirstScene;
        foreach (var e in List(target))
        {
            if (e.Track is not { } track) { log($"shared track {Path.GetFileName(e.File)}: left out, {e.Problem}"); continue; }
            if (tracks.Count >= Most) { log($"shared track {Path.GetFileName(e.File)}: left out, the game races {Most} shared tracks at most"); continue; }
            var island = RaceTrackIsland.Shared(track.Island, tracks.Count, $"Shared track {tracks.Count + 1}: {track.Name}", scene);
            scene = island.LastScene + 1;
            RaceTrackIsland.SharedIslands.Add(island);
            tracks.Add((track, island, e.File));
            log($"shared track {Path.GetFileName(e.File)}: \"{track.Name}\" on {track.Island.Name}, as {island.IleFile} in scenes {island.FirstScene}-{island.LastScene}");
        }
        return tracks;
    }

    // What the last install built from the folder (tracks\shared_built.txt: each file's name and length): the window's Build is on where the
    // folder has changed since
    private static string BuiltFile(string installDir) => Path.Combine(installDir, GameInstall.TracksFolder, "shared_built.txt");
    private static string Signature(string installDir) =>
        string.Join("\n", List(installDir).Select(e => $"{Path.GetFileName(e.File)} {new FileInfo(e.File).Length}"));
    public static void RecordBuilt(string installDir) => File.WriteAllText(BuiltFile(installDir), Signature(installDir), new UTF8Encoding(false));
    public static bool Changed(string installDir) =>
        File.Exists(BuiltFile(installDir)) ? File.ReadAllText(BuiltFile(installDir)) != Signature(installDir) : List(installDir).Count > 0;

    // ---- --shared-tracks ------------------------------------------------------------------------------------------------------------

    // The window: the tracks in the folder, added and taken out, and built into the game -- the game closed for it, built again and started
    // (Updater.Update). Its exit code: 0.
    public static async Task<int> Manage(string installDir)
    {
        installDir = Path.GetFullPath(installDir).TrimEnd('\\', '/');
        var owner = new Window { Width = 0, Height = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false, Topmost = true, ShowActivated = true, Left = -10000, Top = -10000 };
        owner.Show();
        owner.Activate();
        var window = new SharedTracksWindow(installDir) { Owner = owner };
        var build = window.ShowDialog() == true;
        owner.Close();
        var now = Path.Combine(installDir, "Update", NowFile);
        try { File.WriteAllText(now, build ? "build\n" : "closed\n"); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        if (!build) return 0;
        Updater.Log(installDir, "shared tracks: built into the game, the game asked to close");
        var (code, setup) = await Updater.Update(installDir, sharedTracks: true);
        if (setup is not null)
        {
            // (the folders the game is made from gone: the setup window, as an update has it)
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            Application.Current.MainWindow = setup;
            setup.Show();
            return -1;
        }
        return code;
    }
}

// Racing > Shared tracks' "Add or remove tracks...": the tracks in the player's folder, a file added from wherever a friend's was saved,
// one taken out, and Build to put them into the game
internal sealed class SharedTracksWindow : Window
{
    private readonly string installDir;
    private readonly ListBox list = new() { Height = 190, Margin = new Thickness(0, 10, 0, 0) };
    private readonly TextBlock said = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(0xBF, 0xAF, 0xCB)) };
    private readonly Button build = new() { Content = "Build them into the game", MinWidth = 180, IsDefault = true, IsEnabled = false };
    private readonly Button remove = new() { Content = "Take out", MinWidth = 90, Margin = new Thickness(10, 0, 0, 0), IsEnabled = false };
    private bool changed;

    public SharedTracksWindow(string installDir)
    {
        this.installDir = installDir;
        UpdateLook.Dress(this, $"{GameInstall.Title} - Shared tracks");
        Topmost = true;
        var root = new StackPanel { Margin = new Thickness(22, 16, 22, 18), Width = 560 };
        root.Children.Add(UpdateLook.Header());
        root.Children.Add(new TextBlock
        {
            Text = "Tracks drawn in LBA Assembler and shared as .lbrtrack files (its race track window's \"Share for LBRA\"). The game builds each one from your own " +
                   "copy of LBA2, on a copy of its island, and lists it under Racing > Shared tracks. To race a friend online, both of you add the same tracks.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0),
        });
        // (in the window's dark look, not a list box's white)
        list.Background = UpdateLook.Brush("Panel");
        list.Foreground = Foreground;
        list.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x3A, 0x58));
        root.Children.Add(list);
        root.Children.Add(said);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var add = new Button { Content = "Add a track file...", MinWidth = 140, Background = UpdateLook.Brush("Ember") };
        add.Click += (_, _) => Add();
        remove.Click += (_, _) => Remove();
        row.Children.Add(add);
        row.Children.Add(remove);
        root.Children.Add(row);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        build.Click += (_, _) => DialogResult = true;
        var close = new Button { Content = "Close", Margin = new Thickness(10, 0, 0, 0), IsCancel = true, MinWidth = 90 };
        buttons.Children.Add(build);
        buttons.Children.Add(close);
        root.Children.Add(buttons);
        Content = root;
        list.SelectionChanged += (_, _) => remove.IsEnabled = list.SelectedItem is ListBoxItem;
        Refresh();
    }

    private void Refresh(string? select = null)
    {
        list.Items.Clear();
        var entries = SharedTracks.List(installDir);
        var good = 0;
        foreach (var e in entries)
        {
            var name = Path.GetFileName(e.File);
            var text = e.Track is { } t
                ? (good++ < SharedTracks.Most ? $"{t.Name}  --  on {t.Island.Shown}" : $"{t.Name}  --  left out: the game races {SharedTracks.Most} at most")
                : $"{name}  --  can't be raced: {e.Problem}";
            var item = new ListBoxItem { Content = text, Tag = e.File, ToolTip = e.File };
            list.Items.Add(item);
            if (select is not null && string.Equals(e.File, select, StringComparison.OrdinalIgnoreCase)) list.SelectedItem = item;
        }
        if (entries.Count == 0) list.Items.Add(new ListBoxItem { Content = "No shared tracks yet: add a .lbrtrack file.", IsEnabled = false });
        build.IsEnabled = changed || SharedTracks.Changed(installDir);
        if (!changed) said.Text ="Add or take out tracks, then build them into the game: it closes, is built again with them (a few minutes) and starts again.";
    }

    private void Add()
    {
        var open = new OpenFileDialog
        {
            Title = "Add a shared track", Filter = "Shared tracks (*.lbrtrack)|*.lbrtrack", Multiselect = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };
        if (open.ShowDialog(this) != true) return;
        var folder = SharedTracks.Folder(installDir);
        Directory.CreateDirectory(folder);
        var added = new List<string>();
        var refused = new List<string>();
        string? last = null;
        foreach (var file in open.FileNames)
        {
            try
            {
                var track = RaceTrackShare.Read(file);
                // (its own file in the folder: the same file again replaces itself, another of the same name is kept beside it)
                var to = Path.Combine(folder, Path.GetFileName(file));
                if (!string.Equals(Path.GetFullPath(file), to, StringComparison.OrdinalIgnoreCase) && File.Exists(to) && !SameBytes(file, to))
                    for (var k = 2; File.Exists(to); k++) to = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(file)} ({k}){RaceTrackShare.Extension}");
                if (!string.Equals(Path.GetFullPath(file), to, StringComparison.OrdinalIgnoreCase)) File.Copy(file, to, true);
                added.Add(track.Name);
                last = to;
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { refused.Add($"{Path.GetFileName(file)}: {e.Message}"); }
        }
        if (added.Count > 0) changed = true;
        Refresh(last);
        said.Text = (added.Count > 0 ? $"Added: {string.Join(", ", added)}. Build them into the game to race them." : "Nothing was added.") +
                    (refused.Count > 0 ? "\n" + string.Join("\n", refused) : "");
    }

    private void Remove()
    {
        if (list.SelectedItem is not ListBoxItem { Tag: string file }) return;
        if (MessageBox.Show(this, $"Take {Path.GetFileName(file)} out of the game's shared tracks?", Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        try
        {
            File.Delete(file);
            changed = true;
            Refresh();
            said.Text = $"{Path.GetFileName(file)} taken out. Build them into the game to have it gone from there.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { said.Text = $"It couldn't be taken out: {e.Message}"; }
    }

    private static bool SameBytes(string a, string b) => new FileInfo(a).Length == new FileInfo(b).Length && File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
}
