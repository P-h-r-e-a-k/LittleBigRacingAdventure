using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace LBRAInstaller;

// Sharing ghosts (2026-10-26 -- the user: "Let's update ghost sharing so a user can share a zip file with all of their ghosts in, exported
// with their name"). The game (RACEGAME.CPP's "Other people's ghosts" page) starts this setup's copy beside it:
//     LBRA-Setup.exe --export-ghosts "<the user's GHOSTS folder>"   -- the player's own ghosts into one zip, under the name they give
//     LBRA-Setup.exe --import-ghosts "<the user's GHOSTS folder>"   -- a zip a friend shared (or .ghost files) into the GHOSTS folder
// The player's own ghosts are the game's "<track>.ghost" files there (no " - " in the name, no driver= line). In the zip each is
// "<track> - <name>.ghost" with a driver=<name> line, so the game lists it under that name. The name is kept in GHOST_NAME.TXT beside
// the GHOSTS folder for next time.
internal static class GhostShare
{
    private const string NameFile = "GHOST_NAME.TXT";

    // A name for a file: letters, digits, spaces and - _ ( ) kept, the rest '_'
    private static string Safe(string s)
    {
        var b = new StringBuilder();
        foreach (var c in s.Trim()) b.Append(char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '(' or ')' ? c : '_');
        return b.Length > 0 ? b.ToString() : "ghost";
    }

