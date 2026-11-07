"""功能：在独立Blender新场景重导入本轮FBX，检查真实门窗射线/屋顶遮光与语义网格数量。
边界：只读FBX，只写本轮报告；不修改源blend、FBX或Unity。此证据不替代Unity实际导入。
"""
import json
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

BASE=Path('E:/WORLDCREATOR/XingHaiHuiLang/研究/VOXEL-MAP-01/Models')
def inspect_fbx(file):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(file),use_anim=False)
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];bpy.context.view_layer.update()
 result={'file':str(file),'meshes':[]};trees=[]
 for obj in meshes:
  vertices=[obj.matrix_world@v.co for v in obj.data.vertices];polygons=[list(p.vertices) for p in obj.data.polygons]
  trees.append(BVHTree.FromPolygons(vertices,polygons))
  result['meshes'].append({'name':obj.name,'vertices':len(vertices),'materials':[m.name for m in obj.data.materials],
    'boundsBlender':[[min(v[i] for v in vertices) for i in range(3)],[max(v[i] for v in vertices) for i in range(3)]]})
 def ray(p,d,range):
  p=Vector((p[0],-p[2],p[1]));d=Vector((d[0],-d[2],d[1]));hits=[]
  for tree in trees:
   hit,normal,index,distance=tree.ray_cast(p,d,range)
   if hit is not None:hits.append(distance)
  return min(hits) if hits else None
 return result,ray

house,ray=inspect_fbx(BASE/'TimberHouse-v1/ossastra.timber-house.semantic-export.material.fbx')
tests={}
tests['eightSemanticMeshes']=len(house['meshes'])==8
for name,p,d,dist in [('door',(0,1.125,-4.25),(0,0,1),1.5),('westWindow',(-4.75,1.625,0),(1,0,0),1.5),('eastWindow',(4.75,1.625,0),(-1,0,0),1.5),('northWindow',(0,1.625,4.25),(0,0,-1),1.5)]:
 hit=ray(p,d,dist);tests[name+'IsRealHole']=hit is None;house[name+'FirstHitMeters']=hit
tests['doorLintelIsSolid']=ray((0,2.625,-4.25),(0,0,1),1.5) is not None
tests['windowJambIsSolid']=ray((-4.75,1.625,1.125),(1,0,0),1.5) is not None
roof=ray((0,7,0),(0,-1,0),5);tests['roofIsSolidFromAbove']=roof is not None and abs(roof-2.25)<1e-5
house['tests']=tests;house['pass']=all(tests.values())
results=[house]
for file in ['ossastra.tree.layeredgreen.semantic-export.material.fbx','ossastra.tree.redumbrella.semantic-export.material.fbx']:
 item,_=inspect_fbx(BASE/'Trees-v1'/file);item['pass']=len(item['meshes'])==2;results.append(item)
report={'pass':all(item['pass'] for item in results),'scope':'Blender 5.1.2 official FBX roundtrip, not Unity import','assets':results}
(BASE/'ModelExportAudit.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
assert report['pass'],'Actual exported mesh validation failed'
