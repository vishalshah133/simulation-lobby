"""Stitch the ten comp-001 segment recordings into one 16:9 video.

Usage (from the project root):
    python tools/stitch_comp001.py <clips_folder> [--out Renders/comp-001-ten-sims.mp4] [--fade 0.35] [--chapters]

<clips_folder> holds one recording per segment, named so they sort in order, e.g. 01_clack.mp4 ... 10_mve.mp4
(any name works as long as it starts with the segment number 01-10). Each clip is scaled and padded to 1920x1080,
60 fps, with 48 kHz stereo audio (silent audio is added if a clip has none), then joined with a short dip to black.
--chapters prints the YouTube chapter list from the real clip lengths.

Uses the ffmpeg bundled with the imageio-ffmpeg Python package, or `ffmpeg` on PATH if present.
"""
import argparse
import re
import shutil
import subprocess
import sys
from pathlib import Path


def find_ffmpeg():
    exe = shutil.which("ffmpeg")
    if exe:
        return exe
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except ImportError:
        sys.exit("No ffmpeg found. Install it, or run: pip install imageio-ffmpeg")


def duration(ff, clip):
    p = subprocess.run([ff, "-i", str(clip)], capture_output=True, text=True)
    m = re.search(r"Duration: (\d+):(\d+):(\d+\.\d+)", p.stderr)
    if not m:
        sys.exit(f"Could not read the length of {clip}")
    h, mi, s = m.groups()
    return int(h) * 3600 + int(mi) * 60 + float(s)


def has_audio(ff, clip):
    p = subprocess.run([ff, "-i", str(clip)], capture_output=True, text=True)
    return "Audio:" in p.stderr


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("folder")
    ap.add_argument("--out", default="Renders/comp-001-ten-sims.mp4")
    ap.add_argument("--fade", type=float, default=0.35, help="dip-to-black seconds at each cut (0 = hard cut)")
    ap.add_argument("--chapters", action="store_true")
    a = ap.parse_args()

    ff = find_ffmpeg()
    clips = {}
    for f in sorted(Path(a.folder).glob("*.mp4")):
        m = re.match(r"(\d{1,2})", f.name)
        if m:
            clips[int(m.group(1))] = f
    missing = [n for n in range(1, 11) if n not in clips]
    if missing:
        sys.exit(f"Missing segments: {missing}. Name each clip with its number first, e.g. 03_rom.mp4")

    ordered = [clips[n] for n in range(1, 11)]
    durs = [duration(ff, c) for c in ordered]
    out = Path(a.out)
    out.parent.mkdir(parents=True, exist_ok=True)

    cmd = [ff, "-y", "-v", "error", "-stats"]
    for c in ordered:
        cmd += ["-i", str(c)]
    parts, labels = [], []
    f = a.fade
    for i, (c, d) in enumerate(zip(ordered, durs)):
        v = (f"[{i}:v]scale=1920:1080:force_original_aspect_ratio=decrease,"
             f"pad=1920:1080:(ow-iw)/2:(oh-ih)/2:black,setsar=1,fps=60,format=yuv420p")
        au = f"[{i}:a]aresample=48000,aformat=channel_layouts=stereo" if has_audio(ff, c) else \
             f"anullsrc=r=48000:cl=stereo,atrim=0:{d:.3f}"
        if f > 0:
            v += f",fade=t=in:st=0:d={f}" if i > 0 else ""
            v += f",fade=t=out:st={max(d - f, 0):.3f}:d={f}" if i < len(ordered) - 1 else ""
            au += f",afade=t=in:st=0:d={f}" if i > 0 else ""
            au += f",afade=t=out:st={max(d - f, 0):.3f}:d={f}" if i < len(ordered) - 1 else ""
        parts.append(f"{v}[v{i}]")
        parts.append(f"{au}[a{i}]")
        labels.append(f"[v{i}][a{i}]")
    parts.append("".join(labels) + f"concat=n={len(ordered)}:v=1:a=1[v][a]")
    cmd += ["-filter_complex", ";".join(parts), "-map", "[v]", "-map", "[a]",
            "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", str(out)]
    subprocess.run(cmd, check=True)

    total = sum(durs)
    print(f"\nWrote {out}  ({total // 60:.0f}:{total % 60:04.1f})")
    if total < 360:
        print("WARNING: under 6:00. Lengthen segments before publishing as long form.")
    if a.chapters:
        t = 0.0
        print("\nChapters:")
        for n, d in zip(range(1, 11), durs):
            print(f"{int(t // 60)}:{int(t % 60):02d} Segment {n}")
            t += d


if __name__ == "__main__":
    main()
