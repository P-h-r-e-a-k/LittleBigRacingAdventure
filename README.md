# Little Big Halloween Racing Adventure

A Halloween special: a kart race through the Temple of Bù from *Little Big Adventure*, rebuilt as an island in *Little Big Adventure 2*
and decked out for Halloween (jack-o'-lanterns, graves, witches throwing fire, ghosts and bats), played on LBA Assembler's LBA2 engine
with a menu of its own in the retail game's style. It comes before the full *Little Big Racing Adventure*.

The game is made on the player's machine from their own copies of LBA1 and LBA2: nothing of either game is in this repo or in the
installer.

## What the installer does

`LBRA-Setup.exe` asks for three folders: LBA1's, LBA2's (GOG and Steam installs are found on their own) and one to install into. Then:

1. LBA2's data is copied in (its HQRs, its islands, its disc image and music; one disc image when GOG's folder has the same one twice).
   The games' own folders are only read, never changed.
2. LBA1's HQRs are copied to `lba1\` (the temple is built from its scenes).
3. LBA Assembler's race track build makes the Temple of Bù island and its Halloween race in the copy.
4. The game goes in: LBA Assembler's engine as `Little Big Halloween Racing Adventure.exe`, the race files (`specials\`), the menu
   (`RACEGAME.TXT`), the menu's picture (the Halloween backdrop, written into the copy's `SCREEN.HQR` in LBA2's own palette) and
   `portable.txt`, so saves, settings and best times stay in the install folder (`User\LBA2\`).

A desktop shortcut is optional. The window shows only the install's progress; what it did goes to `<folder>\setup.log`.
`LBRA-Setup.exe --install <LBA1> <LBA2> <folder>` installs without the window (the same log). The game's copy of the engine is marked a
windowed program, so no console window opens beside the game.

## The game

The main menu: **Story Mode** and **Track select** (greyed until the full game), **Specials**, **Best times**, the game's own
**Options** (with **Power-ups** and **Race controls** at the end), **Quit**; **Resume Game** while a race is on.

- **Specials**: the Halloween track, then a **Race** (five laps against Halloween's monsters -- the skeleton in his ghost car, the one
  to beat, the ghost in hers and the two-headed monster in his monster truck -- with the power-ups: the pumpkins on the road; the School
  of Magic's assistant drops the flag at the start, standing on the road between the grid's two columns with the cars setting off
  either side of her, then hops off to her plinth beside the road at the line to wave the chequered flag at the finish; the red magic
  ball follows the track to the car in front) or a **Time trial** (Twinsen alone, five
  laps, no pickups, a can of Gazogem at the start of every lap). A race then asks its **difficulty**:
  - **Easy**: the monsters slower, easing off when they are ahead of you and never pushing harder behind you, taking items from fewer of
    the pumpkin rows; the witches throw half as often.
  - **Medium**: the race as it always was.
  - **Hard**: the monsters quicker and pushing harder when behind you, holding their penguins, oil and magic balls until they would hit
    you; the witches throw more often.

  The last one chosen comes up first next time. Then the car: Twinsen's pumpkin F1 (the page opens on it) and ten Halloween cars, the
  monsters' first, each turning on the page (left and right to choose, Enter to race, Esc back).
- **Qualifying**: the first race always starts with a qualifying lap (no power-ups on it; its time places Twinsen on the grid; the
  monsters wait in the slip lane beside the start line meanwhile). After that, a race asks after the car page: **Qualifying lap**, or
  **Skip** and start at the back of the grid. The last answer is remembered.
- **Best times**: the ten fastest time trials of each track, with the car (a time trial is the same at every difficulty; times over
  another number of laps -- the Temple's three-lap trials before it became a five-lap race -- are kept in the file but not shown).
- After a race the game goes back to the menu by itself.

The first time: before the first race or time trial a **Controls** page, and before the first race a **Power-ups** page (every power-up
of the track in its item box, with what it does). Enter turns the pages, Esc skips them; they don't come back by themselves, but
Options shows them again.

Driving: the arrow keys, **Ctrl** uses the power-up in the item box (the behaviour menu has no use in the car), P to pause, Esc for
the menu.

The game's own settings (`RACEGAME_SETTINGS.TXT`, beside `BESTTIMES.TXT` in `User\LBA2\`) remember the pages seen, whether a qualifying
lap has been driven, the last qualifying answer and the last difficulty: deleting it, or a new install, shows the first-time pages again.

## Building

The LBA Assembler repo must be beside this one (`..\LBAAssembler`): the installer compiles its race track build from those sources
(linked, not copied) and carries its engine.

```
powershell -ExecutionPolicy Bypass -File build.ps1          # engine, pictures if missing, installer -> out\LBRA-Setup.exe
powershell -ExecutionPolicy Bypass -File tools\make_art.ps1 # redraw Game\menu.png and Installer\pumpkin.ico
```

It needs MSYS2's UCRT64 toolchain (`C:\msys64`) for the engine and the .NET 10 SDK.

## Where things are

- `Installer\` - the installer (WPF): `GameInstall.cs` is the install, `MainWindow.xaml` its window.
- `Game\menu.png` - the menu's backdrop, drawn by `tools\make_art.ps1` (no retail art in it).
- The engine side lives in LBA Assembler: `native\lba2-classic-community\SOURCES\RACEGAME.CPP` (the menu, the difficulty page, the
  car page, best times, the qualifying choice, the power-up and controls pages, the race file it composes) and `RACEMOD.CPP` (the
  race-track mode: `start_scene=`, `start_in_car=`, `trial=`, `end_to_menu=`, `grid_back=`, `difficulty=`, `marshal=`; the power-ups'
  names and descriptions). The levels' numbers are LBA Assembler's (`Terrain\RaceDifficulty.cs`), in the race file the installer writes.
