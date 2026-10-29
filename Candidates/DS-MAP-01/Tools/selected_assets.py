"""DS-MAP-01 local-only selected prop reader. Never writes to game installation.
Binary field layout verified against nsimplex/ktools sources (GPL-2.0-or-later).
This focused research adapter is GPL-2.0-or-later; references remain under ktools-reference.
Uses already bundled Pillow/NumPy, no executable download or system installation.
"""
from pathlib import Path
import argparse, hashlib, io, json, math, shutil, struct, zipfile
import numpy as np
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parent.parent
SOURCE=Path('D:/steam/steamapps/common/dont_starve/data')
SELECTED=ROOT/'02-selected-source'
OUT=ROOT/'03-converted'
class Reader:
 def __init__(self,b): self.b=b; self.i=0
 def r(self,f):
  n=struct.calcsize('<'+f)
  if self.i+n>len(self.b): raise ValueError('truncated binary')
  v=struct.unpack_from('<'+f,self.b,self.i);self.i+=n
  return v[0] if len(v)==1 else v
 def s(self):
  n=self.r('I')
  if n>1048576: raise ValueError('oversized string')
  if self.i+n>len(self.b): raise ValueError('truncated string')
  x=self.b[self.i:self.i+n].decode('utf-8');self.i+=n;return x
 def table(self):
  n=self.r('I'); t={}
  for _ in range(n):
   k=self.r('I'); t[k]=self.s()
  return t
 def end(self):
  if self.i!=len(self.b):raise ValueError(f'trailing data {len(self.b)-self.i}')
def sha(b):return hashlib.sha256(b).hexdigest()
def selected(rel):
 f=SOURCE/rel
 o=SELECTED/rel;o.parent.mkdir(parents=True,exist_ok=True)
 if o.exists() and sha(o.read_bytes())!=sha(f.read_bytes()):raise ValueError('existing selected source differs')
 if not o.exists():shutil.copy2(f,o)
 return o

def tex(b):
 if b[:4]!=b'KTEX':raise ValueError('non-KTEX')
 h=struct.unpack_from('<I',b,4)[0];comp=(h>>4)&31;levels=(h>>13)&31
 if not 0<levels<=16:raise ValueError('bad mip count')
 desc=[struct.unpack_from('<HHHI',b,8+10*i) for i in range(levels)]
 if 8+10*levels+sum(x[3] for x in desc)!=len(b):raise ValueError('KTEX size mismatch')
 w,h,pitch,n=desc[0]; raw=b[8+10*levels:8+10*levels+n]
 if comp in (0,1,2):im=Image.frombytes('RGBA',(w,h),raw,'bcn',(comp+1,['DXT1','DXT3','DXT5'][comp]))
 elif comp==4:im=Image.frombytes('RGBA',(w,h),raw)
 elif comp==5:im=Image.frombytes('RGB',(w,h),raw).convert('RGBA')
 else:raise ValueError(f'unsupported KTEX compression {comp}')
 # Raw KTEX has bottom-up rows. Return upright PNG; use v -> 1-v during sampling.
 a=np.asarray(im.transpose(Image.Transpose.FLIP_TOP_BOTTOM)).astype(np.float32)/255
 return a,dict(width=w,height=h,compression=comp,mipLevels=levels,pitch=pitch)

def loadbuild(b):
 r=Reader(b)
 assert r.r('4s')==b'BILD' and r.r('I')==6
 ns,nf=r.r('II');name=r.s();at=[r.s() for _ in range(r.r('I'))];sy={};cnt=0
 for i in range(ns):
  h,nn=r.r('II');fs=[]
  for _ in range(nn):
   f,d,x,y,w,hg,idx,n=r.r('IIffffII');fs.append(dict(frame=f,duration=d,bbox=[x,y,w,hg],vertexIndex=idx,vertexCount=n));cnt+=1
  sy[h]=fs
 assert cnt==nf
 nv=r.r('I');vs=np.array([r.r('ffffff') for _ in range(nv)],dtype=np.float64)
 ht=r.table();r.end()
 for h,fs in sy.items():
  for f in fs:
   assert f['vertexCount']%3==0
   f['verts']=vs[f['vertexIndex']:f['vertexIndex']+f['vertexCount']]
   assert len(f['verts'])==f['vertexCount']
 return dict(name=name,atlases=at,symbols=sy,names=ht,vertexCount=nv)

