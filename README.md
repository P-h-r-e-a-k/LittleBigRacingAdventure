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
3. LBA Assembler's race track build makes every track in the copy: Track select's fifteen (Polar Island and the palace and Island CX
   made as islands of their own) and the Temple of Bù island with its Halloween race.
4. The game goes in: LBA Assembler's engine as `LBRA.exe`, the race files (`specials\`, `tracks\`), the menu
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
  plays on. Options > **Camera, window, updates**: **Updates: Automatic / Ask first / Off** (`updates=auto|ask|off`, Enter goes round them) and
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

The main menu: **Story Mode** (greyed until the full game), **Track select**, **Specials**, **Best laps**, the game's own
**Options** (with **Race controls, power-ups** and **Camera, window, updates** at the end), **Quit**; **Resume Game** while a race is on.

- **Track select** (for now behind a code): choosing it asks for the code -- type the digits (the keyboard's row or its keypad; each
  shows as a star, Backspace takes one back), **Enter** to open, **Esc** back. A wrong code says "Wrong code" and empties; the right
  one (**1234**) opens Track select for the rest of the run (it isn't kept: the next start asks again). The code is the installer's
  `tracks_code=` line in `RACEGAME.TXT`, so it can change without a new engine; without the line there is no code.
  Then the tracks, in this order, each with its best lap beside it (up and down, Enter, Esc back; the mouse too):
  1. Polar Island (a sprint from the dock to the peak: start to finish once)
  2. Citadel Island: the storm (in the rain, against Raph's time)
  3. Citadel Island: the town (the town circuit, once the storm is over)
  4. Desert Island
  5. The Emerald Moon
  6. Otringal
  7. Celebration Island: the lava lake
  8. Island of the Francos
  9. The Elevator Platform (the rollercoaster)
  10. Wannies Island
  11. Mosquibees Island
  12. Volcano Island
  13. Island CX
  14. The Emperor's palace (a sprint through its rooms, out of the round window)
  15. Celebration Island: the statue

  Each track: a **Race** (its own line-up and power-ups, three laps; a sprint once from start to finish -- won or lost, the menu
  follows, no story's waking up) or a **Time trial** (Twinsen alone, no power-ups; its fastest lap, or a sprint's time, goes to Best
  laps, and it first asks **how many laps** -- 1, 3, 5, 10 or **Endless: until you stop** -- and which **ghost** to race, see
  below); then the **car grid**; then, for a race, the qualifying question as for the specials (not on a sprint). Best laps goes
  through every track once Track select is open.
- **The car grid** (Track select's car page): every car of the game, 7 x 7 on a page (222 cars: five pages) -- Twinsen's Halloween
  buggy first, his pumpkin F1 (locked as on the Halloween car page until the Halloween race is won, then for time trials), Halloween's
  monsters' cars (the vampire's coffin, the witch's broomstick, the zombie's hearse), then the cars made by hand for the islands' people
  and the cast's cars. The arrows go through them (a row up and down, on to the next page past the ends), **Page up** / **Page down** a
  page; the car chosen is framed in orange and shown large on the right, turning, with its name and stars; **Enter** (or a click on
  it) races it, **Esc** goes back. The small cars are drawn once as their page opens. The last car taken is remembered
  (`grid_car=` in `RACEGAME_SETTINGS.TXT`).
  Every car has its own stats, adding up to the same 7.5 stars: the Halloween car page's cars keep their own (the table below); every
  other car's come from what it is -- karts and bubble cars nimble and quick away but slow at the top, trucks and tanks slow but sure
  in the bends, rockets and gliders fast and floaty, monster trucks all pull, ghost cars lively and floaty, boats fast on the
  straights, mine carts as on rails -- with half a star (now and then a whole one) moved between two stats by the car's own number, so
  two cars of a kind differ; the same on every install.

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
  back): Twinsen's Halloween buggy (his own buggy painted pumpkin orange, with bats on its sides and its back; the page opens on it),
  Twinsen's pumpkin F1 (locked until you win), Zoe's car, the Emperor's car, the Dark Monk's car, the priest's car, the guitarist's
  guitar, the trooper's tank, Dino-Fly, the Tralu's cave, the skeleton's ghost car, the little Tralu's monster truck and the third
  quetch child's kart. Under each car's name its stats, 0 to 5 stars in half stars: **Acceleration**, **Top speed** and
  **Handling**. Every car's stars add up to the same 7.5, so what a car has more of it pays for in the others, and the race drives it
  by them: the Halloween buggy's 2.5 stars each are the track's own car; from there, up to 5 stars gives up to half as much pull again,
  8 % more top speed and a fifth more steering, and fewer stars as much less. (A time trial uses the car too; Best laps shows it.)

  **Unlocking the pumpkin F1**: until then it is on the car page dark, under a padlock -- "Win the Halloween race to unlock" -- and
  can't be taken. Win a race (first over the line, at any difficulty) and, a moment after the line, the congratulations: fireworks over
  the menu's Halloween night, with their sound, the pumpkin F1 turning, "Twinsen's pumpkin F1 is unlocked for your time trials!"
  (Enter, Esc or a click goes on; it ends by itself with the fireworks, after half a minute). It comes once; the unlock is kept
  (`pumpkin_unlocked=1` in `RACEGAME_SETTINGS.TXT`). From then on the pumpkin F1 is yours for **time trials**; on the car page for a
  race it shows "For time trials" and isn't taken. A race's end says **You win!** or the place you finished in (or who beat you).

  | Car | Acceleration | Top speed | Handling |
  | --- | :-: | :-: | :-: |
  | Twinsen's Halloween buggy | 2.5 | 2.5 | 2.5 |
  | Twinsen's pumpkin F1 (won: time trials) | 2.5 | 2.5 | 2.5 |
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
- **Best laps**: the fastest laps of each track (the page shows five), with the car: each time trial's fastest lap and every qualifying lap -- the laps
  driven with no power-ups (a time trial is the same at every difficulty; a race's laps never count). At the end of a time trial the screen shows its best lap and the rank it
  took, and the whole time under it. The whole five-lap times kept by earlier versions stay in `BESTTIMES.TXT` but aren't shown as laps.
- After a race the game goes back to the menu by itself, on the line you chose last (Track select, Specials or Best laps).
- **Time trial display**: over the lap the display shows **Best time** -- the track's best lap from Best laps (`--:--.--` before there
  is one), or the trial's own fastest lap as soon as one beats it. At the end of every lap, in races and time trials, the lap's time
  stays on the lap line for two seconds before the next lap's clock runs, in gold when it is a new best time.
- **Ghost car** (2026-10-24): every time-trial lap is recorded, and a lap faster than your best one becomes that track's ghost. From the
  next lap on, the ghost drives round with you as a see-through car. At each checkpoint and at the line, the display says how far
  **ahead** (green) or **behind** (red) of it you are; the line above Best time shows the ghost's lap time. A sprint (Polar Island, the
  palace) records its run from start to finish the same way.
  - The recording isn't your key presses. The game moves the car by however long each frame took, so the same keys would drive a
    different lap on another PC. It records where the car was at each moment, so a ghost looks the same on any machine.
  - **Ghosts to beat** (2026-10-25): every track comes with an **Easy**, a **Medium** and a **Hard** ghost, in the game folder's
    `ghosts\`. Easy and Medium are Twinsen's Halloween buggy driven at an easy and a steady pace. Hard is the quickest car for that
    track, flat out, taking the track's shortcuts (on the Temple of Bú: the hidden passage and the drop off the end spiral). The
    display names the ghost you race ("Hard ghost 0:46.28").
    On Wannies Island the frog's mouth decides every lap, so its three ghosts are only a fraction of a second apart.
  - **Racing a ghost** (2026-10-25): pick the track, then **Time trial**, and the **Race a ghost** page asks what to race: **No
    ghost**, **Your best lap**, or the **Easy**, **Medium** or **Hard** ghost, each with its lap time. Medium is locked until you beat
    Easy on that track, and Hard until you beat Medium ("Medium ghost: beat Easy"). Beat one and the display says **You beat the Easy
    ghost! Medium unlocked**. A lap faster than a harder ghost's time unlocks past it too: racing Easy and beating Medium's time
    says **You beat Easy and Medium! Medium and Hard unlocked**. Each track keeps its own unlocks (`ghost_beaten=` lines in
    `RACEGAME_SETTINGS.TXT`), and the page opens on your last choice. Esc on the car page goes back to it.
  - **Laps** (2026-10-25): before the ghost, a time trial asks **How many laps?**: 1, 3, 5, 10, or **Endless: until you stop**
    (`trial_laps=` in `RACEGAME_SETTINGS.TXT`; the last choice comes up first). An endless trial counts "Lap 7" with no end; its
    fastest lap goes into Best laps as soon as you drive it, so stopping through the menu loses nothing. The sprints (Polar Island,
    the palace) have no laps to choose.
  - **Best laps**: one track at a time, its **five fastest laps** with their cars, and under them **Watch**: **Your best lap** or
    **Ghost: Easy / Medium / Hard**. **Up**, **Down** move between the track's name and the two Watch lines. On the track's name,
    **Left**, **Right** go through the tracks; on the ghost line they go round Easy, Medium and Hard. **Enter** plays the line chosen
    back, the camera behind it, round and round until Esc. Any level can be watched, locked or not.
  - "Your best lap" says **Your recorded lap** when a faster lap in the list has no ghost (laps driven before ghosts were recorded).
  - **Sharing**: ghosts are text files in `User\LBA2\GHOSTS\` (yours are named after the track, e.g. `Otringal.ghost`). To share one,
    send the file; the other player renames it (e.g. `Otringal - Sam.ghost`) and puts it in their own `GHOSTS` folder. When it is
    faster than their own, the "Your best lap" line becomes **Shared ghost** on both pages, and it is the one raced and watched.
- **Mini-map**: a small map of the whole track in the bottom-right corner while you race, with a dot for every car -- yours the big
  yellow one, each monster its own colour. Options > **Camera, window, updates** > **Mini-map: On / Off** turns it off or back on
  (`minimap=` in `RACEGAME_SETTINGS.TXT`; on at first). It is hidden behind the menu and the pause, and once you finish.
- **Camera**: three views -- **inside the car** (the driver's view: the car itself isn't drawn, and the view climbs and dips with
  it on the temple's ramps and stairs), **just behind** (the camera as it was up to 2026-10-20) and **further back** (farther behind
  and higher: the car smaller and lower in the picture, more of the road ahead in view; the first time). **V** (a pad's **Back**
  button) goes round them during a race, the view's name shown a moment; Options > **Camera, window, updates** > **Camera** too
  (Enter goes round them). The last one chosen is kept (`camera=0|1|2` in `RACEGAME_SETTINGS.TXT`).

The first time: before the first race or time trial a **Controls** page, and before the first race a **Power-ups** page (every power-up
of the track in its item box, with what it does). Enter turns the pages, Esc skips them; they don't come back by themselves, but
Options shows them again.

Driving: the arrow keys, **Ctrl** uses the power-up in the item box (the behaviour menu has no use in the car), **Shift** is the
handbrake (a pad's **Y** button): hold it into a tight corner and steer -- the back of the car steps out and it slides round, losing
some speed -- then let go and the tyres grip again, the car pointing down the road. **V** changes the camera's view, P to pause, Esc
for the menu. **F11** or **Alt+Enter** switches between full screen and a window at any time (also
Options > **Camera, window, updates**;
kept in `lba2.cfg`). In a window the game fills the window whatever its shape: resize or maximise it and the menus, the race and
its display are laid out to fit, with no black bars. The window's size, and whether it was maximised, is remembered for the next start
(`WindowWidth`, `WindowHeight`, `WindowMaximized` in `User\LBA2\lba2.cfg`). Full screen is unchanged.

**Out of the car** (the Halloween track): stop and press **Space** (a pad's **X** button) to get out and explore the temple on foot --
Twinsen runs, and Space jumps. The race goes on without you: the monsters keep racing and the clock keeps running. **Space** beside
your car gets you back in it; **Ctrl** (a pad's left shoulder) anywhere puts you straight back in it, as does a fall into the dark,
or five minutes on foot. Somewhere in the temple a secret is waiting for those who go looking -- a jack-o'-lantern off the long
corridor shows the way.

The game's own settings (`RACEGAME_SETTINGS.TXT`, beside `BESTTIMES.TXT` in `User\LBA2\`) remember the pages seen, whether a qualifying
lap has been driven, the last qualifying answer, the last difficulty, the car grid's last car, the mini-map, the camera's view, the updates' mode and the last update check: deleting it, or a
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
- The engine side lives in LBA Assembler: `native\lba2-classic-community\SOURCES\RACEGAME.CPP` (the menu, Track select's code, track list and car grid, the difficulty page, the
  car page and its locked car, the congratulations and their fireworks, best times, the qualifying choice, the power-up and controls
  pages, the Updates page and the weekly check, the race file it composes), `RACEUPDATE.CPP` (the updater started, hidden) and
  `RACEMOD.CPP` (the race-track mode: `start_scene=`, `start_in_car=`, `trial=`, `end_to_menu=`, `grid_back=`, `difficulty=`,
  `marshal=`, `twinsen_body=` (the Halloween buggy); the power-ups' names and descriptions). The Halloween buggy's bodies are made by
  the race track build (`Terrain\RaceTrackCharacterCars.HalloweenBuggy.cs`), the fireworks' sound from `sounds\firework.mp3`
  (`Terrain\RaceSounds.cs`: `RACESOUNDS\FIREWORK.WAV`). The levels' numbers are LBA Assembler's (`Terrain\RaceDifficulty.cs`), in the race file the installer writes.
