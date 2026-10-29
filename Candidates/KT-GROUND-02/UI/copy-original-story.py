"""Copy only the explicitly approved original Native render/practice PNGs, byte-for-byte."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path
from PIL import Image


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--story', required=True, type=Path)
    parser.add_argument('--local-root', required=True, type=Path)
    parser.add_argument('--public-root', required=True, type=Path)
    args = parser.parse_args()
    story_root = args.story.resolve()
    story = json.loads((story_root / 'story.json').read_text(encoding='utf-8-sig'))
    if story.get('format') != 'ground-workshop-original-story' or not all(story.get(key) is True for key in ('passed', 'logicPassed', 'baselineRestored', 'renderPassed', 'sceneDiskShaUnchanged')):
        raise RuntimeError('The provided original Native story is not complete/green.')
    check_names = {row['name'] for row in story['checks'] if row.get('passed') is True}
    if '原创场实际依赖无参考媒体/角色资源' not in check_names or '已保存192 Base /32 Overlay /19 连通分叉Road布局' not in check_names:
        raise RuntimeError('Expected the final original-only 192/32/19 connected-road fixture.')
    allowed = ['Original_Before', 'Original_AfterReplacement', 'Original_EdgeClose', 'Original_Base', 'Original_Overlay', 'Original_Road', 'Original_Overlay_DebugMask']
    captures = []
    for name in allowed:
        row = next(row for row in story['images'] if row['name'] == name)
        source = Path(row['path']).resolve()
        if source.parent != story_root or source.name != name + '.png' or digest(source) != row['sha256']:
            raise RuntimeError('Native PNG identity/hash mismatch: ' + name)
        if row['method'] != 'NativeCamera.Render' or Image.open(source).size != (row['width'], row['height']):
            raise RuntimeError('Native capture method/dimensions mismatch: ' + name)
        captures.append({'file': name + '.png', 'width': row['width'], 'height': row['height'], 'method': row['method'], 'sha256': row['sha256'], 'description': row['description']})
    practice = story_root / 'OriginalTechnical32.png'
    tiled = story_root / 'OriginalTechnical32_3x3.png'
    if Image.open(practice).size != (32, 32) or Image.open(tiled).size != (96, 96):
        raise RuntimeError('Practice PNG canvas size mismatch.')
    for root in [args.local_root.resolve(), args.public_root.resolve()]:
        images = root / '03-tutorial/assets/original-story'
        art = root / '04-art-guide/assets'
        images.mkdir(parents=True, exist_ok=True); art.mkdir(parents=True, exist_ok=True)
        for row in captures:
            shutil.copyfile(story_root / row['file'], images / row['file'])
            assert digest(images / row['file']) == row['sha256']
        for source in [practice, tiled]:
            shutil.copyfile(source, art / source.name)
            assert digest(art / source.name) == digest(source)
        provenance = {'format': 'ground-workshop-original-capture-provenance', 'version': '1', 'storyId': story_root.name,
            'scope': 'Original-only ground and decorations, NativeCamera.Render full frames; no third-party character/reference media, not an Editor window capture.',
            'authorFixture': {'baseCells': 192, 'overlayCells': 32, 'roadCells': 19, 'roadConnectivity': 'four-neighbor connected branch', 'decorations': 44},
            'logicPassed': True, 'baselineRestored': True, 'sceneDiskShaUnchanged': True,
            'captures': captures, 'practice': [{'file': str(Path('../../..') / '04-art-guide/assets' / source.name).replace('\\', '/'),
                'width': Image.open(source).width, 'height': Image.open(source).height, 'sha256': digest(source)} for source in [practice, tiled]]}
        (images / 'capture-provenance.json').write_text(json.dumps(provenance, ensure_ascii=False, indent=2), encoding='utf-8')
    print('Copied 7 verified original Native PNGs and 2 original practice PNGs to local/public fixed relative paths; sanitized provenance contains no absolute source paths.')


if __name__ == '__main__':
    main()
