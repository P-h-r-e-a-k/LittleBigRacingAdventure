using System.IO;
using System.Text;
using LBAAssembler;
using LBAAssembler.Terrain;

namespace LBRAInstaller;

// Installing the game into a folder of its own:
//   1. LBA2's data copied in: its HQRs, its islands, its disc image and music, its voices and videos -- each file as the retail game has it
//      (where the folder has a race track build's kept original, *.before-racetrack, that one);
//   2. LBA1's data copied in, to lba1\ (the Temple of Bú is made from its scenes);
//   3. the race tracks built into the copy (LBA Assembler's race track build: RaceTrackService.Build) -- the Temple of Bú's Halloween race;
//   4. the game: LBA Assembler's engine as the game's exe, its race file for each special track (specials\), the cars to choose from and the
//      menu (RACEGAME.TXT), and portable.txt (its saves and settings beside it);
//   5. the updater: this setup copied to Update\, what it installed from in Update\install.json (Updater.Record).
// The player's User\ folder (saves, settings, best laps) is never touched: an install over an install -- an update -- keeps it.
internal static class GameInstall
{
    // (LBRA.exe since 2026-10-19 -- the user: "this needs to be 'Little Big Racing Adventure.exe' or 'LBRA.exe'"; an install over one
    // made before takes the old exe away and moves the desktop shortcut to the new one)
    public const string GameExe = "LBRA.exe";
    public const string OldGameExe = "Little Big Halloween Racing Adventure.exe";
    public const string Title = "Little Big Halloween Racing Adventure";
    public const string WindowTitle = "Little Big Racing Adventure";
    public const string MenuFile = "RACEGAME.TXT";
    public const string Lba1Folder = "lba1";
    public const string SpecialsFolder = "specials";

    // LBA2's islands as the retail game has them (anything else of the folder's -- a user's copies -- left behind)
    private static readonly string[] Islands = { "ASCENCE", "CELEBRA2", "CELEBRAT", "CITABAU", "CITADEL", "DESERT", "EMERAUDE", "ILOTCX", "KNARTAS", "MOON", "MOSQUIBE", "OTRINGAL", "PLATFORM", "SOUSCELB" };
    // ... its disc image and music, and the folders of its voices, videos and music
    private static readonly string[] DiscExtensions = { ".ISO", ".GOG", ".OGG", ".CUE", ".BIN", ".DAT" };
    private static readonly string[] DataFolders = { "VOX", "VIDEO", "MUSIC" };
    private const string Kept = ".before-racetrack";

    // The specials: the special tracks' islands, their names in the menu, their race files' names (specials\<file>.txt, <file>_trial.txt)
    public static readonly (RaceTrackIsland Island, string Name, string File)[] Specials =
    {
        (RaceTrackIsland.Temple, "Halloween Temple of Bu", "temple"),
    };

    // The cars to choose from (RACECARS.JSON's numbers): Twinsen's pumpkin F1 first -- the Temple's own car, the one the car page opens on --
    // then the Halloween ones: the three monsters the Temple's race is against first (LBA Assembler's RaceDriver.Temple, since 2026-10-18:
    // the vampire in his coffin, the one to beat, the witch on her broomstick and the zombie in his hearse), the monsters who raced it before
    // them (the skeleton, the ghost, the two-headed monster), the other monsters, then the Dark Monk's, Ker'aooc's, Tabata's and the Tralu's
    public static readonly (int Number, string Name)[] Cars =
    {
        (RaceTrackCharacterCars.PumpkinNumber, "Twinsen's pumpkin F1"),
        (RaceTrackCharacterCars.VampireNumber, "Vampire's coffin"),
        (RaceTrackCharacterCars.WitchNumber, "Witch's broomstick"),
        (RaceTrackCharacterCars.ZombieNumber, "Zombie's hearse"),
        (65, "Skeleton's ghost car"),
        (114, "Ghost's ghost car"),
        (149, "Two-headed monster truck"),
        (107, "Giant spider's truck"),
        (188, "Gas monster's ghost car"),
        (130, "The Eye's ghost car"),
        (10, "Dark Monk's car"),
        (53, "Ker'aooc's cauldron"),
        (54, "Tabata's spell book"),
        (49, "Tralu's cave"),
    };

    public static bool IsLba2Folder(string dir) => File.Exists(Path.Combine(dir, "RESS.HQR")) && File.Exists(Path.Combine(dir, "SCENE.HQR")) && File.Exists(Path.Combine(dir, "BODY.HQR"));
    public static bool IsLba1Folder(string dir) => LBAAssembler.Lba1.Lba1Game.IsInstalled(dir);

