"""Compose unchanged, identically cropped real GameView captures; no color edits."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[4]
images = root / 'artifacts/VOXEL-LIGHT-01-v12/visual'
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 24)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 18)
names = [('01-M0R0', '原 C：原配色＋原响应'), ('02-M1R0', '仅换配色'),
         ('03-M0R1', '仅换受光响应'), ('04-M1R1-clear', '组合：清透推荐')]
crop = (560, 300, 1390, 790)
sheet = Image.new('RGB', (1660, 1128), '#151d23')
draw = ImageDraw.Draw(sheet)
for index, (name, label) in enumerate(names):
    x, y = (index % 2) * 830, (index // 2) * 554
    draw.text((x + 12, y + 7), label, font=font, fill='white')
    sheet.paste(Image.open(images / f'{name}.png').convert('RGB').crop(crop), (x, y + 45))
draw.text((12, 1100), '同一主场、几何、机位45°、正午、灯光与全场Look；仅切材质与响应。原图无二次调色。', font=small, fill='#c5d3df')
sheet.save(images / 'four-grid.png')

pair = Image.new('RGB', (1600, 490), '#151d23')
draw = ImageDraw.Draw(pair)
for index, (name, label) in enumerate([('09-second-geometry-original', '原台阶 · 原C'), ('10-second-geometry-clear', '同一原台阶 · 清透推荐')]):
    source = Image.open(images / f'{name}.png').convert('RGB')
    pair.paste(source.crop((240, 240, 1280, 800)).resize((800, 431)), (index * 800, 45))
    draw.text((index * 800 + 12, 7), label, font=font, fill='white')
pair.save(images / 'second-geometry-pair.png')
print(images / 'four-grid.png')
