using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace LBRAInstaller;

// The game's updates (2026-10-19). The installer leaves a copy of itself in the game's folder, Update\LBRA-Setup.exe, and what it
// installed from in Update\install.json. The game (LBA Assembler's engine: RACEGAME.CPP) starts that copy once a week, hidden, as
//     LBRA-Setup.exe --check-update "<install dir>" --wait-pid <the game's pid> [--manual]
// which looks at the setup on the site (a HEAD: its ETag, Last-Modified and length against the ones installed from), downloads a new one
// to Update\LBRA-Setup.new.exe, checks it is a whole LBRA setup no older than this one, waits for the game to close, then -- the player's
// choice in the game's Options, RACEGAME_SETTINGS.TXT updates=auto|ask|off -- runs it or asks first:
//     LBRA-Setup.new.exe --update "<install dir>"
// which installs itself over the game from the LBA1 and LBA2 folders the game was installed from, keeping the player's User\ folder
// (saves, settings, best laps), puts itself in Update\ as the next updater and starts the game again. Every step goes to Update\update.log;
// the last word for the game's Updates page to Update\status.txt ("<unix time>|<text>").
// For tests: LBRA_UPDATE_URL (or --update-url) points the check at http://localhost/... (nothing else is taken), LBRA_UPDATE_NO_RELAUNCH=1
// leaves the game closed after an update.
internal static class Updater
{
    public const string Url = "https://lbabirthdayevent.com/LBRA-Setup.exe";
    public const string Folder = "Update";
    public const string SetupFile = "LBRA-Setup.exe";
    public const string NewFile = "LBRA-Setup.new.exe";
    private const string PartFile = "LBRA-Setup.download";
    public const string InfoFile = "install.json";
    public const string LogFile = "update.log";
    public const string StatusFile = "status.txt";
    private const string UrlVariable = "LBRA_UPDATE_URL";
    private const string NoRelaunchVariable = "LBRA_UPDATE_NO_RELAUNCH";

    // What a setup on the site was when looked at: its ETag, Last-Modified and length (each as the server gave it, if it did)
    public sealed class Source
    {
        [JsonPropertyName("etag")] public string? ETag { get; set; }
        public DateTimeOffset? LastModified { get; set; }
        public long? Length { get; set; }

        public override string ToString() => $"ETag {ETag ?? "none"}, Last-Modified {LastModified?.ToString("u") ?? "none"}, {Length?.ToString("N0") ?? "?"} bytes";

        // The same file on the server: the same ETag when both have one, else the same date and length
        public bool SameAs(Source? other) => other is not null &&
            (ETag is not null && other.ETag is not null ? ETag == other.ETag : LastModified == other.LastModified && Length == other.Length);
    }

    // Update\install.json
    public sealed class InstallInfo
    {
        public string? Lba1 { get; set; }
        public string? Lba2 { get; set; }
        public DateTime InstalledUtc { get; set; }
        public string? Version { get; set; }
        public string Updates { get; set; } = "auto";   // the mode at the install (the game's own setting, RACEGAME_SETTINGS.TXT, wins)
        public Source? Source { get; set; }             // the site's setup the game was installed from (none: not known -- a setup run by hand)
        public Source? Pending { get; set; }            // downloaded to Update\LBRA-Setup.new.exe, not installed yet ("Later")
        public Source? Rejected { get; set; }           // downloaded and refused (not an LBRA setup, older): not downloaded again
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // (an ETag's quotes as they are)
    };

