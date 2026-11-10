from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent.parent
folder = root / 'artifacts/visual-quality'
font_path = str(root / 'Assets/Momo/Resources/Fonts/NotoSansCJKsc-Regular.otf')
font = ImageFont.truetype(font_path, 20)
small = ImageFont.truetype(font_path, 15)
title = ImageFont.truetype(font_path, 26)
shots = [('room-390.png', '房间与状态'), ('settings-390.png', '统一面板'),
         ('garden-390.png', '后院与角色'), ('feeding-and-petting-390.png', '双手喂食')]
width, height = 293, 634
sheet = Image.new('RGB', (width*4+24*5, height+156), '#E8EFEB')
d = ImageDraw.Draw(sheet)
d.text((24, 16), 'Momo · 视觉统一调整', font=title, fill='#243F40')
d.text((24, 55), '2026-10-23 · Unity 桌面实际渲染 · 隔离检查场景 · 非 iPhone 截图', font=small, fill='#526965')
for i, (name, label) in enumerate(shots):
    x = 24 + i*(width+24)
    d.text((x, 89), label, font=font, fill='#243F40')
    shot = Image.open(folder/'after'/name).convert('RGB')
    shot.thumbnail((width, height), Image.Resampling.LANCZOS)
    sheet.paste(shot, (x, 126))
sheet.save(folder/'overview.png')
