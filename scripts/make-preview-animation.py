"""Package offscreen WPF frames; this script is build tooling, not an app dependency."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parent.parent / "artifacts" / "previews"
paths = sorted((root / "frames").glob("*.png"))
if not paths:
    raise SystemExit("Render the WPF previews first.")
frames = [Image.open(path).convert("RGB") for path in paths]
atlas = Image.new("RGB", (320 * 8, 200 * ((len(frames) + 7) // 8)))
for i, frame in enumerate(frames):
    atlas.paste(frame.resize((320, 200)), ((i % 8) * 320, (i // 8) * 200))
palette = atlas.quantize(colors=256)
gif = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
gif[0].save(root / "06-liquid-demo.gif", save_all=True, append_images=gif[1:], duration=63, loop=0, optimize=False)
frames[0].save(root / "06-liquid-demo.webp", save_all=True, append_images=frames[1:], duration=63, loop=0, quality=86, method=4)
print(f"Packaged {len(frames)} offscreen frames into GIF and WebP.")
