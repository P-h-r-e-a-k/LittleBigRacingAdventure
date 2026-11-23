#!/usr/bin/env python
"""Pack the voiced new lines for the game: out\\voices.zip, which the setup puts in the install's VOICES\\ folder.

  pack_voices.py <fresh export>     (run with D:\\Chatterbox\\venv\\Scripts\\python.exe: soundfile, numpy)

<fresh export>: what LBA Assembler's `ScriptRoundTrip newlines --built <game> --out <fresh export>` wrote from a game built from the
same source as the setup (build2's): the lines to pack. Every line of it is looked for in NewLines by its language, character and words
(never by its file's name: a line's id moves when a line is put in before it, voices_sync.py says why); one with a .wav there goes in as
    <EN|FR|DE|SP|IT|PO>_<GAM|000..012>/<key>.ogg
the key the first 16 hex digits of the SHA-1 of the line's words (UTF-8, every space, line break and '@' left out). The ids aren't known
until the game is built -- LBRA's setup numbers the texts otherwise than a story test build -- so the setup (and ScriptRoundTrip voices,
for a test build) matches each of the built game's own texts to the clip of its words and writes it as VOICES\\<EN_000>_<text id>.ogg,
the name the engine looks for (LBA Assembler's Terrain\\VoiceClips.cs, MESSAGE.CPP SpeakClip). Ogg Vorbis, 22050 Hz mono, its silence
trimmed at both ends, as loud as the character's retail voice (their sample.wav). Encoded once, kept in out\\voice_cache\\ under the
SHA-1 of the .wav's bytes -- never under its name: voices_sync.py --tidy renames recordings to their lines' new ids, and a clip cached
under a name said the line that had the name before (Zoe's "Twinsen, I am sick of all this rain!" said "Twinsen! Twinsen, wake up!").
Clips no recording makes any more are cleared out of the cache. A line with no recording yet is counted, not packed: the game shows it,
silent, as before.
"""

import argparse
import hashlib
import re
import shutil
import sys
import zipfile
from pathlib import Path

import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parent.parent
NEWLINES = ROOT / "NewLines"
CACHE = ROOT / "out" / "voice_cache"
ZIP = ROOT / "out" / "voices.zip"
LANGS = {"English": "EN_", "French": "FR_", "German": "DE_", "Spanish": "SP_", "Italian": "IT_", "Portuguese": "PO_"}
QUALITY = 1.0          # libsndfile's Vorbis compression level: 1.0 = quality 0, ~33 kbps at 22050 Hz mono -- speech is clear
EDGE_DB = -45.0        # quieter than this at the ends is trimmed ...
MARGIN = 0.08          # ... to this many seconds before the first sound and after the last
GAIN_DB = 6.0          # a clip brought to its character's retail loudness, by this much at most (they came out 0-3 dB louder)
SETTINGS = f"vorbis {QUALITY}, trim {EDGE_DB} dB {MARGIN} s, level to sample.wav +-{GAIN_DB} dB, cached by the .wav's SHA-1"   # the cache made with these


def words(path):
    return "\n".join(l.rstrip() for l in path.read_text(encoding="utf-8").strip().splitlines())


def key(said):
    """VoiceClips.Key: the words' SHA-1 (UTF-8, spaces, line breaks and '@' left out), its first 16 hex digits"""
    return hashlib.sha1(re.sub(r"[ \t\n\r\f\v @]+", "", said).encode("utf-8")).hexdigest()[:16]


def digest(wav):
    """the recording's own name in the cache: the SHA-1 of its bytes (so a renamed recording keeps its clip, and a name never brings back
    another recording's)"""
    return hashlib.sha1(wav.read_bytes()).hexdigest()[:20]


def vox_code(text_file):
    return "GAM" if text_file == 2 else f"{text_file - 3:03d}"


