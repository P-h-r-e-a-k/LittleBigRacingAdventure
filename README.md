# Little Big Halloween Racing Adventure

A Halloween special: a kart race through the Temple of Bù from *Little Big Adventure*, rebuilt as an island in *Little Big Adventure 2*
and decked out for Halloween (jack-o'-lanterns, graves, witches throwing fire, ghosts and bats), played on LBA Assembler's LBA2 engine
with a menu of its own in the retail game's style. It comes before the full *Little Big Racing Adventure*.

The game is made on the player's machine from their own copies of LBA1 and LBA2: nothing of either game is in this repo or in the
installer.

## What the installer does

`LBRA-Setup.exe` asks for three folders: LBA1's, LBA2's (GOG and Steam installs are found on their own) and one to install into. Then:

1. LBA2's data is copied in (its HQRs, its islands, its disc image and music; one disc image when GOG's folder has the same one twice).
   The games' own folders are only read, never changed. The HQRs and islands are then taken from the disc image (GOG's `LBA2.GOG`),
   as they came on the CD: a game folder an editor has saved into still installs the retail game (setup.log names any file that
   differed). Copies an editor kept beside its edits (`Orig*.HQR`, `_*.HQR`) are left out.
2. LBA1's HQRs are copied to `lba1\` (the temple is built from its scenes).
3. LBA Assembler's race track build makes the Temple of Bù island and its Halloween race in the copy.
4. The game goes in: LBA Assembler's engine as `LBRA.exe`, the race files (`specials\`), the menu
   (`RACEGAME.TXT`), the menu's picture (the Halloween backdrop, written into the copy's `SCREEN.HQR` in LBA2's own palette) and
   `portable.txt`, so saves, settings and best times stay in the install folder (`User\LBA2\`).
5. The updater: the setup copies itself to `Update\LBRA-Setup.exe` and writes `Update\install.json` (the LBA1 and LBA2 folders it
   installed from, when, and which file of the site it was, if known).

LBA1's music (the temple's CD track) is looked for in the LBA1 folder and in a GOG or Steam LBA1 on the PC. When none has it, the
setup window says so and asks for the folder it's in (LBA1's CD image -- `LBA.GOG`, `LBA.iso`, a `.cue` and `.bin` -- or its tracks,
`Track_06.mp3`), or installs without it (the race then plays LBA2's music); the folder is kept in `install.json` for updates, and
`--install` takes it as a fifth argument. After an install that worked the Install button stays greyed.

A desktop shortcut (named after the game, starting `LBRA.exe`) is optional. The window shows only the install's progress; what it did
goes to `<folder>\setup.log`. `LBRA-Setup.exe --install <LBA1> <LBA2> <folder>` installs without the window (the same log). The game's
copy of the engine is marked a windowed program, so no console window opens beside the game; its window is titled "Little Big Racing Adventure". Installing over an older install (the
game was `Little Big Halloween Racing Adventure.exe` until 2026-10-19) removes the old exe and moves the desktop shortcut to `LBRA.exe`;
the player's `User\` folder is never touched.

## Updates

The setup is published at <https://lbabirthdayevent.com/LBRA-Setup.exe>; uploading a new build there is all a release takes.

- **When**: the game, at its start, once a week (`update_checked=` in `RACEGAME_SETTINGS.TXT`; never in LBA Assembler's Play or any
  run with the engine's command harness), starts `Update\LBRA-Setup.exe --check-update "<folder>" --wait-pid <the game>` hidden, and
  plays on. Options > **Window, mini-map, updates**: **Updates: Automatic / Ask first / Off** (`updates=auto|ask|off`, Enter goes round them) and
  **Check for updates now** (also with updates off: asked first then); under them what the last check said (`Update\status.txt`).
- **The check** (no window): a HEAD request over HTTPS; a new version is an ETag other than the one installed from -- or, for an install
  whose source isn't known (a setup run by hand), a file newer than the install or of another size. It is downloaded to
  `Update\LBRA-Setup.new.exe` (three tries) and kept only if it is whole (its length as the server said, a Windows program, .NET's
  single-file bundle complete to its last file), the LBRA setup (product and assembly name) and no older than the installed one (the
  setup's version is its build's date and time, `build.ps1`). Anything else is deleted and never run; a file refused for what it is
  isn't downloaded again. The same build as the installed one is only recorded.
- **Installing**: only once the game is closed. Automatic: `LBRA-Setup.new.exe --update "<folder>"` runs; Ask first: "A new version of
  Little Big Halloween Racing Adventure is ready. Install it now?" -- **Install**, or **Later** (the download kept for the next check).
  `--update` reinstalls into the same folder from the stored LBA1 and LBA2 folders with a small progress window, keeps `User\` (saves,
  settings, best laps), becomes `Update\LBRA-Setup.exe`, records the file it came from and starts `LBRA.exe` again. If the stored folders
  are gone it says so and opens the setup window, filled in.
- Every step goes to `Update\update.log`.
- For tests only: `LBRA_UPDATE_URL` (or `--update-url`) points the check at a server on `http://localhost` / `127.0.0.1` (nothing else
  is taken), `LBRA_UPDATE_NO_RELAUNCH=1` leaves the game closed after an update, `LBRA_DESKTOP_DIR` stands in for the desktop. A
  headless run of the game as a player starts it (`LBRA.exe --headless --no-audio`, no harness flags) reaches the menu and the check.

