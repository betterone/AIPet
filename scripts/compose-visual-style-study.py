"""Lay out actual Unity captures for review; do not alter scene pixels or synthesize imagery."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
folder = root / "artifacts/visual-style-study"
if not (folder / "PASS.txt").is_file():
    raise SystemExit("Run the independent Unity Player with --visual-style-review first.")
font_path = "/System/Library/Fonts/Hiragino Sans GB.ttc"
title = ImageFont.truetype(font_path, 30)
label = ImageFont.truetype(font_path, 23)
small = ImageFont.truetype(font_path, 18)
board = Image.new("RGB", (1640, 1330), "#F2F6F6")
draw = ImageDraw.Draw(board)
draw.text((28, 16), "MOMO / 明亮玩具家园 · Unity 实际渲染对照", font=title, fill="#294745")
draw.text((28, 66), "同一只猫、同一姿态、同一机位；两版均移除前侧外墙以便比较室内。", font=small, fill="#55716D")
for column, (variant, name) in enumerate((("current", "A · 当前空间"), ("toy", "B · 玩具家园视觉样片"))):
    x = 20 + column * 820
    draw.text((x + 8, 106), name, font=label, fill="#294745")
    for row, mode in enumerate(("wide", "close")):
        with Image.open(folder / f"{variant}-{mode}.png") as source:
            if source.size != (1440, 1000):
                raise SystemExit(f"Unexpected capture dimensions: {source.size}")
            board.paste(source.convert("RGB").resize((800, 556), Image.Resampling.LANCZOS), (x, 148 + row * 568))
draw.text((28, 1290), "桌面视觉样片：已验证构建与角色移动。手机性能、发热与最终美术接受度尚未验收。", font=small, fill="#55716D")
board.save(folder / "comparison.png")
print(folder / "comparison.png")