def speech_level(mono):
    """the loudness of the voiced parts, dBFS (None for a silent clip)"""
    frames = mono[: len(mono) // 512 * 512].reshape(-1, 512)
    rms = np.sqrt((frames ** 2).mean(axis=1))
    voiced = rms[rms > 0.01]
    return 10 * np.log10((voiced ** 2).mean()) if voiced.size else None


_targets = {}


def target_level(folder):
    """the character's retail voice's loudness (their sample.wav), so a new line is as loud as their old ones"""
    if folder not in _targets:
        sample = folder / "sample.wav"
        level = None
        if sample.exists():
            d, _ = sf.read(sample, dtype="float32", always_2d=True)
            level = speech_level(d.mean(axis=1))
        _targets[folder] = level
    return _targets[folder]


def encode(wav, ogg):
    data, rate = sf.read(wav, dtype="float32", always_2d=True)
    mono = data.mean(axis=1)
    want, have = target_level(wav.parent), speech_level(mono)
    if want is not None and have is not None:
        gain = 10 ** (max(-GAIN_DB, min(GAIN_DB, want - have)) / 20)
        mono = mono * gain
        peak = np.abs(mono).max()
        if peak > 0.97:
            mono = mono * (0.97 / peak)
    loud = np.flatnonzero(np.abs(mono) > 10 ** (EDGE_DB / 20))
    if loud.size:
        pad = int(MARGIN * rate)
        mono = mono[max(0, loud[0] - pad):min(len(mono), loud[-1] + pad + 1)]
    ogg.parent.mkdir(parents=True, exist_ok=True)
    tmp = ogg.with_suffix(".part")
    # (in blocks: libsndfile's Vorbis encoder overflows the stack given a long clip in one write -- a 50-second line did)
    with sf.SoundFile(tmp, "w", rate, 1, format="OGG", subtype="VORBIS", compression_level=QUALITY) as f:
        for at in range(0, len(mono), 4096):
            f.write(mono[at:at + 4096])
    tmp.replace(ogg)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("fresh", type=Path)
    args = ap.parse_args()
    fresh = args.fresh.resolve()
    index = fresh / "index.tsv"
    if not index.exists() or fresh == NEWLINES.resolve():
        sys.exit(f"{fresh}: not a newlines export (no index.tsv), or NewLines itself.")

    # bank -> text file (index.tsv: character, file, bank, text_file, id, ...)
    bank_file = {}
    for row in index.read_text(encoding="utf-8").splitlines()[1:]:
        cols = row.split("\t")
        if len(cols) > 4:
            bank_file[cols[2]] = int(cols[3])

    # the recordings: (language, character, words) -> .wav
    made = {}
    for lang in LANGS:
        for txt in (NEWLINES / lang).glob("*/*.txt"):
            if txt.name.lower() != "readme.txt" and txt.with_suffix(".wav").exists():
                made.setdefault((lang, txt.parent.name, words(txt)), txt.with_suffix(".wav"))

    stamp = CACHE / "settings.txt"
    if CACHE.exists() and (not stamp.exists() or stamp.read_text(encoding="utf-8") != SETTINGS):
        shutil.rmtree(CACHE)                            # (made another way: all encoded again)
    CACHE.mkdir(parents=True, exist_ok=True)
    stamp.write_text(SETTINGS, encoding="utf-8")

    packed, silent, encoded, cached = {}, [], 0, {}
    for lang, prefix in LANGS.items():
        for txt in sorted((fresh / lang).glob("*/*.txt")):
            if txt.name.lower() == "readme.txt":
                continue
            bank, _, ident = txt.stem.rpartition("_")
            if bank not in bank_file or not ident.isdigit():
                print(f"  {txt}: no bank/id in its name", file=sys.stderr)
                continue
            wav = made.get((lang, txt.parent.name, words(txt)))
            if wav is None:
                silent.append(f"{lang}/{txt.parent.name}/{txt.stem}")
                continue
            if wav not in cached:
                cached[wav] = CACHE / wav.parent.relative_to(NEWLINES) / f"{digest(wav)}.ogg"
            ogg = cached[wav]
            if not ogg.exists():
                encode(wav, ogg)
                encoded += 1
            packed.setdefault(f"{prefix}{vox_code(bank_file[bank])}/{key(words(txt))}.ogg", ogg)

    ZIP.parent.mkdir(parents=True, exist_ok=True)
    tmp = ZIP.with_suffix(".part")
    with zipfile.ZipFile(tmp, "w", zipfile.ZIP_STORED) as z:
        for name in sorted(packed):
            z.write(packed[name], name)
    tmp.replace(ZIP)
    keep = set(cached.values())
    stale = [o for o in CACHE.glob("*/*/*.ogg") if o not in keep]
    for o in stale:
        o.unlink()
    size = ZIP.stat().st_size / 1e6
    print(f"{ZIP}: {len(packed)} clips ({encoded} encoded now, {len(stale)} cached clips no recording makes cleared), {size:.0f} MB; "
          f"{len(silent)} lines with no recording yet")
    by_lang = {}
    for s in silent:
        by_lang[s.split("/")[0]] = by_lang.get(s.split("/")[0], 0) + 1
    if by_lang:
        print("  not voiced: " + ", ".join(f"{k} {v}" for k, v in by_lang.items()))


if __name__ == "__main__":
    main()