## The game

The main menu: **Story Mode** and **Track select** (greyed until the full game), **Specials**, **Best laps**, the game's own
**Options** (with **Race controls, power-ups** and **Window, mini-map, updates** at the end), **Quit**; **Resume Game** while a race is on.

- **Specials**: the Halloween track, then a **Race** (five laps against Halloween's monsters -- the vampire in his coffin, the one
  to beat, the witch on her broomstick and the zombie in his hearse -- with the power-ups: the pumpkins on the road; the School
  of Magic's assistant drops the flag at the start, standing on the road between the grid's two columns with the cars setting off
  either side of her, then hops off to her plinth beside the road at the line to wave the chequered flag at the finish; the red magic
  ball follows the track to the car in front) or a **Time trial** (Twinsen alone, five
  laps, no pickups and no power-ups at all; its fastest lap goes to Best laps). A race then asks its **difficulty**:
  - **Easy**: the monsters slower, easing off when they are ahead of you and never pushing harder behind you, taking items from fewer of
    the pumpkin rows; the witches throw half as often.
  - **Medium**: the race as it always was.
  - **Hard**: the monsters quicker and pushing harder when behind you, holding their penguins, oil and magic balls until they would hit
    you; the witches throw more often.

  The last one chosen comes up first next time. Then the car, each turning on the page (left and right to choose, Enter to race, Esc
  back): Twinsen's pumpkin F1 (the page opens on it), Zoe's car, the Emperor's car, the Dark Monk's car, the priest's car, the
  guitarist's guitar, the trooper's tank, Dino-Fly, the Tralu's cave, the skeleton's ghost car, the little Tralu's monster truck and
  the third quetch child's kart. Under each car's name its stats, 0 to 5 stars in half stars: **Acceleration**, **Top speed** and
  **Handling**. Every car's stars add up to the same 7.5, so what a car has more of it pays for in the others, and the race drives it
  by them: the pumpkin F1's 2.5 stars each are the track's own car; from there, up to 5 stars gives up to half as much pull again,
  8 % more top speed and a fifth more steering, and fewer stars as much less. (A time trial uses the car too; Best laps shows it.)

  | Car | Acceleration | Top speed | Handling |
  | --- | :-: | :-: | :-: |
  | Twinsen's pumpkin F1 | 2.5 | 2.5 | 2.5 |
  | Zoe's car | 3 | 1.5 | 3 |
  | The Emperor's car | 2 | 4.5 | 1 |
  | The Dark Monk's car | 1.5 | 4 | 2 |
  | The priest's car | 2 | 2.5 | 3 |
  | The guitarist's guitar | 4 | 2 | 1.5 |
  | The trooper's tank | 1.5 | 2.5 | 3.5 |
  | Dino-Fly | 3.5 | 3.5 | 0.5 |
  | The Tralu's cave | 1 | 4 | 2.5 |
  | The skeleton's ghost car | 3 | 3 | 1.5 |
  | The little Tralu's monster truck | 4 | 1.5 | 2 |
  | The third quetch child's kart | 3 | 1 | 3.5 |
- **Qualifying**: the first race always starts with a qualifying lap (no power-ups on it; its time places Twinsen on the grid; the
  monsters wait in the slip lane beside the start line meanwhile). After that, a race asks after the car page: **Qualifying lap**, or
  **Skip** and start at the back of the grid. The last answer is remembered.