    // A ghost file's head: its key=value lines before points=
    private static Dictionary<string, string> Head(IEnumerable<string> lines)
    {
        var head = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            if (line.StartsWith('#') || line.Length == 0) continue;
            var eq = line.IndexOf('=');
            if (eq <= 0) break;
            head[line[..eq]] = line[(eq + 1)..];
            if (line.StartsWith("points=")) break;
        }
        return head;
    }

    private static bool IsGhost(Dictionary<string, string> head) =>
        head.TryGetValue("ghost", out var v) && int.TryParse(v, out var n) && n >= 1 && head.ContainsKey("track") && head.ContainsKey("points");

    // A window in front of the game's for the dialogs to belong to (the game may be full screen)
    private static Window Owner()
    {
        var w = new Window { Width = 0, Height = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false, Topmost = true, ShowActivated = true, Left = -10000, Top = -10000 };
        w.Show();
        w.Activate();
        return w;
    }

    // ---- --export-ghosts ------------------------------------------------------------------------------------------------------------

    public static int Export(string ghosts)
    {
        ghosts = Path.GetFullPath(ghosts);
        var own = Directory.Exists(ghosts)
            ? Directory.GetFiles(ghosts, "*.ghost").Where(f => !Path.GetFileNameWithoutExtension(f).Contains(" - ")).ToList()
            : new List<string>();
        var mine = own.Select(f => (File: f, Lines: File.ReadAllLines(f, Encoding.UTF8))).Where(g => IsGhost(Head(g.Lines)) && !Head(g.Lines).ContainsKey("driver")).ToList();
        var owner = Owner();
        if (mine.Count == 0)
        {
            MessageBox.Show(owner, "You have no ghosts to share yet: drive a time trial, and your best lap of each track becomes your ghost.", GameInstall.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            owner.Close();
            return 0;
        }
        var namePath = Path.Combine(Path.GetDirectoryName(ghosts) ?? ghosts, NameFile);
        var name = File.Exists(namePath) ? File.ReadAllText(namePath, Encoding.UTF8).Trim() : Environment.UserName;
        var window = new ShareWindow(name, mine.Count);
        window.Owner = owner;
        if (window.ShowDialog() != true) { owner.Close(); return 0; }
        name = window.PlayerName;
        try { File.WriteAllText(namePath, name, new UTF8Encoding(false)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }

        var save = new SaveFileDialog
        {
            Title = "Save your ghosts", Filter = "Zip file (*.zip)|*.zip", FileName = $"LBRA ghosts - {Safe(name)}.zip",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };
        if (save.ShowDialog(owner) != true) { owner.Close(); return 0; }
        var tracks = new List<string>();
        using (var zip = ZipFile.Open(save.FileName + ".tmp", ZipArchiveMode.Create))
        {
            foreach (var (file, lines) in mine)
            {
                var head = Head(lines);
                tracks.Add(head["track"]);
                // (driver=<name> after the head's date= -- or lap= -- line)
                var outLines = new List<string>();
                var put = false;
                foreach (var line in lines)
                {
                    outLines.Add(line);
                    if (!put && (line.StartsWith("date=") || (line.StartsWith("lap=") && !head.ContainsKey("date"))))
                    {
                        outLines.Add("driver=" + name.Replace('\n', ' ').Replace('\r', ' '));
                        put = true;
                    }
                }
                var entry = zip.CreateEntry($"{Path.GetFileNameWithoutExtension(file)} - {Safe(name)}.ghost");
                using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                foreach (var l in outLines) w.Write(l + "\n");
            }
            var readme = zip.CreateEntry("README.txt");
            using (var w = new StreamWriter(readme.Open(), new UTF8Encoding(false)))
                w.Write($"{name}'s ghosts for {GameInstall.Title}: their best lap of {mine.Count} track(s).\r\n\r\n" +
                        "To race them: in the game, choose a track, Time trial, then \"Other people's ghosts\" and \"Import ghosts...\", and pick this zip.\r\n" +
                        $"They are then listed under {name} on that page.\r\n\r\n" + string.Join("\r\n", tracks.OrderBy(t => t)) + "\r\n");
        }
        File.Move(save.FileName + ".tmp", save.FileName, true);
        MessageBox.Show(owner, $"{mine.Count} ghost(s) saved to\n{save.FileName}\n\nSend that file to a friend: they import it from the game's \"Other people's ghosts\" page.",
            GameInstall.Title, MessageBoxButton.OK, MessageBoxImage.Information);
        try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{save.FileName}\""); } catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        owner.Close();
        return 0;
    }

    // ---- --import-ghosts ------------------------------------------------------------------------------------------------------------

    // (files given after the folder -- LBRA-Setup.exe --import-ghosts <GHOSTS> <zip or .ghost> ... -- are taken without the file dialog;
    // LBRA_GHOSTS_QUIET=1 leaves the closing message out and writes it to the console: tests)
    public static int Import(string ghosts, string[] given)
    {
        ghosts = Path.GetFullPath(ghosts);
        Directory.CreateDirectory(ghosts);
        var quiet = Environment.GetEnvironmentVariable("LBRA_GHOSTS_QUIET") == "1";
        var owner = Owner();
        var chosen = given;
        if (chosen.Length == 0)
        {
            var open = new OpenFileDialog
            {
                Title = "Import ghosts", Filter = "Ghosts (*.zip, *.ghost)|*.zip;*.ghost", Multiselect = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            };
            if (open.ShowDialog(owner) != true) { owner.Close(); return 0; }
            chosen = open.FileNames;
        }
        var got = new List<(string Driver, string Track)>();
        var refused = 0;
        void Take(string entryName, string[] lines)
        {
            var head = Head(lines);
            if (!IsGhost(head)) { refused++; return; }
            var track = head["track"];
            // (whose: its driver= line, else its name's part after " - ", else the file's own name)
            var stem = Path.GetFileNameWithoutExtension(entryName);
            var driver = head.TryGetValue("driver", out var d) && d.Trim().Length > 0 ? d.Trim()
                       : stem.Contains(" - ") ? stem[(stem.LastIndexOf(" - ", StringComparison.Ordinal) + 3)..] : stem;
            // (never a name the game takes for the player's own: "<track>.ghost" -- always "<track> - <whose>.ghost")
            var target = Path.Combine(ghosts, $"{Safe(track)} - {Safe(driver)}.ghost");
            File.WriteAllText(target, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
            got.Add((driver, track));
        }
        foreach (var file in chosen)
            try
            {
                if (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    using var zip = ZipFile.OpenRead(file);
                    foreach (var entry in zip.Entries.Where(e => e.Name.EndsWith(".ghost", StringComparison.OrdinalIgnoreCase)))
                    {
                        using var r = new StreamReader(entry.Open(), Encoding.UTF8);
                        Take(entry.Name, r.ReadToEnd().Replace("\r\n", "\n").TrimEnd('\n').Split('\n'));
                    }
                }
                else Take(Path.GetFileName(file), File.ReadAllLines(file, Encoding.UTF8));
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { refused++; }
        var whose = got.Select(g => g.Driver).Distinct().ToList();
        var text = got.Count == 0 ? "No ghosts were found in what you chose."
            : $"{got.Count} ghost(s) installed{(whose.Count > 0 ? $" from {string.Join(", ", whose)}" : "")}:\n{string.Join("\n", got.Select(g => g.Track).Distinct().OrderBy(t => t))}\n\n" +
              "Choose one in the game: a track, Time trial, \"Other people's ghosts\".";
        if (refused > 0) text += $"\n\n{refused} file(s) weren't ghosts and were left out.";
        if (quiet) File.WriteAllText(Path.Combine(ghosts, "..", "ghost_import.log"), text + "\n", new UTF8Encoding(false));
        else MessageBox.Show(owner, text, GameInstall.Title, MessageBoxButton.OK, got.Count > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        owner.Close();
        return 0;
    }
}

// "Share my ghosts": the name they go under, and on to choosing where the zip goes
internal sealed class ShareWindow : Window
{
    private readonly TextBox _name;
    public string PlayerName => _name.Text.Trim();

    public ShareWindow(string name, int count)
    {
        UpdateLook.Dress(this, $"{GameInstall.Title} - Share my ghosts");
        Topmost = true;
        var root = new StackPanel { Margin = new Thickness(22, 16, 22, 18), Width = 520 };
        root.Children.Add(UpdateLook.Header());
        root.Children.Add(new TextBlock
        {
            Text = $"Your {count} ghost(s) -- your best lap of each track -- go into one zip file to send to a friend. They are shown under your name.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0),
        });
        root.Children.Add(new TextBlock { Text = "Your name", Margin = new Thickness(0, 12, 0, 4), Foreground = new SolidColorBrush(Color.FromRgb(0xBF, 0xAF, 0xCB)) });
        _name = new TextBox { Text = name, MaxLength = 24, Padding = new Thickness(4) };
        root.Children.Add(_name);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var ok = new Button { Content = "Save the zip...", Background = UpdateLook.Brush("Ember"), IsDefault = true, MinWidth = 120 };
        ok.Click += (_, _) => { if (PlayerName.Length > 0) DialogResult = true; else _name.Focus(); };
        var cancel = new Button { Content = "Cancel", Margin = new Thickness(10, 0, 0, 0), IsCancel = true, MinWidth = 90 };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);
        Content = root;
        Loaded += (_, _) => { _name.Focus(); _name.SelectAll(); };
    }
}