def loadanim(b):
 r=Reader(b)
 assert r.r('4s')==b'ANIM' and r.r('I')==4
 ne,nf,nev,na=r.r('IIII');anims=[];ec=fc=evc=0
 for _ in range(na):
  name=r.s();face=r.r('B');bank=r.r('I');fps=r.r('f');n=r.r('I');frames=[]
  for __ in range(n):
   bounds=r.r('ffff');events=[r.r('I') for ___ in range(r.r('I'))];els=[];evc+=len(events)
   for ___ in range(r.r('I')):
    sym,fr,layer=r.r('III');m=r.r('ffffff');z=r.r('f')
    els.append(dict(symbol=sym,frame=fr,layer=layer,m=m,z=z));ec+=1
   frames.append(dict(bounds=bounds,events=events,elements=els));fc+=1
  anims.append(dict(name=name,facing=face,bankHash=bank,fps=fps,frames=frames))
 ht=r.table();r.end();assert (ec,fc,evc)==(ne,nf,nev)
 for a in anims:
  a['bank']=ht.get(a['bankHash'],str(a['bankHash']));a['names']=ht
 return anims

def symbolframe(build,element):
 fs=build['symbols'].get(element['symbol'])
 if fs is None:return None
 for f in fs:
  if f['frame']<=element['frame']<f['frame']+f['duration']:return f
 raise ValueError('symbol frame missing')
def transformed(element,verts):
 a,b,c,d,tx,ty=element['m'];x=verts[:,0];y=verts[:,1]
 return np.stack([a*x+b*y+tx,c*x+d*y+ty],axis=1)

def bake(build,anim,atlases,scale=1):
 allxy=[transformed(e,symbolframe(build,e)['verts']) for f in anim['frames'] for e in f['elements'] if symbolframe(build,e) is not None]
 lim=np.concatenate(allxy)
 mn=np.floor(lim.min(axis=0)*scale)-2;mx=np.ceil(lim.max(axis=0)*scale)+2
 w,h=(mx-mn).astype(int)
 if w*h>4096*4096:raise ValueError('unexpected huge prop')
 frames=[]
 for fr in anim['frames']:
  canvas=np.zeros((h,w,4),dtype=np.float32)
  # Source z is back-to-front ordering (smaller z is in front).
  for e in sorted(fr['elements'],key=lambda e:e['z'],reverse=True):
   bf=symbolframe(build,e)
   if bf is None:continue
   vs=bf['verts'];xy=transformed(e,vs)*scale-mn
   # Render this symbol to its own premultiplied layer; adjacent triangles are assigned,
   # not alpha-overdrawn, so shared triangle edges do not darken the composite.
   layer=np.zeros_like(canvas)
   for ii in range(0,len(vs),3):
    tri=xy[ii:ii+3];uv=vs[ii:ii+3,3:5];ai=int(round(vs[ii,5]));atlas=atlases[ai]
    lo=np.maximum(np.floor(tri.min(axis=0)).astype(int),0);hi=np.minimum(np.ceil(tri.max(axis=0)).astype(int),[w,h])
    if np.any(hi<=lo):continue
    u=tri[1]-tri[0];v=tri[2]-tri[0];det=u[0]*v[1]-u[1]*v[0]
    if abs(det)<1e-8:continue
    xx,yy=np.meshgrid(np.arange(lo[0],hi[0])+.5,np.arange(lo[1],hi[1])+.5)
    dx=xx-tri[0,0];dy=yy-tri[0,1]
    s=(dx*v[1]-dy*v[0])/det;t=(u[0]*dy-u[1]*dx)/det
    mask=(s>=-1e-7)&(t>=-1e-7)&(s+t<=1.0000001)
    if not mask.any():continue
    uvw=uv[0]+s[...,None]*(uv[1]-uv[0])+t[...,None]*(uv[2]-uv[0])
    ah,aw=atlas.shape[:2];sx=uvw[...,0]*aw-.5;sy=(1-uvw[...,1])*ah-.5
    # Bilinear sampler in premultiplied source space.
    ix=np.floor(sx).astype(int);iy=np.floor(sy).astype(int);fx=sx-ix;fy=sy-iy
    ix0=np.clip(ix,0,aw-1);ix1=np.clip(ix+1,0,aw-1);iy0=np.clip(iy,0,ah-1);iy1=np.clip(iy+1,0,ah-1)
    col=(atlas[iy0,ix0]*(1-fx)[...,None]*(1-fy)[...,None]+atlas[iy0,ix1]*fx[...,None]*(1-fy)[...,None]+atlas[iy1,ix0]*(1-fx)[...,None]*fy[...,None]+atlas[iy1,ix1]*fx[...,None]*fy[...,None])
    sub=layer[lo[1]:hi[1],lo[0]:hi[0]];sub[mask]=col[mask]
   a=layer[...,3:4];canvas=layer+canvas*(1-a)
  # KTEX textures use premultiplied RGB; export straight RGBA once for Unity.
  alpha=canvas[...,3:4];canvas[...,:3]=np.divide(canvas[...,:3],alpha,out=np.zeros_like(canvas[...,:3]),where=alpha>1/255)
  frames.append(Image.fromarray(np.round(np.clip(canvas,0,1)*255).astype(np.uint8),'RGBA'))
 return frames,[-float(mn[0]),float(mx[1])],dict(minTopLeft=mn.tolist(),maxBottomRight=mx.tolist())

