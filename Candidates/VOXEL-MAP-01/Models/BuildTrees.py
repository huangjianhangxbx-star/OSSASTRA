"""功能：经既有体块工作台API制作两种参考启发的草木，保留blend与语义合并FBX。
边界：仅自有颜色/原生体块；不取参考图为纹理，不操作Unity。单位固定0.25米。
"""
import sys, json, math
from pathlib import Path
from collections import Counter
import bpy
from mathutils import Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
import BuildTimberHouse as house
from addon import api
OUT=Path('E:/WORLDCREATOR/XingHaiHuiLang/研究/VOXEL-MAP-01/Models/Trees-v1')
PALETTE={'wood.trunk':[.28,.19,.12,1], 'wood.branch':[.36,.25,.15,1],
 'leaf.green':[.265,.40,.30,1],'leaf.green.dark':[.20,.32,.24,1],'leaf.green.light':[.33,.47,.35,1],
 'leaf.red':[.62,.20,.12,1],'leaf.red.dark':[.46,.13,.10,1],'leaf.red.light':[.73,.285,.145,1]}

def cells_for(kind):
 cells={}
 def box(part,col,x,y,z,sx,sy,sz):
  for xx in range(x,x+sx):
   for yy in range(y,y+sy):
    for zz in range(z,z+sz):cells[xx,yy,zz]=(part,col)
 def crown(cx,cy,cz,rx,ry,rz,family):
  for x in range(cx-rx,cx+rx+1):
   for y in range(cy-ry,cy+ry+1):
    for z in range(cz-rz,cz+rz+1):
     if ((x-cx)/(rx+.3))**2+((y-cy)/(ry+.3))**2+((z-cz)/(rz+.3))**2<=1:
      col=[family,family+'.dark',family+'.light'][((x//2)*7+(z//2)*3)%3]
      cells[x,y,z]=('Canopy',col)
 if kind=='LayeredGreen':
  box('Trunk','wood.trunk',-1,0,-1,2,14,2)
  for cx,cy,cz,rx,ry,rz in [(0,8,0,5,3,4),(0,12,0,4,3,4),(0,16,0,3,3,3),(0,19,0,1,2,1)]:
   crown(cx,cy,cz,rx,ry,rz,'leaf.green')
 else:
  for x,z,height in [(-2,0,13),(1,1,16),(2,-2,12)]:box('Trunks','wood.trunk',x,0,z,1,height,1)
  for y in range(8,15):box('Trunks','wood.branch',-2-(y-8)//3,y,0,1,1,1)
  # Flat spreading, stepped red crowns echo reference4 without recreating its pixels.
  for p in [(-4,11,0,5,2,4),(1,16,1,6,3,5),(5,10,-1,4,2,4)]:crown(*p,'leaf.red')
 return cells

def render(asset_id,name):
 original=bpy.context.window.scene;scene=bpy.data.scenes.new(name);scene.collection.children.link(api.root(asset_id));bpy.context.window.scene=scene
 data=bpy.data.cameras.new('Camera');cam=bpy.data.objects.new('Camera',data);scene.collection.objects.link(cam);scene.camera=cam
 cam.location=(9,12,8);target=Vector((0,0,2.4));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=6.5
 world=bpy.data.worlds.new('NeutralWorld');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.14,.17,.20,1);world.node_tree.nodes['Background'].inputs[1].default_value=.6;scene.world=world
 light=bpy.data.lights.new('Key','AREA');light.energy=1500;light.size=8;obj=bpy.data.objects.new('Key',light);scene.collection.objects.link(obj);obj.location=(4,6,10);obj.rotation_euler=(target-obj.location).to_track_quat('-Z','Y').to_euler()
 scene.render.engine='CYCLES';scene.cycles.samples=24;scene.view_settings.view_transform='AgX';scene.render.resolution_x=750;scene.render.resolution_y=800;scene.render.resolution_percentage=100;scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
 bpy.context.window.scene=original;bpy.data.scenes.remove(scene)

def main():
 OUT.mkdir(parents=True,exist_ok=True);bpy.ops.wm.read_factory_settings(use_empty=True)
 profile=api.get_profile();profile.update(id='ossastra.voxel-map',revision=1,palette={k:{'color':v,'roughness':.9,'metallic':0} for k,v in PALETTE.items()});api.set_default_profile(profile)
 results=[]
 for kind in ['LayeredGreen','RedUmbrella']:
  asset_id='ossastra.tree.'+kind.lower();cells=cells_for(kind);recipe,parts=house.recipe_for(cells)
  recipe.update(asset_id=asset_id,request_id=kind+'-v1');api.apply_recipe(recipe)
  export_id=house.merged_export(asset_id,parts)
  api.root(asset_id).hide_viewport=True;api.root(asset_id).hide_render=True
  (OUT/(kind+'.recipe.json')).write_text(json.dumps(recipe,indent=2),encoding='utf-8')
  exports=[api.export_asset(export_id,OUT,'material','FBX'),api.export_asset(export_id,OUT,'material','GLB')]
  results.append({'assetId':asset_id,'kind':kind,'cellCount':len(cells),'sourceBlocks':len(api.inspect_asset(asset_id)['instances']),'parts':[{'name':o.name,'palette':[m.get('vw_color') for m in o.data.materials],'boundsBlender':api.object_bounds(o)} for o in api.objects(api.root(export_id))],'exports':exports})
  render(export_id,kind)
 bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Trees.blend'),relative_remap=False)
 (OUT/'TreesManifest.json').write_text(json.dumps({'gridMeters':.25,'paletteSrgb':PALETTE,'assets':results,'axis':'Blender(x,y,z)->Unity(x,z,-y); official FBX -Z Forward/Y Up'},ensure_ascii=False,indent=2),encoding='utf-8')
 print('TREES_RESULT='+json.dumps([{'kind':r['kind'],'blocks':r['sourceBlocks'],'cells':r['cellCount'],'parts':len(r['parts'])} for r in results]))

if __name__=='__main__':main()
