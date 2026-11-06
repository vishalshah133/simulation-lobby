"""Join a Short with its outro using a cross-fade (picture and sound), 1080x1920 at 30 fps.

Usage (from the project root):
    python Tools/stitch_short.py <video.mp4> <outro.mp4> <out.mp4> [--fade 0.6]

The outro is scaled/padded to the video's size and frame rate. Uses the ffmpeg bundled with the imageio-ffmpeg
Python package, or `ffmpeg` on PATH if present. Lives outside Assets/ on purpose: Unity never imports it.
"""
import argparse
import re
import shutil
import subprocess
import sys


def find_ffmpeg():
    exe = shutil.which("ffmpeg")
    if exe:
        return exe
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except ImportError:
        sys.exit("No ffmpeg found. Install it, or run: pip install imageio-ffmpeg")


def probe(ff, clip):
    p = subprocess.run([ff, "-hide_banner", "-i", clip], capture_output=True, text=True)
    m = re.search(r"Duration: (\d+):(\d+):(\d+\.\d+)", p.stderr)
    if not m:
        sys.exit("Could not read the length of " + clip)
    h, mi, s = m.groups()
    size = re.search(r"Video:.*?, (\d{3,5})x(\d{3,5})", p.stderr)
    return int(h) * 3600 + int(mi) * 60 + float(s), (int(size.group(1)), int(size.group(2))), "Audio:" in p.stderr


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("video")
    ap.add_argument("outro")
    ap.add_argument("out")
    ap.add_argument("--fade", type=float, default=0.6)
    a = ap.parse_args()

    ff = find_ffmpeg()
    length, (w, h), has_audio = probe(ff, a.video)
    outro_length, _, outro_audio = probe(ff, a.outro)
    offset = length - a.fade

    norm = f"fps=30,scale={w}:{h}:force_original_aspect_ratio=decrease,pad={w}:{h}:(ow-iw)/2:(oh-ih)/2,setsar=1,format=yuv420p"
    video = f"[0:v]{norm}[v0];[1:v]{norm}[v1];[v0][v1]xfade=transition=fade:duration={a.fade}:offset={offset:.3f}[v]"
    audio = (
        "[0:a]aformat=sample_rates=48000:channel_layouts=stereo[a0];"
        "[1:a]aformat=sample_rates=48000:channel_layouts=stereo[a1];"
        f"[a0][a1]acrossfade=d={a.fade}[a]"
    ) if has_audio and outro_audio else None

    graph = video + (";" + audio if audio else "")
    cmd = [ff, "-hide_banner", "-y", "-i", a.video, "-i", a.outro, "-filter_complex", graph, "-map", "[v]"]
    if audio:
        cmd += ["-map", "[a]"]
    cmd += ["-c:v", "libx264", "-crf", "18", "-preset", "medium", "-r", "30", "-pix_fmt", "yuv420p"]
    if audio:
        cmd += ["-c:a", "aac", "-b:a", "192k"]
    cmd += ["-movflags", "+faststart", a.out]
    subprocess.run(cmd, check=True)
    total = length + outro_length - a.fade
    print(f"Wrote {a.out}: {total:.1f}s ({length:.1f}s video + {outro_length:.1f}s outro, {a.fade}s fade)")


if __name__ == "__main__":
    main()
