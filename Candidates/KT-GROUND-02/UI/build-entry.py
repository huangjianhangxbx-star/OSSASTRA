"""Generate the local KT02 landing page; status remains explicit until root finalizes it."""
import argparse
import hashlib
import html
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', required=True, type=Path)
    parser.add_argument('--stage', default='集成验收中')
    parser.add_argument('--summary', default='制作工具与资料已接线，最新Native批次仍由root复验；本页不提前宣称整轮通过。')
    args = parser.parse_args()
    root = args.root.resolve()
    remote = None
    remote_path = root / '06-delivery/product-remote.json'
    if remote_path.is_file():
        candidate = json.loads(remote_path.read_text(encoding='utf-8-sig'))
        product = candidate.get('product', '')
        if candidate.get('verified') is True and candidate.get('remote') == product and len(product) == 40 and all(c in '0123456789abcdef' for c in product):
            remote = candidate
    links = [
        ('素材图鉴', '02-catalog/场景素材图鉴.html', '按名称、用途与assetId查找；兼容状态与来源分开标记。'),
        ('地面组合教程', '03-tutorial/地面组合逻辑教程.html', '八章跟做：从组成拆解到保存、原创替换和试玩。'),
        ('绘制规范与模板', '04-art-guide/素材绘制规范.html', '底纹、边缘alpha、独立Sprite各画什么；附原创编号与练习。')
    ]
    for _, relative, _ in links:
        if not (root / relative).is_file():
            raise RuntimeError('Entry would create a dead link: ' + relative)
    packages = [
        ('Core原生包 · 83个自有文件', '06-delivery/GroundWorkshop-Core_ba314e638528a8e8.unitypackage', 2157793, '36b662d0586c72fc28849877ab4177579231b60de27047c5223dba172a461740'),
        ('源码ZIP · 原样Native导出内容', '06-delivery/GroundWorkshop-Source_ba314e638528a8e8.zip', 2043911, 'ea1866d3b70bd7c816da66681277280714837ad008dda4530145884391cae8db')
    ]
    verified_packages = []
    for label, relative, expected_bytes, expected_sha in packages:
        package = root / relative
        if not package.is_file():
            continue
        actual_sha = hashlib.sha256(package.read_bytes()).hexdigest()
        if package.stat().st_size != expected_bytes or actual_sha != expected_sha:
            raise RuntimeError('Delivery identity changed: verify and update the entry before publishing ' + relative)
        verified_packages.append((label, relative, expected_bytes, actual_sha))
    cards = ['<article><span>01 / UNITY</span><h2>进入地表工坊</h2><p>在Unity内打开下列菜单。这里复制菜单路径，不会从网页执行本机命令。</p><input readonly value="Tools/OSSASTRA/星骸地表工坊"><button onclick="copyMenu(this)">复制菜单路径</button></article>']
    for index, (label, relative, caption) in enumerate(links, 2):
        cards.append(f'<article><span>0{index} / 离线资料</span><h2>{label}</h2><p>{caption}</p><a class="button" href="{relative}">打开 →</a></article>')
    receipts = []
    for name in ['window-gate-green.json', 'window-owned-state-i.json', 'core-recipes-i.json', 'save-as-isolation-i.json', 'actual-gesture-poll.json', 'reference-input-story-result.json', 'reference-performance-result.json', 'window-owned-red-stack.json', 'window-palette-trace.json', 'source-catalog-coverage.json']:
        if (root / '01-evidence' / name).is_file():
            receipts.append(f'<li><a href="01-evidence/{name}">{name}</a>：某轮实际记录，不能自动视为最新终验。</li>')
    markup = '''<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>开始使用 · 星骸地表工坊</title><style>
*{box-sizing:border-box}body{margin:0;background:#f3f1e8;color:#263b33;font:16px/1.85 'Microsoft YaHei','PingFang SC',sans-serif}main{max-width:1180px;margin:auto;padding:45px 30px 70px}header{padding:24px 0 30px;border-bottom:1px solid #ccd5c9}small,.tag{color:#597362;letter-spacing:.12em}h1{font-size:38px;line-height:1.4;margin:12px 0}h2{font-size:24px;line-height:1.5}section{margin-top:34px}p{margin:12px 0}.status{background:#e4ebde;border-left:4px solid #5c805f;padding:15px 20px;margin-top:22px}.cards{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:18px}article{background:#fbfbf5;border:1px solid #cdd8ca;border-radius:7px;padding:24px}article span{font-size:12px;color:#6b7c6c}article h2{margin:9px 0}input,code{font-family:Consolas,'Microsoft YaHei',monospace}input{display:block;width:100%;padding:9px;border:1px solid #b5c5b3;background:#eef2e8;color:#274530;font-size:13px}button,.button{display:inline-block;border:0;border-radius:3px;background:#386751;color:#fff;text-decoration:none;padding:10px 17px;font:15px 'Microsoft YaHei',sans-serif;margin-top:14px;cursor:pointer}a{color:#35654b;text-underline-offset:3px}li{margin:9px 0}code{background:#e6e9df;padding:3px 6px;overflow-wrap:anywhere}.scope{display:grid;grid-template-columns:1fr 1fr;gap:24px}.scope>div{border-top:2px solid #9eaf98;padding-top:8px}footer{margin-top:40px;border-top:1px solid #ccd5c9;padding-top:15px;font-size:13px;color:#617467}@media(max-width:760px){main{padding:24px 20px 40px}h1{font-size:29px}.cards,.scope{grid-template-columns:1fr}}
</style></head><body><main><header><small>KT–GROUND–02 / 本机工作入口</small><h1>星骸地表工坊</h1><p>选主题、刷地与道路、布置花草，保存为可重新打开的地图。先制作，再按需要理解组成。</p></header>'''
    markup += '<div class="status"><strong>' + html.escape(args.stage) + '</strong><br>' + html.escape(args.summary) + '<br><strong>UI_PHYSICAL_UNVERIFIED</strong>：当前真实桌面工具不可用，物理鼠标完整流程与Editor主窗口实拍未验证；Native事件、合同与Scene/Game渲染分别记录。</div>'
    markup += '<section class="cards">' + ''.join(cards) + '</section>'
    markup += '''<section><h2>第一次做地图</h2><ol><li>打开Unity工坊，选“使用原创教学主题”，填写名称和格数后新建，保存在 <code>Assets/OSSASTRA/GroundWorkshop/Maps</code> 或 <code>Samples</code>。</li><li>选择覆盖或道路层，用刷地/矩形制作。左键拖刷，Alt/中右键导航；一笔一Undo，Esc取消当前独占笔画。层锁保护所有相关写入口。</li><li>点放花草，或按稳定种子、密度、间距和明确区域撒布。道路避让、手摆与锁定项保护需按真实结果检查。</li><li>保存当前地图，关闭场景再打开。替换原创底纹前，先存新配方，再预检当前层已有同类格，决定提交或取消。</li></ol></section>
<section><h2>原创样例与双角色试玩</h2><div class="scope"><div><h3>原创可编辑样例</h3><p>完整工程内场景：<br><code>Assets/OSSASTRA/Scenes/Research/SC_GroundWorkshopOriginalLab.unity</code></p><p>三层原创技术主题、两套独立边缘族和原创装饰用于接入验证，不代表正式美术。纯工坊插件无角色适配时仍可制作、保存和复用地图。</p></div><div><h3>完整游戏工程试玩</h3><p>菜单：<br><code>Tools/OSSASTRA/地表工坊/打开双角色工坊场</code></p><p>在工坊点“保存并进入角色试玩”。完整项目适配使用临时副本承接既有双角色、镜头与单敌人，退出后返回作者图。无适配时明确提示，不另造控制器。</p><p>恢复菜单：<br><code>Tools/OSSASTRA/地表工坊/返回作者地图</code></p></div></div><p>参考工坊场为 <code>Assets/OSSASTRA/Scenes/Research/SC_GroundWorkshopLab.unity</code>。旧Frontier/饥荒样地保持保护；原作媒体与其图鉴只在本机。</p></section>
<section><h2>来源与当前边界</h2><ul><li><strong>SOURCE_VERIFIED</strong>：指定源地图的原生Grid/Tilemap、RuleTile、纹理引用和保存格研究。</li><li><strong>UNITY_ADAPTATION</strong>：自有world-XZ底纹采样、候选世界周期、Unlit、裁剪与工坊；不冒称原作完整渲染器。</li><li><strong>INFERRED / UNKNOWN</strong>：推断逐项标记；原精确Shader UV、完整光照与原作者编辑器仍未知。</li><li>当前v1使用原生Tilemap作为唯一地面格子作者数据，有界1—16层。未来模式新增变体层，不假称同层单材质能让新旧格显示两种底纹。</li><li>复杂动画、世界生成、高度地形、独立EXE与正式商业美术精修不在本轮。视觉、手感和制作体验仍待用户确认。</li></ul></section>'''
    if verified_packages:
        package_cards = ''.join('<article><h3>' + html.escape(label) + '</h3><p>' + f'{size:,}' + ' bytes</p><p><code>SHA256 ' + sha + '</code></p><a class="button" href="' + relative + '" download>打开本机交付文件 →</a></article>' for label, relative, size, sha in verified_packages)
        markup += '<section><h2>自有技术交付包</h2><div class="cards">' + package_cards + '</div><p>Core包含25份源、2 Shader、原创样板与21份自有教材内容；参考图鉴、参考媒体、旧样板Builder/旧刷地窗口与可选角色试玩适配不随包。原生包为83个自有文件，源码ZIP直接取原生导出内容。</p><p>验证依赖为Tuanjie 2022.3.62t16、URP14.2、2D Tilemap/Extras3.1.3与Newtonsoft3.2.2。独立Editor Core编译25源0error/1warning；空工程实际导入、Player、其他渲染管线与物理鼠标流程仍未验证。包存在及SHA核对不等于GitHub远端发布成功。</p></section>'
    if remote:
        markup += '<section><h2>已核验发布</h2><p>产品提交 <a href="https://github.com/huangjianhangxbx-star/OSSASTRA/commit/' + remote['product'] + '">' + remote['product'] + '</a> 已与GitHub main精确核对。物理UI和空工程导入仍未验。</p></section>'
    progress_link = '<a href="03-tutorial/最终验收报告.md">最终技术交接记录</a>；' if (root / '03-tutorial/最终验收报告.md').is_file() else ''
    progress_link += '<a href="03-tutorial/验收进展_实际回执草稿.md">历史实际回执进展记录</a>；' if (root / '03-tutorial/验收进展_实际回执草稿.md').is_file() else ''
    markup += '<section><h2>实际证据与收口记录</h2><p>' + progress_link + '<a href="03-tutorial/最终验收报告_模板.md">验收报告填写模板</a> 逐项对应A01—A25。历史阶段稿和未填写模板不作为最终结论；以最新验证与交接记录为准，保留首次失败和修复轮次。</p><ul>' + ''.join(receipts) + '</ul><p>八章教程已嵌完整原创地图、边缘、图层拆解、mask与换材前后共7张真实NativeCamera.Render帧，附32×32底纹与3×3下载练习。参考Game View与旋转图仅本机。相机渲染、自动SceneView事件、合同与物理鼠标验证分别记录；教材SVG不顶替现场证据。</p></section>'
    markup += '''<footer>本页与资料离线可读。网页不启动任意shell、不运行Unity命令；复制不可用时会选中路径，请Ctrl+C。任务完成后退出Play停在自有保存场，本轮不关机或休眠。<div id="copy-result" role="status"></div></footer></main><script>async function copyMenu(button){const input=button.previousElementSibling;try{if(!navigator.clipboard)throw new Error();await navigator.clipboard.writeText(input.value);document.getElementById('copy-result').textContent='已复制Unity菜单路径。'}catch(error){input.focus();input.select();document.getElementById('copy-result').textContent='已选中菜单路径，请Ctrl+C复制。'}}</script></body></html>'''
    (root / '开始使用.html').write_text(markup, encoding='utf-8')
    status = {'stage': args.stage, 'summary': args.summary, 'physicalUI': 'UI_PHYSICAL_UNVERIFIED', 'emptyProjectImport': 'UNVERIFIED', 'claimsWholeTaskPhysicalAcceptance': False, 'remoteProductVerified': bool(remote), 'remoteProduct': remote['product'] if remote else None, 'verifiedDeliveryFiles': [relative for _, relative, _, _ in verified_packages]}
    (root / '03-tutorial' / 'entry-status.json').write_text(json.dumps(status, ensure_ascii=False, indent=2), encoding='utf-8')
    print('Built local start page; packages=' + str(len(verified_packages)) + '; verified remote receipt=' + str(bool(remote)) + '; physical input and empty-project import remain unverified.')


if __name__ == '__main__':
    main()
