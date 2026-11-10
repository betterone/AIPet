"""Compose actual Unity Player captures for the UI review; no mockup assets."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
font = root / 'Assets/Momo/Resources/Fonts/NotoSansCJKsc-Regular.otf'
board = Image.new('RGB', (1234, 982), '#EAF1EE')
draw = ImageDraw.Draw(board)
draw.text((24, 14), 'MOMO / 操作界面已实现', font=ImageFont.truetype(str(font), 26), fill='#254B48')
draw.text((24, 54), 'Unity Player 实际截图 · 手机竖屏比例 · 非真机截图', font=ImageFont.truetype(str(font), 15), fill='#6C827A')
for index, (name, label) in enumerate([('room', '主界面'), ('play', '一起玩'), ('settings', '设置')]):
    x = 20 + index * 406
    draw.text((x, 86), label, font=ImageFont.truetype(str(font), 18), fill='#254B48')
    with Image.open(root / 'artifacts/mobile-hud' / f'{name}-390.png') as source:
        board.paste(source.convert('RGB'), (x, 114))
output = root / 'artifacts/hud-redesign/implemented-overview.png'
board.save(output)
print(output)
