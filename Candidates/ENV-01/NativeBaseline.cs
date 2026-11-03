/* 功能：在改动前通过Native保存当前主场安全副本，记录真实对象、灯与材质引用。 */
using System;using System.Linq;using System.IO;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;using Newtonsoft.Json;using Funplay.Editor.Tools.Scripting;
public class EnvNativeBaseline:IFunplayCommand{public void Execute(ExecutionContext c){
const string target="Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/SC_RoomVisibilityLab.unity";var s=SceneManager.GetActiveScene();
if(Application.dataPath!="E:/WORLDCREATOR/new/Project_ANKALUTE/Assets"||Application.isPlaying||s.path!=target)throw new Exception("Edit/project/lab guard");
const string dir="E:/WORLDCREATOR/XingHaiHuiLang/研究/环境模拟/ENV-01";string backup=dir+"/MainBefore.unity";
if(File.Exists(backup))throw new Exception("Existing scene backup; inspect outcome");bool copied=EditorSceneManager.SaveScene(s,backup,true);if(!copied)throw new Exception("Nativecopyfailed");
var roots=s.GetRootGameObjects();var monos=roots.SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).Where(x=>x!=null).ToArray();
var m=monos.OfType<Ossastra.MapResearch.MapResearchRoot>().Single();var visibility=monos.OfType<Ossastra.SceneVisibility.SceneVisibilityRoot>().Single();var lights=roots.SelectMany(r=>r.GetComponentsInChildren<Light>(true)).ToArray();
var result=new{copied,s.isDirty,mapId=m.GetInstanceID(),cameraId=m.gameplayCamera.GetInstanceID(),m.pitch,m.distance,visibilityId=visibility.GetInstanceID(),roomsId=visibility.roomModule.GetInstanceID(),sunId=RenderSettings.sun==null?0:RenderSettings.sun.GetInstanceID(),
lights=lights.Select(l=>new{id=l.GetInstanceID(),l.name,l.intensity,type=l.type.ToString(),position=l.transform.position.ToString(),rotation=l.transform.eulerAngles.ToString(),shadows=l.shadows.ToString()}).ToArray(),
fonts=AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath).Take(20).ToArray(),
rendererStates=roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)).Select(r=>new{id=r.GetInstanceID(),r.name,shader=r.sharedMaterial==null?"":r.sharedMaterial.shader.name,materials=r.sharedMaterials.Select(x=>x==null?0:x.GetInstanceID()).ToArray(),cast=r.shadowCastingMode.ToString(),center=r.bounds.center.ToString(),size=r.bounds.size.ToString()}).ToArray()};
File.WriteAllText(dir+"/native-baseline.json",JsonConvert.SerializeObject(result,Formatting.Indented));c.ReturnValue=JsonConvert.SerializeObject(new{copied,m.pitch,map=m.GetInstanceID(),camera=m.gameplayCamera.GetInstanceID(),visibility=visibility.GetInstanceID(),rooms=visibility.roomModule.GetInstanceID(),sun=RenderSettings.sun.GetInstanceID(),fonts=result.fonts,lights=result.lights});
}}
