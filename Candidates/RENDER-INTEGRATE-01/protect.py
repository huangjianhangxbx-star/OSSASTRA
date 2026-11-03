"""Snapshot/check only original dirty bytes and named source/save files; never edits Unity assets."""
from pathlib import Path
import hashlib,json,sys
root=Path(__file__).resolve().parents[2]
evidence=Path('E:/WORLDCREATOR/XingHaiHuiLang/研究/渲染接入/RENDER-INTEGRATE-01')
sha=lambda p: hashlib.sha256(p.read_bytes()).hexdigest() if p.is_file() else None
read=lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
snapshot=evidence/'sources-saves-before-play.json'
if sys.argv[-1]=='snapshot':
    if snapshot.exists(): raise SystemExit('Baseline exists; do not replace')
    prior=read(Path('E:/WORLDCREATOR/XingHaiHuiLang/研究/可见性修复/KT-VIS-06/source-and-saves-baseline.json'))
    files={Path(p) for p in prior}
    for base in ['Assets/XingHaiHuiLang/RenderLab','Assets/OSSASTRA/Commerce','Assets/OSSASTRA/GroundWorkshop']:
        files.update(p for p in (root/base).rglob('*') if p.is_file() and p.suffix in {'.asset','.mat','.comsave'})
    data={str(p):sha(p) for p in sorted(files)}
    snapshot.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'sourceAndSaveFiles':len(data)}))
else:
    errors=[];dirty=read(evidence/'protected-dirty-before.json')
    sync={'docs/agent-sync/PROJECT_STATE.md':'PROJECT_STATE.md.before.bin','docs/agent-sync/TASK_REGISTRY.md':'TASK_REGISTRY.md.before.bin','OSSASTRA_RULES.md':'OSSASTRA_RULES.md.before.bin'}
    for p,expected in dirty.items():
        target=root/p
        if p in sync:
            if not target.read_bytes().startswith((evidence/sync[p]).read_bytes()):errors.append('sync prefix changed '+p)
        elif sha(target)!=expected:errors.append('original dirty changed '+p)
    sources=read(snapshot)
    owned={'E:/WORLDCREATOR/new/Project_ANKALUTE/Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/Rendering/RoomVisibilityRenderer.asset'}
    for p,expected in sources.items():
        if p.replace('\\','/') in owned: continue
        if sha(Path(p))!=expected:errors.append('source/save changed '+p)
    result={'passed':not errors,'originalDirty':len(dirty),'sourceAndSaveFiles':len(sources),'errors':errors}
    (evidence/'protection-final.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False));sys.exit(bool(errors))
