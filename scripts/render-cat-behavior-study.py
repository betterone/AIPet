"""Encode only the frames recorded in a completed 30 fps cat study review."""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
version = sys.argv[1] if len(sys.argv) > 1 else "v2"
if version not in ("v3", "v2", "v1-comparison"):
    raise SystemExit("Choose v3, v2 or v1-comparison")
directory = root / "artifacts" / "cat-behavior-study" / version
if not (directory / "PASS.txt").is_file():
    raise SystemExit("Run the Development Player with --cat-behavior-review first")
count = int((directory / "FRAMECOUNT.txt").read_text())
for index in range(count):
    if not (directory / f"frame-{index:05d}.png").is_file():
        raise SystemExit(f"Missing recorded frame {index}")
output = directory / "review.mp4"
subprocess.run([
    "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
    "-framerate", "30", "-i", str(directory / "frame-%05d.png"),
    "-frames:v", str(count), "-c:v", "libx264", "-crf", "20",
    "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(output),
], check=True)
print(f"{output}\n{count} frames at 30 fps ({count / 30:.2f} seconds)")
