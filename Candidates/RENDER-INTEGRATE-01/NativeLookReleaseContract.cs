// 功能：主场真实环境资源撤销、重入与原Renderer材质引用回归，不修改场景资产。
using UnityEngine;using UnityEditor;using System;using System.Linq;using System.Collections.Generic;using UnityEngine.Rendering.Universal;using Funplay.Editor.Tools.Scripting;
public class NativeLookReleaseContract:IFunplayCommand{
static Dictionary<int,int[]> original;
public void Execute(ExecutionContext c){
var w=UnityEngine.Object.FindObjectOfType<Ossastra.WorldRendering.WorldLookController>();
var d=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/Rendering/RoomVisibilityRenderer.asset");
var f=d.rendererFeatures.OfType<Ossastra.WorldRendering.Rendering.WorldLookRendererFeature>().Single();
original=UnityEngine.Object.FindObjectsOfType<Renderer>().ToDictionary(r=>r.GetInstanceID(),r=>r.sharedMaterials.Select(m=>m==null?0:m.GetInstanceID()).ToArray());
var before=new{textures=f.OwnedTextureCount,materials=f.OwnedMaterialCount};
try{w.enabled=false;var off=new{textures=f.OwnedTextureCount,materials=f.OwnedMaterialCount,cameras=f.ActiveCameraCount};c.ReturnValue=new{pass=before.textures==2&&before.materials==2&&off.textures==0&&off.materials==0&&off.cameras==0,before,off};}
finally{w.enabled=true;}
}
public static object Read(){
var d=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/Rendering/RoomVisibilityRenderer.asset");var f=d.rendererFeatures.OfType<Ossastra.WorldRendering.Rendering.WorldLookRendererFeature>().Single();
var changed=original.Where(p=>{var r=EditorUtility.InstanceIDToObject(p.Key) as Renderer;return r==null||!r.sharedMaterials.Select(m=>m==null?0:m.GetInstanceID()).SequenceEqual(p.Value);}).Select(p=>p.Key).ToArray();
return new{pass=f.OwnedTextureCount==2&&f.OwnedMaterialCount==2&&f.ActiveCameraCount==1&&changed.Length==0,textures=f.OwnedTextureCount,materials=f.OwnedMaterialCount,cameras=f.ActiveCameraCount,sourceRendererCount=original.Count,changed};
}}
