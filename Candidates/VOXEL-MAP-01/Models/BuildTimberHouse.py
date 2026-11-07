"""功能：沿既有Blender工作台API制作受管理木构屋；仅导出副本按语义合并。
边界：不修改工作台，不读取/修改Unity资产；不把参考图作为纹理。原blend保留工作台块身份。
坐标：描述单元是Unity X/Y/Z整数0.25m格；只在生成recipe时换为Blender X/-Z/Y。
"""
import sys, json, math
from pathlib import Path
from collections import defaultdict, Counter
import bpy
from mathutils import Vector

TOOL=Path('E:/WORLDCREATOR/XingHaiHuiLang/Origin/自制工具/voxel_workbench')
OUT=Path('E:/WORLDCREATOR/XingHaiHuiLang/研究/VOXEL-MAP-01/Models/TimberHouse-v1')
sys.path.insert(0,str(TOOL))
from addon import api

PALETTE={
 'wood.frame':[.30,.205,.125,1], 'wood.panel':[.50,.355,.22,1],
 'wood.floor':[.45,.30,.175,1], 'wood.trim':[.60,.455,.29,1],
 'roof.base':[.31,.335,.35,1], 'roof.edge':[.47,.47,.42,1],
 'stone.base':[.405,.45,.47,1], 'stone.cap':[.51,.55,.55,1]}
for family in ['wood.panel','wood.floor','roof.base']:
 for variant,offset in [('dark',-.025),('light',.025)]:
  PALETTE[family+'.'+variant]=[max(0,min(1,c+offset)) for c in PALETTE[family][:3]]+[1]