    public static Version OwnVersion => typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0);

    public static InstallInfo? Load(string installDir)
    {
        try
        {
            var path = Path.Combine(installDir, Folder, InfoFile);
            return File.Exists(path) ? JsonSerializer.Deserialize<InstallInfo>(File.ReadAllText(path), Json) : null;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    private static void Save(string installDir, InstallInfo info)
    {
        var dir = Path.Combine(installDir, Folder);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, InfoFile);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(info, Json), new UTF8Encoding(false));
        File.Move(path + ".tmp", path, true);
    }

    public static void Log(string installDir, string line)
    {
        var path = Path.Combine(installDir, Folder, LogFile);
        for (var attempt = 0; attempt < 20; attempt++)
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{Environment.ProcessId}] {line}\r\n");
                return;
            }
            catch (IOException) { Thread.Sleep(50); }
            catch (UnauthorizedAccessException) { return; }
    }

    // The game's Updates page shows it
    private static void Status(string installDir, string text)
    {
        try { File.WriteAllText(Path.Combine(installDir, Folder, StatusFile), $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}|{text}\n", new UTF8Encoding(false)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    // ---- after an install ---------------------------------------------------------------------------------------------------------

    // An install done (the window's, --install's, --update's): this setup copied into Update\ (the game's updater from now on), what it
    // was installed from in install.json -- the folders, the time, the site's file it came from when known (an update's) -- the mode
    // kept from before. The desktop's shortcut moved from the old exe's name to LBRA.exe.
    public static void Record(string installDir, string lba1, string lba2, Source? source, Action<string> log)
    {
        var dir = Path.Combine(installDir, Folder);
        Directory.CreateDirectory(dir);
        var before = Load(installDir);
        var self = Environment.ProcessPath;
        var copy = Path.Combine(dir, SetupFile);
        if (self is null || !File.Exists(self)) log("updates: no setup file to copy to Update\\ (not run from one)");
        else if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(copy), StringComparison.OrdinalIgnoreCase))
        {
            // (the checker that started an update may still be closing: its file is free a moment later)
            for (var attempt = 0; ; attempt++)
                try { File.Copy(self, copy, true); break; }
                catch (IOException) when (attempt < 60) { Thread.Sleep(500); }
            log($"updates: this setup ({OwnVersion}) copied to {copy}");
        }
        // (an earlier download, now installed or of no use)
        if (!string.Equals(self, Path.Combine(dir, NewFile), StringComparison.OrdinalIgnoreCase)) TryDelete(Path.Combine(dir, NewFile));
        TryDelete(Path.Combine(dir, PartFile));
        var info = new InstallInfo
        {
            Lba1 = Path.GetFullPath(lba1), Lba2 = Path.GetFullPath(lba2), InstalledUtc = DateTime.UtcNow, Version = OwnVersion.ToString(),
            Updates = before?.Updates ?? "auto", Source = source,
        };
        Save(installDir, info);
        log($"updates: {Path.Combine(dir, InfoFile)} written (source: {source?.ToString() ?? "not known"})");
        try { if (Shortcut.Repoint(Path.Combine(installDir, GameInstall.OldGameExe), Path.Combine(installDir, GameInstall.GameExe), GameInstall.Title)) log($"the desktop shortcut now starts {GameInstall.GameExe}"); }
        catch (Exception e) { log("the desktop shortcut not changed: " + e.Message); }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    // ---- --check-update ---------------------------------------------------------------------------------------------------------------

    public static async Task<int> Check(string installDir, int? waitPid, bool manual, string? urlArgument)
    {
        installDir = Path.GetFullPath(installDir).TrimEnd('\\', '/');
        var dir = Path.Combine(installDir, Folder);
        void L(string s) => Log(installDir, s);
        L($"---- check ({(manual ? "asked for in the game" : "the weekly one")}), setup {OwnVersion}, game pid {waitPid?.ToString() ?? "none"}");
        using var mutex = new Mutex(false, @"Local\LBRA-Update-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(installDir.ToUpperInvariant())))[..16]);
        try
        {
            if (!mutex.WaitOne(0))
            {
                L("another check is running: this one stops");
                // (its word said again, now: the game's page waits for one newer than this check)
                var said = Path.Combine(dir, StatusFile);
                try { var text = File.ReadAllText(said).Trim(); var bar = text.IndexOf('|'); Status(installDir, bar >= 0 ? text[(bar + 1)..] : "Checking for a new version..."); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Status(installDir, "Checking for a new version..."); }
                return 0;
            }
        }
        catch (AbandonedMutexException) { }
        try
        {
            var info = Load(installDir) ?? new InstallInfo { InstalledUtc = InstalledTime(installDir) };
            var mode = GameMode(installDir) ?? info.Updates;
            if (mode == "off" && !manual) { L("updates are off: nothing done"); return 0; }
            if (mode == "off") mode = "ask";
            var url = UpdateUrl(urlArgument, L);
            L($"mode {mode}; {url}");

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"LBRA-Updater/{OwnVersion}");
            Source server;
            try
            {
                using var head = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                if (!head.IsSuccessStatusCode) { L($"the server said {(int)head.StatusCode} {head.ReasonPhrase}: no check"); Status(installDir, $"The last check ({Today()}) couldn't reach the server."); return 0; }
                server = Describe(head);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                L("the server can't be reached: " + e.Message);
                Status(installDir, $"The last check ({Today()}) couldn't reach the server.");
                return 0;
            }
            L($"on the server: {server}");
            L($"installed from: {info.Source?.ToString() ?? $"not known (installed {info.InstalledUtc:u})"}");

            var setup = Path.Combine(dir, SetupFile);
            var fresh = Path.Combine(dir, NewFile);
            if (info.Rejected is not null && server.SameAs(info.Rejected)) { L("that file was refused before: no update"); return NoUpdate(installDir, info, null); }
            bool isNew;
            if (info.Source is not null) isNew = !server.SameAs(info.Source);
            else
            {
                // (installed from a setup of unknown origin: the site's is new if it is newer than the install, or not the same size as the
                // setup installed -- an older one is refused below)
                var setupLength = File.Exists(setup) ? new FileInfo(setup).Length : (long?)null;
                isNew = server.LastModified > new DateTimeOffset(DateTime.SpecifyKind(info.InstalledUtc, DateTimeKind.Utc)) || server.Length != setupLength;
            }
            if (!isNew)
            {
                L("no new version");
                // (the site's file taken for the one installed from: later checks compare its ETag)
                return NoUpdate(installDir, info, info.Source is null ? server : null);
            }
            L("a new version on the server");

            // ---- the download (or the one kept from last time: "Later")
            if (info.Pending is not null && server.SameAs(info.Pending) && File.Exists(fresh) && Verify(fresh, info.Pending.Length, L) is null)
                L($"the new version was downloaded before ({fresh}): taken");
            else
            {
                TryDelete(fresh);
                info.Pending = null;
                var part = Path.Combine(dir, PartFile);
                TryDelete(part);
                Source? got = null;
                // (three tries: a connection dropped half-way is tried again)
                for (var attempt = 1; got is null; attempt++)
                    try
                    {
                        using var download = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
                        download.DefaultRequestHeaders.UserAgent.ParseAdd($"LBRA-Updater/{OwnVersion}");
                        using var get = await download.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseHeadersRead);
                        if (!get.IsSuccessStatusCode) { L($"the download: the server said {(int)get.StatusCode}"); return 0; }
                        var described = Describe(get);
                        await using (var from = await get.Content.ReadAsStreamAsync())
                        await using (var to = File.Create(part)) await from.CopyToAsync(to);
                        got = described;
                    }
                    catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
                    {
                        TryDelete(part);
                        if (attempt < 3) { L($"the download failed ({e.Message}): trying again"); await Task.Delay(3000); continue; }
                        L("the download failed: " + e.Message);
                        Status(installDir, $"The last check ({Today()}): the download failed.");
                        return 0;
                    }
                L($"downloaded {new FileInfo(part).Length:N0} bytes ({got})");
                var wrong = Verify(part, got!.Length ?? server.Length, L);
                if (wrong is not null)
                {
                    L("REFUSED, deleted: " + wrong.Value.Why);
                    TryDelete(part);
                    // (a file that isn't a whole LBRA setup no older than this one isn't downloaded again; a short download is, next time)
                    if (wrong.Value.ForGood) { info.Rejected = got; Save(installDir, info); }
                    Status(installDir, $"The last check ({Today()}): the new version was refused.");
                    return 0;
                }
                if (File.Exists(setup) && SameBytes(part, setup))
                {
                    L("the same build as the one installed: nothing to install");
                    TryDelete(part);
                    return NoUpdate(installDir, info, got);
                }
                File.Move(part, fresh, true);
                info.Pending = got;
                info.Rejected = null;
                Save(installDir, info);
                L($"verified: {fresh}");
            }
            var version = FileVersionInfo.GetVersionInfo(fresh).FileVersion;
            Status(installDir, mode == "auto" ? $"Version {version}: installed when you close the game." : $"Version {version}: you are asked when you close the game.");

            // ---- the game closed first
            if (waitPid is { } pid)
            {
                Process? game = null;
                try { game = Process.GetProcessById(pid); } catch (ArgumentException) { }
                if (game is not null)
                {
                    L($"waiting for the game (pid {pid}) to close");
                    await game.WaitForExitAsync();
                    L("the game closed");
                }
            }
            if (mode == "ask" && !Ask(version))
            {
                L("Later: the download is kept for the next check");
                Status(installDir, $"Version {version} is downloaded: offered again at the next check.");
                return 0;
            }
            L($"starting the update: {fresh} --update \"{installDir}\"");
            Process.Start(new ProcessStartInfo(fresh) { ArgumentList = { "--update", installDir }, WorkingDirectory = dir, UseShellExecute = false });
            return 0;
        }
        catch (Exception e)
        {
            L("FAILED: " + e);
            return 1;
        }
        finally { try { mutex.ReleaseMutex(); } catch (ApplicationException) { } }
    }

    private static int NoUpdate(string installDir, InstallInfo info, Source? adopt)
    {
        if (adopt is not null) { info.Source = adopt; Save(installDir, info); Log(installDir, $"recorded as installed from: {adopt}"); }
        // (a download kept from before, of a file the site no longer has, or never installed)
        if (info.Pending is not null) { info.Pending = null; Save(installDir, info); }
        TryDelete(Path.Combine(installDir, Folder, NewFile));
        Status(installDir, $"No new version at the last check ({Today()}).");
        return 0;
    }

    private static string Today() => DateTime.Now.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);

    private static Source Describe(HttpResponseMessage response) => new()
    {
        ETag = response.Headers.ETag?.ToString(), LastModified = response.Content.Headers.LastModified, Length = response.Content.Headers.ContentLength,
    };

    // The install's time when install.json is missing (an install of before 2026-10-19 can't have a checker, but still): RACEGAME.TXT's
    private static DateTime InstalledTime(string installDir)
    {
        var menu = Path.Combine(installDir, GameInstall.MenuFile);
        return File.Exists(menu) ? File.GetLastWriteTimeUtc(menu) : DateTime.UtcNow;
    }

    // The player's choice, the game's Options: RACEGAME_SETTINGS.TXT updates= (the game keeps its settings in User\LBA2\: portable.txt)
    private static string? GameMode(string installDir)
    {
        var path = Path.Combine(installDir, "User", "LBA2", "RACEGAME_SETTINGS.TXT");
        try
        {
            if (!File.Exists(path)) return null;
            foreach (var line in File.ReadLines(path))
                if (line.StartsWith("updates=")) return line["updates=".Length..].Trim() switch { "ask" => "ask", "off" => "off", _ => "auto" };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return null;
    }

    // The site's setup -- or, for tests, one on this machine (http://localhost or 127.0.0.1 only: --update-url, LBRA_UPDATE_URL)
    private static string UpdateUrl(string? argument, Action<string> log)
    {
        var asked = argument ?? Environment.GetEnvironmentVariable(UrlVariable);
        if (string.IsNullOrWhiteSpace(asked)) return Url;
        if (Uri.TryCreate(asked, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && uri.IsLoopback) return uri.ToString();
        log($"the test address {asked} refused (http://localhost only): the site's taken");
        return Url;
    }

    // ---- what is downloaded -------------------------------------------------------------------------------------------------------

    // Null: a whole LBRA setup, `length` bytes long (when known), no older than this one. Else why not, and whether for good (not a setup,
    // an older one) or perhaps only this download (short).
    private static (string Why, bool ForGood)? Verify(string path, long? length, Action<string> log)
    {
        var size = new FileInfo(path).Length;
        if (length is { } want && size != want) return ($"{size:N0} bytes, the server said {want:N0}", false);
        if (size < 1 << 20) return ($"{size:N0} bytes: too small for the setup", true);
        using (var file = File.OpenRead(path))
        {
            var head = new byte[0x40];
            file.ReadExactly(head);
            if (head[0] != 'M' || head[1] != 'Z') return ("not a Windows program", true);
            var pe = BitConverter.ToInt32(head, 0x3C);
            if (pe <= 0 || pe > 4096) return ("not a Windows program", true);
            var sig = new byte[4];
            file.Position = pe;
            file.ReadExactly(sig);
            if (sig[0] != 'P' || sig[1] != 'E' || sig[2] != 0 || sig[3] != 0) return ("not a Windows program", true);
        }
        // (a .NET single-file program: its files listed at its end -- a file cut short has no list, or one past its end)
        var bundle = BundleCheck(path, size);
        if (bundle is not null) return (bundle, false);
        var version = FileVersionInfo.GetVersionInfo(path);
        var name = version.InternalName ?? version.OriginalFilename ?? "";
        if (!(version.ProductName ?? "").Contains("Racing Adventure", StringComparison.OrdinalIgnoreCase) || !name.StartsWith("LBRA-Setup", StringComparison.OrdinalIgnoreCase))
            return ($"not the LBRA setup (product \"{version.ProductName}\", \"{name}\")", true);
        if (!Version.TryParse(version.FileVersion, out var theirs)) return ($"no version (\"{version.FileVersion}\")", true);
        if (theirs < OwnVersion) return ($"version {theirs} is older than the one installed ({OwnVersion})", true);
        log($"the download: {version.ProductName}, {name}, version {theirs} (installed: {OwnVersion}), {size:N0} bytes, whole");
        return null;
    }

    // .NET's single-file bundle: the program part has the bundle's header offset just before a fixed 32-byte signature; the header (at
    // the file's end) lists every file with its offset and size. Null: all there.
    private static readonly byte[] BundleSignature =
    {
        0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38, 0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
        0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18, 0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae,
    };

    private static string? BundleCheck(string path, long size)
    {
        using var file = File.OpenRead(path);
        // (the signature is in the program part, the first few MB)
        var scan = new byte[(int)Math.Min(size, 16 << 20)];
        file.ReadExactly(scan);
        var at = scan.AsSpan().IndexOf(BundleSignature);
        if (at < 8) return "not a single-file setup (no bundle signature)";
        var header = BitConverter.ToInt64(scan, at - 8);
        if (header <= 0 || header >= size) return "cut short (its file list is past its end)";
        try
        {
            file.Position = header;
            using var r = new BinaryReader(file, Encoding.UTF8, true);
            var major = r.ReadUInt32();
            r.ReadUInt32();
            var count = r.ReadInt32();
            if (major < 1 || major > 20 || count <= 0 || count > 100_000) return "its file list can't be read";
            r.ReadString();   // the bundle's id
            if (major >= 2) { r.ReadInt64(); r.ReadInt64(); r.ReadInt64(); r.ReadInt64(); r.ReadUInt64(); }
            for (var i = 0; i < count; i++)
            {
                var offset = r.ReadInt64();
                var length = r.ReadInt64();
                var compressed = major >= 6 ? r.ReadInt64() : 0;
                r.ReadByte();
                r.ReadString();
                var stored = compressed > 0 ? compressed : length;
                if (offset < 0 || stored < 0 || offset + stored > size) return "cut short (a file in it is past its end)";
            }
        }
        catch (EndOfStreamException) { return "cut short (its file list ends early)"; }
        return null;
    }

    private static bool SameBytes(string a, string b)
    {
        if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
        using FileStream fa = File.OpenRead(a), fb = File.OpenRead(b);
        return SHA256.HashData(fa).AsSpan().SequenceEqual(SHA256.HashData(fb));
    }

    private static bool Ask(string? version)
    {
        var window = new AskWindow(version);
        window.ShowDialog();
        return window.Install;
    }

    // ---- --update ---------------------------------------------------------------------------------------------------------------------

    // This setup installed over the game in `installDir` with the folders it was installed from; then the game started again. Zero: done,
    // or handed to the setup window (the folders gone, or the install failed) -- `openWindow` set then.
    public static async Task<(int Code, MainWindow? Window)> Update(string installDir)
    {
        installDir = Path.GetFullPath(installDir).TrimEnd('\\', '/');
        void L(string s) => Log(installDir, s);
        L($"---- update: setup {OwnVersion} ({Environment.ProcessPath}) into {installDir}");
        var info = Load(installDir);
        string lba1 = info?.Lba1 ?? "", lba2 = info?.Lba2 ?? "";
        var pending = info?.Pending;
        if (!File.Exists(Path.Combine(installDir, GameInstall.MenuFile)))
        {
            L($"{installDir} has no game in it: the setup window instead");
            return (0, new MainWindow(null, null, installDir, pending));
        }
        if (!GameInstall.IsLba1Folder(lba1) || !GameInstall.IsLba2Folder(lba2))
        {
            var gone = !GameInstall.IsLba1Folder(lba1) ? $"Little Big Adventure's folder ({(lba1.Length > 0 ? lba1 : "not recorded")})" : $"Little Big Adventure 2's folder ({(lba2.Length > 0 ? lba2 : "not recorded")})";
            L($"{gone} isn't there any more: the setup window instead");
            MessageBox.Show($"A new version of {GameInstall.Title} is ready, but {gone} isn't there any more: the game is made from it.\n\nChoose the folders, then Install. Your saves and settings are kept.",
                GameInstall.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return (0, new MainWindow(GameInstall.IsLba1Folder(lba1) ? lba1 : null, GameInstall.IsLba2Folder(lba2) ? lba2 : null, installDir, pending));
        }
        var window = new UpdateProgressWindow();
        window.Show();
        try
        {
            // (the game closed: its exe can't be opened for writing while it runs)
            var exes = new[] { GameInstall.GameExe, GameInstall.OldGameExe }.Select(e => Path.Combine(installDir, e)).ToList();
            var waited = false;
            while (exes.Any(Running))
            {
                if (!waited) { L("waiting for the game to close"); waited = true; }
                window.Report(0, "Waiting for the game to close...");
                await Task.Delay(1000);
            }
            var lines = new List<string>();
            await Task.Run(() => GameInstall.Run(lba1, lba2, installDir, (f, what) => window.Dispatcher.Invoke(() => window.Report(f, what)), lines.Add, pending));
            File.WriteAllLines(Path.Combine(installDir, "setup.log"), lines);
            foreach (var line in lines.Where(l => l.StartsWith("updates:") || l.StartsWith("the game:") || l.Contains("shortcut") || l.StartsWith(GameInstall.OldGameExe))) L(line);
            L($"INSTALLED version {OwnVersion}; User\\ kept");
            Status(installDir, $"Updated to version {OwnVersion} on {Today()}.");
        }
        catch (Exception e)
        {
            L("FAILED: " + e);
            window.Close();
            MessageBox.Show($"The update failed: {e.Message}\n\nThe setup window opens to install the game again. Your saves and settings are kept.", GameInstall.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return (1, new MainWindow(lba1, lba2, installDir, pending));
        }
        window.Report(1, "Updated. Starting the game...");
        var game = Path.Combine(installDir, GameInstall.GameExe);
        if (Environment.GetEnvironmentVariable(NoRelaunchVariable) == "1") L($"the game not started again ({NoRelaunchVariable}=1): would start {game}");
        else
        {
            L($"starting the game again: {game}");
            try { Process.Start(new ProcessStartInfo(game) { WorkingDirectory = installDir, UseShellExecute = true }); }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { L("the game didn't start: " + e.Message); }
        }
        // (this download, Update\LBRA-Setup.new.exe, of no use now that it is Update\LBRA-Setup.exe: deleted once this program has closed)
        var self = Environment.ProcessPath;
        if (self is not null && string.Equals(Path.GetFileName(self), NewFile, StringComparison.OrdinalIgnoreCase))
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe", $"/d /c ping -n 6 127.0.0.1 >nul & del /f /q \"{self}\"") { CreateNoWindow = true, UseShellExecute = false, WorkingDirectory = installDir });
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { L("the download not deleted: " + e.Message); }
        await Task.Delay(1200);
        window.Done();
        return (0, null);
    }

    // A program running: its file can't be opened for writing
    private static bool Running(string exe)
    {
        if (!File.Exists(exe)) return false;
        try { using var f = new FileStream(exe, FileMode.Open, FileAccess.ReadWrite, FileShare.None); return false; }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
