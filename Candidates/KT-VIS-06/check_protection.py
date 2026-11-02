"""Read-only verification of original dirty bytes, source maps/saves, and prior05 profiles."""
from pathlib import Path
import hashlib,json
root=Path(__file__).resolve().parents[2]
archive=Path('E:/WORLDCREATOR/XingHaiHuiLang/研究/可见性修复/KT-VIS-06')
sync={'docs/agent-sync/PROJECT_STATE.md':'PROJECT_STATE.md.before.bin','docs/agent-sync/TASK_REGISTRY.md':'TASK_REGISTRY.md.before.bin','docs/agent-sync/tasks/2026-11-02-kt-vis-04.md':'2026-11-02-kt-vis-04.md.before.bin','OSSASTRA_RULES.md':'OSSASTRA_RULES.md.before.bin'}
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest() if p.is_file() else None
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
errors=[]
dirty=read(archive/'protected-dirty-before.json')
for name,expected in dirty.items():
    p=root/name
    if name in sync:
        if not p.read_bytes().startswith((archive/sync[name]).read_bytes()):errors.append('changed sync prefix: '+name)
    elif sha(p)!=expected:errors.append('changed original dirty: '+name)
sources=read(archive/'source-and-saves-baseline.json')
for name,expected in sources.items():
    if sha(Path(name))!=expected:errors.append('changed source/save: '+name)
profiles=read(archive/'protected-vis05-profiles-before.json')
for name,expected in profiles.items():
    if sha(Path(name))!=expected['sha256']:errors.append('changed05 profile: '+name)
report={'passed':not errors,'originalDirtyPaths':len(dirty),'nonSyncDirtyPreserved':len(set(dirty)-set(sync)),'sourceAndSaveHashes':len(sources),'prior05ProfileFiles':len(profiles),'errors':errors}
(archive/'protected-final.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
if errors:raise SystemExit(1)