def make_cells():
 cells={}
 def box(part,color,x,y,z,sx,sy,sz,replace=True):
  for xx in range(x,x+sx):
   for yy in range(y,y+sy):
    for zz in range(z,z+sz):
     if replace or (xx,yy,zz) not in cells: cells[xx,yy,zz]=(part,color)
 # Floor top Y=0; closed foundation one voxel below, no raised threshold.
 for x in range(-16,16):
  col=['wood.floor.dark','wood.floor','wood.floor.light'][(x//2)%3]
  box('Structure_Floor',col,x,-1,-14,1,1,28)
 for x in [-16,15]:box('Structure_Floor','stone.base',x,-1,-14,1,1,28)
 for z in [-14,13]:box('Structure_Floor','stone.base',-16,-1,z,32,1,1)
 # Recessed boards; outer posts and trims provide real front/back layers.
 for part,z in [('South',-13),('North',12)]:
  for x in range(-16,16):
   col=['wood.panel','wood.panel.dark','wood.panel.light'][(x//2)%3]
   box('Structure_Wall'+part,col,x,0,z,1,12,1)
 for part,x in [('West',-15),('East',14)]:
  for z in range(-12,12):
   col=['wood.panel','wood.panel.dark','wood.panel.light'][(z//2)%3]
   box('Structure_Wall'+part,col,x,0,z,1,12,1)
 # Massive posts and square stone shoes, inside nominal 8x7m wall footprint.
 for part,z in [('South',-14),('North',12)]:
  for x in [-16,-10,8,14]:
   box('Structure_Wall'+part,'wood.frame',x,0,z,2,12,2)
   box('Structure_Wall'+part,'stone.base',x,0,z,2,2,2)
   box('Structure_Wall'+part,'stone.cap',x,2,z,2,1,2)
   box('Structure_Wall'+part,'wood.trim',x,10,z,2,1,2)
 for part,x in [('West',-16),('East',14)]:
  for z in [-8,6]:
   box('Structure_Wall'+part,'wood.frame',x,0,z,2,12,2)
   box('Structure_Wall'+part,'stone.base',x,0,z,2,2,2)
   box('Structure_Wall'+part,'stone.cap',x,2,z,2,1,2)
 # Continuous deep ring beams/caps remain with structure when roof hides.
 for part,z in [('South',-14),('North',12)]:
  box('Structure_Wall'+part,'wood.frame',-16,11,z,32,1,2)
  box('Structure_Wall'+part,'wood.trim',-16,10,z,32,1,1)
 for part,x in [('West',-16),('East',14)]:
  box('Structure_Wall'+part,'wood.frame',x,11,-12,2,1,24)
 # Wall-line roof level is one voxel above the wide outside eave: close that
 # real gap with structural fascia, never a roof-only cap that vanishes inside.
 for part,z in [('South',-14),('North',12)]:
  box('Structure_Wall'+part,'wood.frame',-16,12,z,32,1,2)
 # Door jambs and lintel; net opening x[-1,+1], y[0,2.25].
 for x in [-5,4]:box('Structure_WallSouth','wood.frame',x,0,-14,1,10,2)
 box('Structure_WallSouth','wood.trim',-5,9,-14,10,1,2)
 # Side/back window frames occupy only OUTSIDE their exact clear aperture.
 for part,x in [('West',-16),('East',14)]:
  box('Structure_Wall'+part,'wood.trim',x,3,-4,2,1,8)
  box('Structure_Wall'+part,'wood.trim',x,9,-4,2,1,8)
  for z in [-4,3]:box('Structure_Wall'+part,'wood.frame',x,4,z,2,5,1)
 box('Structure_WallNorth','wood.trim',-5,3,12,10,1,2)
 box('Structure_WallNorth','wood.trim',-5,9,12,10,1,2)
 for x in [-5,4]:box('Structure_WallNorth','wood.frame',x,4,12,1,5,2)
 # Real empty through-holes, applied after every structural decoration.
 for key in list(cells):
  x,y,z=key
  door=-4<=x<4 and 0<=y<9 and -14<=z<-12
  side=(x<-14 or x>=14) and -3<=z<3 and 4<=y<9
  back=-4<=x<4 and 4<=y<9 and z>=12
  if door or side or back:del cells[key]
 # Closed stepped roof slabs: .50m overhang and discrete stacked timber edge.
 for z in range(-16,16):
  depth=min(z+16,15-z);h=12+min(6,depth//2)
  part='Cover_Ridge' if -2<=z<2 else 'Cover_RoofSouth' if z<0 else 'Cover_RoofNorth'
  for x in range(-18,18):
   color='roof.edge' if x in (-18,17) or depth%2==0 else ['roof.base','roof.base.dark','roof.base.light'][(x//4)%3]
   box(part,color,x,h,z,1,1,1)
   # 连续两格厚的盖板让相邻高低台阶以面连接，而非只靠对角线接触。
   # 已存在的结构封口保留其Structure归属，不会被归入可隐藏顶盖。
   box(part,color,x,h-1,z,1,1,1,replace=False)
  # Stepped end gable fillers remain structure, not cover; roof hides independently.
  if -14<=z<14 and h>12:
   for x,partWall in [(-16,'West'),(15,'East')]:
    box('Structure_Wall'+partWall,'wood.panel.dark',x,12,z,1,h-12,1)
 # Eave brackets below broad slab edges, visible from low camera angles.
 for x in [-15,-9,8,14]:
  for z,side in [(-15,'South'),(14,'North')]:
   box('Cover_Roof'+side,'wood.frame',x,10,z,1,2,1)
 return cells

def greedy_boxes(cells):
 remaining=dict(cells); result=[]
 while remaining:
  x,y,z=min(remaining,key=lambda p:(p[1],p[2],p[0])); val=remaining[x,y,z]
  sx=1
  while remaining.get((x+sx,y,z))==val:sx+=1
  sz=1
  while all(remaining.get((xx,y,z+sz))==val for xx in range(x,x+sx)):sz+=1
  sy=1
  while all(remaining.get((xx,y+sy,zz))==val for xx in range(x,x+sx) for zz in range(z,z+sz)):sy+=1
  for xx in range(x,x+sx):
   for yy in range(y,y+sy):
    for zz in range(z,z+sz):del remaining[xx,yy,zz]
  result.append((val,(x,y,z),(sx,sy,sz)))
 return result

def recipe_for(cells):
 ops=[]; part_ids={}
 for index,((part,color),(x,y,z),(sx,sy,sz)) in enumerate(greedy_boxes(cells)):
  iid=f'{part}.{index}';part_ids[iid]=part
  size=[sx,sz,sy]
  ops.append({'op':'place','id':iid,'template_id':'base.cube','origin_cells':[x,-z-sz,y],
              'size_cells':size,'color_id':color,'anchor':'custom','custom_anchor':[-v*.25/2 for v in size]})
 return {'schema_version':'0.1','request_id':'timber-house-v1-build','asset_id':'ossastra.timber-house',
 'expected_revision':0,'profile_id':'ossastra.voxel-map','profile_revision':1,'operations':ops},part_ids

def merged_export(source_id,part_ids):
 # Thin export adaptation only: preserve the managed model; do not rebuild its geometry.
 src=api.root(source_id);export_id=source_id+'.semantic-export'
 api.apply_recipe({'schema_version':'0.1','request_id':'house-semantic-export-init','asset_id':export_id,'expected_revision':0,'profile_id':'ossastra.voxel-map','profile_revision':1,'operations':[]})
 groups=defaultdict(list)
 for obj in api.objects(src):groups[part_ids[obj['vw_id']]].append(obj)
 for part,objects in groups.items():
  vertices=[];faces=[];materials=[];face_materials=[]
  for obj in objects:
   offset=len(vertices);vertices.extend([tuple(obj.matrix_world@v.co) for v in obj.data.vertices])
   for poly in obj.data.polygons:
    faces.append(tuple(i+offset for i in poly.vertices));mat=obj.data.materials[poly.material_index]
    if mat not in materials:materials.append(mat)
    face_materials.append(materials.index(mat))
  mesh=bpy.data.meshes.new(part);mesh.from_pydata(vertices,[],faces);mesh.update()
  obj=bpy.data.objects.new(part,mesh);bpy.context.scene.collection.objects.link(obj)
  api.adopt_static(export_id,obj,part,materials[0].get('vw_color'))
  obj.data.materials.clear()
  for mat in materials:obj.data.materials.append(mat)
  for face,slot in zip(obj.data.polygons,face_materials):face.material_index=slot
  obj.name=part;obj['source_part']=part
 api.inspect_asset(export_id)
 return export_id

def render(export_id,name,cutaway=False,gray=False):
 collection=api.root(export_id);source=api.root('ossastra.timber-house')
 old_scene=bpy.context.window.scene
 scene=bpy.data.scenes.new('HouseReview.'+name);scene.collection.children.link(collection);bpy.context.window.scene=scene
 old_hides={o:o.hide_render for o in collection.objects}
 for obj in collection.objects:obj.hide_render=cutaway and (obj.name.startswith('Cover_') or obj.name=='Structure_WallSouth')
 camera=bpy.data.cameras.new('ReviewCamera');cam=bpy.data.objects.new('ReviewCamera',camera);scene.collection.objects.link(cam)
 cam.location=Vector((12,16,12));target=Vector((0,0,2));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();camera.type='ORTHO';camera.ortho_scale=13;scene.camera=cam
 world=bpy.data.worlds.new('ReviewWorld');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.14,.17,.21,1);world.node_tree.nodes['Background'].inputs[1].default_value=.45;scene.world=world
 for lightName,pos,energy,color,size in [('Key',(2,7,12),1800,(1,.91,.77),8),('Fill',(-8,-5,8),1200,(.72,.83,1),8)]:
  data=bpy.data.lights.new(lightName,'AREA');data.energy=energy;data.color=color;data.shape='DISK';data.size=size
  obj=bpy.data.objects.new(lightName,data);scene.collection.objects.link(obj);obj.location=pos;obj.rotation_euler=(target-obj.location).to_track_quat('-Z','Y').to_euler()
 scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=1000;scene.render.resolution_y=850;scene.render.resolution_percentage=100
 scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.filepath=str(OUT/(name+'.png'))
 if gray:
  mat=bpy.data.materials.new('GrayboxReview');mat.diffuse_color=(.38,.41,.44,1);mat.use_nodes=True;mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=mat.diffuse_color;mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.8;scene.view_layers[0].material_override=mat
 bpy.ops.render.render(write_still=True)
 for obj,hidden in old_hides.items():obj.hide_render=hidden
 bpy.context.window.scene=old_scene;bpy.data.scenes.remove(scene)

def main():
 OUT.mkdir(parents=True,exist_ok=True);bpy.ops.wm.read_factory_settings(use_empty=True)
 profile=api.get_profile();profile.update(id='ossastra.voxel-map',revision=1,palette={k:{'color':v,'roughness':.86,'metallic':0} for k,v in PALETTE.items()});api.set_default_profile(profile)
 cells=make_cells();recipe,parts=recipe_for(cells)
 # Source invariants precede actual mesh creation; errors never become a half-delivered FBX.
 assert all(not (-4<=x<4 and 0<=y<9 and -14<=z<-12) for x,y,z in cells),'door filled'
 assert min(y for x,y,z in cells)==-1 and max(y for x,y,z in cells)==18
 assert set(part for part,color in cells.values())=={'Structure_Floor','Structure_WallSouth','Structure_WallNorth','Structure_WallWest','Structure_WallEast','Cover_RoofSouth','Cover_RoofNorth','Cover_Ridge'}
 (OUT/'house.recipe.json').write_text(json.dumps(recipe,indent=2),encoding='utf-8');api.apply_recipe(recipe)
 source=api.inspect_asset(recipe['asset_id']);export_id=merged_export(recipe['asset_id'],parts)
 # Keep editable and merged current mesh in one source blend, but show only semantic export by default.
 api.root(recipe['asset_id']).hide_viewport=True;api.root(recipe['asset_id']).hide_render=True
 bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'TimberHouse.blend'),relative_remap=False)
 outputs=[api.export_asset(export_id,OUT,'material','FBX'),api.export_asset(export_id,OUT,'material','GLB')]
 items=[]
 for obj in api.objects(api.root(export_id)):
  bounds=api.object_bounds(obj);items.append({'name':obj.name,'instanceId':obj['vw_id'],'sourceBoundsBlender':[bounds[0],bounds[1]],'vertices':len(obj.data.vertices),'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'palette':[m.get('vw_color') for m in obj.data.materials]})
 roof_columns={(x,z):{y for xx,y,zz in cells if xx==x and zz==z and y>=11} for x in range(-18,18) for z in range(-16,16)}
 cover_columns={(x,z):{y for (xx,y,zz),(part,color) in cells.items() if xx==x and zz==z and part.startswith('Cover_')} for x in range(-18,18) for z in range(-16,16)}
 roof_complete=all(roof_columns.values())
 roof_face_connected=all(roof_columns[x,z]&roof_columns[x,z+1] for x in range(-18,18) for z in range(-16,15))
 assert roof_complete and roof_face_connected,'roof cover has an actual missing column or step gap'
 cover_interface_count=sum(not bool(cover_columns[x,z]&cover_columns[x,z+1]) for x in range(-18,18) for z in range(-16,15))
 roof_geometry={'topDownColumns':len(roof_columns),'allCoverProjectedColumnsSolid':all(cover_columns.values()),'assemblyDepthColumnsShareHeightIncludingStructure':roof_face_connected,'coverOnlyDepthColumnsShareHeight':cover_interface_count==0,'coverOnlyNoSharedHeightNeighbors':cover_interface_count,'structuralInterfaceExplanation':'84 cover-only pairs include retained fascia/gable cells: continuity is verified on the assembled roof+Structure, not asserted for Cover alone. Roof removal retains those structural cells.','opaquePrincipledOnly':True,'texturesOrAlphaMasks':False,'wallFasciaAtY':[3,3.25],'roofStripsAreSolidColoredTopFaces':True}
 result={'assetId':recipe['asset_id'],'gridMeters':.25,'source':source,'exports':outputs,'parts':items,'roofGeometry':roof_geometry,'paletteSrgb':PALETTE,'doorUnity':{'center':[0,1.125,-3.5],'clear':[2,2.25]},'windowsUnity':[{'wall':'West','center':[-4,1.625,0],'clear':[1.5,1.25]},{'wall':'East','center':[4,1.625,0],'clear':[1.5,1.25]},{'wall':'North','center':[0,1.625,3.5],'clear':[2,1.25]}],'cellCount':len(cells),'mergedRenderers':len(items),'axis':'Blender(x,y,z)->Unity(x,z,-y); FBX official -Z forward/Y up; Root must Native-read actual imported bounds once'}
 (OUT/'TimberHouseManifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
 render(export_id,'HouseClosed');render(export_id,'HouseCutaway',True);render(export_id,'HouseGraybox',True,True)
 print('HOUSE_RESULT='+json.dumps({'sourceBlocks':len(source['instances']),'mergedParts':len(items),'cells':len(cells),'output':str(OUT)}))

if __name__=='__main__': main()
