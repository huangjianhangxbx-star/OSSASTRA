"""Prepare only this round's named source/tests/docs; preserve original dirty bytes and index.

Reuses the already reviewed temporary-index/prefix protection implementation.
Never commits, pushes, edits Unity assets, or includes bin/obj/media/scene/save files.
"""
from pathlib import Path
import importlib.util
root=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('protected_publication',root/'Candidates/KT-VIS-06/prepare_publication.py')
base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
base.EVIDENCE=Path('E:/WORLDCREATOR/XingHaiHuiLang/研究/渲染接入/RENDER-INTEGRATE-01')
base.CANDIDATES='Candidates/RENDER-INTEGRATE-01'
base.DOCS='docs/ossastra/RENDER-INTEGRATE-01'
base.SYNC_BASELINES={'docs/agent-sync/PROJECT_STATE.md':'PROJECT_STATE.md.before.bin','docs/agent-sync/TASK_REGISTRY.md':'TASK_REGISTRY.md.before.bin'}
def collect(excluded):
    files=set()
    def add(p):
        if not p.is_file():raise RuntimeError('Missing exact publication file '+str(p))
        files.add(p.relative_to(root).as_posix())
    for name in ['RoomMaskFrame.cs','RoomPositivePass.cs','RoomSightClip.hlsl']:
        add(root/'Assets/OSSASTRA/Scripts/SceneVisibility/Rooms/Rendering'/name)
    for name in ['RoomVisibilitySight.cs','RoomWorldLookRole.cs','RoomWorldLookRole.cs.meta']:
        add(root/'Assets/OSSASTRA/Scripts/SceneVisibility/Rooms/Runtime'/name)
    add(root/'Assets/OSSASTRA/Scripts/WorldRendering.meta')
    for p in (root/'Assets/OSSASTRA/Scripts/WorldRendering').rglob('*'):
        if p.is_file() and p.suffix in {'.cs','.shader','.hlsl','.meta'}:add(p)
    # Frozen original data DTOs only; do not pull old controllers/asmdefs or media.
    for name in ['T7B/Runtime/OutdoorAtmosphereProfile.cs','T7B/Runtime/T7BMath.cs','T7C1/Runtime/RegionalLookProfile.cs','T7C2/Runtime/HD2DPostProfile.cs']:
        add(root/'Assets/XingHaiHuiLang/RenderLab'/name);add(root/'Assets/XingHaiHuiLang/RenderLab'/(name+'.meta'))
    for p in (root/base.CANDIDATES).iterdir():
        if p.is_file() and (p.suffix in {'.md','.py','.cs'} or p.name.endswith('.cs.txt')):add(p)
    for p in (root/base.DOCS).glob('*.md'):add(p)
    add(root/'docs/agent-sync/tasks/2026-11-03-render-integrate-01.md')
    for name in files:base.reject_forbidden(name)
    return sorted(files),[]
base.collect_paths=collect
if __name__=='__main__':base.main()
