"""Focused integrity and raster contracts for DS-MAP-01 local converter."""
import hashlib,json,math,sys
from pathlib import Path
import numpy as np
from PIL import Image
import selected_assets as m
checks=[]
def check(name,cond):
 checks.append(dict(name=name,passed=bool(cond)))
 if not cond:raise AssertionError(name)
def main():
 d=json.loads((m.OUT/'asset-manifest.json').read_text(encoding='utf8'))
 check('selected props exact allowlist',set(x['id'] for x in d['props'])=={'evergreen_tall','evergreen_short','stump','grass','sapling','berrybush','rock','chest','firepit'})
 check('ground exact allowlist',set(x['id'] for x in d['ground'])=={'grass','forest','dirt'})
 for p in d['props']:
  im=Image.open(p['pngPath']);check(p['id']+' output PNG hash',m.sha(Path(p['pngPath']).read_bytes())==p['sha256'])
  check(p['id']+' fixed root pivot inside common cell',0<=p['pivotPixelsBottomLeft'][0]<=p['frameWidth'] and 0<=p['pivotPixelsBottomLeft'][1]<=p['frameHeight'])
  check(p['id']+' full original animation duration',abs(sum(f['durationSeconds']for f in p['frames'])-p['sourceFrameCount']/p['sourceFps'])<1e-8)
  check(p['id']+' rects inside output sheet',all(0<=f['rectPixelsBottomLeft'][0] and 0<=f['rectPixelsBottomLeft'][1] and f['rectPixelsBottomLeft'][0]+f['rectPixelsBottomLeft'][2]<=im.width and f['rectPixelsBottomLeft'][1]+f['rectPixelsBottomLeft'][3]<=im.height for f in p['frames']))
  check(p['id']+' only optional nonwinter symbol hidden',not p['hiddenOptionalSymbols'] or p['hiddenOptionalSymbols']==['snow'])
  for ref in p['sourceRefs']:check(p['id']+' source '+ref['relativePath'],m.sha((m.SOURCE/ref['relativePath']).read_bytes())==ref['sha256'])
 for g in d['ground']:
  check(g['id']+' atlas hash',m.sha(Path(g['pngPath']).read_bytes())==g['sha256'])
  used=[]
  for mask,name in g['recommendedMaskToRegion'].items():
   r=next(x for x in g['regions'] if x['name']==name and not x['variant']);used.append(r)
   check(g['id']+' source corner-mask '+mask,r['measuredCornerMask']==int(mask))
  check(g['id']+' real edge pixels include partial alpha',any(np.any((np.array(Image.open(r['pngPath']))[...,3]>0)&(np.array(Image.open(r['pngPath']))[...,3]<255)) for r in used))
 # Synthetic unequal basis and origin prove affine semantics and stable anchor.
 xy=m.transformed(dict(m=(2,3,-4,5,7,-9)),np.array([[1,2,0,0,0,0]],dtype=float))
 check('affine off-diagonal and translation preserved',np.allclose(xy,[[15,-3]]))
 # One solid premultiplied red symbol, two triangles. Shared diagonal must not multiply alpha.
 vertices=np.array([[0,0,0,0,1,0],[8,0,0,1,1,0],[0,8,0,0,0,0],[8,0,0,1,1,0],[8,8,0,1,0,0],[0,8,0,0,0,0]],float)
 bf=dict(frame=0,duration=1,verts=vertices);b=dict(symbols={1:[bf]});e=dict(symbol=1,frame=0,m=(1,0,0,1,0,0),z=0);a=dict(frames=[dict(elements=[e])]);at=np.zeros((8,8,4),np.float32);at[...,0]=.5;at[...,3]=.5
 ims,piv,lim=m.bake(b,a,[at]);arr=np.array(ims[0]);center=arr[4:8,4:8]
 check('shared triangle seam not alpha-overdrawn',np.all(center[...,3]==128))
 check('premultiplied input exported straight exactly once',np.all(center[...,0]==255) and np.all(center[...,1:3]==0))
 check('origin retained in padded render',piv==[2.0,10.0])
 check('unselected character or audio files absent',not any(any(s in str(f.relative_to(m.SELECTED)).lower() for s in ['wilson','wendy','wx78','sound/','voice']) for f in m.SELECTED.rglob('*') if f.is_file()))
 result=dict(passed=sum(x['passed']for x in checks),failed=sum(not x['passed']for x in checks),checks=checks)
 (m.ROOT/'01-research/conversion-validation.json').write_text(json.dumps(result,indent=2),encoding='utf8')
 print(json.dumps({k:v for k,v in result.items() if k!='checks'}))
if __name__=='__main__':main()
