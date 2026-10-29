"""Rebuild the DS-MAP-01 selected local media. GPL-2.0-or-later; see selected_assets.py."""
from pathlib import Path
import hashlib,json,math,platform,sys,xml.etree.ElementTree as ET
import numpy as np
from PIL import Image,ImageDraw
import selected_assets as m
JOBS=[
 ('evergreen_tall','常青树（高）','evergreen_new',['evergreen_tall_old','evergreen_short_normal'],'sway1_loop_tall',.25,True),
 ('evergreen_short','常青树（矮）','evergreen_new',['evergreen_tall_old','evergreen_short_normal'],'sway1_loop_short',.25,True),
 ('stump','树桩','evergreen_new',['evergreen_tall_old','evergreen_short_normal'],'stump_tall',.25,False),
 ('grass','草丛','grass1',['grass'],'idle',0,True),
 ('sapling','树苗','sapling',['sapling'],'sway',0,True),
 ('berrybush','浆果丛','berrybush',['berrybush'],'berriesmost',.1,False),
 ('rock','岩石','rock',['rock'],'full',1,False),
 ('chest','木箱','treasure_chest',['treasure_chest'],'closed',0,False),
 ('firepit','石火坑（无火）','firepit',['firepit'],'idle',.3,False),
]
CORNER_MAP={1:18,2:19,3:3,4:21,5:22,6:5,7:7,8:25,9:2,10:27,11:4,12:9,13:10,14:13,15:1}
def writej(path,v):path.parent.mkdir(exist_ok=True,parents=True);path.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def hashfile(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def recordsource(rel):
 f=m.SOURCE/rel
 return dict(relativePath=rel,sha256=hashfile(f),bytes=f.stat().st_size)
def props():
 results=[]
 for id,label,bz,az,pick,radius,loop in JOBS:
  b,aa,at,meta=m.loadpair(bz,az);a=next(x for x in aa if x['name']==pick)
  frames,pivot,lim=m.bake(b,a,at,scale=1/3)
  # Preserve origin even when animation's art is entirely above/below it.
  unique=[];timeline=[]
  for im in frames:
   digest=hashlib.sha256(im.tobytes()).hexdigest()
   if unique and digest==unique[-1][1]:
    timeline[-1]['durationSeconds']+=1/a['fps']
   else:
    unique.append((im,digest));timeline.append(dict(frameIndex=len(unique)-1,durationSeconds=1/a['fps']))
  w,h=frames[0].size;n=len(unique);cols=max(1,math.ceil(math.sqrt(n*h/w)));rows=math.ceil(n/cols)
  if cols*w>4096:cols=max(1,4096//w);rows=math.ceil(n/cols)
  if rows*h>4096:raise ValueError('selected sheet exceeds 4096 cap')
  sw=1<<(cols*w-1).bit_length();sh=1<<(rows*h-1).bit_length()
  sheet=Image.new('RGBA',(sw,sh));rects=[]
  for i,(im,dh) in enumerate(unique):
   x=(i%cols)*w;y=(i//cols)*h;sheet.paste(im,(x,y));rects.append([x,sh-y-h,w,h])
  d=m.OUT/'props'/id;d.mkdir(parents=True,exist_ok=True);png=d/'sheet.png';sheet.save(png)
  frames[0].save(d/'preview.png')
  missing=sorted({a['names'].get(e['symbol'],str(e['symbol'])) for f in a['frames'] for e in f['elements'] if m.symbolframe(b,e) is None})
  if missing and missing!=['snow']:raise ValueError(f'unexpected missing symbols in {id}: {missing}')
  # Only snow absent: native standardcomponents.MakeSnowCovered installs and hides this
  # override in non-winter. Seasonal assets are expressly outside the selected set.
  refs=[recordsource('anim/'+z+'.zip') for z in dict.fromkeys([bz]+az)]
  entry=dict(id=id,label=label,pngPath=str(png),width=sw,height=sh,frameWidth=w,frameHeight=h,pivotPixelsBottomLeft=pivot,pixelsPerUnit=100,pixelScaleFromAnimation=1/3,pixelsPerUnitEvidence='UNITY_ADAPTATION calibration; not native engine pixel ratio',frames=[dict(rectPixelsBottomLeft=rects[t['frameIndex']],durationSeconds=t['durationSeconds']) for t in timeline],sourceFrameCount=len(frames),uniqueFrameCount=n,sourceFps=a['fps'],loop=loop,sourceBank=a['bank'],sourceBuild=bz,sourceAnimation=pick,sourceFacingByte=a['facing'],displayMode='Billboard',displayEvidence='constants.lua ANIM_ORIENTATION.Default=Billboard; selected prefab no override; native axis details UNKNOWN',sourceCollisionRadius=radius,collisionEvidence='SOURCE_VERIFIED' if radius else 'No obstacle physics in selected prefab; Unity nonblocking',bounds=lim,hiddenOptionalSymbols=missing,hiddenOptionalEvidence='standardcomponents.lua MakeSnowCovered hides snow in non-winter' if missing else None,atlasInfo=meta,sourceRefs=refs,sha256=hashfile(png),bytes=png.stat().st_size)
  writej(d/'asset.json',entry);results.append(entry)
  print('PROP',id,len(frames),'frames',n,'unique',(w,h),'sheet',sheet.size,'pivot',pivot,flush=True)
 return results

def ground():
 result=[];od=m.OUT/'ground';od.mkdir(parents=True,exist_ok=True)
 for name in ['grass','forest','dirt']:
  tex=m.selected('levels/tiles/'+name+'.tex');data,meta=m.tex(tex.read_bytes());im=Image.fromarray(np.round(data*255).astype(np.uint8));atlas=od/(name+'-atlas.png');im.save(atlas)
  xml=m.selected('levels/tiles/'+name+'.xml');regions=[];els=ET.fromstring(xml.read_text()).findall('./Elements/Element')
  for e in els:
   d=e.attrib;u1,u2,v1,v2=[float(d[k]) for k in ['u1','u2','v1','v2']];w,h=im.size;l,t,ri,bot=[round(u1*w),round((1-v2)*h),round(u2*w),round((1-v1)*h)];cr=im.crop((l,t,ri,bot));fn=name+'-'+d['name']+'-'+(d.get('variant')or'base')+'.png';fp=od/fn;cr.save(fp)
   a=np.array(cr);hh,ww=a.shape[:2];pts=[(int(hh*.1),int(ww*.1)),(int(hh*.1),int(ww*.9)),(int(hh*.9),int(ww*.9)),(int(hh*.9),int(ww*.1))];al=[int(a[y,x,3]) for y,x in pts];mask=sum(1<<i for i,v in enumerate(al) if v>128)
   regions.append(dict(name=d['name'],variant=d.get('variant',''),pngPath=str(fp),uv=[u1,v1,u2,v2],rectPixelsBottomLeft=[l,h-bot,ri-l,bot-t],size=cr.size,measuredCornerMask=mask,sha256=hashfile(fp)))
  thumbs=Image.new('RGB',(8*144,math.ceil(len(regions)/8)*160),(40,46,42));draw=ImageDraw.Draw(thumbs)
  for i,r in enumerate(regions):
   cr=Image.open(r['pngPath']);cr.thumbnail((128,128));xx=(i%8)*144;yy=(i//8)*160;thumbs.paste(cr,(xx,yy),cr);draw.text((xx+2,yy+130),r['name']+' '+r['variant'],fill='white')
  thumbs.save(m.ROOT/'01-research'/(name+'-tiles-contact.png'))
  noiseRel='levels/textures/Ground_noise_dirt.tex' if name=='dirt' else 'levels/textures/Ground_noise.tex'
  no=m.selected(noiseRel);nimg,nmeta=m.tex(no.read_bytes());npath=od/(name+'-noise.png');Image.fromarray(np.round(nimg*255).astype(np.uint8)).save(npath)
  entry=dict(id=name,pngPath=str(atlas),texture=meta,regions=regions,noisePngPath=str(npath),noiseTexture=nmeta,logicTileSize=4,logicTileEvidence='SOURCE_VERIFIED constants.lua TILE_SCALE=4; no Unity voxel-unit change',cornerBits='top-left=1,top-right=2,bottom-right=4,bottom-left=8 (image coordinates)',recommendedMaskToRegion={str(k):f'{v:02d}' for k,v in CORNER_MAP.items()},maskEvidence='UNITY_ADAPTATION from measured PNG corner alpha at10% and90%; native tile-neighbor selection UNKNOWN',sourceRefs=[recordsource('levels/tiles/'+name+'.tex'),recordsource('levels/tiles/'+name+'.xml'),recordsource(noiseRel)],sha256=hashfile(atlas))
  writej(od/(name+'-tiles.json'),entry);result.append(entry);print('GROUND',name,len(regions),'regions',flush=True)
 return result

def sources():
 paths=['scripts/cameras/followcamera.lua','scripts/components/playercontroller.lua','scripts/constants.lua','scripts/worldtiledefs.lua','scripts/prefabutil.lua','scripts/components/placer.lua','scripts/standardcomponents.lua','scripts/map/graphnode.lua','scripts/map/static_layout.lua','scripts/map/customise.lua','scripts/shardindex.lua','shaders/anim.ksh','shaders/ground.ksh']+[f'scripts/prefabs/{x}.lua' for x in ['evergreens','grass','sapling','berrybush','rocks','treasurechest','firepit','campfire']]
 entries=[recordsource(x) for x in paths if (m.SOURCE/x).exists()]
 selectedEntries=[dict(relativePath=str(f.relative_to(m.SELECTED)).replace('\\','/'),sha256=hashfile(f),bytes=f.stat().st_size) for f in m.SELECTED.rglob('*') if f.is_file()]
 writej(m.ROOT/'00-baseline/source-index.json',dict(sourceRoot=str(m.SOURCE),basis='BASE data layer only, not DLC or mods',scriptAndShaderEvidence=entries,selectedMedia=selectedEntries))
 if any('wilson' in x['relativePath'] or '/sound/' in x['relativePath'] for x in selectedEntries):raise ValueError('unexpected media scope')
 return sum(x['bytes'] for x in selectedEntries)
def main():
 p=props();g=ground();size=sources();manifest=dict(format='DS-MAP-01-local-assets-v1',sourceRoot=str(m.SOURCE),sourceLayer='base',nativeBuild='578406 WIN32_STEAM release',steamAppId=219740,steamBuildId=12527201,localOnly=True,tool='focused reviewed Python KTEX/BILD/ANIM adapter + Pillow12.3.0 NumPy2.3.5',conversionCodeSha256=hashfile(Path(__file__)),sampling='Bilinear on premultiplied source; straight RGBA export once; exact BILD triangle UV and ANIM affine/z order, common fixed canvas and origin for every frame',props=p,ground=g,selectedSourceBytes=size,limitations=['No native engine projection/render-state inspection; full/vertical billboard distinction UNKNOWN','No native visual observation; process exited at startup','Nonwinter only; optional snow overrides excluded','Only selected scene objects; no character/audio/world/DLC extraction'])
 writej(m.OUT/'asset-manifest.json',manifest)
 total=sum(f.stat().st_size for base in [m.SELECTED,m.OUT] for f in base.rglob('*') if f.is_file())
 print('COMPLETE',len(p),'prop variants',len(g),'terrain types','selected+convertedBytes',total,flush=True)
if __name__=='__main__':main()
