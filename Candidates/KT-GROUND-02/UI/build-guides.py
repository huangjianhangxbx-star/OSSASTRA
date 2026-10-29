"""Build the two offline author guides and original technical diagrams; no Unity mutation.

The small Markdown renderer supports only the headings, paragraphs, lists, images and
tables used by these owned source documents. No external resources or service is used.
"""
import argparse
import csv
import html
import math
import re
from pathlib import Path
from PIL import Image


def inline(text):
    text = html.escape(text, quote=False)
    text = re.sub(r"!\[([^]]*)\]\(([^)]+)\)", lambda m: '<figure><img src="' + html.escape(m[2], quote=True) + '" alt="' + html.escape(m[1], quote=True) + '"><figcaption>' + m[1] + '</figcaption></figure>', text)
    text = re.sub(r"\[([^]]*)\]\(([^)]+)\)", lambda m: '<a href="' + html.escape(m[2], quote=True) + '"' + (' download' if m[2].lower().endswith('.png') else '') + '>' + m[1] + '</a>', text)
    text = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", text)
    return re.sub(r"`([^`]+)`", r"<code>\1</code>", text)


def render(markdown):
    output, navigation, paragraph = [], [], []
    lines = markdown.splitlines()
    listing, index = None, 0

    def flush():
        if paragraph:
            output.append('<p>' + inline(' '.join(paragraph)) + '</p>')
            paragraph.clear()

    def close_list():
        nonlocal listing
        if listing:
            output.append('</' + listing + '>')
            listing = None

    while index < len(lines):
        line = lines[index].strip()
        if not line:
            flush(); close_list(); index += 1; continue
        if line.startswith('#'):
            flush(); close_list()
            level = len(line) - len(line.lstrip('#'))
            label = line[level:].strip()
            anchor = 'section-' + str(index + 1)
            output.append(f'<h{level} id="{anchor}">{inline(label)}</h{level}>')
            if level == 2:
                navigation.append((anchor, label))
        elif line.startswith('|') and index + 1 < len(lines) and re.match(r'^\|[\s:|\-]+\|$', lines[index + 1].strip()):
            flush(); close_list()
            cells = [part.strip() for part in line.strip('|').split('|')]
            output.append('<div class="table-wrap"><table><thead><tr>' + ''.join('<th>' + inline(cell) + '</th>' for cell in cells) + '</tr></thead><tbody>')
            index += 2
            while index < len(lines) and lines[index].strip().startswith('|'):
                cells = [part.strip() for part in lines[index].strip().strip('|').split('|')]
                output.append('<tr>' + ''.join('<td>' + inline(cell) + '</td>' for cell in cells) + '</tr>')
                index += 1
            output.append('</tbody></table></div>')
            continue
        elif line.startswith('- ') or re.match(r'^\d+\. ', line):
            flush()
            desired = 'ul' if line.startswith('- ') else 'ol'
            if listing != desired:
                close_list(); listing = desired; output.append('<' + listing + '>')
            output.append('<li>' + inline(re.sub(r'^(?:- |\d+\. )', '', line)) + '</li>')
        else:
            close_list()
            if line.startswith('!['):
                flush(); output.append(inline(line))
            else:
                paragraph.append(line)
        index += 1
    flush(); close_list()
    return '\n'.join(output), navigation


