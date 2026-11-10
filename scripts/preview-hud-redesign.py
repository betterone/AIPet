"""Code-drawn layout proposal over a clean existing Unity render, explicitly labelled as a mockup."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root=Path(__file__).resolve().parents[1]
out=root/'artifacts/hud-redesign';out.mkdir(parents=True,exist_ok=True)
font=str(root/'Assets/Momo/Resources/Fonts/NotoSansCJKsc-Regular.otf')
scale=2
def face(n): return ImageFont.truetype(font,n*scale)
with Image.open(root/'artifacts/visual-style-study/toy-close.png') as source:
    background=source.convert('RGB').crop((490,0,952,1000)).resize((780,1688),Image.Resampling.LANCZOS)
draw=ImageDraw.Draw(background)
ink='#254B48';teal='#3A7871';white='#FFFDF5';muted='#708982';gold='#F2C36A'
def rect(box,color,r=20):draw.rounded_rectangle(tuple(int(v*2) for v in box),radius=r*2,fill=color)
def text(x,y,s,size=14,color=ink,anchor=None):draw.text((x*2,y*2),s,font=face(size),fill=color,anchor=anchor)
def line(points,color=ink,w=2):draw.line([(x*2,y*2) for x,y in points],fill=color,width=w*2,joint='curve')
def paw(cx,cy,color=white):
    draw.ellipse(((cx-10)*2,(cy-4)*2,(cx+10)*2,(cy+11)*2),fill=color)
    for x,y in [(-11,-10),(-4,-16),(5,-16),(12,-9)]:draw.ellipse(((cx+x-3)*2,(cy+y-4)*2,(cx+x+3)*2,(cy+y+4)*2),fill=color)
rect((16,20,192,88),white,22);paw(39,52,teal);text(61,30,'Momo',21);text(62,59,'第 1 天 · 上午',11,muted)
for x in [270,326]:rect((x,25,x+48,73),white,24)
draw.ellipse(([(340*2,39*2),(360*2,59*2)]),outline=ink,width=4)
for dx,dy in [(-15,0),(15,0),(0,-15),(0,15)]:line([(350+dx*.65,49+dy*.65),(350+dx,49+dy)],w=3)
rect((283,41,306,57),ink,4);rect((290,44,299,54),white,5)
# Quiet recenter tool, bottom dock, directional control and contextual hint.
rect((326,583,374,631),white,24)
for a,b in [((338,607),(362,607)),((350,595),(350,619))]:line([a,b])
draw.ellipse((342*2,599*2,358*2,615*2),outline=ink,width=3)
rect((16,744,374,824),white,28)
text(75,784,'回忆',13,anchor='mm');text(316,784,'找猫',13,anchor='mm')
rect((150,715,240,805),teal,45);paw(195,751);text(195,784,'一起玩',12,white,anchor='mm')
rect((20,600,146,726),'#D9E9DD',63);rect((56,636,110,690),white,27)
for pts in [[(77,618),(83,612),(89,618)],[(77,708),(83,714),(89,708)],[(38,657),(32,663),(38,669)],[(128,657),(134,663),(128,669)]]:line(pts,teal,2)
rect((164,651,374,708),white,18);text(179,663,'慢慢抚摸它，',13);text(179,683,'看看它会怎样回应。',13)
board=Image.new('RGB',(1660,1888),'#EAF1EE');d=ImageDraw.Draw(board)
d.text((28,20),'MOMO / 操作界面改版方向',font=face(22),fill=ink)
d.text((28,90),'左：当前游戏截图    右：布局草图（使用现有 Unity 场景画面）',font=face(12),fill=muted)
with Image.open(root/'artifacts/exploration/home-390.png') as before:
    board.paste(before.convert('RGB').resize((780,1688)),(30,160))
board.paste(background,(850,160));board.save(out/'layout-proposal.png')
print(out/'layout-proposal.png')
