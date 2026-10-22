using System.Globalization;
using System.IO;
using System.Text;
using LBAAssembler;
using LBAAssembler.Terrain;

namespace LBRAInstaller;

// Installing the game into a folder of its own:
//   1. LBA2's data copied in: its HQRs, its islands, its disc image and music, its voices and videos -- each file as the retail game has it
//      (where the folder has a race track build's kept original, *.before-racetrack, that one);
//   2. LBA1's data copied in, to lba1\ (the Temple of Bú is made from its scenes);
//   3. the race tracks built into the copy (LBA Assembler's race track build: RaceTrackService.Build) -- Track select's fifteen tracks
//      (Tracks, since 2026-10-22) and the Temple of Bú's Halloween race;
//   4. the game: LBA Assembler's engine as the game's exe, its race files for each special track (specials\) and each of Track select's
//      (tracks\), the cars to choose from -- the specials' car page's (car=) and Track select's car grid's, every car (gridcar=) -- and the
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

    // Track select's tracks (2026-10-22 -- the user: "Pull all tracks into LBRA under Track select, but temporarily password gate them with
    // the password 1234", in this order): the island file each is raced on (RaceTrackService.RaceFor picks its track by it -- Citadel
    // Island's two files each carry one, built together: the storm track in CITADEL, the town circuit in CITABAU), the island built for it,
    // its name in the menu and its race files' names (tracks\<file>.txt, <file>_trial.txt). Not the old moon nor Sendell's Well; the Temple
    // of Bú stays a special.
    public const string TracksFolder = "tracks";
    public const string TracksCode = "1234";
    public static readonly (string IleFile, RaceTrackIsland Island, string Name, string File)[] Tracks =
    {
        ("POLAR.ILE", RaceTrackIsland.Polar, "Polar Island", "polar"),
        ("CITADEL.ILE", RaceTrackIsland.Citadel, "Citadel Island: the storm", "citadel"),
        ("CITABAU.ILE", RaceTrackIsland.Citadel, "Citadel Island: the town", "citabau"),
        ("DESERT.ILE", RaceTrackIsland.Desert, "Desert Island", "desert"),
        ("EMERAUDE.ILE", RaceTrackIsland.Emerald, "The Emerald Moon", "emeraude"),
        ("OTRINGAL.ILE", RaceTrackIsland.Otringal, "Otringal", "otringal"),
        ("CELEBRAT.ILE", RaceTrackIsland.CelebrationLava, "Celebration Island: the lava lake", "celebrat"),
        ("KNARTAS.ILE", RaceTrackIsland.Knartas, "Island of the Francos", "knartas"),
        ("ASCENCE.ILE", RaceTrackIsland.Elevator, "The Elevator Platform", "ascence"),
        ("PLATFORM.ILE", RaceTrackIsland.Wannies, "Wannies Island", "platform"),
        ("MOSQUIBE.ILE", RaceTrackIsland.Mosquibe, "Mosquibees Island", "mosquibe"),
        ("SOUSCELB.ILE", RaceTrackIsland.Volcano, "Volcano Island", "souscelb"),
        ("CXTOWER.ILE", RaceTrackIsland.CxTower, "Island CX", "cxtower"),
        ("PALACE.ILE", RaceTrackIsland.Palace, "The Emperor's palace", "palace"),
        ("CELEBRA2.ILE", RaceTrackIsland.Celebration, "Celebration Island: the statue", "celebra2"),
    };

    // The cars to choose from (RACECARS.JSON's numbers): Twinsen's Halloween buggy first -- his own buggy painted orange, with bats, the one
    // the car page opens on (HalloweenBuggy: not a RACECARS.JSON car) -- then Twinsen's pumpkin F1, the Temple's own car, held back until
    // the player wins a race (2026-10-20, the user: "Let's hold this car back until it's unlocked": PumpkinUnlock), then for time trials;
    // then the user's choice for the Temple (2026-10-20), in the user's order: the racer entity's cars 4, 3, 10, 16, 37, 25, 47, 49, then
    // the cast's 65, 90, 104. (The Halloween monsters' cars aren't offered any more: they are the Temple race's line-up, not the player's.)
    // Each car's stats, in stars from 0 to 5 in half stars -- acceleration, top speed, handling -- every car's adding up to the same
    // StarTotal: the car page shows them, and the race drives the car by them (RACEGAME.CPP, RACEMOD.CPP car_accel= car_top=
    // car_handling=: 2.5 stars the track's own setup; 5 stars +50 % pull, +8 % top speed, +20 % steering, and as much less at 0 -- on
    // the Temple a star of top speed is worth the most and one of pull the least, so the ranges differ: the cars' perfect laps within
    // 2.6 s of each other, the pull-heavy ones a little behind on a clean lap and back quicker after every hit that stops them).
    public const double StarTotal = 7.5;
    // Twinsen's Halloween buggy: his own buggy's bodies painted (RaceTrackCharacterCars.InstallHalloweenBuggy), raced as his buggy with them
    // on him (RACEMOD.CPP twinsen_body=: RaceTrackCharacterCars.HalloweenBuggyCar), not a RACECARS.JSON car drawn in its place
    public const int HalloweenBuggy = -100;
    // The pumpkin F1 locked until a race is won, then for time trials only (RACEGAME.CPP: car= unlock=<id> trial; the player's
    // RACEGAME_SETTINGS.TXT pumpkin_unlocked=1), and what the car page says of it until then
    public const string PumpkinUnlock = "pumpkin";
    public const string PumpkinHint = "Win the Halloween race to unlock";
    public static readonly (int Number, string Name, double Accel, double Top, double Handling)[] Cars =
    {
        (HalloweenBuggy, RaceTrackCharacterCars.HalloweenBuggyName, 2.5, 2.5, 2.5),   // the all-rounder: the Temple's setup as it is
        (RaceTrackCharacterCars.PumpkinNumber, "Twinsen's pumpkin F1", 2.5, 2.5, 2.5),   // the same, won: for time trials
        (4, "Zoe's car", 3, 1.5, 3),                            // a nippy runabout: quick away and into the bends, not fast
        (3, "The Emperor's car", 2, 4.5, 1),                    // the fastest at the top, heavy in the bends
        (10, "The Dark Monk's car", 1.5, 4, 2),                 // fast once going, slow to get there
        (16, "The priest's car", 2, 2.5, 3),                    // calm and sure-footed: a little better in the bends, a little slower away
        (37, "The guitarist's guitar", 4, 2, 1.5),              // a rock-and-roll start, all over the road
        (25, "The trooper's tank", 1.5, 2.5, 3.5),              // a slow start, then tracks that turn it on the spot
        (47, "Dino-Fly", 3.5, 3.5, 0.5),                        // quick and fast, but a flyer: the hardest to steer
        (49, "The Tralu's cave", 1, 4, 2.5),                    // a rolling rock: slow away, fast and steady once rolling
        (65, "The skeleton's ghost car", 3, 3, 1.5),            // a lively ghost, floaty in the bends
        (90, "The little Tralu's monster truck", 4, 1.5, 2),    // big wheels, big pull, low top speed
        (104, "The third quetch child's kart", 3, 1, 3.5),      // quick and nimble, slow at the top
    };

    public static bool IsLba2Folder(string dir) => File.Exists(Path.Combine(dir, "RESS.HQR")) && File.Exists(Path.Combine(dir, "SCENE.HQR")) && File.Exists(Path.Combine(dir, "BODY.HQR"));
    public static bool IsLba1Folder(string dir) => LBAAssembler.Lba1.Lba1Game.IsInstalled(dir);

    // `source`: the site's file this setup is (an update's download), for install.json
    // `music`: a folder the player chose for LBA1's music when none was found (the setup window asks: MusicFolderFor), kept for updates;
    // `music2` the same for LBA2's menu music (MenuMusicFolderFor)
    public static void Run(string lba1, string lba2, string target, Action<double, string> progress, Action<string> log, Updater.Source? source = null,
                           string? music = null, string? music2 = null)
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
            // (not the copies an editor kept beside its edits: OrigSCENE.HQR, _SCENE.HQR)
            var wanted = ext == ".HQR" && !stem.StartsWith("ORIG") && !stem.StartsWith('_') || (ext is ".ILE" or ".OBL") && Islands.Contains(stem)
                         || DiscExtensions.Contains(ext) && stem.StartsWith("LBA2");
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
        MenuMusicInto(lba2, target, log, music2);
        RetailFromDisc(images.Select(f => f.From).FirstOrDefault(), files.Select(f => f.To).ToList(), log);

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
        Environment.SetEnvironmentVariable("LBA1_MUSIC_DIR", MusicFolder(lba1, log, music));
        // (Track select's tracks, then the specials: each island once -- Citadel Island's build carries both its tracks)
        var builds = Tracks.Select(t => t.Island).Concat(Specials.Select(s => s.Island)).Distinct()
            .Select(island => new RaceTrackService.TrackBuild(RaceTrackPlan.Built(island), RaceTrackOptions.For(island))).ToList();
        var buildWatch = System.Diagnostics.Stopwatch.StartNew();
        // (none of the editor's private special cars in the game: LBA Assembler's RaceTrackService.PrivateCars)
        RaceTrackService.PrivateCars = false;
        var result = RaceTrackService.Build(target, builds, new Direct<RaceTrackService.BuildProgress>(p => progress(0.5 + 0.4 * p.Done, p.Doing)));
        foreach (var line in result.Log) log("  " + line);
        if (!result.Ok) throw new InvalidDataException(result.Summary);
        log(result.Summary);
        log($"the race tracks: {builds.Count} islands built in {buildWatch.Elapsed.TotalSeconds:0} s");

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
        // (Story Mode greyed: the full game's, still to come; Track select open behind a code -- RACEGAME.CPP CodePage: "temporarily password
        // gate them with the password 1234", the user, 2026-10-22: no tracks_code= line and it asks none)
        menu.Append($"story=0\ntracks=1\ntracks_code={TracksCode}\nlaps={Laps}\nend_to_menu={EndToMenuMs}\n");
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
        // Track select's tracks, in the user's order: each its race and its time trial (tracks\), as the specials'
        var tracksDir = Path.Combine(target, TracksFolder);
        Directory.CreateDirectory(tracksDir);
        var trackCount = 0;
        foreach (var (ile, island, name, file) in Tracks)
        {
            var track = RaceTrackService.RaceFor(target, ile);
            var built = track is null ? null : RaceTrackIsland.All.FirstOrDefault(i => string.Equals(i.Name, track.Island, StringComparison.OrdinalIgnoreCase));
            if (track is null || built is null || !(string.Equals(built.IleFile, ile, StringComparison.OrdinalIgnoreCase) || string.Equals(built.TwinIleFile, ile, StringComparison.OrdinalIgnoreCase)))
            {
                log($"{name}: WARNING: no track on {ile} in this build: left out of Track select");
                continue;
            }
            var raced = RaceTrackService.Raced(track);
            var race = Path.Combine(tracksDir, file + ".txt");
            var trial = Path.Combine(tracksDir, file + "_trial.txt");
            Setup().WriteEngineFile(race, track, prefix: file + "_");
            TrialSetup().WriteEngineFile(trial, track, prefix: file + "_trial_");
            foreach (var written in new[] { race, trial })
            {
                // (a sprint the story dreams or ends with -- Polar Island's dream, the palace's teleporter --: the race alone, start to
                // finish, then the menu; not its story's lines)
                if (raced.Dream is not null) WithoutStory(written);
                Relative(written, target);
            }
            menu.Append($"track={name}|{TracksFolder}/{Path.GetFileName(race)}|{TracksFolder}/{Path.GetFileName(trial)}|{raced.StartScene}\n");
            trackCount++;
            var opponents = (raced.Drivers ?? new()).Where(d => !d.Ghost).Select(d => d.Name).ToList();
            var laps = raced.Dream is not null ? "a sprint, start to finish" : built.RaceLaps is int own ? $"{own} laps" : $"{Laps} laps";
            log($"Track select {trackCount}: {name} ({ile}, {built.Name}): starts in scene {raced.StartScene}, {laps}; " +
                (opponents.Count > 0 ? $"against {string.Join(", ", opponents)}" : "no opponents") +
                ((raced.Drivers ?? new()).Any(d => d.Ghost) ? ", a time to beat" : "") + (raced.Mushrooms is { Count: > 0 } m ? $", {m.Count} power-up spots" : ""));
        }
        var cars = 0;
        foreach (var (number, name, accel, top, handling) in Cars)
        {
            // (the drive_as= value -- or, the Halloween buggy, its race file line, twinsen_body=..., and the body the car page shows: page=)
            string? drive;
            var options = "";
            if (number == HalloweenBuggy)
            {
                var buggy = RaceTrackCharacterCars.HalloweenBuggyCar(target);
                drive = buggy?.Line;
                if (buggy is { } b) options = $"|page={b.Body} {b.Anim}";
            }
            else
            {
                var line = RaceCarEngineFile.DriveAsLine(target, number);
                drive = line?.Split('\n').FirstOrDefault(l => l.StartsWith("drive_as="))?["drive_as=".Length..].Trim();
            }
            if (drive is null) { log($"no car {number} ({name}) in this build: left out"); continue; }
            // (the pumpkin F1: won by winning a race, then for time trials -- RACEGAME.CPP's padlock until then)
            if (number == RaceTrackCharacterCars.PumpkinNumber) options += $"|unlock={PumpkinUnlock} trial|hint={PumpkinHint}";
            // (its stars: acceleration, top speed, handling -- RACEGAME.CPP reads a line without them as a car with none, driven as the
            // track sets it)
            string S(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
            if (Math.Abs(accel + top + handling - StarTotal) > 0.01) log($"{name}: its stars add up to {S(accel + top + handling)}, not {S(StarTotal)}");
            menu.Append($"car={name}|{drive}|{S(accel)} {S(top)} {S(handling)}{options}\n");
            cars++;
        }
        var grid = GridCars(target, log);
        foreach (var line in grid) menu.Append(line).Append('\n');
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
        log($"the game: {GameExe}, {MenuFile}, {Specials.Length} special track(s) ({cars} cars), Track select's {trackCount} tracks ({grid.Count} cars in the car grid)");

        // ---- 5. the updater
        progress(0.99, "Setting up the updates");
        Updater.Record(target, lba1, lba2, source, log, music is not null && HasMusic(music, lba1) ? music : null,
                       music2 is not null && HasMenuMusic(music2) ? music2 : null);
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

    // A Track select race file of a sprint the story dreams or ends with (Polar Island's dream, the Emperor's palace's teleporter to
    // Celebration Island): its story's lines taken out -- the dream's intro and loss lines, the waking up a win leads to and the game
    // variable it sets -- so the race is the race alone: start to finish, then the menu (RACEMOD.CPP: a lost sprint isn't run again
    // when the race goes back to the menu, end_to_menu=).
    private static readonly string[] StoryKeys = { "intro=", "lose=", "wake=", "win=", "wake_flight=", "after_gears=" };
    private static void WithoutStory(string raceFile) =>
        File.WriteAllLines(raceFile, File.ReadAllLines(raceFile).Where(l => !StoryKeys.Any(k => l.StartsWith(k, StringComparison.Ordinal))));

    // ---- the car grid (Track select's): every car the build installed, each with its stats

    // The car grid's gridcar= lines (RACEGAME.CPP GridPage, the same fields as car=): Twinsen's Halloween buggy first, his pumpkin F1
    // (locked as on the specials' car page: won in the Halloween race, then for time trials), Halloween's monsters' cars, then every car
    // of the folder's RACECARS.JSON in its order -- the cars made by hand, then the cast's (the private ones aren't installed:
    // RaceTrackService.PrivateCars). Each car's stats: the specials' hand-set ones for the cars they list (Cars), else CarStats.
    private static List<string> GridCars(string target, Action<string> log)
    {
        string S(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        var lines = new List<string>();
        var catalogue = RaceTrackCharacterCars.Catalogue(target);
        var halloween = new[] { RaceTrackCharacterCars.PumpkinNumber, RaceTrackCharacterCars.VampireNumber, RaceTrackCharacterCars.WitchNumber, RaceTrackCharacterCars.ZombieNumber };
        var order = new List<(int Number, string Name)> { (HalloweenBuggy, RaceTrackCharacterCars.HalloweenBuggyName) };
        foreach (var n in halloween) if (catalogue.FirstOrDefault(e => e.Number == n) is { } e) order.Add((n, e.Name));
        foreach (var e in catalogue.Where(e => !halloween.Contains(e.Number)).OrderBy(e => e.Number))
        {
            // (Twinsen's laser cars, as a folder built on 2026-10-08 lists them: his own buggy firing lasers, not cars of their own)
            if (e.Name is RaceTrackCharacterCars.LaserGreenName or RaceTrackCharacterCars.LaserRedName) continue;
            order.Add((e.Number, e.Name));
        }
        var made = new Dictionary<string, int>();
        var samples = new List<string>();
        foreach (var (number, listedName) in order)
        {
            var given = Cars.FirstOrDefault(c => c.Number == number);
            var name = given.Name ?? listedName;
            string? drive;
            var options = "";
            if (number == HalloweenBuggy)
            {
                var buggy = RaceTrackCharacterCars.HalloweenBuggyCar(target);
                drive = buggy?.Line;
                if (buggy is { } b) options = $"|page={b.Body} {b.Anim}";
            }
            else drive = RaceCarEngineFile.DriveAsLine(target, number)?.Split('\n').FirstOrDefault(l => l.StartsWith("drive_as="))?["drive_as=".Length..].Trim();
            if (drive is null) { log($"the car grid: no car {number} ({name}) in this build: left out"); continue; }
            if (number == RaceTrackCharacterCars.PumpkinNumber) options += $"|unlock={PumpkinUnlock} trial|hint={PumpkinHint}";
            var (accel, top, handling, feel) = given.Name is not null ? (given.Accel, given.Top, given.Handling, "hand-set") : CarStats(number);
            if (Math.Abs(accel + top + handling - StarTotal) > 0.01) log($"the car grid: {name}'s stars add up to {S(accel + top + handling)}, not {S(StarTotal)}");
            made[feel] = made.GetValueOrDefault(feel) + 1;
            if (samples.Count < 12 && (number < 0 || number % 19 == 0 || given.Name is not null)) samples.Add($"{name} {S(accel)}/{S(top)}/{S(handling)} ({feel})");
            lines.Add($"gridcar={name}|{drive}|{S(accel)} {S(top)} {S(handling)}{options}");
        }
        log($"the car grid: {lines.Count} cars ({(lines.Count + 48) / 49} pages of 7 x 7); their stats by kind: {string.Join(", ", made.OrderByDescending(m => m.Value).Select(m => $"{m.Key} {m.Value}"))}");
        log($"the car grid's stats (acceleration/top speed/handling), some of them: {string.Join("; ", samples)}");
        return lines;
    }

    // How a kind of car drives, in stars (acceleration, top speed, handling: each kind's add up to StarTotal) -- karts nimble and slow at
    // the top, trucks and tanks slow away but sure in the bends, rockets and gliders fast and floaty, monster trucks all pull, ghosts
    // lively and floaty, ...
    private enum Feel { Roadster, Jeep, Truck, Kart, Bubble, Ball, Cart, Glider, Boat, Monster, Robot, Basket, Ghost, Rocket, Tank, Saucer, Racer, Flyer }
    private static (double Accel, double Top, double Handling) Profile(Feel feel) => feel switch
    {
        Feel.Roadster => (2.5, 3.5, 1.5),     // long and low: fast, wide in the bends
        Feel.Jeep => (2.5, 2, 3),             // sure-footed
        Feel.Truck => (1.5, 2, 4),            // slow away, slow at the top, steady
        Feel.Kart => (3.5, 1, 3),             // nimble: quick away, slow at the top
        Feel.Bubble => (3, 1.5, 3),           // a zippy little egg
        Feel.Ball => (3.5, 2, 2),             // bouncy: all pull
        Feel.Cart => (2, 2, 3.5),             // as on rails
        Feel.Glider => (1.5, 4.5, 1.5),       // fast once going, floaty
        Feel.Boat => (2, 3.5, 2),             // a fan behind it: fast on the straights
        Feel.Monster => (4, 1.5, 2),          // huge wheels, huge pull, low top speed
        Feel.Robot => (2, 2.5, 3),            // steady, precise
        Feel.Basket => (3, 1.5, 3),           // light and handy
        Feel.Ghost => (3, 3, 1.5),            // lively, floaty in the bends
        Feel.Rocket => (1.5, 5, 1),           // the fastest at the top, a handful
        Feel.Tank => (1, 2, 4.5),             // tracks: slow, turns on the spot
        Feel.Saucer => (3, 3.5, 1),           // hovering: quick, sliding
        Feel.Racer => (3, 3, 1.5),            // a race car
        _ => (2, 4, 1.5),                     // Flyer: fast, light in the bends
    };

    // The cars made by hand (the racer entity's, RACECARS.JSON 0-63) by what they are; Halloween's monsters' cars too
    private static readonly Dictionary<int, Feel> HandMade = new()
    {
        [0] = Feel.Racer, [2] = Feel.Glider, [5] = Feel.Ghost, [6] = Feel.Roadster, [7] = Feel.Robot, [8] = Feel.Truck, [9] = Feel.Rocket,
        [11] = Feel.Cart, [12] = Feel.Boat, [13] = Feel.Boat, [14] = Feel.Rocket, [15] = Feel.Saucer, [17] = Feel.Roadster, [18] = Feel.Kart,
        [19] = Feel.Tank, [20] = Feel.Basket, [21] = Feel.Boat, [22] = Feel.Truck, [23] = Feel.Truck, [24] = Feel.Kart, [26] = Feel.Tank,
        [27] = Feel.Basket, [28] = Feel.Cart, [29] = Feel.Jeep, [30] = Feel.Boat, [31] = Feel.Jeep, [32] = Feel.Truck, [33] = Feel.Flyer,
        [34] = Feel.Truck, [35] = Feel.Basket, [36] = Feel.Ball, [38] = Feel.Ball, [39] = Feel.Kart, [40] = Feel.Boat, [41] = Feel.Basket,
        [42] = Feel.Basket, [43] = Feel.Basket, [44] = Feel.Boat, [45] = Feel.Boat, [46] = Feel.Truck, [48] = Feel.Cart, [50] = Feel.Monster,
        [51] = Feel.Roadster, [52] = Feel.Ball, [53] = Feel.Ghost, [54] = Feel.Flyer, [55] = Feel.Roadster, [56] = Feel.Truck,
        [57] = Feel.Flyer, [58] = Feel.Roadster, [59] = Feel.Racer, [60] = Feel.Jeep, [61] = Feel.Cart, [62] = Feel.Jeep, [63] = Feel.Robot,
        [RaceTrackCharacterCars.VampireNumber] = Feel.Ghost, [RaceTrackCharacterCars.WitchNumber] = Feel.Flyer,
        [RaceTrackCharacterCars.ZombieNumber] = Feel.Truck,
    };

    // A car's stats: its kind's (a cast's car by the style its kind drives, RaceTrackCharacterCars.StyleOf; a car made by hand by HandMade),
    // then -- so that two cars of a kind differ -- half a star (now and then a whole one) moved from one stat to another, picked by the car's
    // number: the same every install, the total always StarTotal, each stat kept to half a star .. 5.
    private static (double Accel, double Top, double Handling, string Feel) CarStats(int number)
    {
        var cast = RaceTrackCharacterCars.Cast;
        var feel = number >= RaceTrackCharacterCars.CastFirst && number - RaceTrackCharacterCars.CastFirst < cast.Length
            ? Enum.Parse<Feel>(RaceTrackCharacterCars.StyleOf(cast[number - RaceTrackCharacterCars.CastFirst]).ToString())
            : HandMade.GetValueOrDefault(number, Feel.Jeep);
        var (a, t, h) = Profile(feel);
        var s = new[] { a, t, h };
        var hash = (uint)(number + 1000) * 2654435761u;
        hash ^= hash >> 13;
        hash *= 0x5bd1e995u;
        hash ^= hash >> 15;
        var moves = new[] { (0, 1), (1, 0), (1, 2), (2, 1), (0, 2), (2, 0) };
        var pick = (int)(hash % 8);
        if (pick < 7)
        {
            var (from, to) = moves[pick < 6 ? pick : (int)(hash >> 8) % 6];
            var amount = pick == 6 ? 1.0 : 0.5;
            if (s[from] - amount >= 0.5 && s[to] + amount <= 5) { s[from] -= amount; s[to] += amount; }
        }
        return (s[0], s[1], s[2], feel.ToString().ToLowerInvariant());
    }

    // Where LBA1's own temple music (its CD track 6) is: the chosen LBA1 folder when its CD image (GOG's LBA.GOG + LBA.DAT) or the track
    // as a file is there or beside it -- else, since a copy of LBA1's data files alone has no music (the CD's music is audio tracks, not
    // files), the LBA1 of GOG or Steam on this machine, for the music only. Null: none anywhere (the race then plays LBA2's music).
    // (`chosen`: the folder the player chose when none was found -- MusicFolderFor -- tried first)
    private static string? MusicFolder(string lba1, Action<string> log, string? chosen = null)
    {
        Environment.SetEnvironmentVariable(LBAAssembler.Terrain.Temple.TempleMusic.EnvDirectory, null);
        if (chosen is not null && HasMusic(chosen, lba1)) { log($"LBA1's music: from {chosen} (chosen)"); return chosen; }
        if (chosen is not null) log($"LBA1's music: {chosen} (chosen) has no LBA1 CD music any more");
        if (MusicFolderFor(lba1) is not { } dir) { log($"LBA1's music: no CD music in {lba1} nor in a GOG or Steam LBA1 on this machine: the race plays LBA2's music"); return null; }
        if (!string.Equals(dir, lba1, StringComparison.OrdinalIgnoreCase)) log($"LBA1's music: {lba1} has no CD music (LBA1's data files alone); taking it from {dir}");
        return dir;
    }

    // Where LBA1's temple music would be taken from for this LBA1 folder (MusicFolder), without a word: the folder itself, else a GOG or
    // Steam LBA1 on this machine; null when none has it -- the setup window then asks the player where it is (the user, 2026-10-20: "if we
    // can't find the LBA1 music, alert the user and allow them to select where we should copy it from", players reporting no music).
    public static string? MusicFolderFor(string lba1)
    {
        if (HasMusic(lba1, lba1)) return lba1;
        // (GOG's and Steam's LBA1 installs on this PC -- registry, Steam libraries, the usual folders; Steam's Classic keeps its CD tracks as
        // Common\Music\Track_06.mp3: TempleMusic.InstalledLba1Folders)
        foreach (var dir in LBAAssembler.Terrain.Temple.TempleMusic.InstalledLba1Folders().Where(d => !string.Equals(Path.GetFullPath(d).TrimEnd('\\'), Path.GetFullPath(lba1).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
            if (HasMusic(dir, lba1)) return dir;
        return null;
    }

    // LBA2's menu music -- its CD's sixth music, the title theme the menus play (TADPCM6) -- is the one music the CD has as an audio track,
    // not a file in its MUSIC folder (TADPCM1-5): a copy of the CD's files (the user's E:\dump\Test\LBA2\LBA2, 2026-10-20) has none, and the
    // game's menus were silent. GOG's LBA2 has it as LBA2.OGG over its cue sheet LBA2.DAT; a raw image of the CD (.GOG / .bin with a .cue)
    // has it as a track; a rip as MUSIC\TADPCM6.WAV or track6.ogg (the engine's fallbacks: LIB386/AIL/SDL/STREAM.CPP).
    public static bool HasMenuMusic(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return false;
            bool In(string sub, string name) => Directory.Exists(Path.Combine(dir, sub)) && Directory.EnumerateFiles(Path.Combine(dir, sub)).Any(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
            if (In("MUSIC", "TADPCM6.WAV") || In("MUSIC", "track6.ogg")) return true;
            var files = Directory.EnumerateFiles(dir).Select(f => (Path: f, Name: Path.GetFileName(f).ToUpperInvariant())).ToList();
            var cue = files.Any(f => f.Name is "LBA2.DAT" || f.Name.StartsWith("LBA2") && f.Name.EndsWith(".CUE"));
            if (cue && files.Any(f => f.Name == "LBA2.OGG")) return true;
            return cue && files.Any(f => f.Name.StartsWith("LBA2") && Path.GetExtension(f.Name) is ".GOG" or ".BIN" or ".IMG" && new FileInfo(f.Path).Length > 100_000_000);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }

    // Where the menu music would be taken from for this LBA2 folder: the folder itself, else a GOG or Steam LBA2 on this PC; null: none
    // (the setup window then asks where it is)
    public static string? MenuMusicFolderFor(string lba2)
    {
        if (HasMenuMusic(lba2)) return lba2;
        return InstalledLba2Folders().FirstOrDefault(d => !Same(d, lba2) && HasMenuMusic(d));
    }

    // GOG's and Steam's LBA2 folders on this PC, the usual places on every fixed drive
    public static IEnumerable<string> InstalledLba2Folders()
    {
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
            foreach (var where in new[] { "GOG Games", Path.Combine("Program Files (x86)", "GOG Galaxy", "Games"), Path.Combine("Program Files (x86)", "Steam", "steamapps", "common"),
                         Path.Combine("SteamLibrary", "steamapps", "common") })
                foreach (var name in new[] { "Little Big Adventure 2", Path.Combine("Little Big Adventure 2", "LBA2"), Path.Combine("Little Big Adventure 2", "Common") })
                {
                    var dir = Path.Combine(drive.RootDirectory.FullName, where, name);
                    if (Directory.Exists(dir)) yield return dir;
                }
    }

    // The menu music into the install when the copy of the LBA2 folder hasn't got it: from the chosen folder, or a GOG or Steam LBA2 on this
    // PC -- the cue sheet and its OGG or image, or the rip's file -- else said in the log (the menus will be silent).
    private static void MenuMusicInto(string lba2, string target, Action<string> log, string? chosen)
    {
        if (HasMenuMusic(target)) { log("LBA2's menu music: in its folder"); return; }
        var from = chosen is not null && HasMenuMusic(chosen) ? chosen : MenuMusicFolderFor(lba2);
        if (from is null)
        {
            log($"LBA2's menu music: WARNING: not in {lba2} (its title theme is an audio track on LBA2's CD, which a copy of the CD's files hasn't got) nor in a GOG or Steam LBA2 on this PC: the menus have no music");
            return;
        }
        var copied = new List<string>();
        // (GOG's small OGG and its cue sheet when it has them -- not its 700 MB image as well)
        var ogg = File.Exists(Path.Combine(from, "LBA2.OGG"));
        foreach (var path in Directory.GetFiles(from))
        {
            var name = Path.GetFileName(path).ToUpperInvariant();
            if (!(name is "LBA2.DAT" or "LBA2.OGG" || name.StartsWith("LBA2") && Path.GetExtension(name) is ".CUE"
                  || !ogg && name.StartsWith("LBA2") && Path.GetExtension(name) is ".GOG" or ".BIN" or ".IMG")) continue;
            var to = Path.Combine(target, Path.GetFileName(path));
            if (File.Exists(to) && new FileInfo(to).Length == new FileInfo(path).Length) continue;
            CopyPlain(path, to);
            copied.Add(Path.GetFileName(path));
        }
        foreach (var name in new[] { "TADPCM6.WAV", "track6.ogg" })
        {
            var rip = Directory.Exists(Path.Combine(from, "MUSIC")) ? Directory.EnumerateFiles(Path.Combine(from, "MUSIC")).FirstOrDefault(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase)) : null;
            if (rip is null) continue;
            Directory.CreateDirectory(Path.Combine(target, "music"));
            CopyPlain(rip, Path.Combine(target, "music", Path.GetFileName(rip)));
            copied.Add("MUSIC\\" + Path.GetFileName(rip));
        }
        log($"LBA2's menu music: not in {lba2} (an audio track on LBA2's CD); taken from {from}{(chosen is not null && Same(from, chosen) ? " (chosen)" : "")}: {string.Join(", ", copied)}");
    }

    // Whether LBA1's temple music (its CD track for `lba1`'s version) is in or beside `dir`: a CD image (LBA.GOG / LBA.iso with its cue
    // sheet, a .cue and .bin) or the track as a file (Steam's Music\Track_06.mp3) -- TempleMusic.Places/Find
    public static bool HasMusic(string dir, string lba1)
    {
        // (Places looks in LBA1_MUSIC_DIR first, which an install sets: the folder alone here)
        var env = Environment.GetEnvironmentVariable(LBAAssembler.Terrain.Temple.TempleMusic.EnvDirectory);
        try
        {
            Environment.SetEnvironmentVariable(LBAAssembler.Terrain.Temple.TempleMusic.EnvDirectory, null);
            var track = LBAAssembler.Terrain.Temple.TempleMusic.MusicNumber(lba1) + 1;
            return Directory.Exists(dir) && LBAAssembler.Terrain.Temple.TempleMusic.Find(LBAAssembler.Terrain.Temple.TempleMusic.Places(dir), track) is not null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException) { return false; }
        finally { Environment.SetEnvironmentVariable(LBAAssembler.Terrain.Temple.TempleMusic.EnvDirectory, env); }
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

    // The game's data as it came on its CD: a game folder's own files may have been changed (an editor's work saved into it -- the
    // user's GOG folder had an old race track build's SCENE.HQR and BODY.HQR, the build found no racer to copy, and the Temple's race
    // came with no opponents and no grid, 2026-10-19), but its disc image (GOG's LBA2.GOG, a raw MODE1 CD) never is. Every copied data
    // file the disc has (its LBA2 folder's HQRs and islands) is written again from it; one that differed is said. No image, or one
    // that can't be read: the folder's files stay.
    private static void RetailFromDisc(string? image, List<string> copied, Action<string> log)
    {
        if (image is null) { log("LBA2's data: the folder's own (no CD image beside it)"); return; }
        try
        {
            using var disc = new LBAAssembler.Lba1.Runtime.Lba1Iso(image);
            var onDisc = disc.Files.Where(f => f.Path.StartsWith("/LBA2/", StringComparison.OrdinalIgnoreCase) && f.Path.Count(c => c == '/') == 2)
                .GroupBy(f => Path.GetFileName(f.Path).ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First());
            var taken = 0; var changed = new List<string>();
            foreach (var to in copied)
            {
                var name = Path.GetFileName(to).ToUpperInvariant();
                if (Path.GetExtension(name) is not (".HQR" or ".ILE" or ".OBL") || !onDisc.TryGetValue(name, out var entry)) continue;
                var bytes = disc.Read(entry);
                if (new FileInfo(to).Length != bytes.Length || !File.ReadAllBytes(to).AsSpan().SequenceEqual(bytes))
                {
                    changed.Add(name);
                    File.WriteAllBytes(to, bytes);
                }
                taken++;
            }
            log($"LBA2's data: {taken} files as they are on the CD image ({Path.GetFileName(image)})" +
                (changed.Count > 0 ? $"; the folder's own {string.Join(", ", changed)} had been changed: the CD's used" : ", the folder's the same"));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or EndOfStreamException)
        {
            log($"LBA2's data: the folder's own (its CD image {Path.GetFileName(image)} could not be read: {e.Message})");
        }
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