CSS = """
:root{color-scheme:light;--ink:#263b33;--muted:#596c63;--paper:#f5f3eb;--line:#d3dad0;--accent:#386751}
*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:var(--paper);color:var(--ink);font:16px/1.85 'Microsoft YaHei','PingFang SC',sans-serif}
aside{position:fixed;inset:0 auto 0 0;width:260px;background:#213a30;color:#e5ede5;padding:36px 24px;overflow:auto}aside small{color:#b8cdbf;letter-spacing:.14em}aside h2{font-size:24px;line-height:1.4;margin:16px 0 25px}aside a{display:block;color:#dce9de;text-decoration:none;font-size:14px;margin:9px 0;padding:3px 0}aside a:hover{color:white;text-decoration:underline}aside .other{margin-top:26px;border-top:1px solid #4a6355;padding-top:18px}
main{margin-left:260px;padding:48px 6vw 70px;max-width:1280px}h1{font-size:36px;letter-spacing:.03em;line-height:1.35;margin:8px 0 24px}h2{font-size:24px;margin:54px 0 18px;padding-top:18px;border-top:1px solid var(--line);scroll-margin-top:22px}p{margin:15px 0}strong{font-weight:700;color:#234b39}a{color:var(--accent);text-underline-offset:3px}li{padding-left:5px;margin:10px 0}figure{margin:25px 0;padding:18px;background:#e7ebe1;border:1px solid var(--line);border-radius:6px}figure img{display:block;width:100%;max-height:440px;object-fit:contain}figcaption{font-size:13px;color:var(--muted);margin-top:12px}code{font:14px/1.6 Consolas,monospace;background:#e8e9df;padding:2px 5px;overflow-wrap:anywhere}.table-wrap{overflow:auto;margin:22px 0}table{width:100%;border-collapse:collapse;font-size:14px;min-width:600px}td,th{padding:12px 14px;vertical-align:top;border:1px solid var(--line);text-align:left}th{background:#dce5d9}tr:nth-child(even){background:#eeefe6}.status{padding:12px 16px;border-left:4px solid #66846a;background:#e6ecdf;color:#415848;font-size:14px}.topline{font-size:12px;letter-spacing:.15em;color:var(--muted)}footer{margin-top:45px;padding-top:20px;border-top:1px solid var(--line);font-size:13px;color:var(--muted)}
figure img[src$="OriginalTechnical32.png"],figure img[src$="OriginalTechnical32_3x3.png"]{max-height:230px;image-rendering:pixelated}
@media(max-width:920px){aside{position:relative;width:auto;padding:22px}aside h2{margin:8px 0;font-size:20px}aside nav{display:flex;flex-wrap:wrap;gap:4px 16px}aside a{margin:2px 0}aside .other{margin-top:10px;padding-top:10px}main{margin:0;padding:25px 22px 50px}h1{font-size:29px}}@media print{aside{display:none}main{margin:0;padding:0;max-width:none}body{font-size:11pt;background:white}.table-wrap{overflow:visible}h2{break-after:avoid}figure,table{break-inside:avoid}}
"""


def page(source, other, public=False):
    markdown = source.read_text(encoding='utf-8')
    if public and re.search(r'!\[[^]]*\]\([^)]*(?:reference|images/)', markdown, re.I):
        raise RuntimeError('Private reference/scene capture cannot enter the public guide: ' + str(source))
    content, navigation = render(markdown)
    title = source.stem
    links = ''.join(f'<a href="#{anchor}">{html.escape(label)}</a>' for anchor, label in navigation)
    local_links = ''
    if not public:
        local_links = '<a href="../开始使用.html">开始使用与验收状态 →</a><a href="../02-catalog/场景素材图鉴.html">本机素材图鉴 →</a>'
    result = f'''<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{title} · 星骸地表工坊</title><style>{CSS}</style></head><body><aside><small>KT–GROUND–02</small><h2>星骸地表工坊<br>制作手册</h2><nav>{links}</nav><div class="other"><a href="{other}">另一份制作手册 →</a>{local_links}</div></aside><main><div class="topline">AUTHORING / 原创技术示意 / 离线可读</div><div class="status">本文说明实际接口与制作工作流。功能实测、实际截图和未测项请同时查阅交付入口；示意图不替代现场证据或人工验收。</div>{content}<footer>资料可离线阅读，无CDN、外部字体或网络脚本。来源事实、Unity适配、推断和未知须分别记录；自有技术示意不代表正式美术。</footer></main></body></html>'''
    source.with_suffix('.html').write_text(result, encoding='utf-8')


def svg_text(x, y, value, size=17, fill='#263b33'):
    return f'<text x="{x}" y="{y}" font-family="Microsoft YaHei,sans-serif" font-size="{size}" fill="{fill}">{html.escape(value)}</text>'


