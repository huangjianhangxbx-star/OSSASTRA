"""Stage only KT02's native export closure and owned source/docs; never user changes."""
from pathlib import Path
import difflib, hashlib, json, subprocess

repo = Path(__file__).resolve().parents[2]
research = Path('E:/WORLDCREATOR/XingHaiHuiLang/研究/开拓物语地表/KT-GROUND-02')
def git(*args):
    return subprocess.check_output(['git', '-c', 'core.quotepath=false', *args], cwd=repo)
def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()
assert git('rev-parse', 'HEAD').decode().startswith('19ea13b')
assert not git('diff', '--cached', '--name-only').strip(), 'Existing index: inspect, do not reset'
plan = json.loads((research / '06-delivery/CoreExportPlan_ba314e638528a8e8.json').read_text(encoding='utf-8-sig'))
owned = set()
for row in plan['paths']:
    p = repo / row['path']
    assert sha(p) == row['sha256'], row['path']
    assert sha(Path(str(p) + '.meta')) == row['metaSha256'], row['path'] + '.meta'
    owned.add(row['path'])
    owned.add(row['path'] + '.meta')
    for parent in p.parents:
        if parent == repo or parent == repo / 'Assets': break
        meta = Path(str(parent) + '.meta')
        if meta.is_file(): owned.add(meta.relative_to(repo).as_posix())
for name in ('GroundWorkshopPlaygroundBuilder.cs', 'GroundWorkshopPlaygroundSession.cs'):
    matches = list((repo / 'Assets/OSSASTRA/Scripts/GroundResearch').rglob(name))
    assert len(matches) == 1
    owned.add(matches[0].relative_to(repo).as_posix())
    owned.add(Path(str(matches[0]) + '.meta').relative_to(repo).as_posix())
for p in (repo / 'Candidates/KT-GROUND-02').rglob('*'):
    if not p.is_file() or 'qa' in p.parts or '__pycache__' in p.parts: continue
    rel = p.relative_to(repo).as_posix()
    if 'PublicGuide' in p.parts:
        doc_rel = p.relative_to(repo / 'Candidates/KT-GROUND-02/UI/PublicGuide').as_posix()
        if 'Assets/OSSASTRA/GroundWorkshop/Documentation/' + doc_rel in owned:
            owned.add(rel)
    elif p.suffix in {'.txt','.py','.js','.cjs','.md','.patch'}:
        owned.add(p.relative_to(repo).as_posix())
for p in (repo / 'docs/ossastra/KT-GROUND-02').rglob('*'):
    if p.is_file(): owned.add(p.relative_to(repo).as_posix())
owned.update({'docs/agent-sync/PROJECT_STATE.md', 'docs/agent-sync/tasks/2026-10-30-kt-ground-02.md'})
for p in owned:
    assert not any(x in p for x in ('LocalReference/', 'LocalGenerated/', 'LocalMedia/', 'DontStarveReferenceLocal/', 'af1eeb44144945249ae2a8f797cd2c5d', 'SC_GroundWorkshopLab.unity', 'Assets/Script/', 'Assets/Scripts/')), p
    assert (repo / p).is_file(), p
manifest = research / '06-delivery/git-stage-paths.txt'
manifest.write_bytes(b'\0'.join(p.encode() for p in sorted(owned)) + b'\0')
git('add', '-f', '--pathspec-from-file=' + str(manifest), '--pathspec-file-nul')

# Registry already has 17 unrelated added lines. Build a HEAD->own-section patch only.
registry = 'docs/agent-sync/TASK_REGISTRY.md'
before = git('show', 'HEAD:' + registry).decode('utf-8')
current = (repo / registry).read_text(encoding='utf-8-sig')
start = current.index('## KT-GROUND-02 ')
end = current.index('\n## ', start + 1)
section = current[start:end]
anchor = '## KT-GROUND-01 '
after = before[:before.index(anchor)] + section + '\n' + before[before.index(anchor):]
patch = ''.join(difflib.unified_diff(before.splitlines(True), after.splitlines(True), fromfile='a/' + registry, tofile='b/' + registry))
patch_path = research / '06-delivery/registry-own-stage.patch'
patch_path.write_text(patch, encoding='utf-8', newline='')
git('apply', '--cached', str(patch_path))
staged = git('diff', '--cached', '--name-only', '-z').decode().rstrip('\0').split('\0')
assert set(staged) <= owned | {registry}, 'Unexpected staged paths'
remaining = git('diff', '--name-status').decode().splitlines()
initial = (research / '01-evidence/initial-status-with-own-sync.txt').read_text(encoding='utf-8-sig').splitlines()
expected = {line[3:]:line[:2].strip() for line in initial if line[3:] != 'docs/agent-sync/PROJECT_STATE.md'}
actual = {line.split('\t',1)[1]:line.split('\t',1)[0] for line in remaining}
assert len(expected) == 37 and actual == expected, 'Original dirty path/status separation failed'
assert git('diff','--numstat','--',registry).decode().startswith('17\t0\t'), 'Registry must retain the unrelated 17 lines'
receipt = {'head':git('rev-parse','HEAD').decode().strip(), 'nativeClosure':len(plan['paths']), 'allowlistCount':len(owned), 'stagedCount':len(staged), 'staged':staged, 'remaining':remaining, 'registryUnstagedNumstat':git('diff','--numstat','--',registry).decode().strip()}
(research / '06-delivery/git-stage-receipt.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in receipt.items() if k not in {'staged','remaining'}},ensure_ascii=False))
