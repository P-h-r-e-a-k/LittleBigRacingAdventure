#!/usr/bin/env python
"""Bring NewLines (the lines Chatterbox voices) up to the game's current new lines.

  voices_sync.py <fresh export>            what is voiced, what isn't (nothing written)
  voices_sync.py <fresh export> --add      the lines no recording says yet go into NewLines, for the batch runner to voice
  voices_sync.py <fresh export> --tidy     also: every line's files renamed to its id in the export, lines no longer in the game
                                           retired, the export's tables copied in. Not while the batch runner is going (it renames)

<fresh export> is a folder LBA Assembler's `ScriptRoundTrip newlines --built <game> --out <fresh export>` wrote -- never NewLines
itself: that run wipes its --out (it refuses a folder with recordings in it), and NewLines holds the recordings (hours of GPU).

A line's text id is NOT stable: the race track build numbers the mod's new texts in order, so a line put in the middle of a table moves
every one after it. A recording is matched to a line by what it says -- the language, the character and the words -- never by its
file's name (the packer, pack_voices.py, does the same). So:
  voiced     a .txt of NewLines has the same language, character and words, and its .wav is made
  pending    ... its .wav isn't made yet (the batch runner's next)
  missing    no .txt of NewLines says it: --add writes its .txt into NewLines\\<Language>\\<Character>\\ (and the character's
             sample.wav / README.txt from the export when the folder is new), under the line's id -- or the id and "~N" when a file
             of that name is there already (another line's, from before the ids moved)
  gone       (--tidy) a NewLines line no line of the game says any more: moved to NewLines\\_retired\\<when>\\, never deleted
The batch runner (D:\\Chatterbox\\run_batches.py) voices every .txt with no .wav beside it.
"""

import argparse
import shutil
import sys
from datetime import datetime
from pathlib import Path

NEWLINES = Path(r"E:\dump\LittleBigRacingAdventure\NewLines")
LANGS = ("English", "French", "German", "Spanish", "Italian", "Portuguese")
TABLES = ("index.tsv", "left_out.tsv", "samples.tsv", "README.txt")


def words(path):
    return "\n".join(l.rstrip() for l in path.read_text(encoding="utf-8").strip().splitlines())


def lines_of(root):
    """[(language, character, stem, words, path)] for every line's .txt"""
    out = []
    for lang in LANGS:
        ldir = root / lang
        if not ldir.is_dir():
            continue
        for cdir in sorted(p for p in ldir.iterdir() if p.is_dir()):
            for txt in sorted(cdir.glob("*.txt")):
                if txt.name.lower() != "readme.txt":
                    out.append((lang, cdir.name, txt.stem, words(txt), txt))
    return out


def recordings(root):
    """(language, character, words) -> the .txt whose .wav says it (a made one first)"""
    found = {}
    for lang, char, stem, said, txt in lines_of(root):
        key = (lang, char, said)
        if key not in found or (not found[key].with_suffix(".wav").exists() and txt.with_suffix(".wav").exists()):
            found[key] = txt
    return found


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("fresh", type=Path)
    ap.add_argument("--add", action="store_true")
    ap.add_argument("--tidy", action="store_true")
    args = ap.parse_args()

    fresh = args.fresh.resolve()
    if fresh == NEWLINES.resolve():
        sys.exit("The fresh export must be another folder than NewLines.")
    if not (fresh / "index.tsv").exists():
        sys.exit(f"{fresh} has no index.tsv: not a newlines export.")

    have = recordings(NEWLINES)
    want = lines_of(fresh)
    counts = {"voiced": 0, "pending": 0, "missing": 0}
    used = set()
    for lang, char, stem, said, src in want:
        txt = have.get((lang, char, said))
        if txt is not None:
            used.add(txt)
            counts["voiced" if txt.with_suffix(".wav").exists() else "pending"] += 1
            continue
        counts["missing"] += 1
        print(f"missing  {lang}/{char}/{stem}: {said[:100]}")
        if args.add or args.tidy:
            cdir = NEWLINES / lang / char
            cdir.mkdir(parents=True, exist_ok=True)
            for extra in ("sample.wav", "README.txt"):
                if (src.parent / extra).exists() and not (cdir / extra).exists():
                    shutil.copy2(src.parent / extra, cdir / extra)
            name, n = stem, 1
            while (cdir / (name + ".txt")).exists() or (cdir / (name + ".wav")).exists():
                n += 1
                name = f"{stem}~{n}"
            shutil.copy2(src, cdir / (name + ".txt"))
            used.add(cdir / (name + ".txt"))
            have[(lang, char, said)] = cdir / (name + ".txt")

    if args.tidy:
        stamp = datetime.now().strftime("%Y-%m-%d_%H%M")
        retired = NEWLINES / "_retired" / stamp
        gone = 0
        for lang, char, stem, said, txt in lines_of(NEWLINES):
            if txt in used:
                continue
            gone += 1
            dest = retired / lang / char
            dest.mkdir(parents=True, exist_ok=True)
            for f in (txt, txt.with_suffix(".wav")):
                if f.exists():
                    shutil.move(str(f), str(dest / f.name))
        # every line under its id in the export: through a temporary name first, as ids swap places
        moves = []
        for lang, char, stem, said, src in want:
            txt = have[(lang, char, said)]
            if txt.stem != stem:
                moves.append((txt, txt.parent / (stem + ".txt")))
        staged = []
        for old, new in moves:
            for ext in (".txt", ".wav"):
                f = old.with_suffix(ext) if ext == ".wav" else old
                if f.exists():
                    tmp = f.with_name(f.stem + ".tidy" + ext)
                    f.rename(tmp)
                    staged.append((tmp, new.with_suffix(ext)))
        for tmp, final in staged:
            tmp.rename(final)
        for t in TABLES:
            if (fresh / t).exists():
                shutil.copy2(fresh / t, NEWLINES / t)
        print(f"tidy: {gone} lines retired to {retired}, {len(moves)} renamed to their ids")

    print(", ".join(f"{v} {k}" for k, v in counts.items())
          + ("" if args.add or args.tidy else "  (nothing written: --add to put the missing lines in)"))


if __name__ == "__main__":
    main()
