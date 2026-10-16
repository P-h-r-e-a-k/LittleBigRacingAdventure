# Little Big Halloween Racing Adventure

A Halloween special: a kart race through the Temple of Bù from *Little Big Adventure*, rebuilt as an island in *Little Big Adventure 2*
and decked out for Halloween (jack-o'-lanterns, graves, witches throwing fire, ghosts and bats), played on LBA Assembler's LBA2 engine
with a menu of its own in the retail game's style. It comes before the full *Little Big Racing Adventure*.

The game is made on the player's machine from their own copies of LBA1 and LBA2: nothing of either game is in this repo or in the
installer.

## What the installer does

`LBHRA-Setup.exe` asks for three folders: LBA1's, LBA2's (GOG and Steam installs are found on their own) and one to install into. Then:

1. LBA2's data is copied in (its HQRs, its islands, its disc image and music; one disc image when GOG's folder has the same one twice).
   The games' own folders are only read, never changed.
2. LBA1's HQRs are copied to `lba1\` (the temple is built from its scenes).
3. LBA Assembler's race track build makes the Temple of Bù island and its Halloween race in the copy.
4. The game goes in: LBA Assembler's engine as `Little Big Halloween Racing Adventure.exe`, the race files (`specials\`), the menu
   (`RACEGAME.TXT`), the menu's picture (the Halloween backdrop, written into the copy's `SCREEN.HQR` in LBA2's own palette) and
   `portable.txt`, so saves, settings and best times stay in the install folder (`User\LBA2\`).

A desktop shortcut is optional. `LBHRA-Setup.exe --install <LBA1> <LBA2> <folder>` installs without the window (log in
`<folder>\setup.log`).

## The game

The main menu: **Story Mode** and **Track select** (greyed until the full game), **Specials**, **Best times**, the game's own
**Options**, **Quit**; **Resume Game** while a race is on.

- **Specials**: the Halloween track, then a **Race** (three laps against the Dark Monk, Ker'aooc and Tabata, with the power-ups: the
  pumpkins on the road) or a **Time trial** (Twinsen alone, no pickups, a can of Gazogem at the start of every lap), then the car: ten
  Halloween cars, each turning on the page (left and right to choose, Enter to race).
- **Best times**: the ten fastest time trials of each track, with the car.
- After a race the game goes back to the menu by itself.

Driving: the arrow keys, **Ctrl** uses the power-up in the selected slot (the behaviour menu has no use in the car), **Q** switches
slots, Esc for the menu.

## Building

The LBA Assembler repo must be beside this one (`..\LBAAssembler`): the installer compiles its race track build from those sources
(linked, not copied) and carries its engine.

```
powershell -ExecutionPolicy Bypass -File build.ps1          # engine, pictures if missing, installer -> out\LBHRA-Setup.exe
powershell -ExecutionPolicy Bypass -File tools\make_art.ps1 # redraw Game\menu.png and Installer\pumpkin.ico
```

It needs MSYS2's UCRT64 toolchain (`C:\msys64`) for the engine and the .NET 10 SDK.

## Where things are

- `Installer\` - the installer (WPF): `GameInstall.cs` is the install, `MainWindow.xaml` its window.
- `Game\menu.png` - the menu's backdrop, drawn by `tools\make_art.ps1` (no retail art in it).
- The engine side lives in LBA Assembler: `native\lba2-classic-community\SOURCES\RACEGAME.CPP` (the menu, the car page, best times,
  the race file it composes) and `RACEMOD.CPP` (the race-track mode: `start_scene=`, `start_in_car=`, `trial=`, `end_to_menu=`).