def diagrams(root):
    tutorial = root / '03-tutorial' / 'assets'
    art = root / '04-art-guide' / 'assets'
    tutorial.mkdir(parents=True, exist_ok=True); art.mkdir(parents=True, exist_ok=True)
    stack = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 850 350"><rect width="850" height="350" rx="12" fill="#eef1e8"/>'
    for n, (label, color) in enumerate([('Tilemap：哪里有这一层', '#a1b28b'), ('边缘 alpha：显示轮廓', '#c9b895'), ('世界底纹：内部颜色与细节', '#6e9167')]):
        y = 210 - n * 65
        stack += f'<path d="M65 {y} L240 {y-55} L425 {y} L240 {y+55}Z" fill="{color}" stroke="#456349" stroke-width="2"/>'
        stack += svg_text(470, y + 4, label)
    stack += '<path d="M620 235v40M590 270l30 15 30-15" fill="none" stroke="#557b5f" stroke-width="3"/>'
    stack += svg_text(470, 320, '独立花草：另存脚点与实例，不写入底纹') + '</svg>'
    (tutorial / '地表组合示意.svg').write_text(stack, encoding='utf-8')
    coordinate = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 850 350"><rect width="850" height="350" rx="12" fill="#eef1e8"/><rect x="65" y="55" width="230" height="230" fill="#d7e0ce" stroke="#6c806c" stroke-width="2"/><rect x="91" y="81" width="178" height="178" fill="none" stroke="#427ca0" stroke-dasharray="7 5" stroke-width="3"/><path d="M180 55v230M65 170h230" stroke="#869b80"/><circle cx="180" cy="170" r="5" fill="#9b604b"/>'
    coordinate += svg_text(65, 33, 'Sprite 外框与逻辑格') + svg_text(70, 315, '虚线为逻辑格；外框可越格', 15)
    coordinate += '<path d="M480 260H775" stroke="#7c8f75" stroke-width="3"/><path d="M610 257Q550 180 600 115Q645 150 630 257Z" fill="#78916d" stroke="#456349" stroke-width="2"/><path d="M615 257Q650 170 690 170Q685 235 630 257" fill="#94a37c" stroke="#456349" stroke-width="2"/><circle cx="623" cy="260" r="8" fill="#ae694d"/><path d="M623 260v-35m0 35h40" stroke="#ae694d" stroke-width="2"/>'
    coordinate += svg_text(490, 68, '独立 Sprite 的视觉与作者脚点') + svg_text(510, 309, '转向改变视觉；地面根保持脚点', 15) + '</svg>'
    (tutorial / '坐标与脚点.svg').write_text(coordinate, encoding='utf-8')
    template = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 410"><defs><pattern id="check" width="16" height="16" patternUnits="userSpaceOnUse"><rect width="16" height="16" fill="#d7dbd1"/><path d="M0 0h8v8H0ZM8 8h8v8H8Z" fill="#edf0e7"/></pattern></defs><rect width="600" height="410" fill="#eef1e8"/><g transform="translate(40 45) scale(2.3)"><rect width="128" height="128" fill="url(#check)" stroke="#647b64"/><rect id="alpha-shape" x="14.5" y="14.5" width="99" height="99" rx="24" fill="white"/><g id="teaching-guides" fill="none" stroke="#427ca0" stroke-width=".6"><rect x="14.5" y="14.5" width="99" height="99" stroke-dasharray="3 2"/><path d="M64 4v120M4 64h120"/><circle cx="64" cy="64" r="2" fill="#ac654a"/></g></g>'
    template += svg_text(365, 95, '白色RGB + 有效alpha', 16) + svg_text(365, 145, '128×128 画布', 16) + svg_text(365, 180, '中心99像素逻辑格', 16) + svg_text(365, 215, '居中pivot / PPU99', 16) + svg_text(40, 375, '原创单片练习。标线仅在SVG教学层；PNG保留透明，不是完整边缘族。', 15) + '</svg>'
    (art / '原创边缘练习模板.svg').write_text(template, encoding='utf-8')
    atlas = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1920 2060"><rect width="1920" height="2060" fill="#eef1e8"/>'
    atlas += svg_text(25, 36, '原创技术族：八邻域精确编号（不使用原作15片规则）', 25)
    atlas += svg_text(25, 67, '本索引按编号从上往下；实际Sprite Rect以Unity左下角坐标为准。绿色=同族邻格，浅色=不匹配。所有规则Fixed。', 17)
    neighbors = [(-1, 1), (0, 1), (1, 1), (-1, 0), (1, 0), (-1, -1), (0, -1), (1, -1)]
    rows = []
    for mask in range(256):
        bx, by = 25 + (mask % 16) * 118, 90 + (mask // 16) * 120
        atlas += f'<g id="mask_{mask:03d}"><rect x="{bx}" y="{by}" width="110" height="113" fill="#f8f8f0" stroke="#bdcab9"/>'
        atlas += svg_text(bx + 6, by + 20, f'{mask:03d}', 16)
        atlas += f'<rect x="{bx+45}" y="{by+54}" width="19" height="19" fill="#355c43"/>'
        for bit, (nx, ny) in enumerate(neighbors):
            fill = '#83a66d' if mask & (1 << bit) else '#dde2d5'
            atlas += f'<rect x="{bx+45+nx*22}" y="{by+54-ny*22}" width="19" height="19" fill="{fill}" stroke="#b4c2af"/>'
        atlas += '</g>'
        rows.append([f'mask_{mask:03d}', mask, (mask % 16) * 128, (mask // 16) * 128, 128, 128, 99, '.5,.5', 'Fixed'] + [('This' if mask & (1 << bit) else 'NotThis') for bit in range(8)])
    atlas += svg_text(25, 2045, '原创数学构造的编号说明；完整绘制图集与RuleTile由工坊原创样本工厂提供。单片练习不是完整边族。', 17) + '</svg>'
    (art / '原创技术边缘族_编号图.svg').write_text(atlas, encoding='utf-8')
    with (art / '原创技术边缘族_编号表.csv').open('w', encoding='utf-8-sig', newline='') as output:
        writer = csv.writer(output)
        writer.writerow(['spriteName', 'maskId', 'rectX_UnityBottomLeft', 'rectY_UnityBottomLeft', 'rectWidth', 'rectHeight', 'PPU', 'pivotNormalized', 'ruleTransform', 'NW', 'N', 'NE', 'W', 'E', 'SW', 'S', 'SE'])
        writer.writerows(rows)
    image = Image.new('RGBA', (128, 128), (255, 255, 255, 0))
    pixels = image.load()
    for y in range(128):
        for x in range(128):
            qx, qy = abs(x + .5 - 64) - (49.5 - 24), abs(y + .5 - 64) - (49.5 - 24)
            distance = math.hypot(max(qx, 0), max(qy, 0)) + min(max(qx, qy), 0) - 24
            alpha = round(max(0, min(1, .5 - distance)) * 255)
            pixels[x, y] = (255, 255, 255, alpha)
    image.save(art / '原创边缘练习_孤岛128.png')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', required=True, type=Path)
    parser.add_argument('--public', action='store_true', help='Omit local-only catalog/entry links for the pure-original distributable.')
    args = parser.parse_args()
    root = args.root.resolve()
    diagrams(root)
    page(root / '03-tutorial' / '地面组合逻辑教程.md', '../04-art-guide/素材绘制规范.html', args.public)
    page(root / '04-art-guide' / '素材绘制规范.md', '../03-tutorial/地面组合逻辑教程.html', args.public)
    for folder in ('03-tutorial', '04-art-guide'):
        for path in (root / folder).glob('*.html'):
            text = path.read_text(encoding='utf-8')
            for relative in re.findall(r'<img src="([^"]+)"', text):
                if not (path.parent / html.unescape(relative)).is_file():
                    raise RuntimeError('Missing diagram: ' + relative)
            if 'http://' in text or 'https://' in text:
                raise RuntimeError('Unexpected external page resource')
    print('Built both offline guides, 4 original editable SVGs, a 256-row semantics CSV and one RGBA practice PNG; all embedded media exist.')


if __name__ == '__main__':
    main()