    // `source`: the site's file this setup is (an update's download), for install.json
    public static void Run(string lba1, string lba2, string target, Action<double, string> progress, Action<string> log, Updater.Source? source = null)
    {
        if (!IsLba1Folder(lba1)) throw new InvalidDataException($"{lba1} isn't an LBA1 game folder (it needs SCENE.HQR, LBA_GRI.HQR, LBA_BLL.HQR, LBA_BRK.HQR and RESS.HQR).");
        if (!IsLba2Folder(lba2)) throw new InvalidDataException($"{lba2} isn't an LBA2 game folder (it needs RESS.HQR, SCENE.HQR and BODY.HQR).");
        Directory.CreateDirectory(target);
        if (Same(target, lba2) || Same(target, lba1)) throw new InvalidDataException("Install into a folder of its own, not into a game's folder.");

        // ---- 1. LBA2
        progress(0.01, "Copying LBA2's data");
        var files = new List<(string From, string To)>();
        foreach (var path in Directory.GetFiles(lba2))
        {
            var name = Path.GetFileName(path);
            var ext = Path.GetExtension(name).ToUpperInvariant();
            var stem = Path.GetFileNameWithoutExtension(name).ToUpperInvariant();
            var wanted = ext == ".HQR" || (ext is ".ILE" or ".OBL") && Islands.Contains(stem) || DiscExtensions.Contains(ext) && stem.StartsWith("LBA2");
            if (!wanted) continue;
            var original = path + Kept;
            files.Add((File.Exists(original) ? original : path, Path.Combine(target, name)));
        }
        // (one disc image: GOG's folder has the same image twice, LBA2.GOG and LBA2.iso, and the engine mounts whichever it finds)
        var images = files.Where(f => new FileInfo(f.From).Length > 100_000_000).ToList();
        foreach (var twin in images.Skip(1).Where(f => images.Take(images.IndexOf(f)).Any(g => SameFile(g.From, f.From))).ToList()) files.Remove(twin);
        foreach (var folder in Directory.GetDirectories(lba2).Where(d => DataFolders.Contains(Path.GetFileName(d).ToUpperInvariant())))
            foreach (var path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                files.Add((path, Path.Combine(target, Path.GetRelativePath(lba2, path))));
        var total = Math.Max(1, files.Sum(f => new FileInfo(f.From).Length));
        long done = 0;
        foreach (var (from, to) in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            CopyPlain(from, to);
            done += new FileInfo(from).Length;
            progress(0.01 + 0.44 * done / total, $"Copying LBA2's data: {Path.GetFileName(to)}");
        }
        log($"LBA2: {files.Count} files copied ({total / (1024 * 1024)} MB)");

        // ---- 2. LBA1
        progress(0.45, "Copying LBA1's data");
        var lba1Target = Path.Combine(target, Lba1Folder);
        Directory.CreateDirectory(lba1Target);
        var lba1Files = Directory.GetFiles(lba1, "*.HQR");
        foreach (var path in lba1Files) CopyPlain(path, Path.Combine(lba1Target, Path.GetFileName(path)));
        log($"LBA1: {lba1Files.Length} files copied");

        // ---- 3. the race tracks
        progress(0.5, "Building the race tracks");
        Environment.SetEnvironmentVariable("LBA1_DIR", lba1Target);
        // (LBA1's own temple music, CD track 6, from the player's LBA1 folder -- its CD image beside the HQRs copied above: TempleMusic)
        Environment.SetEnvironmentVariable("LBA1_MUSIC_DIR", MusicFolder(lba1, log));
        var builds = Specials.Select(s => new RaceTrackService.TrackBuild(RaceTrackPlan.Built(s.Island), RaceTrackOptions.For(s.Island))).ToList();
        // (none of the editor's private special cars in the game: LBA Assembler's RaceTrackService.PrivateCars)
        RaceTrackService.PrivateCars = false;
        var result = RaceTrackService.Build(target, builds, new Direct<RaceTrackService.BuildProgress>(p => progress(0.5 + 0.4 * p.Done, p.Doing)));
        foreach (var line in result.Log) log("  " + line);
        if (!result.Ok) throw new InvalidDataException(result.Summary);
        log(result.Summary);

        // ---- 4. the game
        progress(0.9, "Writing the game");
        var specials = Path.Combine(target, SpecialsFolder);
        Directory.CreateDirectory(specials);
        var info = RaceTrackService.ReadInfo(target) ?? throw new InvalidDataException("The race track build wrote no RACETRACK.JSON.");
        var menu = new StringBuilder();
        menu.Append($"# {Title}: the race game's menu (LBA Assembler's engine reads it: RACEGAME.CPP). Made by the installer.\n");
        menu.Append($"title={Title}\n");
        // (the window's title bar: the series' name, not the engine's -- RACEGAME.CPP RaceGame_WindowTitle, read before the window is made)
        menu.Append($"window_title={WindowTitle}\n");
        // (Story Mode and Track select greyed: the full game's, still to come)
        menu.Append($"story=0\ntracks=0\nlaps={Laps}\nend_to_menu={EndToMenuMs}\n");
        foreach (var (island, name, file) in Specials)
        {
            var track = RaceTrackService.RaceFor(target, island.IleFile) ?? info;
            // the race (the line-up, the power-ups) and the time trial (Twinsen alone, no mushrooms: RACEMOD.CPP trial=, which the engine
            // adds), each its own file, its lines beside it
            var race = Path.Combine(specials, file + ".txt");
            var trial = Path.Combine(specials, file + "_trial.txt");
            Setup().WriteEngineFile(race, track, prefix: file + "_");
            TrialSetup().WriteEngineFile(trial, track, prefix: file + "_trial_");
            foreach (var written in new[] { race, trial }) Relative(written, target);
            menu.Append($"special={name}|{SpecialsFolder}/{Path.GetFileName(race)}|{SpecialsFolder}/{Path.GetFileName(trial)}|{track.StartScene}\n");
            log($"{name}: starts in scene {track.StartScene}; against {string.Join(", ", (track.Drivers ?? new()).Where(d => !d.Ghost).Select(d => d.Name))}" +
                (island.Levels is not null ? "; easy, medium and hard (the menu's difficulty page)" : ""));
        }
        var cars = 0;
        foreach (var (number, name) in Cars)
        {
            var line = RaceCarEngineFile.DriveAsLine(target, number);
            var drive = line?.Split('\n').FirstOrDefault(l => l.StartsWith("drive_as="))?["drive_as=".Length..].Trim();
            if (drive is null) { log($"no car {number} ({name}) in this build: left out"); continue; }
            menu.Append($"car={name}|{drive}\n");
            cars++;
        }
        File.WriteAllText(Path.Combine(target, MenuFile), menu.ToString(), new UTF8Encoding(false));
        // the menu's picture: the Halloween backdrop in LBA2's own palette, in the copy's SCREEN.HQR
        MenuPicture(target);
        log("the menu's picture: SCREEN.HQR entry 4");
        using (var engine = typeof(GameInstall).Assembly.GetManifestResourceStream("engine.exe") ?? throw new InvalidDataException("The installer has no engine in it."))
        using (var exe = File.Create(Path.Combine(target, GameExe))) engine.CopyTo(exe);
        // (LBA Assembler's engine is a console program -- its headless runs log to the console -- so Windows would open a console window
        // beside the game: the game's copy is marked a windowed program instead; output redirected by a caller still works)
        if (WindowedProgram(Path.Combine(target, GameExe))) log($"{GameExe}: a windowed program (no console window)");
        var old = Path.Combine(target, OldGameExe);
        if (File.Exists(old))
        {
            try { File.Delete(old); log($"{OldGameExe} removed (the game is {GameExe} now)"); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { log($"{OldGameExe} not removed: {e.Message}"); }
        }
        foreach (var res in typeof(GameInstall).Assembly.GetManifestResourceNames().Where(r => r.StartsWith("game/")))
        {
            var to = Path.Combine(target, res["game/".Length..].Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            using var s = typeof(GameInstall).Assembly.GetManifestResourceStream(res)!;
            using var f = File.Create(to);
            s.CopyTo(f);
        }
        File.WriteAllText(Path.Combine(target, "portable.txt"), "Little Big Halloween Racing Adventure keeps its saves and settings in this folder.\n");
        // (the race track build's own keepsakes -- the files as they were before it, its .bak copies -- of no use to the game: a reinstall
        // copies LBA2's files in afresh)
        foreach (var kept in Directory.GetFiles(target, "*" + Kept).Concat(Directory.GetFiles(target, "*.bak"))) File.Delete(kept);
        log($"the game: {GameExe}, {MenuFile}, {Specials.Length} special track(s), {cars} cars");

        // ---- 5. the updater
        progress(0.99, "Setting up the updates");
        Updater.Record(target, lba1, lba2, source, log);
        progress(1, "Installed");
    }

    // (a race's laps: the menu's, for a special whose race file names none -- the Temple of Bú's names its own five, RaceTrackIsland.RaceLaps,
    // and the engine races those: RACEGAME.CPP SpecialLaps)
    public const int Laps = 3;
    public const int EndToMenuMs = 8000;
    public const string IconFile = "pumpkin.ico";

    // The race file's own files (the opponents' lines, the line to drive by, the raised road) named from the game's folder, so the folder
    // can move: the engine finds them beside its data (RACEGAME.CPP)
    private static void Relative(string raceFile, string target)
    {
        var root = Path.GetFullPath(target).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
        var lines = File.ReadAllLines(raceFile).Select(l =>
        {
            var eq = l.IndexOf('=');
            if (eq < 0 || l.StartsWith('#')) return l;
            var value = l[(eq + 1)..];
            return value.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? l[..(eq + 1)] + value[root.Length..].Replace('\\', '/') : l;
        });
        File.WriteAllLines(raceFile, lines);
    }

    // Where LBA1's own temple music (its CD track 6) is: the chosen LBA1 folder when its CD image (GOG's LBA.GOG + LBA.DAT) or the track
    // as a file is there or beside it -- else, since a copy of LBA1's data files alone has no music (the CD's music is audio tracks, not
    // files), the LBA1 of GOG or Steam on this machine, for the music only. Null: none anywhere (the race then plays LBA2's music).
    private static string? MusicFolder(string lba1, Action<string> log)
    {
        Environment.SetEnvironmentVariable(LBAAssembler.Terrain.Temple.TempleMusic.EnvDirectory, null);
        var track = LBAAssembler.Terrain.Temple.TempleMusic.MusicNumber(lba1) + 1;
        bool Has(string dir)
        {
            try { return Directory.Exists(dir) && LBAAssembler.Terrain.Temple.TempleMusic.Find(LBAAssembler.Terrain.Temple.TempleMusic.Places(dir), track) is not null; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException) { return false; }
        }
        if (Has(lba1)) return lba1;
        // (GOG's and Steam's LBA1 installs on this PC -- registry, Steam libraries, the usual folders; Steam's Classic keeps its CD tracks as
        // Common\Music\Track_06.mp3: TempleMusic.InstalledLba1Folders)
        foreach (var dir in LBAAssembler.Terrain.Temple.TempleMusic.InstalledLba1Folders().Where(d => !string.Equals(Path.GetFullPath(d).TrimEnd('\\'), Path.GetFullPath(lba1).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
            if (Has(dir))
            {
                log($"LBA1's music: {lba1} has no CD music (LBA1's data files alone); taking it from {dir}");
                return dir;
            }
        log($"LBA1's music: no CD music in {lba1} nor in a GOG or Steam LBA1 on this machine: the race plays LBA2's music");
        return null;
    }

    // A program's PE header marked a windowed (GUI) program rather than a console one: the optional header's Subsystem field (offset 68 in
    // both PE32 and PE32+), 3 = console, 2 = windows. False when it isn't a console program already (left as it is).
    private static bool WindowedProgram(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
        var buffer = new byte[4];
        file.Position = 0x3C;
        file.ReadExactly(buffer);
        var pe = BitConverter.ToInt32(buffer);
        file.Position = pe;
        file.ReadExactly(buffer);
        if (buffer[0] != 'P' || buffer[1] != 'E' || buffer[2] != 0 || buffer[3] != 0) throw new InvalidDataException($"{Path.GetFileName(path)} isn't a Windows program.");
        var subsystem = pe + 4 + 20 + 68;
        file.Position = subsystem;
        var value = file.ReadByte() | file.ReadByte() << 8;
        if (value != 3) return false;
        file.Position = subsystem;
        file.Write(new byte[] { 2, 0 });
        return true;
    }

    // The menu's picture (SCREEN.HQR entry 4, a 640x480 picture in RESS.HQR's palette 0, the menus' own): the installer's Halloween
    // backdrop, its colours matched to that palette with a little dithering (Floyd-Steinberg) so its night sky's gradients don't band
    private static void MenuPicture(string target)
    {
        const int width = 640, height = 480;
        var screen = Path.Combine(target, "SCREEN.HQR");
        var palette = HqrArchive.Open(Path.Combine(target, "RESS.HQR")).Read(0);
        if (palette.Length < 768 || !File.Exists(screen)) throw new InvalidDataException("LBA2's RESS.HQR has no palette, or there is no SCREEN.HQR.");
        using var png = typeof(GameInstall).Assembly.GetManifestResourceStream("art/menu.png") ?? throw new InvalidDataException("The installer has no menu picture in it.");
        var frame = System.Windows.Media.Imaging.BitmapFrame.Create(png, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        var bgra = new System.Windows.Media.Imaging.FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        if (bgra.PixelWidth != width || bgra.PixelHeight != height) throw new InvalidDataException("The menu picture isn't 640x480.");
        var pixels = new byte[width * height * 4];
        bgra.CopyPixels(pixels, width * 4, 0);
        var error = new float[(width + 2) * 2 * 3];
        var picture = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            var here = (y & 1) * (width + 2) * 3;
            var next = ((y + 1) & 1) * (width + 2) * 3;
            Array.Clear(error, next, (width + 2) * 3);
            for (var x = 0; x < width; x++)
            {
                var p = (y * width + x) * 4;
                var e = here + (x + 1) * 3;
                float r = pixels[p + 2] + error[e] * 0.75f, g = pixels[p + 1] + error[e + 1] * 0.75f, b = pixels[p] + error[e + 2] * 0.75f;
                int best = 0;
                var bestD = float.MaxValue;
                for (var c = 0; c < 256; c++)
                {
                    float dr = r - palette[c * 3], dg = g - palette[c * 3 + 1], db = b - palette[c * 3 + 2];
                    var d = dr * dr * 0.3f + dg * dg * 0.59f + db * db * 0.11f;
                    if (d < bestD) { bestD = d; best = c; }
                }
                picture[y * width + x] = (byte)best;
                float er = r - palette[best * 3], eg = g - palette[best * 3 + 1], eb = b - palette[best * 3 + 2];
                void Spread(int at, float k) { error[at] += er * k; error[at + 1] += eg * k; error[at + 2] += eb * k; }
                Spread(e + 3, 7 / 16f);
                Spread(next + x * 3, 3 / 16f);
                Spread(next + (x + 1) * 3, 5 / 16f);
                Spread(next + (x + 2) * 3, 1 / 16f);
            }
        }
        File.WriteAllBytes(screen, HqrWriter.ReplaceEntry(File.ReadAllBytes(screen), 4, HqrWriter.CompressedEntry(picture)));
    }

    // The race car as the game drives it: the LBA Assembler user's own setup (2026-10-16) -- five gears to 110 km/h, quick off the line,
    // sharp brakes and steering -- against the line-up, with the power-ups, straight onto the grid (no qualifying lap in the file: the
    // engine's race game adds one to the first race, and after that one or Twinsen at the back of the grid, as the player chooses). A track
    // with levels (the Temple's: easy, medium, hard) has them all in its race file, medium its own; the menu's difficulty page adds the
    // player's pick after them (RACEGAME.CPP).
    private static RaceCarSetup Setup() => new()
    {
        Gears = 5, GearTopKmh = new() { 45, 57, 63, 97, 110, 40 }, AccelerationPercent = 300, BrakingPercent = 300, CoastingPercent = 300,
        ReverseKmh = 24, SteeringPercent = 200, Automatic = true, ShowDisplay = true, Opponent = true, MainSkill = 86, RacerSkill = 70,
        PowerUps = true, Qualifying = false, FineWeather = true, OpponentsFightBack = true, StartAtLine = true, Difficulty = RaceDifficulty.Medium,
    };

    // ... and for a time trial: the same car, alone on the track, no mushrooms (the engine hands out a can of Gazogem every lap)
    private static RaceCarSetup TrialSetup()
    {
        var car = Setup();
        car.Opponent = false;
        car.PowerUps = false;
        return car;
    }

    // (a plain copy -- never a link: a link to the game's own file would be changed by the race track build with it)
    private static void CopyPlain(string from, string to)
    {
        if (File.Exists(to)) { var fi = new FileInfo(to); if (fi.IsReadOnly) fi.IsReadOnly = false; }
        File.Copy(from, to, true);
        var info = new FileInfo(to);
        if (info.IsReadOnly) info.IsReadOnly = false;
    }

    // Progress said as it is made (Progress<T> posts it to the window's thread, later: after a windowless install's log is closed)
    private sealed class Direct<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    // Two big files the same: their sizes, and their first and last megabytes
    private static bool SameFile(string a, string b)
    {
        if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
        using FileStream fa = File.OpenRead(a), fb = File.OpenRead(b);
        var size = (int)Math.Min(1 << 20, fa.Length);
        byte[] ba = new byte[size], bb = new byte[size];
        foreach (var at in new[] { 0, fa.Length - size })
        {
            fa.Position = fb.Position = at;
            fa.ReadExactly(ba); fb.ReadExactly(bb);
            if (!ba.AsSpan().SequenceEqual(bb)) return false;
        }
        return true;
    }

    private static bool Same(string a, string b) => string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
}
