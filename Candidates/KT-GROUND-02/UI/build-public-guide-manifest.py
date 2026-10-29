"""Verify the exact pure-original public guide payload and emit its SHA-256 manifest."""
import argparse
import hashlib
import json
import re
from pathlib import Path


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', required=True, type=Path)
    root = parser.parse_args().root.resolve()
    paths = sorted(path for path in root.rglob('*') if path.is_file() and 'qa' not in path.parts and path.name != 'public-manifest.json')
    rows = [{'path': path.relative_to(root).as_posix(), 'bytes': path.stat().st_size, 'sha256': sha(path)} for path in paths]
    if len(rows) != 21:
        raise RuntimeError('Expected the explicitly approved 11 original guide files + 7 Native PNG + 2 practice PNG + provenance, got ' + str(len(rows)))
    for row in rows:
        if 'reference-play' in row['path'] or row['path'].startswith('01-evidence/'):
            raise RuntimeError('Private payload rejected: ' + row['path'])
        path = root / row['path']
        if path.suffix.lower() in ('.html', '.md') and re.search(r'images/reference', path.read_text(encoding='utf-8')):
            raise RuntimeError('Private capture link rejected: ' + row['path'])
    image_root = root / '03-tutorial/assets/original-story'
    provenance = json.loads((image_root / 'capture-provenance.json').read_text(encoding='utf-8'))
    if len(provenance['captures']) != 7 or not all(sha(image_root / row['file']) == row['sha256'] for row in provenance['captures']):
        raise RuntimeError('Native original frame identity/SHA mismatch.')
    for path in root.rglob('*.html'):
        for link in re.findall(r'(?:src|href)="([^"]+)"', path.read_text(encoding='utf-8')):
            if not link.startswith('#') and not (path.parent / link).is_file():
                raise RuntimeError('Missing public link: ' + link)
    manifest = {'format': 'ground-workshop-public-guides', 'version': '2',
        'content': 'owned text, independently generated diagrams/semantics, 7 approved original NativeCamera.Render frames, 32px practice PNG and 3x3 preview',
        'originalStory': 'Story_af1facdf26994ef4ab3c532ec4a83005',
        'excluded': 'qa layout images and reports; all private source tables/media/catalog/Unity reference screenshots', 'files': rows}
    (root / 'public-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'files': len(rows), 'bytes': sum(row['bytes'] for row in rows), 'nativeOriginalFrames': 7,
        'nativeFramesExactSHA': True, 'practicePNGs': 2, 'missingPublicLinks': 0, 'privateReferenceImages': 0}))


if __name__ == '__main__':
    main()