def loadpair(buildzip,animzips):
 buildpath=selected('anim/'+buildzip+'.zip')
 with zipfile.ZipFile(buildpath) as z:
  build=loadbuild(z.read('build.bin'));atlases=[];meta=[]
  for n in build['atlases']:
   a,m=tex(z.read(n));atlases.append(a);meta.append(m)
 anims=[]
 for an in animzips:
  with zipfile.ZipFile(selected('anim/'+an+'.zip')) as z:anims+=loadanim(z.read('anim.bin'))
 return build,anims,atlases,meta

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--inspect',action='store_true');ap.add_argument('--only',default='rock');args=ap.parse_args()
 pairs=[('rock',['rock']),('grass1',['grass']),('sapling',['sapling']),('berrybush',['berrybush']),('treasure_chest',['treasure_chest']),('firepit',['firepit']),('evergreen_new',['evergreen_tall_old','evergreen_short_normal'])]
 for bz,az in pairs:
  if bz!=args.only and args.only!='all':continue
  build,anims,atlases,meta=loadpair(bz,az)
  print(bz,'build',build['name'],'atlases',meta)
  print([(a['bank'],a['name'],a['facing'],len(a['frames']),a['fps']) for a in anims])
  if args.inspect:continue
  pick={'rock':'full','grass1':'idle','sapling':'sway','berrybush':'berriesmost','treasure_chest':'closed','firepit':'idle','evergreen_new':'idle_tall'}[bz]
  anim=next(a for a in anims if a['name']==pick)
  frames,pivot,lim=bake(build,anim,atlases)
  dest=OUT/'props'/bz;dest.mkdir(parents=True,exist_ok=True)
  frames[0].save(dest/'frame-000.png')
  (dest/'sample.json').write_text(json.dumps(dict(build=bz,anim=pick,size=frames[0].size,pivotPixelsBottomLeft=pivot,bounds=lim,firstElements=anim['frames'][0]['elements']),indent=2),encoding='utf-8')
  print('BAKED',len(frames),frames[0].size,pivot)
if __name__=='__main__':main()