- **Best laps**: the ten fastest laps of each track, with the car: each time trial's fastest lap and every qualifying lap -- the laps
  driven with no power-ups (a time trial is the same at every difficulty; a race's laps never count). At the end of a time trial the screen shows its best lap and the rank it
  took, and the whole time under it. The whole five-lap times kept by earlier versions stay in `BESTTIMES.TXT` but aren't shown as laps.
- After a race the game goes back to the menu by itself.
- **Time trial display**: over the lap the display shows **Best time** -- the track's best lap from Best laps (`--:--.--` before there
  is one), or the trial's own fastest lap as soon as one beats it. At the end of every lap, in races and time trials, the lap's time
  stays on the lap line for two seconds before the next lap's clock runs, in gold when it is a new best time.
- **Mini-map**: a small map of the whole track in the bottom-right corner while you race, with a dot for every car -- yours the big
  yellow one, each monster its own colour. Options > **Window, mini-map, updates** > **Mini-map: On / Off** turns it off or back on
  (`minimap=` in `RACEGAME_SETTINGS.TXT`; on at first). It is hidden behind the menu and the pause, and once you finish.

The first time: before the first race or time trial a **Controls** page, and before the first race a **Power-ups** page (every power-up
of the track in its item box, with what it does). Enter turns the pages, Esc skips them; they don't come back by themselves, but
Options shows them again.

Driving: the arrow keys, **Ctrl** uses the power-up in the item box (the behaviour menu has no use in the car), P to pause, Esc for
the menu. **F11** or **Alt+Enter** switches between full screen and a window at any time (also Options > **Window, mini-map, updates**;
kept in `lba2.cfg`). In a window the game fills the window whatever its shape: resize or maximise it and the menus, the race and
its display are laid out to fit, with no black bars. The window's size, and whether it was maximised, is remembered for the next start
(`WindowWidth`, `WindowHeight`, `WindowMaximized` in `User\LBA2\lba2.cfg`). Full screen is unchanged.

**Out of the car** (the Halloween track): stop and press **Space** (a pad's **X** button) to get out and explore the temple on foot --
Twinsen runs, and Space jumps. The race goes on without you: the monsters keep racing and the clock keeps running. **Space** beside
your car gets you back in it; **Ctrl** (a pad's left shoulder) anywhere puts you straight back in it, as does a fall into the dark,
or five minutes on foot. Somewhere in the temple a secret is waiting for those who go looking -- a jack-o'-lantern off the long
corridor shows the way.

The game's own settings (`RACEGAME_SETTINGS.TXT`, beside `BESTTIMES.TXT` in `User\LBA2\`) remember the pages seen, whether a qualifying
lap has been driven, the last qualifying answer, the last difficulty, the mini-map, the updates' mode and the last update check: deleting it, or a
new install into another folder, shows the first-time pages again (an update, or an install over the game, keeps it).

## Building

The LBA Assembler repo must be beside this one (`..\LBAAssembler`): the installer compiles its race track build from those sources
(linked, not copied) and carries its engine.

```
powershell -ExecutionPolicy Bypass -File build.ps1          # engine, pictures if missing, installer -> out\LBRA-Setup.exe (version: now)
powershell -ExecutionPolicy Bypass -File tools\make_art.ps1 # redraw Game\menu.png and Installer\pumpkin.ico
```

It needs MSYS2's UCRT64 toolchain (`C:\msys64`) for the engine and the .NET 10 SDK.

## Where things are

- `Installer\` - the installer (WPF): `GameInstall.cs` is the install, `MainWindow.xaml` its window, `Updater.cs` the update check and
  `--update`, `UpdateWindows.cs` the "Install it now?" question and the update's progress window.
- `Game\menu.png` - the menu's backdrop, drawn by `tools\make_art.ps1` (no retail art in it).
- The engine side lives in LBA Assembler: `native\lba2-classic-community\SOURCES\RACEGAME.CPP` (the menu, the difficulty page, the
  car page, best times, the qualifying choice, the power-up and controls pages, the Updates page and the weekly check, the race file it
  composes), `RACEUPDATE.CPP` (the updater started, hidden) and `RACEMOD.CPP` (the
  race-track mode: `start_scene=`, `start_in_car=`, `trial=`, `end_to_menu=`, `grid_back=`, `difficulty=`, `marshal=`; the power-ups'
  names and descriptions). The levels' numbers are LBA Assembler's (`Terrain\RaceDifficulty.cs`), in the race file the installer writes.
