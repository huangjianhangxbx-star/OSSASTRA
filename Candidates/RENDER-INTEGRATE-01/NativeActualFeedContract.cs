/* 功能：对主场实际渲染产生的颜色/许可/材质进行GPU对照，验证人物装备与黑幕未被环境效果改色。
 * 边界：不伪造许可、不移动角色、不修改Profile或场景；不替代完整视频/低端性能验收。
 */
using System;using System.Linq;using System.Collections;using System.Reflection;using System.Collections.Generic;using System.IO;using UnityEngine;using UnityEditor;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;using Ossastra.WorldRendering;using Ossastra.WorldRendering.Rendering;using Ossastra.SceneVisibility.Rooms.Rendering;using Funplay.Editor.Tools.Scripting;using Newtonsoft.Json;
public class NativeActualFeedContract:IFunplayCommand{
static Color[] Read(RenderTexture t){var before=RenderTexture.active;var cpu=new Texture2D(t.width,t.height,TextureFormat.RGBAFloat,false,true);try{RenderTexture.active=t;cpu.ReadPixels(new Rect(0,0,t.width,t.height),0,0,false);return cpu.GetPixels();}finally{RenderTexture.active=before;UnityEngine.Object.DestroyImmediate(cpu);}}
static float Difference(Color a,Color b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b));
public void Execute(ExecutionContext ctx){
if(!Application.isPlaying||Application.dataPath!="E:/WORLDCREATOR/new/Project_ANKALUTE/Assets")throw new Exception("Play/project guard");
var w=UnityEngine.Object.FindObjectOfType<WorldLookController>();if(w==null||w.Current==null)throw new Exception("No active look");
var data=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/Rendering/RoomVisibilityRenderer.asset");
var f=data.rendererFeatures.OfType<WorldLookRendererFeature>().Single();var rf=data.rendererFeatures.OfType<RoomMaskRendererFeature>().Single();
var flags=BindingFlags.Instance|BindingFlags.NonPublic;
var passes=(IDictionary)typeof(WorldLookRendererFeature).GetField("cameras",flags).GetValue(f);var pass=passes[w.gameplayCamera.GetInstanceID()];if(pass==null)throw new Exception("Look pass missing");
var pc=pass.GetType();var source=(RTHandle)pc.GetField("colorCopy",flags).GetValue(pass);var material=(Material)pc.GetField("look",flags).GetValue(pass);
var positive=(IDictionary)typeof(RoomMaskRendererFeature).GetField("positiveCameras",flags).GetValue(rf);var pp=positive[w.gameplayCamera.GetInstanceID()];var permission=(RTHandle)pp.GetType().GetField("labelPermission",flags).GetValue(pp);
RoomMaskFrame frame; if(!RoomMaskCameraRegistry.TryGet(w.gameplayCamera,out frame))throw new Exception("No actual frame");
var descriptor=source.rt.descriptor;descriptor.msaaSamples=1;descriptor.depthBufferBits=0;descriptor.depthStencilFormat=UnityEngine.Experimental.Rendering.GraphicsFormat.None;
var output=RTHandles.Alloc(descriptor,filterMode:FilterMode.Point,name:"Owned actual feed contract output");
var cmd=new CommandBuffer{name="Actual main world environment contract"};
try{
Blitter.BlitCameraTexture(cmd,source,output,material,0);Graphics.ExecuteCommandBuffer(cmd);
var before=Read(source.rt);var after=Read(output.rt);var masks=Read(permission.rt);
long environment=0,changedEnvironment=0,protectedVisible=0,black=0;float protectedError=0,blackError=0;
for(int i=0;i<before.Length;i++){
bool permitted=masks[i].r>.998f&&masks[i].g>.998f&&masks[i].a>.998f;
float brightness=Mathf.Max(before[i].r,Mathf.Max(before[i].g,before[i].b));float error=Difference(before[i],after[i]);
if(permitted){environment++;if(error>.002f)changedEnvironment++;}
else if(brightness>.01f){protectedVisible++;protectedError=Mathf.Max(protectedError,error);}
else if(brightness<.000001f){black++;blackError=Mathf.Max(blackError,error);}
}
var actors=frame.PositiveSurfaces.Where(s=>s.Renderer!=null&&s.Renderer.GetComponentInParent<Ossastra.Characters.CharacterRoot>(true)!=null).ToArray();
int badRole=actors.Count(s=>s.WorldLookEnvironment);var actorRegions=new List<object>();long actorProtected=0;
foreach(var a in actors){
var b=a.Renderer.bounds;var points=new[]{b.min,b.max,new Vector3(b.min.x,b.min.y,b.max.z),new Vector3(b.max.x,b.max.y,b.min.z),new Vector3(b.min.x,b.max.y,b.min.z),new Vector3(b.max.x,b.min.y,b.max.z),new Vector3(b.min.x,b.max.y,b.max.z),new Vector3(b.max.x,b.min.y,b.min.z)}.Select(p=>w.gameplayCamera.WorldToViewportPoint(p)).ToArray();
int minX=Mathf.Clamp((int)(points.Min(p=>p.x)*source.rt.width),0,source.rt.width-1),maxX=Mathf.Clamp((int)(points.Max(p=>p.x)*source.rt.width),0,source.rt.width-1),minY=Mathf.Clamp((int)(points.Min(p=>p.y)*source.rt.height),0,source.rt.height-1),maxY=Mathf.Clamp((int)(points.Max(p=>p.y)*source.rt.height),0,source.rt.height-1);long count=0;
for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++){int i=y*source.rt.width+x;if(masks[i].a<.998f&&Mathf.Max(before[i].r,Mathf.Max(before[i].g,before[i].b))>.02f)count++;}
actorProtected+=count;actorRegions.Add(new{name=a.Renderer.name,protectedPixels=count,a.WorldLookEnvironment});
}
var result=new{pass=environment>1000&&changedEnvironment>100&&protectedVisible>100&&protectedError<.004f&&black>1000&&blackError<.000001f&&actors.Length>=2&&badRole==0&&actorProtected>100,width=source.rt.width,height=source.rt.height,environment,changedEnvironment,protectedVisible,protectedError,black,blackError,actorSurfaces=actors.Length,badRole,actorProtected,actorRegions,ownedTextures=f.OwnedTextureCount,ownedMaterials=f.OwnedMaterialCount,source="actual main-camera pre-effect color, actual legal permission and configured production material",boundary="GPU isolated replay of actual frame, plus actual on-screen main capture; not whole-video/Player performance"};
string path="E:/WORLDCREATOR/XingHaiHuiLang/研究/渲染接入/RENDER-INTEGRATE-01/actual-feed.json";File.WriteAllText(path,JsonConvert.SerializeObject(result,Formatting.Indented));ctx.ReturnValue=result;
}finally{cmd.Release();output.Release();}
}}
